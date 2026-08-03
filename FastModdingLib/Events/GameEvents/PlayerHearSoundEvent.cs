namespace FeatherMod.Events.GameEvents
{
    /// <summary>
    /// 玩家听到声音事件。桥接游戏原生 <c>AIMainBrain.OnPlayerHearSound</c> 静态事件
    /// （原生签名 <c>Action&lt;AISound&gt;</c>）。
    /// </summary>
    public sealed class PlayerHearSoundEvent : Event
    {
        /// <summary>声音信息。</summary>
        public AISound SoundInfo { get; }

        public PlayerHearSoundEvent(AISound soundInfo)
        {
            SoundInfo = soundInfo;
        }
    }
}
