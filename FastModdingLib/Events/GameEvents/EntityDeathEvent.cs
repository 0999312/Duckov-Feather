using System;

namespace FeatherMod.Events.GameEvents
{
    /// <summary>
    /// 实体死亡事件。桥接游戏原生 <c>Health.OnDead</c> 静态事件
    /// （原生签名 <c>Action&lt;Health, DamageInfo&gt;</c>）。
    /// </summary>
    public sealed class EntityDeathEvent : Event
    {
        /// <summary>死亡实体的 Health 组件。</summary>
        public Health Victim { get; }

        /// <summary>致命伤害信息。</summary>
        public DamageInfo Info { get; }

        public EntityDeathEvent(Health victim, DamageInfo info)
        {
            Victim = victim ?? throw new ArgumentNullException(nameof(victim));
            Info = info;
        }
    }
}
