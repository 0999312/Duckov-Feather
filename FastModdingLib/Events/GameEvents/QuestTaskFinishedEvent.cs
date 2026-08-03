namespace FeatherMod.Events.GameEvents
{
    /// <summary>
    /// 任务子任务完成事件。桥接游戏原生 <c>QuestManager.OnTaskFinishedEvent</c> 静态事件
    /// （原生签名 <c>Action&lt;Quest, Task&gt;</c>，2 参）。
    /// </summary>
    public sealed class QuestTaskFinishedEvent : Event
    {
        /// <summary>所属 Quest。</summary>
        public Duckov.Quests.Quest Quest { get; }

        /// <summary>完成的 Task。</summary>
        public Duckov.Quests.Task Task { get; }

        public QuestTaskFinishedEvent(Duckov.Quests.Quest quest, Duckov.Quests.Task task)
        {
            Quest = quest ?? throw new System.ArgumentNullException(nameof(quest));
            Task = task ?? throw new System.ArgumentNullException(nameof(task));
        }
    }
}
