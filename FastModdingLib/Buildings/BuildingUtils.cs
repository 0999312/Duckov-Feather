using Duckov.Buildings;
using Duckov.Economy;
using Duckov.Utilities;
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

        /// <summary>用于反射调用 <c>BuildingManager.BuyAndPlace</c> 的 MethodInfo 缓存。</summary>
        private static readonly MethodInfo? _buyAndPlaceMethod = typeof(BuildingManager)
            .GetMethod("BuyAndPlace", BindingFlags.NonPublic | BindingFlags.Static);

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
                cost = config.BuildCost()
            };

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
        /// 放置建筑。通过反射调用 <see cref="BuildingManager.BuyAndPlace"/>（该方法是 internal）。
        /// areaId 和 buildingId 均为 Identifier——FML 内部将其 Path 映射为游戏原生 string ID。
        /// </summary>
        public static BuildingBuyAndPlaceResults PlaceBuilding(
            Identifier areaId, Identifier buildingId,
            Vector2Int coord, BuildingRotation rotation)
        {
            if (_buyAndPlaceMethod == null)
                throw new InvalidOperationException("BuildingManager.BuyAndPlace not found via reflection.");

            return (BuildingBuyAndPlaceResults)_buyAndPlaceMethod.Invoke(null,
                new object[] { areaId.Path, buildingId.Path, coord, rotation });
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
            var existing = UnityEngine.Object.FindObjectsOfType<Building>();
            foreach (var b in existing)
            {
                if (b != null && b.data != null && b.data.Info.id == path)
                {
                    try { callback?.Invoke(b); }
                    catch (Exception e) { Debug.LogError($"[BuildingUtils.OnBuildingBuilt] replay callback for '{buildingId}' threw: {e}"); }
                }
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

        private static void HookBuildingEvents()
        {
            if (_buildingEventsHooked) return;
            _buildingEventsHooked = true;

            // Hook OnBuildingBuiltComplex
            var builtEvt = typeof(BuildingManager).GetEvent("OnBuildingBuiltComplex",
                BindingFlags.Public | BindingFlags.Static);
            var builtHandler = typeof(BuildingUtils).GetMethod("OnBuildingBuiltHandler",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (builtEvt != null && builtHandler != null)
            {
                var handler = Delegate.CreateDelegate(builtEvt.EventHandlerType!, (object?)null, builtHandler);
                builtEvt.AddEventHandler(null, handler);
            }

            // Hook OnBuildingDestroyedComplex（对称的回收/拆除事件）
            var demolishEvt = typeof(BuildingManager).GetEvent("OnBuildingDestroyedComplex",
                BindingFlags.Public | BindingFlags.Static);
            var demolishHandler = typeof(BuildingUtils).GetMethod("OnBuildingDemolishedHandler",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (demolishEvt != null && demolishHandler != null)
            {
                var handler = Delegate.CreateDelegate(demolishEvt.EventHandlerType!, (object?)null, demolishHandler);
                demolishEvt.AddEventHandler(null, handler);
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

        /// <summary>
        /// 修复 BuildingInfo 中可能为 null 的数组字段，初始化为空数组。
        /// 游戏原生 <c>RequirementsSatisfied()</c> / <c>BuildingAreaData.Any()</c>
        /// 遍历这些字段时不检查 null，必须提前补齐。
        /// </summary>
        private static void SanitizeBuildingInfo(ref BuildingInfo info)
        {
            // requireBuildings: string[] — RequirementsSatisfied() 会 foreach 遍历
            try
            {
                var field = typeof(BuildingInfo).GetField("requireBuildings",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && field.GetValue(info) == null)
                    field.SetValueDirect(__makeref(info), Array.Empty<string>());
            }
            catch { /* 字段不存在或类型不匹配时静默跳过 */ }

            // requireQuests: int[] — 同上
            try
            {
                var field = typeof(BuildingInfo).GetField("requireQuests",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && field.GetValue(info) == null)
                    field.SetValueDirect(__makeref(info), Array.Empty<int>());
            }
            catch { /* 字段不存在或类型不匹配时静默跳过 */ }

            // alternativeFor: string[] — BuildingAreaData.Any() 遍历时不检查 null，
            // 建造 FML 建筑后触发 Contains(null) → ArgumentNullException 扩散全局
            try
            {
                var field = typeof(BuildingInfo).GetField("alternativeFor",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && field.GetValue(info) == null)
                    field.SetValueDirect(__makeref(info), Array.Empty<string>());
            }
            catch { /* 字段不存在或类型不匹配时静默跳过 */ }
        }
    }
}
