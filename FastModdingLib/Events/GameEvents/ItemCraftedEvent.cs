namespace FeatherMod.Events.GameEvents
{
    /// <summary>
    /// 物品合成事件。桥接游戏原生 <c>CraftingManager.OnItemCrafted</c> 静态事件
    /// （原生签名 <c>Action&lt;CraftingFormula, Item&gt;</c>）。
    /// </summary>
    public sealed class ItemCraftedEvent : Event
    {
        /// <summary>合成的配方。</summary>
        public CraftingFormula Formula { get; }

        /// <summary>合成的产物 Item。</summary>
        public ItemStatsSystem.Item Item { get; }

        public ItemCraftedEvent(CraftingFormula formula, ItemStatsSystem.Item item)
        {
            Formula = formula;
            Item = item;
        }
    }
}
