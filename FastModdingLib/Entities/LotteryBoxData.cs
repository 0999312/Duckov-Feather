using System.Collections.Generic;

namespace FeatherMod.Entities
{
    /// <summary>
    /// 记录单条 LotteryBox 注入规则及其备份数据，用于卸载时恢复原始 candidates。
    /// </summary>
    public class LotteryBoxData
    {
        /// <summary>注入的物品引用（支持 Identifier 或 typeID）。</summary>
        public ItemEntry Item;

        /// <summary>在 candidates 池中的权重。</summary>
        public float Weight;

        /// <summary>目标 LotteryBox GameObject 名称模式（支持前缀通配 "*"）。</summary>
        public string? SceneNamePattern;

        /// <summary>所有被修改的 LotteryBox candidates 备份，卸载时用于恢复。</summary>
        public List<CandidateBackup> Backups = new List<CandidateBackup>();

        /// <summary>candidates 中单个 Entry 的备份（itemTypeID + weight）。</summary>
        public struct CandidateSnapshot
        {
            public int ItemTypeID;
            public float Weight;
        }

        /// <summary>
        /// 单次 candidates 池修改的备份记录。
        /// </summary>
        public struct CandidateBackup
        {
            /// <summary>被修改的 LotteryBox 实例引用。</summary>
            public Duckov.LotteryBox Box;

            /// <summary>candidates.entries 的原始副本（itemTypeID + weight）。</summary>
            public CandidateSnapshot[] OriginalEntries;
        }
    }
}
