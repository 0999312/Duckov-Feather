namespace FeatherMod.Events.GameEvents
{
    /// <summary>
    /// 关卡初始化完成事件。桥接游戏原生 <c>LevelManager.OnLevelInitialized</c> 事件
    /// （原生为无参 <c>Action</c>）。Manager 字段恒为 null——如需 LevelManager 实例，
    /// 订阅方可直接通过单例访问，不依赖本字段。
    /// </summary>
    public sealed class LevelInitializedEvent : Event
    {
        /// <summary>事件携带的 LevelManager 实例（原生事件无参，恒为 null）。</summary>
        public LevelManager? Manager { get; }

        public LevelInitializedEvent(LevelManager? manager = null)
        {
            Manager = manager;
        }
    }
}
