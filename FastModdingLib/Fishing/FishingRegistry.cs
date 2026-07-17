using FeatherMod.Register;
using FeatherMod.Utils;
using System.Collections.Generic;

namespace FeatherMod
{
    /// <summary>
    /// 钓鱼注册表。维护 Identifier → FishingPoolConfig 主映射
    /// 和特殊配对（baitID → fishID）列表。
    /// OnRemoved 时清理特殊配对缓存。
    /// </summary>
    public sealed class FishingRegistry : SimpleRegistry<FishingPoolConfig>
    {
        /// <summary>特殊配对：精确 baitID → fishID 映射。</summary>
        private readonly List<SpecialCatchEntry> _specialCatches = new List<SpecialCatchEntry>();

        /// <summary>Identifier → itemTypeId 查找缓存。</summary>
        private readonly Dictionary<Identifier, int> _fishIdCache = new Dictionary<Identifier, int>();

        /// <summary>注册钓鱼池。</summary>
        public void RegisterPool(Identifier id, FishingPoolConfig config, string modid)
        {
            Set(id, config, modid);
        }

        /// <summary>注册特殊配对。</summary>
        public void RegisterSpecialCatch(SpecialCatchEntry entry, string modid)
        {
            _specialCatches.Add(entry);
            // 使用合成 key 保证可被 RemoveAllByOwner 追踪
            var synthKey = new Identifier(entry.BaitId.Domain, $"special_{entry.BaitId.Path}_{entry.FishId.Path}");
            Set(synthKey, default, modid);
        }

        /// <summary>按 Identifier 查询钓鱼池配置。</summary>
        public bool TryGetPool(Identifier id, out FishingPoolConfig config)
        {
            return TryGet(id, out config);
        }

        /// <summary>获取全部特殊配对。</summary>
        public IReadOnlyList<SpecialCatchEntry> GetSpecialCatches()
        {
            return _specialCatches.AsReadOnly();
        }

        /// <summary>缓存并返回 Identifier 对应的 itemTypeId。</summary>
        public void CacheFishId(Identifier id, int typeId)
        {
            _fishIdCache[id] = typeId;
        }

        /// <summary>从缓存查询 FishId。</summary>
        public bool TryGetCachedFishId(Identifier id, out int typeId)
        {
            return _fishIdCache.TryGetValue(id, out typeId);
        }

        protected override void OnRemoved(Identifier id, FishingPoolConfig value, string? modid)
        {
            // 清理特殊配对（检查是否为合成 key）
            if (id.Path.StartsWith("special_"))
            {
                _specialCatches.RemoveAll(e =>
                    id.Path == $"special_{e.BaitId.Path}_{e.FishId.Path}" && id.Domain == e.BaitId.Domain);
            }

            // 清理 fish ID 缓存中属于此 owner 的条目
            // （简化处理：不清除非所有者缓存，下一次使用时若失效由 ItemUtils 兜底）
        }

        /// <summary>内部清理指定特殊配对（供 FishingUtils.UnregisterSpecialCatch 使用）。</summary>
        internal void OnRemovedInternal(Identifier baitId, Identifier fishId)
        {
            _specialCatches.RemoveAll(e => e.BaitId.Equals(baitId) && e.FishId.Equals(fishId));
            _fishIdCache.Remove(fishId);
        }

        public new void Clear()
        {
            _specialCatches.Clear();
            _fishIdCache.Clear();
            base.Clear();
        }
    }

    /// <summary>
    /// 特殊配对：精确的 baitID → fishID 映射，含概率。
    /// </summary>
    public struct SpecialCatchEntry
    {
        public Identifier BaitId;
        public Identifier FishId;
        public float Chance;
    }
}
