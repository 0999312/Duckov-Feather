using Duckov.Economy;
using FeatherMod.Utils;
using System;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// 建筑配置 DTO。modder 用纯 C# 创建此对象，传入
    /// <see cref="BuildingUtils.RegisterBuilding(BuildingConfig, Building?)"/>。
    /// FML 内部负责转换为游戏原生 <see cref="Duckov.Buildings.BuildingInfo"/>。
    /// </summary>
    /// <example>
    /// <code>
    /// BuildingUtils.RegisterBuilding(new BuildingConfig
    /// {
    ///     Id = new Identifier("mymod", "forge"),
    ///     Dimensions = new Vector2Int(3, 3),
    ///     Money = 5000,
    ///     CostItems = new[]
    ///     {
    ///         ItemEntry.Of("duckov:Iron", 20),
    ///         ItemEntry.Of("duckov:Stone", 10),
    ///         ItemEntry.ByTag("Wood", 30)
    ///     },
    ///     PrefabName = "Building_Forge",
    ///     UnlockedByDefault = false
    /// });
    /// </code>
    /// </example>
    public class BuildingConfig
    {
        /// <summary>建筑 Identifier（domain 自动作为 modid）。必填。</summary>
        public Identifier Id { get; set; } = null!;

        /// <summary>占地尺寸（如 (2,2)）。默认 (2, 2)。</summary>
        public Vector2Int Dimensions { get; set; } = new Vector2Int(2, 2);

        /// <summary>建筑 prefab 名称（与游戏 BuildingDataCollection 中 prefab 名称对应）。</summary>
        public string PrefabName { get; set; } = "";

        /// <summary>
        /// 可选：引用游戏已有 Building Prefab 名称（如 "Building_Workbench"），
        /// 克隆其 graphicsContainer 和 functionContainer 结构。
        /// 设置后 <see cref="PrefabName"/> 用于注册标识，实际 prefab 从克隆创建。
        /// </summary>
        public string? ExistingPrefabName { get; set; }

        /// <summary>最大建造数量。默认 1。</summary>
        public int MaxAmount { get; set; } = 1;

        /// <summary>
        /// 建筑图标 Sprite。设置后写入 BuildingInfo.iconReference，
        /// 在 BuilderView 和建筑信息面板中显示。为 null 时使用原版默认图标。
        /// </summary>
        public Sprite? Icon { get; set; }

        // ── 成本 ──

        /// <summary>建造所需金钱。</summary>
        public long Money { get; set; }

        /// <summary>
        /// 建造所需物品列表（从 FML <see cref="ItemEntry"/> 构建，
        /// 自动解析 Identifier → 游戏原生 TypeID）。
        /// </summary>
        public ItemEntry[]? CostItems { get; set; }

        // ── 解锁 ──

        /// <summary>是否默认解锁（无需任务条件）。默认 true。</summary>
        public bool UnlockedByDefault { get; set; } = true;

        /// <summary>
        /// 前置建筑列表（Identifier 优先）。需先建造才能解锁。
        /// 使用 Identifier("duckov", "Workbench") 引用原版建筑，
        /// 使用 Identifier("mymod", "forge") 引用自定义建筑。
        /// </summary>
        public Identifier[]? RequireBuildings { get; set; }

        /// <summary>
        /// 前置任务列表（Identifier 优先）。需完成任务才能解锁。
        /// 使用 Identifier("duckov", "QuestName") 引用原版任务（自动反查 ID），
        /// 使用 Identifier("mymod", "quest_xxx") 引用 FML 注册的任务。
        /// </summary>
        public Identifier[]? RequireQuests { get; set; }

        // ── 工厂 ──

        /// <summary>快速创建（仅 Id）。其余字段用对象初始化器设置。</summary>
        public static BuildingConfig Create(Identifier id) => new BuildingConfig { Id = id };

        /// <summary>快速创建（从 "domain:path" 字符串）。</summary>
        public static BuildingConfig Create(string idString) => Create(Identifier.Parse(idString));

        // ── 内部辅助 ──

        /// <summary>
        /// 将 <see cref="CostItems"/>（FML ItemEntry）转换为游戏原生 <see cref="Cost"/> struct。
        /// 自动调用 <see cref="ItemEntry.ResolveTypeId"/> 解析 Identifier。
        /// </summary>
        internal Cost BuildCost()
        {
            var items = CostItems ?? Array.Empty<ItemEntry>();
            var nativeItems = new Cost.ItemEntry[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                nativeItems[i] = new Cost.ItemEntry
                {
                    // 防御：ResolveTypeId 不会抛异常（有 fallback），
                    // 但无效 Identifier 会导致 id=0，下游 Cost.Enough/Pay 可能出错
                    id = items[i].ResolveTypeId(),
                    amount = items[i].Amount
                };
            }
            return new Cost
            {
                money = Money,
                items = nativeItems
            };
        }
    }
}
