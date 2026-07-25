using Duckov.PerkTrees;
using Duckov.PerkTrees.Behaviours;
using FeatherMod.Items;
using FeatherMod.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherMod
{
    // ═══════════════════════════════════════════════════════════════
    //  抽象基类
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// PerkBehaviour 配置抽象基类。子类封装单个原版 PerkBehaviour 的配置逻辑，
    /// FML 内部调用 <see cref="ApplyTo"/> 完成 AddComponent + 字段赋值。
    /// </summary>
    public abstract class PerkBehaviourConfig
    {
        internal abstract void ApplyTo(GameObject perkGo);
    }

    // ═══════════════════════════════════════════════════════════════
    //  #1 UnlockFormula — 解锁 requirePerk 匹配的配方
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 解锁与当前 Perk 关联的所有配方。
    /// 无需额外配置——Behaviour 自动匹配 <c>CraftingFormula.requirePerk == "treeID/perkName"</c> 的配方。
    /// 使用前需在 <see cref="CraftingFormulaData.RequirePerk"/> 中声明对应值。
    /// </summary>
    public class UnlockFormulaConfig : PerkBehaviourConfig
    {
        internal override void ApplyTo(GameObject perkGo)
        {
            perkGo.AddComponent<UnlockFormula>();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  #2 UnlockAchievement — 解锁成就
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Perk 解锁后触发指定成就。
    /// </summary>
    public class UnlockAchievementConfig : PerkBehaviourConfig
    {
        /// <summary>成就 key（游戏 AchievementManager 中定义的成就标识符）。</summary>
        public string AchievementKey { get; set; } = string.Empty;

        internal override void ApplyTo(GameObject perkGo)
        {
            var bh = perkGo.AddComponent<UnlockAchievement>();
            bh.achievementKey = AchievementKey;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  #3 ModifyCharacterStats — 修改角色属性
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 单条属性修改项。对应 <see cref="ModifyCharacterStatsBase.Entry"/>。
    /// </summary>
    public struct StatModifierEntry
    {
        /// <summary>属性 key（如 "MaxHealth"、"MoveSpeed"、"JumpForce" 等）。</summary>
        public string Key;
        /// <summary>修改值。正值增加，负值减少。</summary>
        public float Value;
        /// <summary>是否为百分比修改。true 时 Value=0.15 表示 +15%。</summary>
        public bool Percentage;
    }

    /// <summary>
    /// Perk 解锁后修改角色属性（生命值、移速、跳跃力等）。
    /// </summary>
    public class ModifyStatsConfig : PerkBehaviourConfig
    {
        /// <summary>属性修改条目列表。</summary>
        public StatModifierEntry[] Entries { get; set; } = System.Array.Empty<StatModifierEntry>();

        internal override void ApplyTo(GameObject perkGo)
        {
            var bh = perkGo.AddComponent<ModifyCharacterStatsBase>();
            var list = new List<ModifyCharacterStatsBase.Entry>();
            foreach (var e in Entries)
            {
                list.Add(new ModifyCharacterStatsBase.Entry
                {
                    key = e.Key,
                    value = e.Value,
                    percentage = e.Percentage
                });
            }
            bh.entries = list;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  #4 ChangeBlackMarketRefreshTimeFactor — 黑市刷新时间系数
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Perk 解锁后修改黑市刷新时间系数。
    /// </summary>
    public class BlackMarketRefreshTimeConfig : PerkBehaviourConfig
    {
        /// <summary>时间系数变化量。默认 -0.1（即缩短 10% 刷新时间）。</summary>
        public float Amount { get; set; } = -0.1f;

        internal override void ApplyTo(GameObject perkGo)
        {
            var bh = perkGo.AddComponent<ChangeBlackMarketRefreshTimeFactor>();
            bh.amount = Amount;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  #6 AddBlackMarketRefreshChance — 增加黑市刷新次数
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Perk 解锁后增加黑市每日刷新次数上限。
    /// </summary>
    public class BlackMarketRefreshChanceConfig : PerkBehaviourConfig
    {
        /// <summary>增加的刷新次数。默认 1。</summary>
        public int AddAmount { get; set; } = 1;

        internal override void ApplyTo(GameObject perkGo)
        {
            var bh = perkGo.AddComponent<AddBlackMarketRefreshChance>();
            bh.addAmount = AddAmount;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  #8 AddPlayerStorage — 增加仓库容量
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Perk 解锁后增加玩家仓库容量。
    /// </summary>
    public class AddPlayerStorageConfig : PerkBehaviourConfig
    {
        /// <summary>增加的仓库容量（格数）。</summary>
        public int Capacity { get; set; }

        internal override void ApplyTo(GameObject perkGo)
        {
            var bh = perkGo.AddComponent<AddPlayerStorage>();
            bh.addCapacity = Capacity;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  #10 UnlockStockShopItem — 解锁商店物品
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Perk 解锁后解锁指定物品在商店中的销售权。
    /// </summary>
    public class UnlockShopItemConfig : PerkBehaviourConfig
    {
        /// <summary>要解锁的物品 Identifier。FML 内部解析为 game typeID。</summary>
        public Identifier ItemId { get; set; } = null!;

        internal override void ApplyTo(GameObject perkGo)
        {
            int typeId = ItemUtils.ResolveItemRef(ItemId, 0);
            var bh = perkGo.AddComponent<UnlockStockShopItem>();
            bh.itemTypeID = typeId;
        }
    }
}
