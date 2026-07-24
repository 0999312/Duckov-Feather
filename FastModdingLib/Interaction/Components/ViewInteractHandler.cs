using Duckov;
using FeatherMod.Utils;
using UnityEngine;

namespace FeatherMod.Interaction.Components
{
    /// <summary>
    /// View 交互处理器。挂载到交互点 GameObject 上，
    /// 交互完成时通过 ViewDispatcher 打开指定 View。
    /// </summary>
    /// <remarks>
    /// ViewType 和 ViewParam 由 InteractionUtils 在创建交互点时直接赋值。
    /// </remarks>
    public class ViewInteractHandler : InteractableBase
    {
        /// <summary>目标 View 类型 Identifier（如 GameViews.PerkTree）。</summary>
        public Identifier ViewType;

        /// <summary>可选参数，传递给 View 打开方法。</summary>
        public string? ViewParam;

        /// <summary>
        /// 交互名称的本地化 key。非空时启用自定义交互名，
        /// 由 Awake() 写入 <c>overrideInteractName</c> 与 <c>_overrideInteractNameKey</c>。
        /// </summary>
        public string? InteractNameKey;

        /// <summary>
        /// 交互标记的世界空间偏移量。非空时由 Awake() 写入
        /// <c>interactMarkerOffset</c>，用于调整交互提示图标的位置。
        /// </summary>
        public Vector3? MarkerOffset;

        /// <summary>
        /// 交互冷却时间（秒）。大于 0 时启用冷却机制：
        /// Awake() 将 <c>interactTime</c> 置 0、<c>finishWhenTimeOut</c> 设为
        /// <see cref="FinishWhenTimeOut"/>、<c>coolTime</c> 设为本值。
        /// </summary>
        public float CoolTime = 0f;

        /// <summary>
        /// 冷却模式下是否在计时结束时自动完成交互。仅当 <see cref="CoolTime"/> &gt; 0 时生效，
        /// 由 Awake() 写入 <c>finishWhenTimeOut</c>。
        /// </summary>
        public bool FinishWhenTimeOut = true;

        protected override void Awake()
        {
            // InteractableBase.Awake 会 foreach otherInterablesInGroup，
            // AddComponent 创建的实例该字段为 null，必须先初始化为空列表。
            otherInterablesInGroup = new System.Collections.Generic.List<InteractableBase>();

            base.Awake();

            // 始终启用自定义交互名（参考 FeatherShopInteract 等组件的 Pattern B）。
            // InteractNameKey 为空时 fallback 到 "UI_Interact"（游戏默认交互文本），
            // 非空时用作本地化 key 通过 ToPlainText() 解析。
            overrideInteractName = true;
            _overrideInteractNameKey = !string.IsNullOrEmpty(InteractNameKey)
                ? InteractNameKey
                : "UI_Interact";

            if (MarkerOffset.HasValue)
            {
                interactMarkerOffset = MarkerOffset.Value;
            }

            if (CoolTime > 0f)
            {
                interactTime = 0f;
                finishWhenTimeOut = FinishWhenTimeOut;
                coolTime = CoolTime;
            }
        }

        protected override void OnInteractFinished()
        {
            ViewDispatcher.Open(ViewType, ViewParam);
        }
    }
}
