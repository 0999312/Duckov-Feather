using Duckov;
using Duckov.UI;
using FeatherMod.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherMod.Interaction.Components
{
    /// <summary>
    /// Feathered 配方注册交互封装（蓝图研究台）。
    /// 通过 Identifier 管理生命周期，Mod 卸载时自动清理。
    /// 挂载到建筑 functionContainer 或自定义物件上，
    /// 交互时打开 FormulasRegisterView，按 RegisterTag 过滤可注册配方。
    /// </summary>
    /// <remarks>
    /// 可通过 <see cref="Attach"/> 以纯代码创建，也可通过
    /// <see cref="InteractionGroupBuilder"/> 组合到多交互建筑中：
    /// <code>
    /// new InteractionGroupBuilder()
    ///     .Add(new Identifier("mymod", "research"), GameViews.FormulasRegister,
    ///          viewParam: "weapon")
    ///     .BuildOn(functionContainer);
    /// </code>
    /// </remarks>
    public class FeatherFormulasRegisterInteract : InteractableBase
    {
        /// <summary>交互点唯一标识，注册到 InteractionRegistry 用于生命周期追踪。</summary>
        public Identifier InteractId;

        [SerializeField] private string? _registerTag;
        [SerializeField] private string? _interactNameKey;

        /// <summary>配方标签过滤（对应 Recipe.Tags）。为 null 时显示全部可注册配方。</summary>
        public string? RegisterTag
        {
            get => _registerTag;
            set => _registerTag = value;
        }

        /// <summary>交互提示本地化键。为空时使用 "UI_Research"。</summary>
        public string? InteractNameKey
        {
            get => _interactNameKey;
            set => _interactNameKey = value;
        }

        /// <summary>
        /// 将 FeatherFormulasRegisterInteract 挂载到目标 GameObject 并注册到 InteractionRegistry。
        /// 自动处理 Awake 时序：先禁用 GO → 挂组件 + 设字段 → 恢复原激活状态，确保 Awake 时字段已就绪。
        /// </summary>
        /// <param name="id">交互点唯一标识。</param>
        /// <param name="target">目标 GameObject（建筑 functionContainer、自定义物件等）。</param>
        /// <param name="registerTag">可选配方标签过滤，为 null 时显示全部可注册配方。</param>
        /// <param name="interactNameKey">可选交互提示本地化键，默认 "UI_Research"。</param>
        /// <returns>创建的 FeatherFormulasRegisterInteract 实例。</returns>
        public static FeatherFormulasRegisterInteract Attach(
            Identifier id, GameObject target, string? registerTag = null, string? interactNameKey = null)
        {
            bool wasActive = target.activeSelf;
            target.SetActive(false);
            FeatherFormulasRegisterInteract comp;
            try
            {
                comp = target.AddComponent<FeatherFormulasRegisterInteract>();
                comp.InteractId = id;
                comp._registerTag = registerTag;
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

            overrideInteractName = true;
            _overrideInteractNameKey = !string.IsNullOrEmpty(_interactNameKey)
                ? _interactNameKey : "UI_Research";

            // 配置碰撞体
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
            ViewDispatcher.Open(GameViews.FormulasRegister, _registerTag);
        }
    }
}
