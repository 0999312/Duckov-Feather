namespace FeatherMod.Events.GameEvents
{
    /// <summary>
    /// 物品合成事件。桥接自游戏原生 <c>CraftingManager.OnItemCrafted</c>（Action&lt;CraftingFormula, Item&gt;）。
    /// 仅观察用途，不支持取消。
    /// </summary>
    public sealed class ItemCraftedEvent : Event
    {
        /// <summary>合成的配方。</summary>
        public object Formula { get; }
        /// <summary>合成的产物 Item。</summary>
        public object Item { get; }

        public ItemCraftedEvent(object formula, object item)
        {
            Formula = formula;
            Item = item;
        }
    }
}
