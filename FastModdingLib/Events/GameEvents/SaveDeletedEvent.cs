namespace FeatherMod.Events.GameEvents
{
    /// <summary>
    /// 存档槽位被删除事件。桥接自游戏原生 <c>SavesSystem.OnSaveDeleted</c> 静态事件。
    /// </summary>
    /// <remarks>
    /// <para><b>触发时机</b>：玩家在主菜单长按删除存档槽位 →
    /// <c>SavesSystem.DeleteCurrentSave()</c> 完成磁盘删除并写入占位文件（"Created"=false）→ 触发本事件。</para>
    /// <para><b>使用场景</b>：FML 各模块订阅此事件清理<strong>内存注册表</strong>和
    /// <strong>跨槽位持久化状态</strong>（如 QuestGiver ID 映射），避免删除存档后内存残留导致
    /// 新存档继承旧状态或 Quest 链断裂。</para>
    /// <para><b>不可取消</b>：磁盘删除已完成，事件为通知性质。</para>
    /// </remarks>
    public sealed class SaveDeletedEvent : Event
    {
        /// <summary>被删除的存档槽位 ID（1-based）。来源于 <c>SavesSystem.CurrentSlot</c> 触发时的快照。</summary>
        public int Slot { get; }

        public SaveDeletedEvent(int slot)
        {
            Slot = slot;
        }
    }
}