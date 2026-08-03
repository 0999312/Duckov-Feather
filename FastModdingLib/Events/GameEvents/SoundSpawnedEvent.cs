namespace FeatherMod.Events.GameEvents
{
    /// <summary>
    /// 声音生成事件。桥接游戏原生 <c>AIMainBrain.OnSoundSpawned</c> 静态事件
    /// （原生签名 <c>Action&lt;AISound&gt;</c>）。
    /// </summary>
    public sealed class SoundSpawnedEvent : Event
    {
        /// <summary>声音信息。</summary>
        public AISound SoundInfo { get; }

        public SoundSpawnedEvent(AISound soundInfo)
        {
            SoundInfo = soundInfo;
        }
    }
}
