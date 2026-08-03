namespace FeatherMod.Events.GameEvents
{
    /// <summary>
    /// 合成配方解锁事件。桥接游戏原生 <c>CraftingManager.OnFormulaUnlocked</c> 静态事件
    /// （原生签名 <c>Action&lt;string&gt;</c>，参数为配方 ID）。
    /// </summary>
    public sealed class FormulaUnlockedEvent : Event
    {
        /// <summary>解锁的合成配方 ID。</summary>
        public string Formula { get; }

        public FormulaUnlockedEvent(string formula)
        {
            Formula = formula ?? throw new System.ArgumentNullException(nameof(formula));
        }
    }
}
