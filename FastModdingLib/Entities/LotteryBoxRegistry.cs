using FastModdingLib.Register;
using FastModdingLib.Utils;

namespace FastModdingLib.Entities
{
    /// <summary>
    /// LotteryBox 注入注册表。维护 Identifier → LotteryBoxData 映射，
    /// 继承 SimpleRegistry 获得 owner 追踪和 RemoveAllByOwner。
    /// OnRemoved 时自动恢复所有被修改的 LotteryBox candidates 数据。
    /// </summary>
    public sealed class LotteryBoxRegistry : SimpleRegistry<LotteryBoxData>, ERegistry
    {
        /// <summary>
        /// 删除 entry 时恢复所有被修改的 candidates。
        /// </summary>
        protected override void OnRemoved(Identifier id, LotteryBoxData data, string? modid)
        {
            foreach (var backup in data.Backups)
            {
                LotteryBoxPatch.RestoreCandidates(backup);
            }
        }

        /// <summary>ERegistry 接口 — 供 RegistryManager 批量卸载。</summary>
        int ERegistry.RemoveAllByOwner(string modid)
        {
            return RemoveAllByOwner(modid);
        }
    }
}
