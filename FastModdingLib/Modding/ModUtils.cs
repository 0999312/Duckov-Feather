using Duckov.Modding;

namespace FeatherMod.Modding
{
    /// <summary>
    /// 运行时 Mod 状态查询工具。
    /// 供 modder 在 <c>OnAfterSetup</c> 中实现条件内容注册——
    /// 例如"仅当 OtherMod 已激活时才注册联动内容"。
    /// </summary>
    /// <example>
    /// <code>
    /// protected override void OnAfterSetup()
    /// {
    ///     base.OnAfterSetup();
    ///
    ///     // 通用内容始终注册
    ///     ItemUtils.CreateCustomItem(new Identifier("MyMod", "common_sword"), commonConfig);
    ///
    ///     // 条件联动：仅在 ExpansionMod 激活时注册额外内容
    ///     if (ModUtils.IsModLoaded("ExpansionMod"))
    ///     {
    ///         ItemUtils.CreateCustomItem(new Identifier("MyMod", "expansion_sword"), expansionConfig);
    ///         CraftingUtils.AddCraftingFormula(expansionFormula);
    ///     }
    /// }
    /// </code>
    /// </example>
    public static class ModUtils
    {
        /// <summary>
        /// 检查指定 modid 的模组是否处于激活状态（已启用且完成初始化）。
        /// 等价于 <c>ModManager.modInfos</c> 中存在该名称
        /// 且 <c>ModManager.IsModActive</c> 返回 true。
        /// </summary>
        /// <param name="modid">目标模组的唯一标识符（与 <c>ModInfo.name</c> 一致）。</param>
        /// <returns>true 表示该模组已安装且处于激活状态。</returns>
        public static bool IsModLoaded(string modid)
        {
            var info = FindModInfo(modid);
            return info.HasValue && ModManager.IsModActive(info.Value, out _);
        }

        /// <summary>
        /// 检查指定 modid 的模组是否已安装（不论玩家是否手动启用）。
        /// 仅检查 <c>ModManager.modInfos</c> 中存在该名称，不关心激活状态。
        /// </summary>
        /// <param name="modid">目标模组的唯一标识符（与 <c>ModInfo.name</c> 一致）。</param>
        /// <returns>true 表示该模组已安装（但不一定激活）。</returns>
        public static bool IsModInstalled(string modid)
        {
            return FindModInfo(modid) != null;
        }

        /// <summary>
        /// 按 modid 遍历 <c>ModManager.modInfos</c> 查找对应的 <see cref="ModInfo"/>。
        /// 内部辅助方法，不缓存结果——每次调用实时查询 modInfos 列表。
        /// </summary>
        private static ModInfo? FindModInfo(string modid)
        {
            var infos = ModManager.modInfos;
            if (infos == null) return null;

            for (int i = 0; i < infos.Count; i++)
            {
                if (infos[i].name == modid)
                    return infos[i];
            }

            return null;
        }
    }
}
