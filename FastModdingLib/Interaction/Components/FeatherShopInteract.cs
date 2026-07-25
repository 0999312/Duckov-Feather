using Duckov;
using Duckov.Economy;
using FeatherMod.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherMod.Interaction.Components
{
    /// <summary>
    /// Feathered 商店交互封装。内部使用 NpcShopInteract 和 StockShop，
    /// 通过 Identifier 管理生命周期，Mod 卸载时自动清理。
    /// 可在建筑、自定义物件等非 NPC 场景使用。
    /// </summary>
    public class FeatherShopInteract : InteractableBase
    {
        /// <summary>交互点唯一标识，注册到 InteractionRegistry 用于生命周期追踪。</summary>
        public Identifier InteractId;

        [SerializeField] private string? _merchantId;
        [SerializeField] private string? _interactNameKey;

        /// <summary>商店 MerchantProfile ID（对应 ShopUtils.CreateMerchantProfile 的 merchantID）。</summary>
        public string? MerchantId
        {
            get => _merchantId;
            set => _merchantId = value;
        }

        /// <summary>交互提示本地化键。为空时使用 "UI_Trade"。</summary>
        public string? InteractNameKey
        {
            get => _interactNameKey;
            set => _interactNameKey = value;
        }

        // 内部组件引用
        private NpcShopInteract? _nativeInteract;
        private StockShop? _stockShop;

        /// <summary>
        /// 将 FeatherShopInteract 挂载到目标 GameObject 并注册到 InteractionRegistry。
        /// 自动处理 Awake 时序：先禁用 GO → 挂组件 + 设字段 → 恢复原激活状态，确保 Awake 时字段已就绪。
        /// </summary>
        /// <param name="id">交互点唯一标识。</param>
        /// <param name="target">目标 GameObject（建筑、自定义物件等）。</param>
        /// <param name="merchantId">MerchantProfile ID（需先通过 ShopUtils.CreateMerchantProfile 注册）。</param>
        /// <param name="interactNameKey">可选交互提示本地化键，默认 "UI_Trade"。</param>
        /// <returns>创建的 FeatherShopInteract 实例。</returns>
        public static FeatherShopInteract Attach(
            Identifier id, GameObject target, string merchantId, string? interactNameKey = null)
        {
            bool wasActive = target.activeSelf;
            target.SetActive(false);
            FeatherShopInteract comp;
            try
            {
                comp = target.AddComponent<FeatherShopInteract>();
                comp.InteractId = id;
                comp._merchantId = merchantId;
                comp._interactNameKey = interactNameKey;
            }
            finally
            {
                target.SetActive(wasActive);
            }

            // 注册到 InteractionRegistry，mod 卸载时自动 Destroy
            InteractionUtils.Registry.Set(id, new InteractionEntry
            {
                Target = target,
                Modid = id.Domain
            }, id.Domain);

            return comp;
        }

        protected override void Awake()
        {
            // InteractableBase.Awake 会 foreach otherInterablesInGroup，
            // AddComponent 创建的实例该字段为 null，必须先初始化为空列表。
            otherInterablesInGroup = new List<InteractableBase>();

            base.Awake();

            // 设置交互名
            overrideInteractName = true;
            _overrideInteractNameKey = !string.IsNullOrEmpty(_interactNameKey)
                ? _interactNameKey : "UI_Trade";

            // 添加游戏原生组件前先禁用 GO，初始化 otherInterablesInGroup 后恢复，
            // 确保原生 InteractableBase 子类的 Awake 被 Harmony patch 拦截时不 NRE。
            bool wasActive = gameObject.activeSelf;
            gameObject.SetActive(false);

            // 配置原版 NpcShopInteract
            _nativeInteract = gameObject.AddComponent<NpcShopInteract>();
            _nativeInteract.otherInterablesInGroup = new List<InteractableBase>();
            _nativeInteract.overrideInteractName = true;
            _nativeInteract._overrideInteractNameKey = _overrideInteractNameKey;
            _nativeInteract.zoomIn = false;
            _nativeInteract.interactTime = 0.2f;
            _nativeInteract.coolTime = 0.2f;

            // 配置 StockShop
            if (!string.IsNullOrEmpty(_merchantId))
            {
                _stockShop = gameObject.AddComponent<StockShop>();
                _stockShop.merchantID = _merchantId;
                _stockShop.DisplayNameKey = _overrideInteractNameKey;
                _stockShop.accountAvaliable = true;
                _stockShop.returnCash = false;
                _stockShop.sellFactor = 1f;
                _stockShop.refreshAfterTimeSpan = 6000000000L;
                _stockShop.refreshStockOnStart = false;
            }

            gameObject.SetActive(wasActive);

            // 配置碰撞体（非 InteractableBase，不需要 deactivate 包裹）
            var col = gameObject.GetComponent<Collider>();
            if (col == null)
            {
                var sphereCol = gameObject.AddComponent<SphereCollider>();
                sphereCol.isTrigger = false;
                sphereCol.radius = 4f;
                sphereCol.center = Vector3.zero;
                interactCollider = sphereCol;
            }
            else
            {
                interactCollider = col;
            }
        }

        protected override void OnInteractFinished()
        {
            if (_stockShop != null)
                _stockShop.ShowUI();
            else
                ViewDispatcher.Open(GameViews.Shop, _merchantId);
        }
    }
}