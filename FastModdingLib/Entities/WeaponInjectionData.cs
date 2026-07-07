using System.Collections.Generic;
using Duckov.Utilities;

namespace FeatherMod.Entities
{
    /// <summary>
    /// 记录单条武器注入规则及其备份数据，用于卸载时恢复到原始状态。
    /// </summary>
    public class WeaponInjectionData
    {
        /// <summary>注入的武器引用（支持 Identifier 或 typeID）。</summary>
        public ItemEntry Weapon;

        /// <summary>注入概率 (0-1)，作为 Pool 权重。</summary>
        public float Chance;

        /// <summary>按预设名匹配（支持前缀通配 "Cname_Scav*"）；null 表示按 Team 匹配。</summary>
        public string? PresetNamePattern;

        /// <summary>按阵营匹配；null 表示按 nameKey 匹配。</summary>
        public Teams? Team;

        /// <summary>所有被修改的 Pool 备份，卸载时用于恢复。</summary>
        public List<PoolBackup> Backups = new List<PoolBackup>();

        /// <summary>Pool 中单个 Entry 的备份（itemTypeID + weight）。</summary>
        public struct PoolEntrySnapshot
        {
            public int ItemTypeID;
            public float Weight;
        }

        /// <summary>
        /// 单条 Pool 修改的备份记录。
        /// </summary>
        public struct PoolBackup
        {
            /// <summary>被修改的 preset 引用。</summary>
            public CharacterRandomPreset Preset;

            /// <summary>itemsToGenerate 中的索引。</summary>
            public int DescriptionIndex;

            /// <summary>itemPool.entries 的原始副本（itemTypeID + weight）。</summary>
            public PoolEntrySnapshot[] OriginalEntries;

            /// <summary>原始 chance 值。</summary>
            public float OriginalChance;
        }
    }
}
