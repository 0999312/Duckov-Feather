using Duckov;
using Duckov.Buildings.UI;
using Duckov.PerkTrees;
using Duckov.UI;
using FeatherMod.Interaction.Components;
using FeatherMod.Register;
using FeatherMod.UI;
using FeatherMod.Utils;
using System;
using System.Collections.Generic;
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
        /// <returns>创建的交互点 GameObject。</returns>
        public static GameObject SpawnViewInteract(
            Identifier id, Vector3 position, Identifier viewType,
            string? viewParam = null, Quaternion? rotation = null, Vector3? colliderSize = null)
        {
            var go = CreateInteractPoint(id, position, rotation ?? Quaternion.identity, colliderSize);

            var handler = go.AddComponent<ViewInteractHandler>();
            handler.ViewType = viewType;
            handler.ViewParam = viewParam;

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

        /// <summary>给已有 GameObject 挂载 View 交互处理器。</summary>
        public static void AttachViewInteract(
            Identifier id, GameObject target, Identifier viewType,
            string? viewParam = null, bool addColliderIfMissing = true)
        {
            EnsureColliderAndLayer(target, addColliderIfMissing);

            var handler = target.AddComponent<ViewInteractHandler>();
            handler.ViewType = viewType;
            handler.ViewParam = viewParam;

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
