using System;
using System.Collections.Generic;

using Duckov;
using Duckov.Buildings.UI;
using Duckov.PerkTrees;
using Duckov.UI;
using Duckov.Utilities;

using FeatherMod.Interaction.Components;
using FeatherMod.Register;
using FeatherMod.UI;
using FeatherMod.Utils;

using Unity.VisualScripting;

using UnityEngine;

namespace FeatherMod.Interaction
{
    /// <summary>
    /// 交互系统主入口。提供 Spawn（创建交互点）、Attach（绑定到已有对象）、
    /// Query（查询交互点）和 Cleanup（卸载）的完整 API。
    /// </summary>
    public static class InteractionUtils
    {
        private static readonly InteractionRegistry _interactionRegistry = new InteractionRegistry();
        private static bool _initialized;

        /// <summary>暴露给 RegisterBootstrap 用于注册到元表。</summary>
        public static InteractionRegistry Registry => _interactionRegistry;

        // ═══════════════════════════════════════════════════
        //  Lifecycle
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 初始化：将 InteractionRegistry 注册到元表 + 注册内置 View 打开方法。
        /// 由 RegisterBootstrap.Init() 调用（幂等）。
        /// </summary>
        internal static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            // 注册到元表
            var meta = RegistryManager.Instance.Registry;
            var id = new Identifier(FMLConstants.Domain, "interaction");
            if (meta is NonAlterableSimpleRegistry<ERegistry> nonAlt)
                nonAlt.SetIfAbsent(id, _interactionRegistry, RegistryManager.CurrentModid);
            else
                meta.Set(id, _interactionRegistry, RegistryManager.CurrentModid);

            // 注册内置 View 打开方法
            RegisterBuiltInViews();
        }

        /// <summary>注册游戏内置 View 的打开方法。</summary>
        private static void RegisterBuiltInViews()
        {
            // PerkTree：通过 PerkTreeManager 查找并打开
            ViewDispatcher.Register(GameViews.PerkTree, param =>
            {
                if (string.IsNullOrEmpty(param)) return;
                var tree = PerkTreeManager.GetPerkTree(param);
                if (tree != null)
                {
                    PerkTreeView.Show(tree);
                }
            }, FMLConstants.Domain);

            // Building：打开 BuilderView
            ViewDispatcher.Register(GameViews.Building, _ =>
            {
                BuilderView.Show(null);
            }, FMLConstants.Domain);

            // Endowment：EndowmentSelectionPanel 由游戏原生触发
            // FML 通过 Patch 注入自定义天赋，无需直接打开
            ViewDispatcher.Register(GameViews.Endowment, _ =>
            {
                // 游戏原生 UI，通过 EndowmentManagerPatch 注入
            }, FMLConstants.Domain);

            // Crafting：委托给 GameUIUtils.OpenCraftingView
            ViewDispatcher.Register(GameViews.Crafting, param =>
            {
                string[]? tags = null;
                if (!string.IsNullOrEmpty(param))
                    tags = param.Split(',');
                GameUIUtils.OpenCraftingView(tags);
            }, FMLConstants.Domain);

            // Shop：打开 StockShopView。ViewParam 为 shopId（MerchantProfile.merchantID）。
            // 正常流程由 NpcShopInteract 处理；此 handler 供 ViewInteractHandler 等自定义交互点使用。
            ViewDispatcher.Register(GameViews.Shop, param =>
            {
                if (string.IsNullOrEmpty(param)) return;
                // 遍历 FriendlyNpcUtils.Registry 查找匹配的 NPC
                foreach (var kvp in FriendlyNpcUtils.Registry)
                {
                    if (kvp.Value == null) continue;
                    var shop = kvp.Value.GetComponent<global::Duckov.Economy.StockShop>();
                    if (shop != null && shop.MerchantID == param)
                    {
                        shop.ShowUI();
                        return;
                    }
                }
                // 兜底：直接创建临时 StockShop 并打开（商品从 StockShopDatabase 按 merchantID 加载）
                Debug.LogWarning($"[FML] GameViews.Shop: No NPC found with shopId '{param}', " +
                    "Shop goods must be registered via ShopUtils.AddGoods().");
            }, FMLConstants.Domain);

            // Quest：打开 QuestView（任务面板）
            ViewDispatcher.Register(GameViews.Quest, _ =>
            {
                global::Duckov.Quests.UI.QuestView.Show();
            }, FMLConstants.Domain);

            // FormulasIndex：配方索引浏览
            ViewDispatcher.Register(GameViews.Formulas, _ =>
            {
                FormulasIndexView.Show();
            }, FMLConstants.Domain);

            // FormulasRegister：配方注册（提交物品学习配方）
            // viewParam 作为 registerTag 标签名，运行时通过 TagUtils.RegisterTag 解析为 Tag 对象，
            // 传递给原生 Show(ICollection<Tag>) 以过滤提交槽可接受的物品。
            ViewDispatcher.Register(GameViews.FormulasRegister, param =>
            {
                ICollection<Tag>? tags = null;
                if (!string.IsNullOrEmpty(param))
                {
                    var tag = TagUtils.RegisterTag(param);
                    if (tag != null) tags = new[] { tag };
                }
                FormulasRegisterView.Show(tags);
            }, FMLConstants.Domain);

            // Decompose：物品分解
            ViewDispatcher.Register(GameViews.Decompose, _ =>
            {
                ItemDecomposeView.Show();
            }, FMLConstants.Domain);

            // Machine：建筑设备交互（完整 Machine View 待后续 Phase 实现）
            // 当前仅做 Perk 门控检查；Machine 核心功能（子库存 + Recipe）由 BuildingSlotsWatcher 自动驱动。
            ViewDispatcher.Register(GameViews.Machine, param =>
            {
                if (string.IsNullOrEmpty(param)) return;

                // Perk 门控检查
                var slashIdx = param.LastIndexOf('/');
                if (slashIdx > 0 && slashIdx < param.Length - 1)
                {
                    var machineKey = param.Substring(slashIdx + 1);
                    if (!BuildingUtils.IsMachineAvailableByKey(machineKey))
                    {
                        Debug.LogWarning($"[FML] Machine '{param}' is locked (Perk required).");
                        return;
                    }
                }

                Debug.Log($"[FML] Machine '{param}' opened. Full Machine View (sub-inventory panel + progress bar) is not yet implemented.");
            }, FMLConstants.Domain);
        }

        // ═══════════════════════════════════════════════════
        //  Spawn
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 在世界坐标生成 View 交互点。
        /// 自动创建 GameObject + BoxCollider(Trigger) + "Interact" 图层 + ViewInteractHandler。
        /// </summary>
        /// <param name="id">交互点唯一标识。</param>
        /// <param name="position">世界坐标。</param>
        /// <param name="viewType">目标 View 类型 Identifier。</param>
        /// <param name="viewParam">可选参数，传递给 View 打开方法。</param>
        /// <param name="rotation">可选旋转。</param>
        /// <param name="colliderSize">可选碰撞体尺寸，默认 (1, 1, 1)。</param>
        /// <param name="interactNameKey">可选交互名称本地化 key，覆盖默认交互提示文本。</param>
        /// <param name="markerOffset">可选交互标记相对交互点的世界偏移。</param>
        /// <param name="coolTime">可选交互冷却时间（秒），0 表示无冷却。</param>
        /// <returns>创建的交互点 GameObject。</returns>
        public static GameObject SpawnViewInteract(
            Identifier id, Vector3 position, Identifier viewType,
            string? viewParam = null, Quaternion? rotation = null, Vector3? colliderSize = null,
            string? interactNameKey = null, Vector3? markerOffset = null, float coolTime = 0f)
        {
            var go = CreateInteractPoint(id, position, rotation ?? Quaternion.identity, colliderSize);

            var handler = go.AddComponent<ViewInteractHandler>();
            handler.ViewType = viewType;
            handler.ViewParam = viewParam;
            handler.overrideInteractName = interactNameKey != null;
            handler._overrideInteractNameKey = interactNameKey;
            handler.InteractNameKey = interactNameKey;
            handler.MarkerOffset = markerOffset;
            handler.CoolTime = coolTime;

            _interactionRegistry.Set(id, new InteractionEntry
            {
                Target = go,
                Modid = id.Domain
            }, id.Domain);

            return go;
        }

        /// <summary>
        /// 在世界坐标生成自定义交互点。
        /// 自动创建 GameObject + BoxCollider(Trigger) + "Interact" 图层 + DelegateInteractHandler。
        /// </summary>
        /// <param name="id">交互点唯一标识。</param>
        /// <param name="position">世界坐标。</param>
        /// <param name="onInteract">自定义交互回调。</param>
        /// <param name="rotation">可选旋转。</param>
        /// <param name="colliderSize">可选碰撞体尺寸，默认 (1, 1, 1)。</param>
        /// <returns>创建的交互点 GameObject。</returns>
        public static GameObject SpawnCustomInteract(
            Identifier id, Vector3 position, Action onInteract,
            Quaternion? rotation = null, Vector3? colliderSize = null)
        {
            var go = CreateInteractPoint(id, position, rotation ?? Quaternion.identity, colliderSize);

            var handler = go.AddComponent<DelegateInteractHandler>();
            handler.OnInteract = onInteract;

            _interactionRegistry.Set(id, new InteractionEntry
            {
                Target = go,
                Modid = id.Domain
            }, id.Domain);

            return go;
        }

        // ═══════════════════════════════════════════════════
        //  Attach
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 给已有 GameObject 挂载 View 交互处理器。
        /// </summary>
        /// <param name="id">交互点唯一标识。</param>
        /// <param name="target">目标 GameObject。</param>
        /// <param name="viewType">目标 View 类型 Identifier。</param>
        /// <param name="viewParam">可选参数，传递给 View 打开方法。</param>
        /// <param name="addColliderIfMissing">目标缺少 Collider 时是否自动添加 BoxCollider(Trigger)。</param>
        /// <param name="interactNameKey">可选交互名称本地化 key，覆盖默认交互提示文本。</param>
        /// <param name="markerOffset">可选交互标记相对交互点的世界偏移。</param>
        /// <param name="coolTime">可选交互冷却时间（秒），0 表示无冷却。</param>
        public static void AttachViewInteract(
            Identifier id, GameObject target, Identifier viewType,
            string? viewParam = null, bool addColliderIfMissing = true,
            string? interactNameKey = null, Vector3? markerOffset = null, float coolTime = 0f)
        {
            EnsureColliderAndLayer(target, addColliderIfMissing);

            var handler = target.AddComponent<ViewInteractHandler>();
            handler.ViewType = viewType;
            handler.ViewParam = viewParam;
            handler.overrideInteractName = interactNameKey != null;
            handler._overrideInteractNameKey = interactNameKey;
            handler.InteractNameKey = interactNameKey;
            handler.MarkerOffset = markerOffset;
            handler.CoolTime = coolTime;

            _interactionRegistry.Set(id, new InteractionEntry
            {
                Target = target,
                Modid = id.Domain
            }, id.Domain);
        }

        /// <summary>给已有 GameObject 挂载自定义交互处理器。</summary>
        public static void AttachCustomInteract(
            Identifier id, GameObject target, Action onInteract,
            bool addColliderIfMissing = true)
        {
            EnsureColliderAndLayer(target, addColliderIfMissing);

            var handler = target.AddComponent<DelegateInteractHandler>();
            handler.OnInteract = onInteract;

            _interactionRegistry.Set(id, new InteractionEntry
            {
                Target = target,
                Modid = id.Domain
            }, id.Domain);
        }

        /// <summary>
        /// 按名称查找 NPC 并挂载 View 交互。
        /// 先通过 GameObject.Find 查找，再兜底遍历 AICharacterController。
        /// </summary>
        /// <returns>是否成功找到并挂载。</returns>
        public static bool AttachToNPC(
            Identifier id, string npcName, Identifier viewType, string? viewParam = null)
        {
            // 路径 A：直接 Find
            var npc = GameObject.Find(npcName);
            if (npc == null)
            {
                // 路径 B：遍历 AICharacterController
                var controllers = GameObject.FindObjectsOfType<AICharacterController>();
                foreach (var ctrl in controllers)
                {
                    if (ctrl.name.Contains(npcName) || ctrl.gameObject.name.Contains(npcName))
                    {
                        npc = ctrl.gameObject;
                        break;
                    }
                }
            }

            if (npc == null)
            {
                Debug.LogWarning($"[InteractionUtils] NPC '{npcName}' not found.");
                return false;
            }

            AttachViewInteract(id, npc, viewType, viewParam, addColliderIfMissing: true);
            return true;
        }

        // ═══════════════════════════════════════════════════
        //  Interaction Group
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 将多个交互点编组到同一个 primary 之下。primary 持有组标记与成员列表，
        /// 其余成员的标记关闭、位置/旋转同步到 primary、碰撞体禁用，避免重复交互。
        /// 调用方自行决定哪个是 primary（无优先级回退逻辑）。
        /// </summary>
        /// <param name="primary">组的主交互点，将作为唯一可交互入口。</param>
        /// <param name="members">其余成员交互点；null 与 primary 自身会被自动过滤。</param>
        public static void SetupInteractionGroup(InteractableBase primary, params InteractableBase[] members)
        {
            if (primary == null) return;

            // 过滤 null 与 primary 自身
            var validMembers = new List<InteractableBase>(members?.Length ?? 0);
            if (members != null)
            {
                foreach (var member in members)
                {
                    if (member == null || ReferenceEquals(member, primary)) continue;
                    validMembers.Add(member);
                }
            }
            if (validMembers.Count == 0) return;

            primary.interactableGroup = true;
            primary.otherInterablesInGroup = validMembers;

            foreach (var member in validMembers)
            {
                member.MarkerActive = false;
                member.transform.SetPositionAndRotation(primary.transform.position, primary.transform.rotation);
                member.interactMarkerOffset = primary.interactMarkerOffset;
                if (member.interactCollider != null)
                    member.interactCollider.enabled = false;
            }
        }

        // ═══════════════════════════════════════════════════
        //  Query
        // ═══════════════════════════════════════════════════

        /// <summary>按 Identifier 获取交互点 GameObject。不存在返回 null。</summary>
        public static GameObject? GetInteractPoint(Identifier id)
        {
            if (_interactionRegistry.TryGet(id, out var entry))
                return entry.Target;
            return null;
        }

        /// <summary>尝试按 Identifier 获取交互点。</summary>
        public static bool TryGetInteractPoint(Identifier id, out GameObject point)
        {
            point = null!;
            if (_interactionRegistry.TryGet(id, out var entry))
            {
                point = entry.Target;
                return point != null;
            }
            return false;
        }

        // ═══════════════════════════════════════════════════
        //  Cleanup
        // ═══════════════════════════════════════════════════

        /// <summary>按 Identifier 移除交互点（触发 OnRemoved → Destroy GameObject）。</summary>
        public static bool RemoveInteract(Identifier id)
            => _interactionRegistry.Remove(id);

        /// <summary>批量移除指定 mod 注册的全部交互点。</summary>
        public static int RemoveAllInteracts(string modid)
            => _interactionRegistry.RemoveAllByOwner(modid);

        // ═══════════════════════════════════════════════════
        //  Internal
        // ═══════════════════════════════════════════════════

        /// <summary>创建交互点基础 GameObject（含 BoxCollider + Interact 图层）。</summary>
        private static GameObject CreateInteractPoint(
            Identifier id, Vector3 position, Quaternion rotation, Vector3? colliderSize)
        {
            var go = new GameObject($"Interact_{id.Path}");
            go.transform.position = position;
            go.transform.rotation = rotation;

            var collider = go.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = colliderSize ?? Vector3.one;

            int interactLayer = LayerMask.NameToLayer("Interactable");
            if (interactLayer != -1)
                go.layer = interactLayer;

            return go;
        }

        /// <summary>确保目标有 BoxCollider 和正确的 Interact 图层。</summary>
        private static void EnsureColliderAndLayer(GameObject target, bool addColliderIfMissing)
        {
            if (addColliderIfMissing && target.GetComponent<Collider>() == null)
            {
                var collider = target.AddComponent<BoxCollider>();
                collider.isTrigger = true;
            }

            int interactLayer = LayerMask.NameToLayer("Interactable");
            if (interactLayer != -1 && target.layer != interactLayer)
                target.layer = interactLayer;
        }
    }
}
