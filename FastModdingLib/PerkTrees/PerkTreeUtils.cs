using Duckov.PerkTrees;
using FeatherMod.Events;
using FeatherMod.Events.GameEvents;
using FeatherMod.Register;
using FeatherMod.Utils;
using NodeCanvas.Framework;
using Saves;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// PerkTree 与 Perk 系统公共 API。
    /// 所有 public API 使用 <see cref="Identifier"/> 作为资源标识符。
    /// </summary>
    /// <remarks>
    /// <para><b>Identifier 约定</b>：</para>
    /// <list type="bullet">
    /// <item><b>PerkTree</b> — Domain=modid 或 "duckov"（原版），Path=treeID</item>
    /// <item><b>自定义 Perk</b> — Domain=modid，Path=perk名（通过 <see cref="AddPerk"/> 显式指定 treeId）</item>
    /// <item><b>原版 Perk 引用</b> — Domain="duckov"，Path="treeID/perkName"（首次使用时自动懒注册到 Registry）</item>
    /// </list>
    /// <para><b>多树支持</b>：一个 mod 可注册多棵 PerkTree，Perk 通过 treeId 参数显式指定归属树。</para>
    /// </remarks>
    public static class PerkTreeUtils
    {
        private static readonly PerkTreeRegistry _perkRegistry = new PerkTreeRegistry();
        private static readonly HashSet<string> _registeredTreeIds = new HashSet<string>();
        private static readonly Dictionary<string, HashSet<string>> _treeIdsByOwner = new Dictionary<string, HashSet<string>>();
        /// <summary>FML 内部 PerkTree 缓存。key=treeId(path)，独立于 PerkTreeManager.Instance 时序。</summary>
        private static readonly Dictionary<string, PerkTree> _registeredTrees = new Dictionary<string, PerkTree>();
        private static bool _initialized;
        private static bool _vanillaRetryHooked;
        private static bool _onSetFileHooked;
        private static bool _dumped; // 一次性 dump 标记

        /// <summary>
        /// Perk.Unlocked setter 事件抑制标志。PerkTree.SetupSaveData 批量操作期间设为 true，
        /// 阻止每个 perk 的 onUnlockStateChanged 触发 PerkDetails.Refresh() 导致 NRE
        /// （存档槽位选择时 UI 组件未就绪）。
        /// 由 PerkSetupSaveDataSuppressPatch 控制生命周期。
        /// </summary>
        internal static bool SuppressPerkEvents;

        /// <summary>原版树注入延迟队列：graph 在 OnAfterSetup 时未反序列化，待 MainSceneLoaded 后重试。</summary>
        private static readonly List<DeferredVanillaInject> _deferredVanillaInjects = new();

        /// <summary>已完成注入的原版 Perk 追踪集（PerkId 字符串形式）。
        /// 场景重载后 Perk 子对象被销毁，MainSceneLoaded 时检测到已销毁则重建。</summary>
        private static readonly HashSet<string> _completedPerkInjects = new();

        private struct DeferredVanillaInject
        {
            public string treeId;
            public PerkConfig config;
        }

        /// <summary>使用 <c>"PerkTree_"</c> 前缀标记 FML 注册的自定义 PerkTree。</summary>
        internal const string FML_TREE_PREFIX = "PerkTree_";

        // ── 全局重布局参数（首次访问时触发一次） ──
        private const float LAYOUT_X_SPACING = 120f;  // 居中布局的 X 间距
        private const float LAYOUT_Y_SPACING = 100f;  // 居中布局的 Y 间距
        private static readonly HashSet<Graph> _layoutedGraphs = new HashSet<Graph>();
        private static readonly HashSet<PerkRelationNode> _manualNodes = new HashSet<PerkRelationNode>();

        internal static PerkTreeRegistry Registry => _perkRegistry;

        internal static void Init()
        {
            if (_initialized) return;
            _initialized = true;
            var id = new Identifier(FMLConstants.Domain, "perk");
            var meta = RegistryManager.Instance.Registry;
            if (meta is NonAlterableSimpleRegistry<ERegistry> nonAlt)
                nonAlt.SetIfAbsent(id, _perkRegistry, RegistryManager.CurrentModid);
            else
                meta.Set(id, _perkRegistry, RegistryManager.CurrentModid);

            HookVanillaRetry();
            HookOnSetFileCleanup();
        }

        /// <summary>订阅 MainSceneLoaded，重试 graph 加载期间暂缓的原版树注入。</summary>
        private static void HookVanillaRetry()
        {
            if (_vanillaRetryHooked) return;
            _vanillaRetryHooked = true;
            EventBusManager.Instance.Sync.Register<MainSceneLoadedEvent>(OnMainSceneLoadedRetryInjects);
        }

        /// <summary>
        /// 订阅 SavesSystem.OnSetFile，在存档槽位切换时清理场景上下文相关的缓存。
        /// _deferredVanillaInjects：旧场景的延迟注入队列已无效（场景已切换）。
        /// _completedPerkInjects：场景重建后 Perk 子对象被销毁，旧追踪集失效。
        /// </summary>
        private static void HookOnSetFileCleanup()
        {
            if (_onSetFileHooked) return;
            _onSetFileHooked = true;
            SavesSystem.OnSetFile += OnSetFileCleanup;
        }

        private static void OnSetFileCleanup()
        {
            int deferredCount = _deferredVanillaInjects.Count;
            int completedCount = _completedPerkInjects.Count;

            _deferredVanillaInjects.Clear();
            _completedPerkInjects.Clear();

            if (deferredCount > 0 || completedCount > 0)
                Debug.Log($"[PerkTreeUtils] OnSetFile: cleared {deferredCount} deferred inject(s) + {completedCount} completed inject(s).");
        }

        private static void OnMainSceneLoadedRetryInjects(MainSceneLoadedEvent evt)
        {
            if (_deferredVanillaInjects.Count == 0) return;
            Debug.Log($"[PerkTreeUtils] MainSceneLoaded: retrying {_deferredVanillaInjects.Count} deferred vanilla inject(s)...");

            // ═══ Pass 1：收集需要重建的条目 + 创建 Perk GameObject + 注册 ═══
            // 先创建所有 Perk 再建连接，确保 ResolvePerk(reqId) 始终返回新实例（非临时 Perk）。
            var rebuildList = new List<(DeferredVanillaInject entry, PerkTree tree, PerkRelationGraph graph, Perk perk)>();
            var deadTreeIds = new List<int>(); // 待移除的无效条目索引

            for (int i = _deferredVanillaInjects.Count - 1; i >= 0; i--)
            {
                var entry = _deferredVanillaInjects[i];
                var perkIdStr = entry.config.PerkId.ToString();

                var tree = ResolvePerkTree(new Identifier(FMLConstants.DuckovDomain, entry.treeId));
                if (tree == null)
                {
                    Debug.LogWarning($"[PerkTreeUtils] Retry failed: vanilla tree '{entry.treeId}' not found.");
                    deadTreeIds.Add(i);
                    _completedPerkInjects.Remove(perkIdStr);
                    continue;
                }

                var graph = tree.relationGraphOwner?.graph as PerkRelationGraph;
                if (graph == null) continue; // graph 未就绪，保留等待

                // 存活检测
                bool isCompleted = _completedPerkInjects.Contains(perkIdStr);
                bool perkAlive = false;
                if (_perkRegistry.TryGet(entry.config.PerkId, out var existingPerk)
                    && existingPerk != null && existingPerk.gameObject != null)
                    perkAlive = true;

                if (isCompleted && perkAlive) continue; // 已完成且存活，跳过

                // ── 需要重建 ──
                DestroyOldPerkChild(tree, entry.config.PerkId.Path);

                var perkGo = new GameObject(entry.config.PerkId.Path);
                perkGo.transform.SetParent(tree.transform, false);
                var perk = perkGo.AddComponent<Perk>();
                perk.icon = entry.config.Icon;
                perk.displayName = entry.config.DisplayNameKey;
                perk.hasDescription = entry.config.HasDescription;
                perk.quality = entry.config.Quality;
                perk.defaultUnlocked = entry.config.DefaultUnlocked;
                perk.requirement = entry.config.BuildPerkRequirement();

                tree.Collect();
                _perkRegistry.Set(entry.config.PerkId, perk, entry.config.PerkId.Domain);
                _completedPerkInjects.Add(perkIdStr);

                rebuildList.Add((entry, tree, graph, perk));
            }

            // 移除无效条目
            for (int i = deadTreeIds.Count - 1; i >= 0; i--)
                _deferredVanillaInjects.RemoveAt(deadTreeIds[i]);

            // ═══ Pass 2：建立连接（此时所有 Perk 新实例均已在 _perkRegistry 中） ═══
            var loadedTrees = new HashSet<string>();
            foreach (var (entry, tree, graph, perk) in rebuildList)
            {
                if (entry.config.RequiredPerks != null && entry.config.RequiredPerks.Length > 0)
                {
                    foreach (var reqId in entry.config.RequiredPerks)
                    {
                        var reqPerk = ResolvePerkDirect(tree, reqId);
                        if (reqPerk != null)
                            ConnectPerksInternal(reqPerk, perk, entry.config.Position);
                    }
                }

                EnsureGraphNode(tree, perk, entry.config);

                if (loadedTrees.Add(entry.treeId))
                    tree.Load();

                if (entry.config.Behaviours != null)
                {
                    foreach (var bhCfg in entry.config.Behaviours)
                        bhCfg.ApplyTo(perk.gameObject);
                }
            }

            if (rebuildList.Count > 0)
                Debug.Log($"[PerkTreeUtils] Retried {rebuildList.Count} vanilla inject(s).");
        }

        /// <summary>销毁 PerkTree 上指定名称的旧子对象（避免 tree.Collect 收集重复 Perk）。
        /// 注意：<c>Transform.Find</c> 返回 Transform，必须取其 gameObject 再 Destroy。</summary>
        private static void DestroyOldPerkChild(PerkTree tree, string childName)
        {
            var oldChild = tree.transform.Find(childName);
            if (oldChild != null)
                UnityEngine.Object.Destroy(oldChild.gameObject);
        }

        // ===== 注册 PerkTree =====

        /// <summary>
        /// 完整注册一棵自定义 PerkTree，含 LevelConfig patch。
        /// 自动创建 PerkTree GameObject + PerkRelationGraph + 注入到 PerkTreeManager。
        /// </summary>
        /// <param name="id">Identifier——Domain=modid, Path=treeID。</param>
        /// <param name="horizontal">连线方向是否水平（默认 false=垂直）。</param>
        /// <returns>创建的 PerkTree 实例。</returns>
        public static PerkTree RegisterPerkTree(Identifier id, bool horizontal = false)
        {
            Init();
            string treeId = id.Path;

            // 1. 创建 PerkTree GameObject + PerkTree 组件
            //    先 SetActive(false) 阻止 Awake() 在 perkTreeID 未设置时执行，
            //    避免 Load() 使用空 SaveKey 导致存档永久无法恢复（Bug #1）。
            var go = new GameObject($"{FML_TREE_PREFIX}{treeId}");
            go.SetActive(false);
            var tree = go.AddComponent<PerkTree>();

            // PerkTree 模板需跨场景存活，防止场景切换后 PerkTreeManager 持有僵尸引用
            UnityEngine.Object.DontDestroyOnLoad(go);

            // 所有字段必须在 SetActive(true) 前设置完毕，确保 Awake→Load 时已就绪
            tree.perkTreeID = treeId;
            tree.horizontal = horizontal;

            // 2. 创建 PerkTreeRelationGraphOwner（graph 暂不赋值，匹配原版 null-graph 防御路径）
            var graph = ScriptableObject.CreateInstance<PerkRelationGraph>();
            graph.name = $"PerkRelationGraph_{treeId}";
            var graphOwner = go.AddComponent<PerkTreeRelationGraphOwner>();
            tree.relationGraphOwner = graphOwner;

            // ★ 此时 Awake() 才执行：perkTreeID 已正确，Load() 用正确 SaveKey 读档
            //    GraphOwner.Awake 遇到 null graph 走原版防御路径，不会 NRE
            go.SetActive(true);

            // graph 在 Awake 之后赋值（匹配原版时序：AddComponent 触发 Awake → 再设 graph）
            graphOwner.graph = graph;

            // 3. 注入到 PerkTreeManager.perkTrees（public 字段，直接访问）
            if (PerkTreeManager.Instance != null)
            {
                var perkTrees = PerkTreeManager.Instance.perkTrees;
                if (perkTrees != null && !perkTrees.Contains(tree))
                    perkTrees.Add(tree);
            }

            // 4. 记录已注册的 treeId（供 IsFMLTree / patches 使用）和 owner 映射
            //    同时缓存 PerkTree 引用到 FML 内部字典，避免依赖 PerkTreeManager.Instance 时序
            _registeredTrees[treeId] = tree;
            _registeredTreeIds.Add(treeId);
            if (!_treeIdsByOwner.TryGetValue(id.Domain, out var treeSet))
            {
                treeSet = new HashSet<string>();
                _treeIdsByOwner[id.Domain] = treeSet;
            }
            treeSet.Add(treeId);
            // 在 Perk 注册表中注册占位条目（便于按 modid 卸载时触发遍历）
            var registryId = new Identifier(id.Domain, $"tree_{treeId}");
            _perkRegistry.Set(registryId, null!, id.Domain);

            Debug.Log($"[PerkTreeUtils] Registered PerkTree '{treeId}' from mod '{id.Domain}'. "
                + $"(cached={_registeredTrees.ContainsKey(treeId)}, "
                + $"treeIds={_registeredTreeIds.Contains(treeId)}, "
                + $"instanceNull={PerkTreeManager.Instance == null})");
            return tree;
        }

        /// <summary>检查指定 treeId 是否由 FML 注册。</summary>
        internal static bool IsFMLTree(string treeId)
        {
            return _registeredTreeIds.Contains(treeId);
        }

        /// <summary>从 FML 内部缓存查找已注册的 PerkTree（供 PerkTreeManager 补丁使用）。</summary>
        internal static bool TryGetRegisteredTree(string treeId, out PerkTree tree)
        {
            return _registeredTrees.TryGetValue(treeId, out tree);
        }

        // ===== 添加 Perk =====

        /// <summary>
        /// 在指定 PerkTree 上注册新 Perk。
        /// </summary>
        /// <param name="treeId">
        /// 目标 PerkTree 的 Identifier。
        /// <br/>Domain="duckov" → 原版树，按 Path=treeID 从 <see cref="PerkTreeManager"/> 查找；
        /// <br/>其他 Domain → FML 注册的自定义树。
        /// </param>
        /// <param name="config">Perk 配置 DTO。PerkId.Domain=modid, PerkId.Path=perk名（兼作 GameObject.name，影响存档 key）。</param>
        /// <returns>创建的 Perk 实例。</returns>
        /// <exception cref="ArgumentException">目标 PerkTree 不存在时抛出。</exception>
        public static Perk AddPerk(Identifier treeId, PerkConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            Init();
            string owner = config.PerkId.Domain;

            // 解析目标 PerkTree
            PerkTree? tree = ResolvePerkTree(treeId);
            if (tree == null)
                throw new ArgumentException($"PerkTree '{treeId}' not found.");

            // 创建子 GameObject 挂 Perk 组件
            var perkGo = new GameObject(config.PerkId.Path);
            perkGo.transform.SetParent(tree.transform, false);
            var perk = perkGo.AddComponent<Perk>();

            // 直接设置 Perk 字段（[SerializeField] private 经 Publicizer 已公开）
            if (config.Icon != null)
                perk.icon = config.Icon;
            perk.displayName = config.DisplayNameKey;
            perk.hasDescription = config.HasDescription;
            perk.quality = config.Quality;
            perk.defaultUnlocked = config.DefaultUnlocked;
            perk.requirement = config.BuildPerkRequirement();

            // 注册 Perk 到 PerkTree：
            // - 原版树正常走 Collect（扫描子对象 + 设置 Master）
            // - FML 自定义树由 PerkTreeCollectGuard 拦截 Collect，需手动注册
            if (IsFMLTree(tree.perkTreeID))
            {
                perk.Master = tree;
                tree.perks.Add(perk);
                // 自动从存档恢复解锁状态（PerkTreeLoadDeferPatch 在 Awake 期拦截了空 perks 的 Load，此处补触发）
                tree.Load();
            }
            else
            {
                tree.Collect();

                // 原版树注入：OnAfterSetup 时 graph 未反序列化，延迟到 MainSceneLoaded 重试
                var graph = tree.relationGraphOwner?.graph as PerkRelationGraph;
                if (graph == null)
                {
                    Debug.Log($"[PerkTreeUtils] Deferring vanilla inject: '{config.PerkId.Path}' into '{tree.perkTreeID}' (graph not loaded yet).");
                    _deferredVanillaInjects.Add(new DeferredVanillaInject
                    {
                        treeId = tree.perkTreeID,
                        config = config
                    });
                    // 仍注册到 Registry（供 modder 当前会话查询），MainSceneLoaded 时会被覆盖
                    _perkRegistry.Set(config.PerkId, perk, owner);
                    return perk;
                }

                // graph 已就绪：从存档恢复注入节点的解锁状态
                // 原版 PerkTree.Awake→Load 在注入前执行，此时 perk 不在列表中会被跳过；
                // 此处补触发 Load 以恢复存档中的 perk 状态。
                tree.Load();
            }

            // 注册到 FML Registry
            _perkRegistry.Set(config.PerkId, perk, owner);

            // ★ 先建立连线（ConnectPerksInternal 基于父节点坐标计算子节点位置）
            if (config.RequiredPerks != null)
            {
                foreach (var requiredId in config.RequiredPerks)
                {
                    var requiredPerk = ResolvePerk(requiredId);
                    if (requiredPerk != null)
                        ConnectPerksInternal(requiredPerk, perk, config.Position);
                }
            }

            // 兜底：无 RequiredPerks 或连线后仍缺失时创建独立节点
            EnsureGraphNode(tree, perk, config);

            // 处理 PerkBehaviour 声明式配置
            if (config.Behaviours != null)
            {
                foreach (var bhCfg in config.Behaviours)
                {
                    bhCfg.ApplyTo(perk.gameObject);
                }
            }

            return perk;
        }

        /// <summary>解析 PerkTree：Domain="duckov" → 原版树，否则 → FML/PerkTreeManager 查找。</summary>
        private static PerkTree? ResolvePerkTree(Identifier treeId)
        {
            if (treeId.Domain == FMLConstants.DuckovDomain)
            {
                // 原版树：直接按 Path=treeID 查找
                return PerkTreeManager.GetPerkTree(treeId.Path);
            }

            // FML 注册的自定义树：优先走 FML 内部缓存（不依赖 PerkTreeManager.Instance 时序）
            if (_registeredTrees.TryGetValue(treeId.Path, out var cachedTree))
            {
                // 延迟注入：PerkTreeManager 已可用但 RegisterPerkTree 时 Instance 为 null 导致未注入
                // 此处自动补注入，使树对游戏存档/UI 等原生系统可见
                if (PerkTreeManager.Instance != null)
                {
                    var perkTrees = PerkTreeManager.Instance.perkTrees;
                    if (perkTrees != null && !perkTrees.Contains(cachedTree))
                        perkTrees.Add(cachedTree);
                }
                return cachedTree;
            }

            // 回退：已追踪但缓存丢失时，尝试从 PerkTreeManager 查找
            if (_registeredTreeIds.Contains(treeId.Path))
            {
                var prefixName = $"{FML_TREE_PREFIX}{treeId.Path}";
                if (PerkTreeManager.Instance?.perkTrees != null)
                {
                    foreach (var t in PerkTreeManager.Instance.perkTrees)
                    {
                        if (t != null && t.name == prefixName)
                            return t;
                    }
                }
            }

            // 回退：直接走 PerkTreeManager（支持未被 FML 追踪但存在于游戏中的树）
            return PerkTreeManager.GetPerkTree(treeId.Path);
        }

        // ===== 连接 Perk =====

        /// <summary>
        /// 建立 Perk 前置关系：fromPerk 是 toPerk 的前置条件。
        /// 两个参数均为 Identifier。自定义 Perk 直接从 Registry 查询，
        /// 原版 Perk（Domain="duckov"）首次引用时自动懒注册到 Registry。
        /// </summary>
        public static void ConnectPerks(Identifier fromPerkId, Identifier toPerkId)
        {
            var fromPerk = ResolvePerk(fromPerkId);
            var toPerk = ResolvePerk(toPerkId);
            if (fromPerk == null || toPerk == null) return;

            ConnectPerksInternal(fromPerk, toPerk);
        }

        /// <summary>Perk → Perk 内部连接（无需 Identifier 解析）。创建 Graph 节点并建立连线。</summary>
        private static void ConnectPerksInternal(Perk fromPerk, Perk toPerk, Vector2? manualPos = null)
        {
            if (fromPerk.Master == null || toPerk.Master == null) return;
            if (fromPerk.Master != toPerk.Master) return;

            var graph = fromPerk.Master.relationGraphOwner?.graph as PerkRelationGraph;
            if (graph == null) return;

            // 确保 fromNode 存在
            var fromNode = graph.GetRelatedNode(fromPerk)
                ?? graph.AddNode<PerkRelationNode>(Vector2.zero);
            fromNode.relatedNode = fromPerk;

            // 确保 toNode 存在
            var toNode = graph.GetRelatedNode(toPerk);
            if (toNode == null)
            {
                Vector2 pos;
                if (manualPos.HasValue)
                {
                    pos = manualPos.Value;
                }
                else if (IsFMLTree(fromPerk.Master.perkTreeID))
                {
                    // FML 自定义树：占位，等 TryLayoutGraph 全局居中布局
                    pos = Vector2.zero;
                }
                else
                {
                    // 原版树注入节点：即时计算位置（不会触发全局布局）
                    var existing = graph.GetOutgoingNodes(fromNode);
                    pos = new Vector2(
                        fromNode.cachedPosition.x + (existing?.Count ?? 0) * LAYOUT_X_SPACING,
                        fromNode.cachedPosition.y + LAYOUT_Y_SPACING);
                }

                toNode = graph.AddNode<PerkRelationNode>(pos);
                toNode.cachedPosition = pos;
                if (manualPos.HasValue) _manualNodes.Add(toNode);
            }
            toNode.relatedNode = toPerk;

            graph.ConnectNodes(fromNode, toNode);
        }

        /// <summary>为 Perk 创建 Graph 节点。FML 树用占位，原版树注入用即时位置。</summary>
        private static void EnsureGraphNode(PerkTree tree, Perk perk, PerkConfig config)
        {
            var graph = tree.relationGraphOwner?.graph as PerkRelationGraph;
            if (graph == null)
            {
                if (!IsFMLTree(tree.perkTreeID))
                    Debug.LogWarning($"[PerkTreeUtils] EnsureGraphNode: tree '{tree.perkTreeID}' has no graph (owner={tree.relationGraphOwner != null}).");
                return;
            }
            if (graph.GetRelatedNode(perk) != null)
            {
                Debug.Log($"[PerkTreeUtils] EnsureGraphNode: node for '{perk.name}' already exists in graph.");
                return;
            }

            Vector2 pos;
            if (config.Position.HasValue)
            {
                pos = config.Position.Value;
            }
            else if (IsFMLTree(tree.perkTreeID))
            {
                // FML 树：占位，留待 TryLayoutGraph 全局居中
                pos = Vector2.zero;
            }
            else
            {
                // 原版树注入：放在现有节点最右侧，避免覆盖
                float maxX = 0f;
                foreach (var n in graph.allNodes.OfType<PerkRelationNode>())
                {
                    if (n.relatedNode != null && n.cachedPosition.x > maxX)
                        maxX = n.cachedPosition.x;
                }
                pos = new Vector2(maxX + LAYOUT_X_SPACING, 0);
            }

            var node = graph.AddNode<PerkRelationNode>(pos);
            node.relatedNode = perk;
            node.cachedPosition = pos;
            if (config.Position.HasValue) _manualNodes.Add(node);

            if (!IsFMLTree(tree.perkTreeID))
            {
                Debug.Log($"[PerkTreeUtils] Injected graph node for '{perk.name}' into vanilla tree '{tree.perkTreeID}' at ({pos.x:F0}, {pos.y:F0}).");
            }
        }

        // ===== 全局布局 =====

        /// <summary>对 graph 执行 BFS 分层居中布局。每个 graph 仅布局一次（幂等）。</summary>
        internal static void TryLayoutGraph(PerkRelationGraph graph)
        {
            if (graph == null || !_layoutedGraphs.Add(graph)) return;

            var allNodes = graph.allNodes.OfType<PerkRelationNode>()
                .Where(n => n.relatedNode != null).ToList();
            if (allNodes.Count == 0) return;

            // Step 1: BFS 分配深度（处理多前置取最大值）
            var depth = new Dictionary<PerkRelationNode, int>();
            var queue = new Queue<PerkRelationNode>();
            foreach (var n in allNodes)
            {
                var incoming = graph.GetIncomingNodes(n);
                if (incoming == null || incoming.Count == 0) { depth[n] = 0; queue.Enqueue(n); }
            }
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                var outgoing = graph.GetOutgoingNodes(cur);
                if (outgoing == null) continue;
                int nextD = depth[cur] + 1;
                foreach (var child in outgoing)
                {
                    if (!depth.TryGetValue(child, out var d) || d < nextD)
                    { depth[child] = nextD; if (!queue.Contains(child)) queue.Enqueue(child); }
                }
            }

            // Step 2: 按深度分组，每层居中排列
            var byDepth = new Dictionary<int, List<PerkRelationNode>>();
            int maxDepth = 0;
            foreach (var n in allNodes)
            {
                int d = depth.TryGetValue(n, out var v) ? v : 0;
                if (!byDepth.TryGetValue(d, out var list)) byDepth[d] = list = new List<PerkRelationNode>();
                list.Add(n);
                if (d > maxDepth) maxDepth = d;
            }

            for (int d = 0; d <= maxDepth; d++)
            {
                if (!byDepth.TryGetValue(d, out var nodes)) continue;
                // 按父节点期望 X 排序（使同分支节点聚合）
                nodes.Sort((a, b) => AvgParentX(graph, a).CompareTo(AvgParentX(graph, b)));

                // 收集非手动节点，居中排列
                var autoNodes = nodes.Where(n => !_manualNodes.Contains(n)).ToList();
                if (autoNodes.Count == 0) continue;

                float totalWidth = (autoNodes.Count - 1) * LAYOUT_X_SPACING;
                float startX = -totalWidth / 2f;

                for (int i = 0; i < autoNodes.Count; i++)
                    autoNodes[i].cachedPosition = new Vector2(startX + i * LAYOUT_X_SPACING, d * LAYOUT_Y_SPACING);
            }
        }

        private static float AvgParentX(PerkRelationGraph graph, PerkRelationNode node)
        {
            var incoming = graph.GetIncomingNodes(node);
            if (incoming == null || incoming.Count == 0) return 0f;
            return incoming.Average(n => n.cachedPosition.x);
        }

        // ===== PerkBehaviour 辅助 =====

        /// <summary>在已有 Perk 上挂载自定义 PerkBehaviour。perkId 为 Identifier。</summary>
        public static T AddPerkBehaviour<T>(Identifier perkId) where T : PerkBehaviour
        {
            var perk = ResolvePerk(perkId);
            if (perk == null) return null!;
            return perk.gameObject.AddComponent<T>();
        }

        // ===== 解锁 =====

        /// <summary>
        /// 强制解锁指定 Perk。自定义 Perk 直接从 Registry 查询，
        /// 原版 Perk（Domain="duckov"）首次引用时自动懒注册。
        /// </summary>
        public static void ForceUnlock(Identifier perkId)
        {
            var perk = ResolvePerk(perkId);
            if (perk != null)
                perk.ForceUnlock();
        }

        /// <summary>
        /// 检查指定 Perk 是否已解锁。
        /// 自定义 Perk 直接从 Registry 查询，原版 Perk 首次引用时自动懒注册。
        /// </summary>
        public static bool IsPerkUnlocked(Identifier perkId)
        {
            var perk = ResolvePerk(perkId);
            if (perk == null) return false;
            return perk.defaultUnlocked || perk.Unlocked;
        }

        // ===== 移除 =====

        /// <summary>按 Identifier 移除 Perk。</summary>
        public static bool RemovePerk(Identifier id) => _perkRegistry.Remove(id);

        /// <summary>批量卸载指定 mod 注册的全部 Perk 和自定义 PerkTree。</summary>
        public static int RemoveAllPerks(string modid)
        {
            int count = _perkRegistry.RemoveAllByOwner(modid);

            // 清理该 mod 注册的自定义 PerkTree
            if (_treeIdsByOwner.TryGetValue(modid, out var treeSet))
            {
                foreach (var treeId in treeSet)
                {
                    // 从 FML 内部缓存移除
                    _registeredTreeIds.Remove(treeId);
                    if (_registeredTrees.TryGetValue(treeId, out var cachedTree))
                    {
                        _registeredTrees.Remove(treeId);
                        if (cachedTree != null && cachedTree.gameObject != null)
                            UnityEngine.Object.Destroy(cachedTree.gameObject);
                    }

                    // 从 PerkTreeManager 同步移除（如果 Instance 可用）
                    var trees = PerkTreeManager.Instance?.perkTrees;
                    if (trees != null)
                    {
                        for (int i = trees.Count - 1; i >= 0; i--)
                        {
                            if (trees[i] != null && trees[i].name == $"{FML_TREE_PREFIX}{treeId}")
                            {
                                trees.RemoveAt(i);
                                break;
                            }
                        }
                    }
                    count++;
                }
                treeSet.Clear();
            }

            return count;
        }

        // ===== 内部辅助 =====

        /// <summary>
        /// 按 Identifier 解析 Perk 实例。先在 Registry 中查找，
        /// 未找到且 Domain="duckov" 时触发懒注册（从原版 PerkTree 中查找对应 Perk）。
        /// </summary>
        private static Perk? ResolvePerk(Identifier perkId)
        {
            if (_perkRegistry.TryGet(perkId, out var perk))
                return perk;
            return TryLazyRegister(perkId);
        }

        /// <summary>
        /// 懒注册原版 Perk。从 PerkTreeManager 中按 "treeID/perkName" 路径查找对应 Perk，
        /// 找到后写入 _perkRegistry（owner = FMLConstants.VanillaOwner），供后续 API 使用。
        /// </summary>
        private static Perk? TryLazyRegister(Identifier perkId)
        {
            if (perkId.Domain != FMLConstants.DuckovDomain) return null;

            // Path 格式: "treeID/perkName"
            var slashIndex = perkId.Path.IndexOf('/');
            if (slashIndex <= 0 || slashIndex >= perkId.Path.Length - 1) return null;

            var treeId = perkId.Path.Substring(0, slashIndex);
            var perkName = perkId.Path.Substring(slashIndex + 1);

            var tree = PerkTreeManager.GetPerkTree(treeId);
            if (tree == null) return null;

            foreach (var p in tree.perks)
            {
                if (p != null && p.name == perkName)
                {
                    _perkRegistry.Set(perkId, p, FMLConstants.VanillaOwner);
                    return p;
                }
            }

            return null;
        }

        /// <summary>
        /// 在指定树中直接搜索前置 Perk（不走 PerkTreeManager，避免场景重载后返回陈旧实例）。
        /// 先在 _perkRegistry 中查找，未命中则在 tree.perks 中按 name 匹配。
        /// </summary>
        private static Perk? ResolvePerkDirect(PerkTree tree, Identifier perkId)
        {
            // 先查 Registry（已注册的自定义 Perk 或已懒注册的原版 Perk）
            if (_perkRegistry.TryGet(perkId, out var perk) && perk != null)
                return perk;

            // 原版 Perk：从 Path 提取 perkName，在 tree.perks 中直接搜索
            if (perkId.Domain != FMLConstants.DuckovDomain) return null;

            var slashIndex = perkId.Path.IndexOf('/');
            if (slashIndex <= 0 || slashIndex >= perkId.Path.Length - 1) return null;
            var perkName = perkId.Path.Substring(slashIndex + 1);

            foreach (var p in tree.perks)
            {
                if (p != null && p.name == perkName)
                {
                    _perkRegistry.Set(perkId, p, FMLConstants.VanillaOwner);
                    return p;
                }
            }

            return null;
        }

        // ===== 诊断：导出原版 PerkTree 节点数据 =====

        /// <summary>
        /// 导出所有 PerkTree（含原版和 FML 自定义树）的节点信息到日志。
        /// 用于帮助 modder 确认 <c>RequiredPerks</c> 中正确的 perkName 格式。
        /// <para>输出包含：Tree ID、perk GameObject.name（用于匹配）、DisplayName 本地化结果、DefaultUnlocked 状态。</para>
        /// </summary>
        public static void DumpAllPerkTrees()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════");
            sb.AppendLine("  PerkTree Node Reference — copy-paste ready");
            sb.AppendLine("═══════════════════════════════════════════════");

            var trees = PerkTreeManager.Instance?.perkTrees;
            if (trees == null)
            {
                Debug.LogWarning("[PerkTreeUtils] Cannot dump: PerkTreeManager.Instance or perkTrees is null. "
                    + "Call this after scene load (e.g. in OnLevelInitialized).");
                return;
            }

            foreach (var tree in trees)
            {
                if (tree == null) continue;
                sb.AppendLine();
                sb.AppendLine($"── PerkTree: {tree.ID} ──");
                sb.AppendLine($"   GameObject.name: {tree.name}");
                sb.AppendLine($"   DisplayName: {tree.DisplayName}");
                sb.AppendLine($"   IsFML: {IsFMLTree(tree.ID)}");
                sb.AppendLine($"   Perk count: {tree.perks.Count}");
                sb.AppendLine();

                for (int i = 0; i < tree.perks.Count; i++)
                {
                    var perk = tree.perks[i];
                    if (perk == null) continue;
                    sb.AppendLine($"   [{i}] name=\"{perk.name}\""
                        + $"  displayKey=\"{perk.displayName}\""
                        + $"  displayName=\"{perk.DisplayName}\""
                        + $"  defaultUnlocked={perk.defaultUnlocked}"
                        + $"  quality={perk.quality}");
                    sb.AppendLine($"       → Identifier: new Identifier(\"duckov\", \"{tree.ID}/{perk.name}\")");
                }
            }

            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════");
            sb.AppendLine("  Usage in PerkConfig:");
            sb.AppendLine("    RequiredPerks = new[] {");
            sb.AppendLine("        new Identifier(\"duckov\", \"TreeID/perkName\"),");
            sb.AppendLine("    }");
            sb.AppendLine("═══════════════════════════════════════════════");

            Debug.Log(sb.ToString());
        }
    }
}
