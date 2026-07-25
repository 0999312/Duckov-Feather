using Duckov.PerkTrees;
using Duckov.Economy;
using FeatherMod.Utils;
using ItemStatsSystem;
using System;
using System.Linq;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// Perk 配置 DTO。模组作者通过此对象声明 Perk 的全部属性，
    /// FML 内部转换为游戏原生 <see cref="Perk"/> 和 <see cref="PerkRequirement"/>。
    /// </summary>
    /// <remarks>
    /// <para><b>Identifier 约定</b>：</para>
    /// <list type="bullet">
    /// <item><see cref="PerkId"/> — Domain=modid, Path=perk名（自定义 Perk）</item>
    /// <item>原版 Perk 引用（<see cref="RequiredPerks"/>）— Domain="duckov", Path="treeID/perkName"</item>
    /// <item><see cref="CostItems"/> — 复用 <see cref="ItemEntry"/>，Identifier → typeID 自动解析</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// PerkTreeUtils.AddPerk(
    ///     new Identifier("duckov", "CombatTree"),
    ///     new PerkConfig
    ///     {
    ///         PerkId = new Identifier("mymod", "rapid_fire"),
    ///         Icon = myIcon,
    ///         DisplayNameKey = "Perk_rapid_fire",
    ///         RequiredLevel = 10,
    ///         CostItems = new[] { ItemEntry.Of("duckov:GoldCoin", 5000) },
    ///         RequireTimeTicks = TimeSpan.FromHours(2).Ticks,
    ///         RequiredPerks = new[] { new Identifier("duckov", "CombatTree/Marksman") }
    ///     });
    /// </code>
    /// </example>
    public class PerkConfig
    {
        // ── 标识 ──

        /// <summary>Perk Identifier。Domain=modid, Path=perk名（兼作 GameObject.name，影响存档 key）。</summary>
        public Identifier PerkId { get; set; } = null!;

        // ── 外观 / 本地化 ──

        /// <summary>Perk 图标 Sprite。</summary>
        public Sprite? Icon { get; set; }

        /// <summary>
        /// 显示名本地化键（"Perks" 表）。对应 <see cref="Perk.displayName"/>。
        /// 设为 "Perk_xxx" 可在游戏 Perks 本地化表中查找翻译。
        /// </summary>
        public string DisplayNameKey { get; set; } = "未命名技能";

        /// <summary>
        /// 是否有描述。true 时描述 key = <see cref="DisplayNameKey"/> + "_Desc"。
        /// 对应 <see cref="Perk.hasDescription"/>。
        /// </summary>
        public bool HasDescription { get; set; }

        /// <summary>物品品质标识。对应 <see cref="Perk.DisplayQuality"/>。</summary>
        public DisplayQuality Quality { get; set; }

        /// <summary>是否默认解锁。对应 <see cref="Perk.DefaultUnlocked"/>。</summary>
        public bool DefaultUnlocked { get; set; }

        // ── 需求（桥接 PerkRequirement） ──

        /// <summary>解锁所需等级。对应 <see cref="PerkRequirement.level"/>。</summary>
        public int RequiredLevel { get; set; }

        /// <summary>
        /// 消耗物品列表（FML <see cref="ItemEntry"/> 桥接）。
        /// 内部通过 <see cref="ItemEntry.ResolveTypeId"/> 自动解析 Identifier → 游戏原生 TypeID。
        /// 对应 <see cref="Cost.items"/>。
        /// </summary>
        public ItemEntry[]? CostItems { get; set; }

        /// <summary>消耗金钱。对应 <see cref="Cost.money"/>。</summary>
        public long Money { get; set; }

        /// <summary>解锁所需时间（TimeSpan.Ticks）。对应 <see cref="PerkRequirement.requireTime"/>。</summary>
        public long RequireTimeTicks { get; set; }

        // ── 前置 Perk ──

        /// <summary>
        /// 前置 Perk 列表。FML 内部自动建立 <see cref="PerkTreeUtils.ConnectPerks"/> 关系。
        /// 自定义 Perk 用 <c>Identifier("mymod", "other_perk")</c>，
        /// 原版 Perk 用 <c>Identifier("duckov", "treeID/perkName")</c>。
        /// </summary>
        public Identifier[]? RequiredPerks { get; set; }

        // ── PerkBehaviour ──

        /// <summary>
        /// PerkBehaviour 声明式配置列表。FML 内部对每个条目执行 AddComponent + 字段赋值。
        /// 支持 7 种原版 Behaviour 的 FML 封装配置：UnlockFormula、UnlockAchievement、
        /// ModifyStats、BlackMarketRefreshTime、BlackMarketRefreshChance、AddPlayerStorage、UnlockShopItem。
        /// 自定义 PerkBehaviour 仍走 <see cref="PerkTreeUtils.AddPerkBehaviour{T}"/>。
        /// </summary>
        public PerkBehaviourConfig[]? Behaviours { get; set; }

        // ── 布局 ──

        /// <summary>
        /// Perk 在技能树 UI 中的节点坐标。null 时使用自动布局。
        /// X=分支偏移, Y=深度（0=根节点, 1=一级前置, ...）。
        /// 自动布局: X=parent.X + siblingIndex*200, Y=(maxParentDepth+1)*150。
        /// </summary>
        public Vector2? Position { get; set; }

        // ── 内部转换 ──

        /// <summary>
        /// 将 <see cref="PerkConfig"/> 的需求字段转换为游戏原生 <see cref="PerkRequirement"/>。
        /// </summary>
        internal PerkRequirement BuildPerkRequirement()
        {
            var items = CostItems ?? Array.Empty<ItemEntry>();
            var nativeItems = new Cost.ItemEntry[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                nativeItems[i] = new Cost.ItemEntry
                {
                    id = items[i].ResolveTypeId(),
                    amount = items[i].Amount
                };
            }
            return new PerkRequirement
            {
                level = RequiredLevel,
                cost = new Cost
                {
                    money = Money,
                    items = nativeItems
                },
                requireTime = RequireTimeTicks
            };
        }
    }
}
