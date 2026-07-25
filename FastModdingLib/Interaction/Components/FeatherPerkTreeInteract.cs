using Duckov;
using Duckov.PerkTrees.Interactable;
using FeatherMod.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherMod.Interaction.Components
{
    /// <summary>
    /// Feathered 技能树交互封装。内部使用 PerkTreeUIInvoker，
    /// 通过 Identifier 管理生命周期，Mod 卸载时自动清理。
    /// 可在建筑、自定义物件等非 NPC 场景使用。
    /// </summary>
    public class FeatherPerkTreeInteract : InteractableBase
    {
        /// <summary>交互点唯一标识，注册到 InteractionRegistry 用于生命周期追踪。</summary>
        public Identifier InteractId;

        [SerializeField] private string? _perkTreeId;
        [SerializeField] private string? _interactNameKey;

        /// <summary>技能树 ID（对应 PerkTreeManager 中的 tree.ID）。</summary>
        public string? PerkTreeId
        {
            get => _perkTreeId;
            set => _perkTreeId = value;
        }

        public string? InteractNameKey
        {
            get => _interactNameKey;
            set => _interactNameKey = value;
        }

        private PerkTreeUIInvoker? _native;

        /// <summary>
        /// 将 FeatherPerkTreeInteract 挂载到目标 GameObject 并注册到 InteractionRegistry。
        /// 自动处理 Awake 时序：先禁用 GO → 挂组件 + 设字段 → 恢复原激活状态，确保 Awake 时字段已就绪。
        /// </summary>
        /// <param name="id">交互点唯一标识。</param>
        /// <param name="target">目标 GameObject。</param>
        /// <param name="perkTreeId">技能树 ID（需已通过 PerkTreeUtils.RegisterPerkTree 注册或为原版树）。</param>
        /// <param name="interactNameKey">可选交互提示本地化键，默认使用 perkTreeId 或 "UI_PerkTree"。</param>
        /// <returns>创建的 FeatherPerkTreeInteract 实例。</returns>
        public static FeatherPerkTreeInteract Attach(
            Identifier id, GameObject target, string perkTreeId, string? interactNameKey = null)
        {
            bool wasActive = target.activeSelf;
            target.SetActive(false);
            FeatherPerkTreeInteract comp;
            try
            {
                comp = target.AddComponent<FeatherPerkTreeInteract>();
                comp.InteractId = id;
                comp._perkTreeId = perkTreeId;
                comp._interactNameKey = interactNameKey;
            }
            finally
            {
                target.SetActive(wasActive);
            }

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

            overrideInteractName = true;
            _overrideInteractNameKey = !string.IsNullOrEmpty(_interactNameKey)
                ? _interactNameKey : (_perkTreeId ?? "UI_PerkTree");

            // PerkTreeUIInvoker 也是 InteractableBase 子类，AddComponent 时其 Awake
            // 被 Harmony patch 拦截会访问 otherInterablesInGroup。先禁用 GO → 添加 →
            // 初始化字段 → 恢复激活，确保 Awake 触发时字段已就绪。
            bool wasActive = gameObject.activeSelf;
            gameObject.SetActive(false);
            _native = gameObject.AddComponent<PerkTreeUIInvoker>();
            _native.otherInterablesInGroup = new List<InteractableBase>();
            if (!string.IsNullOrEmpty(_perkTreeId))
                _native.perkTreeID = _perkTreeId;
            _native.interactMarkerOffset = new Vector3(0, 1.5f, 0);
            _native.overrideInteractName = true;
            _native._overrideInteractNameKey = _overrideInteractNameKey;
            _native.interactTime = 0f;
            _native.finishWhenTimeOut = true;
            _native.coolTime = 0.2f;
            gameObject.SetActive(wasActive);
        }

        protected override void OnInteractFinished()
        {
            ViewDispatcher.Open(GameViews.PerkTree, _perkTreeId);
        }
    }
}