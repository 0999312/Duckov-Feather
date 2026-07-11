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

        protected override void OnInteractFinished()
        {
            ViewDispatcher.Open(ViewType, ViewParam);
        }
    }
}
