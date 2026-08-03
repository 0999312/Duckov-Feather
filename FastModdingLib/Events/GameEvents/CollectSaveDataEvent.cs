namespace FeatherMod.Events.GameEvents
{
    /// <summary>
    /// 收集存档数据事件。桥接游戏原生 <c>SavesSystem.OnCollectSaveData</c> 静态事件
    /// （原生为无参 <c>Action</c>）。SaveData 字段恒为 null——本事件是"时机通知"，
    /// 各 ISaveDataProvider 在收到事件后自行向 SavesSystem 写入自身键。
    /// </summary>
    public sealed class CollectSaveDataEvent : Event
    {
        /// <summary>存档数据（原生事件无参，恒为 null；保留字段以兼容订阅方）。</summary>
        public object? SaveData { get; }

        public CollectSaveDataEvent(object? saveData)
        {
            SaveData = saveData;
        }
    }
}
