using ItemStatsSystem.Stats;
using System;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// 天赋效果修饰符。modder 用此类型描述天赋效果，无需接触游戏内部类型。
    /// FML 内部负责将此 DTO 转换为游戏原生的 <c>EndowmentEntry.ModifierDescription</c>。
    /// </summary>
    public class EndowmentModifier
    {
        /// <summary>属性键（如 "moveSpeed"、"maxHealth"），对应 <c>ItemStatsSystem.Stats</c> 中的 stat key。</summary>
        public string StatKey { get; set; } = "";

        /// <summary>修饰类型：Add（直接加减）、PercentageAdd（百分比加减）、PercentageMultiply（百分比乘）。</summary>
        public ModifierType Type { get; set; }

        /// <summary>修饰数值。</summary>
        public float Value { get; set; }
    }

    /// <summary>
    /// 天赋配置 DTO。modder 用纯 C# 创建此对象，传入 <see cref="EndowmentUtils.RegisterEndowment(Identifier, EndowmentConfig, string?)"/>。
    /// FML 内部负责将此 DTO 转换为游戏原生的 <see cref="Duckov.Endowment.EndowmentEntry"/>。
    /// </summary>
    /// <example>
    /// <code>
    /// var icon = ItemUtils.LoadSprite("endowment_assassin", myWeaponTypeID);
    /// EndowmentUtils.RegisterEndowment(
    ///     new Identifier("mymod", "assassin"),
    ///     new EndowmentConfig
    ///     {
    ///         Modifiers = new[]
    ///         {
    ///             new EndowmentModifier { StatKey = "moveSpeed", Type = ModifierType.PercentageAdd, Value = 0.15f },
    ///             new EndowmentModifier { StatKey = "maxHealth", Type = ModifierType.PercentageAdd, Value = -0.1f }
    ///         },
    ///         Icon = icon,
    ///         UnlockedByDefault = false,
    ///         RequirementTextKey = "endowment_assassin_requirement"
    ///     });
    /// </code>
    /// </example>
    public class EndowmentConfig
    {
        /// <summary>效果修饰符列表。每个元素描述一项属性变化。</summary>
        public EndowmentModifier[] Modifiers { get; set; } = Array.Empty<EndowmentModifier>();

        /// <summary>
        /// 天赋图标 Sprite。可通过 <see cref="ItemUtils.LoadSprite(string, int)"/> 从
        /// <c>assets/textures/</c> 目录加载 PNG 文件。为 null 时使用默认图标。
        /// </summary>
        public Sprite? Icon { get; set; }

        /// <summary>是否默认解锁（无需任务条件）。默认 false。</summary>
        public bool UnlockedByDefault { get; set; }

        /// <summary>解锁条件提示文本的本地化 key。</summary>
        public string RequirementTextKey { get; set; } = "";
    }
}
