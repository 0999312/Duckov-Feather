namespace FeatherMod.Events.GameEvents
{
    /// <summary>
    /// 任务完成事件。桥接自游戏原生 <c>QuestManager.OnTaskFinishedEvent</c>（Action&lt;Quest, Task&gt;）。
    /// 仅观察用途，不支持取消。
    /// </summary>
    public sealed class QuestTaskFinishedEvent : Event
    {
        /// <summary>所属 Quest。</summary>
        public object Quest { get; }
        /// <summary>完成的 Task。</summary>
        public object Task { get; }

        public QuestTaskFinishedEvent(object quest, object task)
        {
            Quest = quest;
            Task = task;
        }
    }
}
