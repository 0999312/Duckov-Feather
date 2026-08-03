namespace FeatherMod.Events.GameEvents
{
    /// <summary>
    /// 玩家死亡事件。桥接游戏原生 <c>LevelManager.OnMainCharacterDead</c> 静态事件
    /// （原生签名 <c>Action&lt;DamageInfo&gt;</c>）。
    /// </summary>
    public sealed class PlayerDeathEvent : Event
    {
        /// <summary>致死伤害信息。</summary>
        public DamageInfo Info { get; }

        public PlayerDeathEvent(DamageInfo info)
        {
            Info = info;
        }
    }
}
