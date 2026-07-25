using Duckov;
using System;
using UnityEngine;

namespace FeatherMod.Interaction.Components
{
    /// <summary>
    /// 自定义委托交互处理器。挂载到交互点 GameObject 上，
    /// 交互完成时调用绑定的 Action 委托。
    /// </summary>
    /// <remarks>
    /// OnInteract 委托由 InteractionUtils 在创建交互点时直接赋值。
    /// </remarks>
    public class DelegateInteractHandler : InteractableBase
    {
        /// <summary>自定义交互回调（可选）。</summary>
        public Action? OnInteract;

        protected override void Awake()
        {
            // InteractableBase.Awake 会 foreach otherInterablesInGroup，
            // AddComponent 创建的实例该字段为 null，必须先初始化为空列表。
            otherInterablesInGroup = new System.Collections.Generic.List<InteractableBase>();

            base.Awake();
        }

        protected override void OnInteractFinished()
        {
            OnInteract?.Invoke();
        }
    }
}
