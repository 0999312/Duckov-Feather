using FeatherMod.Utils;
using System;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// 建筑 UI 配置。声明式定义建筑的 DetailsView 自定义布局。
    /// 一个建筑可包含多个 Machine，每个 Machine 有独立的子库存和 Recipe。
    /// </summary>
    public class BuildingUIConfig
    {
        /// <summary>主库存面板标题。null = 使用 Building 注册名。</summary>
        public string? DisplayName;

        /// <summary>
        /// 建筑上的机器列表。每个 Machine 是独立的处理单元，
        /// 拥有自己的子库存、Recipe、进度条和按钮。
        /// 多个 Machine 在同一建筑上并行运行，互不干扰。
        /// </summary>
        public MachineDef[]? Machines;
    }

    /// <summary>
    /// 机器定义。描述建筑上一个独立的生产/处理单元。
    /// 每个 Machine 有独立的 UI 区域（在 DetailsView 中从上到下排列）和独立的 Recipe 逻辑。
    /// </summary>
    public class MachineDef
    {
        /// <summary>机器标识（在同一建筑内唯一，用于存档 key）。</summary>
        public string MachineKey = "";

        /// <summary>UI 中显示的机器名称（本地化 key）。需通过 I18n 或 LocalizationManager.SetOverrideText() 注册对应翻译。
        /// 空串时使用游戏默认交互文本（"UI_Interact" → "交互"）。</summary>
        public string DisplayName = "";

        /// <summary>是否默认解锁（无需 Perk 即可使用）。默认 true。</summary>
        public bool UnlockedByDefault = true;

        /// <summary>需要解锁的 Perk。仅在 UnlockedByDefault=false 时生效。</summary>
        public Identifier? RequiredPerk;

        /// <summary>本机器的子库存定义。</summary>
        public SubInventoryDef[]? SubInventories;

        /// <summary>本机器的 Recipe（MachineRecipe 子类实例，可为 null 表示无自动生产）。</summary>
        public MachineRecipe? Recipe;

        /// <summary>本机器的进度条。</summary>
        public ProgressBarDef[]? ProgressBars;

        /// <summary>本机器的自定义按钮。</summary>
        public BuildingButtonDef[]? Buttons;
    }

    /// <summary>
    /// 子库存定义：描述一个独立的物品容器及其 UI 显示。
    /// </summary>
    public class SubInventoryDef
    {
        /// <summary>子库存标识（在同一 Machine 内唯一）。</summary>
        public string SubKey = "";

        /// <summary>UI 中显示的标题。</summary>
        public string DisplayName = "";

        /// <summary>槽位数量。</summary>
        public int SlotCount = 4;

        /// <summary>每个槽位的标签过滤（可选）。null = 无过滤。</summary>
        public string[]? SlotTags;

        /// <summary>是否只读（禁止玩家放入/取出）。默认 false。</summary>
        public bool ReadOnly;
    }

    /// <summary>
    /// 进度条定义。
    /// </summary>
    public class ProgressBarDef
    {
        /// <summary>进度条标签。</summary>
        public string Label = "";

        /// <summary>获取进度的回调（0~1）。FML 每帧轮询更新进度条显示。</summary>
        public Func<float> GetProgress = () => 0f;
    }

    /// <summary>
    /// 建筑 UI 自定义按钮。
    /// </summary>
    public class BuildingButtonDef
    {
        /// <summary>按钮文字。</summary>
        public string Label = "";

        /// <summary>点击回调。参数为建筑主 Inventory。</summary>
        public Action<ItemStatsSystem.Inventory>? OnClick;
    }
}
