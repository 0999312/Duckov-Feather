using System;

namespace FeatherMod.Events.GameEvents
{
    /// <summary>
    /// 实体受伤事件。桥接游戏原生 <c>Health.OnHurt</c> 静态事件
    /// （原生签名 <c>Action&lt;Health, DamageInfo&gt;</c>）。
    /// 标注 [Cancelable]：可被 FML handler 拦截，跳过游戏原生伤害效果的应用（观察 + 拦截双路径 gating）。
    /// </summary>
    [Cancelable]
    public sealed class HurtEvent : Event
    {
        /// <summary>受伤的目标角色的 Health 组件。</summary>
        public Health Target { get; }

        /// <summary>伤害信息。</summary>
        public DamageInfo Info { get; }

        public HurtEvent(Health target, DamageInfo info)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Info = info;
        }
    }
}
