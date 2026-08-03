using System;

namespace FeatherMod.Events.GameEvents
{
    /// <summary>
    /// 物品解锁状态变化事件。桥接游戏原生 <c>EconomyManager.OnItemUnlockStateChanged</c> 静态事件
    /// （原生签名 <c>Action&lt;int&gt;</c>，仅 1 参：物品 typeID）。
    /// 原生事件不含"是否解锁"参数，Unlocked 由桥接层固定为 true（事件仅表示"刚解锁"）。
    /// </summary>
    public sealed class ItemUnlockStateChangedEvent : Event
    {
        /// <summary>物品 typeID。</summary>
        public int ItemId { get; }

        /// <summary>物品是否已解锁（原生事件仅表示解锁动作，恒为 true）。</summary>
        public bool Unlocked { get; }

        public ItemUnlockStateChangedEvent(int itemId, bool unlocked)
        {
            ItemId = itemId;
            Unlocked = unlocked;
        }
    }
}
