using Duckov;
using Duckov.Buildings;
using Duckov.Economy;
using Duckov.Utilities;
using FeatherMod.Events;
using FeatherMod.Events.GameEvents;
using FeatherMod.Interaction;
using FeatherMod.Interaction.Components;
using FeatherMod.Register;
using FeatherMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FeatherMod
{
    public static class BuildingUtils
    {
        private static readonly BuildingRegistry _buildingRegistry = new BuildingRegistry();
        private static bool _initialized;

        // BuyAndPlace 经 Publicizer 公开，可直接调用。
        // 保留 PlaceBuilding 方法通过 BuildingManager.BuyAndPlace(...) 直接调用。

        /// <summary>暴露给 RegisterBootstrap 和 Patch 层用于注册到元表和查询。</summary>
        public static BuildingRegistry Registry => _buildingRegistry;

        /// <summary>
        /// 建筑 prefab 的 inactive 容器。代码创建的 prefab 必须跨场景存活
        /// （DontDestroyOnLoad），但 active 的 prefab 会被加载界面 Curtain 相机
        /// （CullingMask=Everything，DepthOnly）渲染，导致"建筑出现在加载界面"。
        /// 挂到 inactive 容器下：prefab.activeSelf 保持 true（Instantiate 的实例正常激活），
        /// 但 activeInHierarchy=false（prefab 本身不渲染，与原版 asset prefab 语义一致）。
        /// </summary>
        private static GameObject? _prefabHolder;
        private static GameObject PrefabHolder
        {
            get
            {
                if (_prefabHolder == null)
                {
                    _prefabHolder = new GameObject("FML_BuildingPrefabs");
                    _prefabHolder.SetActive(false);
                    UnityEngine.Object.DontDestroyOnLoad(_prefabHolder);
                }
                return _prefabHolder;
            }
        }

        /// <summary>初始化：将 BuildingRegistry 注册到 RegistryManager 元表。</summary>
        internal static void Init()
        {
            if (_initialized) return;
            _initialized = true;
            var id = new Identifier(FMLConstants.Domain, "building");
            var meta = RegistryManager.Instance.Registry;
            if (meta is NonAlterableSimpleRegistry<ERegistry> nonAlt)
                nonAlt.SetIfAbsent(id, _buildingRegistry, RegistryManager.CurrentModid);
            else
                meta.Set(id, _buildingRegistry, RegistryManager.CurrentModid);

            // 永久订阅场景加载事件：场景重载后重建 Machine 交互节点。
            // 参考 FriendlyNpcUtils.HookSaveRestore() 模式——场景加载后自动恢复运行时对象。
            HookSceneLoadEvent();
        }

        // ===== 注册 / 卸载 =====

        /// <summary>
        /// 注册自定义建筑。将 <see cref="BuildingInfo"/> 和对应 <see cref="Building"/>
        /// prefab 登入 FML Registry。<see cref="BuildingCollectionPatch"/> 的 Harmony Postfix
        /// 自动回退到 Registry 查找，无需直接写入 <c>BuildingDataCollection</c>。
        /// modid 从 <see cref="Identifier.Domain"/> 推导。
        /// </summary>
        public static void RegisterBuilding(Identifier id, BuildingInfo info, Building prefab)
        {
            Init();

            // 修复 BuildingInfo 中可能为 null 的引用类型字段，
            // 防止游戏原生 RequirementsSatisfied() 遍历时 NRE
            SanitizeBuildingInfo(ref info);

            // Prefab 需跨场景存活（纯代码创建的 GameObject 在场景切换时会被销毁）
            UnityEngine.Object.DontDestroyOnLoad(prefab.gameObject);
            // 挂到 inactive 容器下：prefab 本体不渲染（修复加载界面 Curtain 相机误渲染），
            // Instantiate 出的实例 activeSelf=true 不受影响，正常显示。
            prefab.gameObject.transform.SetParent(PrefabHolder.transform, false);

            // 仅将 prefab 写入原生 collection.prefabs（游戏 BeginPlacing 直接遍历此列表取 prefab），
            // BuildingInfo 不写 infos（由 Harmony GetBuildingsToDisplay_Postfix 追加，避免双注册）
            var collection = GameplayDataSettings.BuildingDataCollection;
            if (collection != null)
            {
                collection.prefabs ??= new System.Collections.Generic.List<Building>();
                if (!collection.prefabs.Contains(prefab))
                    collection.prefabs.Add(prefab);
            }

            // FML Registry 侧登记
            _buildingRegistry.Register(id, info, prefab, id.Domain);
        }

        /// <summary>
        /// 注册自定义建筑（BuildingConfig 重载）。自动将 <see cref="BuildingConfig"/>
        /// 转换为游戏原生 <see cref="BuildingInfo"/>，包括 Identifier→TypeID 成本解析。
        /// 若未提供 prefab，自动调用 <see cref="CreateSimpleBuilding"/> 创建。
        /// </summary>
        /// <param name="config">建筑配置 DTO（含 Id、尺寸、成本、解锁条件）。</param>
        /// <param name="prefab">可选：自定义 Building prefab。为 null 时从 config 自动创建。</param>
        public static void RegisterBuilding(BuildingConfig config, Building? prefab = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            // 确定 prefabName：modder 可显式指定 PrefabName，
            // 否则自动加 "Building_" 前缀与游戏原生命名一致。
            // 注意 info.id 不带前缀，因为 BuildingInfo.DisplayNameKey 会追加 "Building_"。
            var prefabName = config.PrefabName.Length > 0
                ? config.PrefabName
                : $"Building_{config.Id.Path}";

            // 解析或创建 prefab
            if (prefab == null)
            {
                prefab = CreateSimpleBuilding(config.Id, config.Dimensions, config.ExistingPrefabName);
            }

            // Prefab 需跨场景存活，防止场景切换后注册表持有僵尸引用
            // （鸭科夫场景切换频繁，纯代码创建的 GameObject 不会自动保留）
            if (prefab != null)
                UnityEngine.Object.DontDestroyOnLoad(prefab.gameObject);

            // 确保 prefab GameObject.name 与 prefabName 一致，
            // 游戏 GetPrefab() 按 e.name == prefabName 精确匹配。
            if (prefab != null && prefab.name != prefabName)
                prefab.name = prefabName;

            // 构建原生 BuildingInfo
            var info = new BuildingInfo
            {
                id = config.Id.Path,
                prefabName = prefabName,
                maxAmount = config.MaxAmount,
                cost = config.BuildCost(),
                iconReference = config.Icon
            };

            // ── 解析 RequireBuildings：Identifier[] → string[] ──
            if (config.RequireBuildings != null && config.RequireBuildings.Length > 0)
            {
                var reqBuildings = new string[config.RequireBuildings.Length];
                for (int i = 0; i < config.RequireBuildings.Length; i++)
                    reqBuildings[i] = config.RequireBuildings[i].Path;
                info.requireBuildings = reqBuildings;
            }

            // ── 解析 RequireQuests：Identifier[] → int[] ──
            // 支持 FML 注册任务（QuestUtils.TryGetQuestId）和原版任务反查（TryGetVanillaQuestId）
            if (config.RequireQuests != null && config.RequireQuests.Length > 0)
            {
                var reqQuests = new System.Collections.Generic.List<int>(config.RequireQuests.Length);
                foreach (var qId in config.RequireQuests)
                {
                    if (QuestUtils.TryGetQuestId(qId, out int questIntId))
                        reqQuests.Add(questIntId);
                    else
                        Debug.LogWarning($"[FML Building] RequireQuest '{qId}' not found (not in FML registry or vanilla collection).");
                }
                if (reqQuests.Count > 0)
                    info.requireQuests = reqQuests.ToArray();
            }

            // 修复 null 数组字段，防止 RequirementsSatisfied() NRE
            SanitizeBuildingInfo(ref info);

            RegisterBuilding(config.Id, info, prefab);
        }

        /// <summary>按 Identifier 移除已注册的建筑。</summary>
        public static bool UnregisterBuilding(Identifier id) => _buildingRegistry.Remove(id);

        /// <summary>批量卸载指定 mod 注册的全部建筑。</summary>
        public static int UnregisterAllBuildings(string modid) => _buildingRegistry.RemoveAllByOwner(modid);

        // ===== 查询（Identifier 优先） =====

        /// <summary>按 Identifier 查询 BuildingInfo。优先查 Registry，再回退到 native collection。</summary>
        public static BuildingInfo? GetBuildingInfo(Identifier id)
        {
            // 优先查 Registry（覆盖自定义建筑）
            if (_buildingRegistry.TryGet(id, out var info))
                return info;

            // 回退到 native collection
            return GetBuildingInfo(id.Path);
        }

        /// <summary>
        /// 按建筑 id 字符串查询 BuildingInfo。
        /// </summary>
        [Obsolete("Use GetBuildingInfo(Identifier) instead.")]
        public static BuildingInfo? GetBuildingInfo(string buildingId)
        {
            var collection = GameplayDataSettings.BuildingDataCollection;
            foreach (var info in collection?.infos ?? System.Linq.Enumerable.Empty<BuildingInfo>())
            {
                if (info.id == buildingId)
                    return info;
            }
            return null;
        }

        /// <summary>获取所有已注册的建筑 Identifier 列表。</summary>
        public static IReadOnlyList<Identifier> GetAllBuildingIds()
        {
            var result = new List<Identifier>();
            foreach (var kvp in _buildingRegistry)
            {
                result.Add(kvp.Key);
            }
            return result;
        }

        /// <summary>
        /// 获取所有已注册的建筑 id 字符串列表（旧版）。
        /// </summary>
        [Obsolete("Use GetAllBuildingIds() (returns IReadOnlyList<Identifier>) instead.")]
        public static List<string> GetAllBuildingIdStrings()
        {
            var result = new List<string>();
            foreach (var info in GameplayDataSettings.BuildingDataCollection?.infos ?? System.Linq.Enumerable.Empty<BuildingInfo>())
            {
                result.Add(info.id);
            }
            return result;
        }

        /// <summary>
        /// 按 Identifier 获取建筑 prefab（GameObject with <see cref="Building"/> component）。
        /// 优先查 FML Registry，再回退到原生 <c>BuildingDataCollection</c>。
        /// </summary>
        /// <param name="buildingId">建筑 Identifier。</param>
        /// <returns>建筑 prefab；未注册时返回 null。</returns>
        public static Building? GetBuildingPrefab(Identifier buildingId)
        {
            // 优先查 FML Registry
            var info = GetBuildingInfo(buildingId);
            if (info != null && _buildingRegistry.TryGetPrefab(info.Value.prefabName, out var prefab))
                return prefab;

            // 回退到原生 collection
            return BuildingDataCollection.GetPrefab(buildingId.Path);
        }

        // ===== 放置建筑 =====

        /// <summary>
        /// 放置建筑。直接调用 <see cref="BuildingManager.BuyAndPlace"/>（经 Publicizer 公开）。
        /// areaId 和 buildingId 均为 Identifier——FML 内部将其 Path 映射为游戏原生 string ID。
        /// </summary>
        public static BuildingBuyAndPlaceResults PlaceBuilding(
            Identifier areaId, Identifier buildingId,
            Vector2Int coord, BuildingRotation rotation)
        {
            return BuildingManager.BuyAndPlace(areaId.Path, buildingId.Path, coord, rotation);
        }

        /// <summary>
        /// 放置建筑（旧版 string 签名）。
        /// </summary>
        [Obsolete("Use PlaceBuilding(Identifier, Identifier, Vector2Int, BuildingRotation) instead.")]
        public static BuildingBuyAndPlaceResults PlaceBuilding(
            string areaID, string buildingID,
            Vector2Int coord, BuildingRotation rotation)
        {
            return PlaceBuilding(
                new Identifier(RegistryManager.CurrentModid, areaID),
                new Identifier(RegistryManager.CurrentModid, buildingID),
                coord, rotation);
        }

        // ===== 成本构建 =====

        /// <summary>
        /// 从 FML <see cref="ItemEntry"/> 数组构建游戏原生 <see cref="Cost"/> struct。
        /// 自动调用 <see cref="ItemEntry.ResolveTypeId"/> 将每个 Identifier 解析为游戏原生 TypeID。
        /// </summary>
        /// <param name="money">所需金钱。</param>
        /// <param name="items">消耗物品列表（FML ItemEntry，支持 Identifier、标签、耐久度折算）。</param>
        /// <returns>可直接赋值给 <c>BuildingInfo.cost</c> 的原生 <see cref="Cost"/>。</returns>
        /// <example>
        /// <code>
        /// var info = new BuildingInfo { id = "forge" };
        /// info.cost = BuildingUtils.CreateCost(5000,
        ///     ItemEntry.Of("duckov:Iron", 20),
        ///     ItemEntry.Of("duckov:Stone", 10));
        /// </code>
        /// </example>
        public static Cost CreateCost(long money, params ItemEntry[] items)
        {
            if (items == null || items.Length == 0)
                return new Cost(money);

            var nativeItems = new Cost.ItemEntry[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                nativeItems[i] = new Cost.ItemEntry
                {
                    id = items[i].ResolveTypeId(),
                    amount = items[i].Amount
                };
            }
            return new Cost
            {
                money = money,
                items = nativeItems
            };
        }

        // ===== 成本查询 =====

        /// <summary>
        /// 查询建筑的建造成本（金钱 + 物品）。
        /// 返回游戏原生 <see cref="Cost"/> struct，可直接调用其 <c>.Enough</c> / <c>.Pay()</c> 方法。
        /// </summary>
        /// <param name="buildingId">建筑 Identifier。</param>
        /// <returns>建筑成本；建筑未注册时返回 null。</returns>
        public static Cost? GetBuildingCost(Identifier buildingId)
        {
            var info = GetBuildingInfo(buildingId);
            return info?.cost;
        }

        /// <summary>
        /// 检查玩家是否能负担建筑的建造成本（金钱 + 背包物品充足）。
        /// 直接委托给游戏原生 <c>Cost.Enough</c> → <c>EconomyManager.IsEnough()</c>。
        /// </summary>
        /// <param name="buildingId">建筑 Identifier。</param>
        /// <returns>可负担返回 true；建筑未注册或资源不足返回 false。</returns>
        public static bool CanAffordBuilding(Identifier buildingId)
        {
            var info = GetBuildingInfo(buildingId);
            if (info == null) return false;
            return info.Value.cost.Enough;
        }

        /// <summary>
        /// 手动扣除建筑建造成本（从玩家账户扣钱、从背包移除物品）。
        /// 注意：<see cref="PlaceBuilding"/> 内部已通过游戏 <c>BuyAndPlace</c> 自动处理成本扣除，
        /// 一般无需手动调用。此 API 用于需要在建造前预先扣费的场景。
        /// </summary>
        /// <param name="buildingId">建筑 Identifier。</param>
        /// <returns>扣除成功返回 true；建筑未注册或资源不足返回 false。</returns>
        public static bool SpendBuildingCost(Identifier buildingId)
        {
            var info = GetBuildingInfo(buildingId);
            if (info == null) return false;
            return info.Value.cost.Pay();
        }

        // ===== 便捷回调 =====

        private static readonly Dictionary<string, List<(Identifier buildingId, Action<Building> callback)>> _buildingCallbacks
            = new Dictionary<string, List<(Identifier, Action<Building>)>>();
        private static bool _buildingEventsHooked;

        /// <summary>
        /// 注册建筑建成回调。当指定 buildingId 的建筑建造完成时触发。
        /// FML 内部订阅 <c>BuildingManager.OnBuildingBuiltComplex</c>，按 buildingInfo.id 匹配。
        /// owner modid 自动从 <paramref name="buildingId"/>.<see cref="Identifier.Domain"/> 推导。
        ///
        /// 🆕 Bug Fix: 注册回调时立即检查场景中是否已存在匹配建筑（存档加载后建筑已存在但回调未注册）。
        /// 若存在，立即调用回调，确保 NPC、交互点等在存档加载后正确生成。
        /// </summary>
        public static void OnBuildingBuilt(Identifier buildingId, Action<Building> callback)
        {
            Init();
            HookBuildingEvents();

            var path = buildingId.Path;
            if (!_buildingCallbacks.ContainsKey(path))
                _buildingCallbacks[path] = new List<(Identifier, Action<Building>)>();

            _buildingCallbacks[path].Add((buildingId, callback));

            // 🆕 立即检查场景中是否已存在匹配建筑：遍历所有场景中的 Building 实例，
            // 查找 buildingInfo.id 匹配的建筑并立即执行回调。
            // 这解决了存档加载后的时序问题：建筑在 RepaintAll 时已实例化，
            // 但 OnBuildingBuiltComplex 不触发——回调在 mod OnAfterSetup 时才注册。
            var found = false;
            var existing = UnityEngine.Object.FindObjectsOfType<Building>();
            foreach (var b in existing)
            {
                if (b != null && b.data != null && b.data.Info.id == path)
                {
                    found = true;
                    // 自动装配 Machine 交互节点
                    SetupBuildingMachines(b);
                    try { callback?.Invoke(b); }
                    catch (Exception e) { Debug.LogError($"[BuildingUtils.OnBuildingBuilt] replay callback for '{buildingId}' threw: {e}"); }
                }
            }

            // 如果当前场景中还没加载建筑（如 OnAfterSetup 时场景尚未切换），
            // 标记 pending 并订阅 MainSceneLoadedEvent，在建筑实际加载后再重试。
            if (!found && !_pendingSceneReplay.Contains(path))
            {
                _pendingSceneReplay.Add(path);
                HookSceneLoadEvent();
            }
        }

        /// <summary>移除建筑建成回调。</summary>
        public static void OffBuildingBuilt(Identifier buildingId, Action<Building> callback)
        {
            if (!_buildingCallbacks.TryGetValue(buildingId.Path, out var list)) return;
            list.RemoveAll(e => e.buildingId.Equals(buildingId) && e.callback == callback);
        }

        // 建筑回收回调（与 _buildingCallbacks 共用同一字典）
        private static readonly Dictionary<string, List<(Identifier buildingId, Action<Building> callback)>> _buildingDemolishCallbacks
            = new Dictionary<string, List<(Identifier, Action<Building>)>>();

        /// <summary>
        /// 注册建筑回收/拆除回调。当指定 buildingId 的建筑被回收或拆除时触发。
        /// FML 内部订阅 <c>BuildingManager.OnBuildingDestroyedComplex</c>，按 buildingInfo.id 匹配。
        /// </summary>
        public static void OnBuildingDemolished(Identifier buildingId, Action<Building> callback)
        {
            Init();
            HookBuildingEvents();

            var path = buildingId.Path;
            if (!_buildingDemolishCallbacks.ContainsKey(path))
                _buildingDemolishCallbacks[path] = new List<(Identifier, Action<Building>)>();

            _buildingDemolishCallbacks[path].Add((buildingId, callback));
        }

        /// <summary>移除建筑回收回调。</summary>
        public static void OffBuildingDemolished(Identifier buildingId, Action<Building> callback)
        {
            if (!_buildingDemolishCallbacks.TryGetValue(buildingId.Path, out var list)) return;
            list.RemoveAll(e => e.buildingId.Equals(buildingId) && e.callback == callback);
        }

        // 追踪哪些 buildingId 的路径 B 扫描未找到建筑，需要在场景加载后重试
        private static readonly HashSet<string> _pendingSceneReplay = new HashSet<string>();
        private static bool _sceneLoadEventHooked;

        private static void HookBuildingEvents()
        {
            if (_buildingEventsHooked) return;
            _buildingEventsHooked = true;

            // Publicizer 已公开：直接订阅，天然支持取消订阅
            BuildingManager.OnBuildingBuiltComplex += OnBuildingBuiltHandler;
            BuildingManager.OnBuildingDestroyedComplex += OnBuildingDemolishedHandler;
        }

        private static void HookSceneLoadEvent()
        {
            if (_sceneLoadEventHooked) return;
            _sceneLoadEventHooked = true;

            // 场景刚加载完时建筑可能尚未实例化（RepaintAll 在 LevelInit 期间执行），
            // 因此订阅 MainSceneLoaded（主场景就绪）+ LevelInitialized（建筑已加载）两个事件。
            // FriendlyNpcUtils 的做法也是订阅多个事件以确保时序覆盖。
            EventBusManager.Instance.Sync.Register<MainSceneLoadedEvent>(OnMainSceneLoadedReplayBuildings);
            EventBusManager.Instance.Sync.Register<LevelInitializedEvent>(OnLevelInitializedReplayBuildings);
        }

        private static void OnMainSceneLoadedReplayBuildings(MainSceneLoadedEvent evt)
        {
            ReplayPendingBuildingCallbacks();
            RestoreBuildingMachines();
        }

        private static void OnLevelInitializedReplayBuildings(LevelInitializedEvent evt)
        {
            ReplayPendingBuildingCallbacks();
            RestoreBuildingMachines();
        }

        private static void ReplayPendingBuildingCallbacks()
        {
            if (_pendingSceneReplay.Count == 0) return;

            var existing = UnityEngine.Object.FindObjectsOfType<Building>();
            var resolved = new List<string>();

            foreach (var path in _pendingSceneReplay)
            {
                if (!_buildingCallbacks.TryGetValue(path, out var callbacks) || callbacks.Count == 0)
                {
                    resolved.Add(path);
                    continue;
                }

                foreach (var b in existing)
                {
                    if (b != null && b.data != null && b.data.Info.id == path)
                    {
                        // 自动装配 Machine 交互节点
                        SetupBuildingMachines(b);

                        foreach (var (buildingId, callback) in callbacks)
                        {
                            try { callback?.Invoke(b); }
                            catch (Exception e) { Debug.LogError($"[BuildingUtils] scene-load replay callback for '{buildingId}' threw: {e}"); }
                        }
                        resolved.Add(path);
                        break;
                    }
                }
            }

            foreach (var r in resolved)
                _pendingSceneReplay.Remove(r);
        }

        /// <summary>
        /// 场景加载后重建所有已注册建筑的 Machine 交互节点。
        /// 参考 <see cref="FriendlyNpcUtils.RestoreNpcSpawns"/> 设计模式：
        /// 配置（_buildingMachines）是持久的，运行时对象（交互节点 GameObject）在场景重载后被销毁，
        /// 此处从配置重建所有交互节点。
        /// </summary>
        private static void RestoreBuildingMachines()
        {
            if (_buildingMachines.Count == 0) return;

            var existing = UnityEngine.Object.FindObjectsOfType<Building>();
            if (existing.Length == 0) return;

            int restored = 0;

            foreach (var kvp in _buildingMachines)
            {
                var path = kvp.Key;
                foreach (var b in existing)
                {
                    if (b != null && b.data != null && b.data.Info.id == path)
                    {
                        SetupBuildingMachines(b);
                        restored++;
                        break;
                    }
                }
            }

            if (restored > 0)
                Debug.Log($"[BuildingUtils] RestoreBuildingMachines: rebuilt machines on {restored} building(s).");
        }

        /// <summary>
        /// <summary>
        /// 取消订阅 BuildingManager 事件。RegistryManager.RemoveAllByOwner 卸载时调用。
        /// </summary>
        internal static void UnhookBuildingEvents()
        {
            if (!_buildingEventsHooked) return;
            _buildingEventsHooked = false;
            BuildingManager.OnBuildingBuiltComplex -= OnBuildingBuiltHandler;
            BuildingManager.OnBuildingDestroyedComplex -= OnBuildingDemolishedHandler;

            // 取消场景加载事件订阅
            if (_sceneLoadEventHooked)
            {
                _sceneLoadEventHooked = false;
                EventBusManager.Instance.Sync.Unregister<MainSceneLoadedEvent>(OnMainSceneLoadedReplayBuildings);
                EventBusManager.Instance.Sync.Unregister<LevelInitializedEvent>(OnLevelInitializedReplayBuildings);
            }
        }

        // ═══════════════════════════════════════════════════
        //  Machine 运行时自动装配
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 在建筑实例化到场景后自动装配 Machine 交互节点。
        /// 读取 <see cref="ConfigureBuildingUI"/> 注册的 <see cref="MachineDef"/> 列表，
        /// 为每个 Machine 在 functionContainer 上创建交互节点。
        /// 由 <see cref="OnBuildingBuiltHandler"/> 和 <see cref="ReplayPendingBuildingCallbacks"/> 自动调用。
        /// </summary>
        /// <remarks>
        /// 完整 Machine View（子库存面板 + 进度条）待后续 Phase 实现。
        /// 当前交互节点指向 <see cref="FeatherMod.Interaction.GameViews.Machine"/>，
        /// ViewDispatcher 中已注册 placeholder handler。
        /// </remarks>
        private static void SetupBuildingMachines(Building building)
        {
            if (building == null || building.data == null) return;
            var path = building.data.Info.id;
            if (string.IsNullOrEmpty(path)) return;

            if (!_buildingMachines.TryGetValue(path, out var machines) || machines.Count == 0)
                return;

            var funcContainer = GetFunctionContainer(building);
            if (funcContainer == null) return;

            var handlers = new List<ViewInteractHandler>();

            foreach (var machine in machines)
            {
                var childName = $"Machine_{machine.MachineKey}";
                var existing = funcContainer.transform.Find(childName);
                if (existing != null)
                {
                    var existingHandler = existing.GetComponent<ViewInteractHandler>();
                    if (existingHandler != null) handlers.Add(existingHandler);
                    continue; // 幂等：已初始化则跳过
                }

                try
                {
                    var child = new GameObject(childName);
                    child.transform.SetParent(funcContainer.transform, false);
                    child.layer = funcContainer.layer;

                    // 碰撞体（交互检测）
                    var collider = child.AddComponent<BoxCollider>();
                    collider.isTrigger = true;
                    collider.size = Vector3.one;

                    // 交互处理器
                    var interactId = new Identifier(FMLConstants.Domain, $"machine_{path}_{machine.MachineKey}");
                    var handler = child.AddComponent<ViewInteractHandler>();
                    handler.ViewType = GameViews.Machine;
                    handler.ViewParam = $"{path}/{machine.MachineKey}";
                    handler.InteractNameKey = machine.DisplayName;

                    InteractionUtils.Registry.Set(interactId, new InteractionEntry
                    {
                        Target = child,
                        Modid = interactId.Domain
                    }, interactId.Domain);

                    handlers.Add(handler);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[BuildingUtils] Failed to setup machine '{machine.MachineKey}' on building '{path}': {e}");
                }
            }

            // 编组：多台机器时，第一台为主交互，其余为组成员（碰撞体禁用，共用交互提示）
            if (handlers.Count > 1)
            {
                var primary = handlers[0];
                var members = new InteractableBase[handlers.Count - 1];
                for (int i = 1; i < handlers.Count; i++)
                    members[i - 1] = handlers[i];
                InteractionUtils.SetupInteractionGroup(primary, members);
            }
        }

        private static void OnBuildingBuiltHandler(int guid, BuildingInfo info)
        {
            if (!_buildingCallbacks.TryGetValue(info.id, out var list)) return;
            // 查找场景中实际放置的 Building 实例（非 prefab 资源）
            // OnBuildingBuiltComplex 事件同步触发时实例已存在（BuildingArea.Display 已 Instantiate + Setup）
            var building = UnityEngine.Object.FindObjectsOfType<Building>()
                .FirstOrDefault(b => b != null && b.GUID == guid);
            var arg = building ?? info.Prefab;  // 找到实例 → 传实例；未找到 → fallback prefab

            // 自动装配 Machine 交互节点
            if (building != null)
                SetupBuildingMachines(building);

            foreach (var (buildingId, callback) in list)
            {
                try
                {
                    callback?.Invoke(arg);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[BuildingUtils.OnBuildingBuilt] callback for '{buildingId}' threw: {e}");
                }
            }
        }

        private static void OnBuildingDemolishedHandler(int guid, BuildingInfo info)
        {
            if (!_buildingDemolishCallbacks.TryGetValue(info.id, out var list)) return;
            // 回收时建筑实例可能仍存在（拆除前），尝试查找
            var building = UnityEngine.Object.FindObjectsOfType<Building>()
                .FirstOrDefault(b => b != null && b.GUID == guid);
            var arg = building ?? info.Prefab;
            foreach (var (buildingId, callback) in list)
            {
                try
                {
                    callback?.Invoke(arg);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[BuildingUtils.OnBuildingDemolished] callback for '{buildingId}' threw: {e}");
                }
            }
        }

        // ===== 代码端创建建筑 =====

        private static readonly BindingFlags _buildingFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        /// <summary>
        /// 纯代码创建简易 Building GameObject（无需 Unity 编辑器）。
        /// 自动创建带 Building 组件 + 基础 Cube 模型 + 网格碰撞体的完整 Prefab 结构。
        /// </summary>
        /// <param name="id">建筑 Identifier。</param>
        /// <param name="dimensions">占地尺寸，如 (2,2)。</param>
        /// <param name="existingPrefabName">
        /// 可选：引用游戏已有 Building Prefab 名称（如 "Building_Workbench"），
        /// 克隆其 graphicsContainer 和 functionContainer 结构。
        /// </param>
        /// <returns>创建好的 Building 组件实例。</returns>
        public static Building CreateSimpleBuilding(
            Identifier id, Vector2Int dimensions, string? existingPrefabName = null)
        {
            // 路径 A：克隆游戏已有 Building Prefab
            if (existingPrefabName != null)
            {
                var existingPrefab = BuildingDataCollection.GetPrefab(existingPrefabName);
                if (existingPrefab != null)
                {
                    var clone = UnityEngine.Object.Instantiate(existingPrefab);
                    clone.name = $"Building_{id.Path}";
                    SetBuildingField(clone, "id", id.Path);
                    SetBuildingField(clone, "dimensions", dimensions);
                    // Prefab 需跨场景存活，防止场景切换导致注册表持有僵尸引用
                    UnityEngine.Object.DontDestroyOnLoad(clone.gameObject);
                    return clone;
                }
            }

            // 路径 B：纯代码创建（默认 Cube 模型 + 交互碰撞体）
            return CreateBuildingFromScratch(id, dimensions);
        }

        private static Building CreateBuildingFromScratch(Identifier id, Vector2Int dimensions)
        {
            var go = new GameObject($"Building_{id.Path}");
            go.SetActive(false);  // 阻止 Awake 在字段就绪前触发

            var building = go.AddComponent<Building>();

            // 设置 Building 组件字段
            SetBuildingField(building, "id", id.Path);
            SetBuildingField(building, "dimensions", dimensions);

            // 创建 graphicsContainer（美术层 + 物理碰撞）
            var graphics = new GameObject("Graphics");
            graphics.transform.SetParent(go.transform);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(graphics.transform);
            cube.transform.localPosition = Vector3.zero;
            cube.transform.localScale = new Vector3(dimensions.x, 2f, dimensions.y);
            cube.name = "Model_Cube";

            // 物理碰撞箱（非 trigger），阻止玩家穿过建筑
            // 游戏原生 Building prefab 的 graphicsContainer 中有 Collider，
            // Building.SetupPreview() 会遍历 graphicsContainer.GetComponentsInChildren<Collider>()
            var physicsCollider = graphics.AddComponent<BoxCollider>();
            physicsCollider.isTrigger = false;
            physicsCollider.size = new Vector3(dimensions.x, 2f, dimensions.y);
            physicsCollider.center = Vector3.zero;

            SetBuildingField(building, "graphicsContainer", graphics);

            // 创建 functionContainer（功能层——交互碰撞体）
            var func = new GameObject("Function");
            func.transform.SetParent(go.transform);
            func.layer = LayerMask.NameToLayer("Interactable") != -1 ? LayerMask.NameToLayer("Interactable") : 8;
            var collider = func.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(dimensions.x, 2f, dimensions.y);
            SetBuildingField(building, "functionContainer", func);

            go.SetActive(true);  // 字段就绪，允许 Awake

            // Prefab 需跨场景存活，防止场景切换导致注册表持有僵尸引用
            UnityEngine.Object.DontDestroyOnLoad(go);
            return building;
        }

        /// <summary>
        /// 将自定义 3D 模型注入到 Building 的 graphicsContainer 中。
        /// </summary>
        /// <param name="buildingId">已注册的建筑 Identifier。</param>
        /// <param name="modelPrefab">自定义模型 prefab（可从 AssetBundle 加载）。</param>
        /// <param name="replaceExisting">是否替换 graphicsContainer 下现有子物体（默认 true）。</param>
        public static void SetBuildingModel(
            Identifier buildingId, GameObject modelPrefab, bool replaceExisting = true)
        {
            if (!_buildingRegistry.TryGet(buildingId, out var info)) return;
            var prefab = BuildingDataCollection.GetPrefab(info.prefabName);
            if (prefab == null) return;

            var graphics = GetBuildingField<GameObject>(prefab, "graphicsContainer");
            if (graphics == null) return;

            if (replaceExisting)
            {
                for (int i = graphics.transform.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(graphics.transform.GetChild(i).gameObject);
            }

            var model = UnityEngine.Object.Instantiate(modelPrefab, graphics.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            ShaderReplacer.ApplyTo(model);
        }

        // ===== Building 反射辅助 =====

        /// <summary>通过反射设置 Building 的 private [SerializeField] 字段。</summary>
        private static void SetBuildingField<T>(Building building, string fieldName, T value)
        {
            var field = typeof(Building).GetField(fieldName, _buildingFlags);
            if (field != null) field.SetValue(building, value);
            else Debug.LogWarning($"[BuildingUtils] Field '{fieldName}' not found on Building.");
        }

        /// <summary>通过反射读取 Building 的 private [SerializeField] 字段。</summary>
        private static T? GetBuildingField<T>(Building building, string fieldName) where T : class
        {
            var field = typeof(Building).GetField(fieldName, _buildingFlags);
            return field?.GetValue(building) as T;
        }

        // ══════════ Container Access ══════════

        /// <summary>
        /// 获取建筑的 Function 容器（功能层）。
        /// <para>该容器承载 trigger collider，用于玩家交互检测、可点击区域等逻辑判定，
        /// 不参与视觉渲染。模组应通过本方法访问而非硬编码 <c>building.transform.Find("Function")</c>，
        /// 以避免子物体命名或层级变动导致的破坏性变更。</para>
        /// </summary>
        /// <param name="building">目标建筑实例。</param>
        /// <returns>Function 容器 GameObject；若字段缺失或为空则返回 <c>null</c>。</returns>
        public static GameObject? GetFunctionContainer(Building building)
            => GetBuildingField<GameObject>(building, "functionContainer");

        /// <summary>
        /// 获取建筑的 Graphics 容器（视觉层）。
        /// <para>该容器承载视觉模型以及用于阻挡玩家的非 trigger physics collider
        /// （即玩家碰撞体积）。模组应通过本方法访问而非硬编码
        /// <c>building.transform.Find("Graphics")</c>，以避免子物体命名或层级变动
        /// 导致的破坏性变更。</para>
        /// </summary>
        /// <param name="building">目标建筑实例。</param>
        /// <returns>Graphics 容器 GameObject；若字段缺失或为空则返回 <c>null</c>。</returns>
        public static GameObject? GetGraphicsContainer(Building building)
            => GetBuildingField<GameObject>(building, "graphicsContainer");

        /// <summary>
        /// 修复 BuildingInfo 中可能为 null 的数组字段，初始化为空数组。
        /// 游戏原生 <c>RequirementsSatisfied()</c> / <c>BuildingAreaData.Any()</c>
        /// 遍历这些字段时不检查 null，必须提前补齐。
        /// </summary>
        private static void SanitizeBuildingInfo(ref BuildingInfo info)
        {
            // Publicizer 已公开，直接赋值（struct 需要 ref）
            info.requireBuildings ??= Array.Empty<string>();
            info.requireQuests ??= Array.Empty<int>();
            info.alternativeFor ??= Array.Empty<string>();
        }

        // ===== Machine 管理（新增） =====

        private static readonly Dictionary<string, List<MachineDef>> _buildingMachines
            = new Dictionary<string, List<MachineDef>>();

        /// <summary>
        /// 为指定建筑注册自定义 UI 配置（含 Machine 定义）。
        /// 当玩家交互该建筑打开 DetailsView 时，FML 自动注入 Machine UI 元素。
        /// MachineDef.Recipe 非 null 时自动创建 BuildingSlotsWatcher。
        /// </summary>
        /// <param name="buildingId">已注册的建筑 Identifier。</param>
        /// <param name="config">UI 配置（含 Machines 列表）。</param>
        /// <param name="modid">归属 modid。</param>
        public static void ConfigureBuildingUI(
            Identifier buildingId,
            BuildingUIConfig config,
            string modid)
        {
            Init();

            var path = buildingId.Path;
            if (!_buildingMachines.ContainsKey(path))
                _buildingMachines[path] = new List<MachineDef>();

            if (config.Machines != null)
            {
                foreach (var machine in config.Machines)
                {
                    _buildingMachines[path].Add(machine);

                    // 注册 Recipe（如果 MachineDef 包含）
                    if (machine.Recipe != null)
                    {
                        if (string.IsNullOrEmpty(machine.Recipe.Id.Domain))
                            machine.Recipe.Id = new Identifier(modid, $"{path}_{machine.MachineKey}");
                        machine.Recipe.SaveKey = $"{path}/{machine.MachineKey}";
                    }
                }
            }
        }

        /// <summary>
        /// 为建筑上指定 Machine 动态注册 Recipe。
        /// Recipe 的类型由子类确定（SimpleMachineRecipe 或自定义），Id 为合成表标识。
        /// FML 内部自动创建 BuildingSlotsWatcher 并绑定子库存。
        /// </summary>
        /// <param name="buildingId">已注册的建筑 Identifier。</param>
        /// <param name="machineKey">Machine 标识（与 MachineDef.MachineKey 对应）。</param>
        /// <param name="recipe">MachineRecipe 子类实例。类型决定逻辑，Id 为合成表标识。</param>
        /// <param name="modid">归属 modid。</param>
        public static void RegisterMachineRecipe(
            Identifier buildingId,
            string machineKey,
            MachineRecipe recipe,
            string modid)
        {
            Init();

            recipe.SaveKey = $"{buildingId.Path}/{machineKey}";

            // 查找或创建 MachineDef
            var path = buildingId.Path;
            if (!_buildingMachines.TryGetValue(path, out var machines))
            {
                machines = new List<MachineDef>();
                _buildingMachines[path] = machines;
            }

            var existing = machines.Find(m => m.MachineKey == machineKey);
            if (existing != null)
            {
                existing.Recipe = recipe;
            }
            else
            {
                machines.Add(new MachineDef
                {
                    MachineKey = machineKey,
                    Recipe = recipe,
                    UnlockedByDefault = true
                });
            }
        }

        /// <summary>移除建筑上指定 Machine 的 Recipe。</summary>
        public static bool UnregisterMachineRecipe(Identifier buildingId, string machineKey)
        {
            if (!_buildingMachines.TryGetValue(buildingId.Path, out var machines))
                return false;

            var machine = machines.Find(m => m.MachineKey == machineKey);
            if (machine != null)
            {
                machine.Recipe = null;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取建筑的全部 Machine 定义（供内部 Patch 层和 UI 注入引擎使用）。
        /// </summary>
        internal static List<MachineDef>? GetBuildingMachines(Identifier buildingId)
        {
            _buildingMachines.TryGetValue(buildingId.Path, out var list);
            return list;
        }

        /// <summary>检查 Machine 是否当前可用（Perk 门控）。</summary>
        internal static bool IsMachineAvailable(MachineDef machine)
        {
            if (machine.UnlockedByDefault) return true;
            if (machine.RequiredPerk == null) return true;
            return PerkTreeUtils.IsPerkUnlocked(machine.RequiredPerk);
        }

        /// <summary>
        /// 按 machineKey 检查 Machine 是否当前可用（Perk 门控）。
        /// 遍历所有已注册建筑的 Machine 列表查找匹配 key。
        /// </summary>
        public static bool IsMachineAvailableByKey(string machineKey)
        {
            foreach (var kvp in _buildingMachines)
            {
                var machine = kvp.Value.Find(m => m.MachineKey == machineKey);
                if (machine != null)
                    return IsMachineAvailable(machine);
            }
            return true; // 未注册的 key，不阻止
        }

        /// <summary>
        /// 为建筑实例挂载 BuildingBehaviour 子类。
        /// </summary>
        /// <typeparam name="T">BuildingBehaviour 子类。</typeparam>
        /// <param name="buildingId">建筑 Identifier。</param>
        /// <param name="behaviour">预配置的 Behaviour 实例（可选，null = 创建新实例）。</param>
        public static void AttachBehaviour<T>(Identifier buildingId, T? behaviour = null) where T : BuildingBehaviour
        {
            if (!_buildingRegistry.TryGet(buildingId, out var info)) return;
            var prefab = BuildingDataCollection.GetPrefab(info.prefabName);
            if (prefab == null) return;

            // 挂载到 prefab（所有实例继承）
            var instance = behaviour ?? prefab.gameObject.AddComponent<T>();
            var inventory = prefab.GetComponent<ItemStatsSystem.Inventory>();
            instance.SetBuilding(prefab, inventory);
        }

        /// <summary>卸载指定 mod 的全部 Building Machine 配置。</summary>
        internal static void RemoveAllMachinesForMod(string modid)
        {
            var keysToRemove = new List<string>();
            foreach (var kvp in _buildingMachines)
            {
                kvp.Value.RemoveAll(m =>
                {
                    if (m.Recipe?.Id.Domain == modid)
                    {
                        m.Recipe = null;
                        return true;
                    }
                    return false;
                });
                if (kvp.Value.Count == 0)
                    keysToRemove.Add(kvp.Key);
            }
            foreach (var key in keysToRemove)
                _buildingMachines.Remove(key);
        }
    }
}
