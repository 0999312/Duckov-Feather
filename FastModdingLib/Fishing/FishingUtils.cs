using FeatherMod.Events;
using FeatherMod.Register;
using FeatherMod.Utils;
using FmlEvent = FeatherMod.Events.Event;
using ItemStatsSystem;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// 钓鱼系统公共 API。提供钓鱼池注册、特殊配对注册和钓鱼统计属性查询。
    /// 所有 public API 使用 <see cref="Identifier"/> 作为标识符。
    /// </summary>
    /// <example>
    /// <code>
    /// FishingUtils.RegisterFishingPool(
    ///     new Identifier("mymod", "lake"),
    ///     new FishingPoolConfig
    ///     {
    ///         WaterId = new Identifier("mymod", "lake"),
    ///         Entries = new[] {
    ///             new FishingPoolEntry { FishId = new Identifier("mymod", "salmon"), Weight = 0.5f, MinQuality = 2 },
    ///             new FishingPoolEntry { FishId = new Identifier("mymod", "trout"), Weight = 0.3f }
    ///         }
    ///     });
    /// </code>
    /// </example>
    public static class FishingUtils
    {
        private static FishingRegistry _registry;
        private static bool _initialized;

        /// <summary>FishingTime 属性哈希（影响 ring 缩小速度）。</summary>
        private static readonly int _hashFishingTime = "FishingTime".GetHashCode();

        /// <summary>FishingDifficulty 属性哈希（鱼物品上的难度值）。</summary>
        private static readonly int _hashFishingDifficulty = "FishingDifficulty".GetHashCode();

        /// <summary>FishingQualityFactor 属性哈希（影响上钩品质）。</summary>
        private static readonly int _hashFishingQualityFactor = "FishingQualityFactor".GetHashCode();

        public static FishingRegistry Registry => _registry;

        /// <summary>
        /// 初始化钓鱼模块（幂等）。将注册表注册到 <see cref="RegistryManager"/> 元表。
        /// </summary>
        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            _registry = new FishingRegistry();
            RegistryManager.Instance.Registry.SetIfAbsent(
                new Identifier(FMLConstants.Domain, "fishing"),
                _registry,
                RegistryManager.CurrentModid);

            FishSpawnerPatch.EnsurePatched();
        }

        // ===== 钓鱼池注册 =====

        /// <summary>
        /// 注册一个钓鱼池（水域）。定义该水域中可钓到的鱼种和权重。
        /// </summary>
        public static void RegisterFishingPool(Identifier id, FishingPoolConfig config, string? modid = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            Init();
            string owner = modid ?? id.Domain;
            _registry.RegisterPool(id, config, owner);
        }

        /// <summary>
        /// 注册一条特殊配对——指定鱼饵可钓到指定鱼（精确映射）。
        /// </summary>
        /// <param name="baitId">鱼饵物品 Identifier。</param>
        /// <param name="fishId">鱼物品 Identifier。</param>
        /// <param name="chance">概率（0-1）。</param>
        /// <param name="modid">所属 mod 标识。</param>
        public static void RegisterSpecialCatch(Identifier baitId, Identifier fishId, float chance, string? modid = null)
        {
            Init();
            string owner = modid ?? baitId.Domain;
            var entry = new SpecialCatchEntry
            {
                BaitId = baitId,
                FishId = fishId,
                Chance = Mathf.Clamp01(chance)
            };
            _registry.RegisterSpecialCatch(entry, owner);

            // 缓存 fish ID（如果可解析）
            if (ItemUtils.TryResolveTypeId(fishId, out var typeId))
            {
                _registry.CacheFishId(fishId, typeId);
            }
        }

        /// <summary>按 Identifier 移除钓鱼池。</summary>
        public static bool UnregisterFishingPool(Identifier id) => _registry.Remove(id);

        /// <summary>移除特殊配对。</summary>
        public static bool UnregisterSpecialCatch(Identifier baitId, Identifier fishId)
        {
            var entries = _registry.GetSpecialCatches();
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].BaitId.Equals(baitId) && entries[i].FishId.Equals(fishId))
                {
                    _registry.OnRemovedInternal(baitId, fishId);
                    return true;
                }
            }
            return false;
        }

        /// <summary>批量卸载指定 mod 注册的全部钓鱼池和特殊配对。</summary>
        public static int UnregisterAll(string modid) => _registry.RemoveAllByOwner(modid);

        // ===== 查询 =====

        /// <summary>获取全部已注册的钓鱼池配置。</summary>
        public static IReadOnlyList<FishingPoolConfig> GetAllPools()
        {
            var result = new List<FishingPoolConfig>();
            foreach (var kvp in _registry)
            {
                if (kvp.Value != null && !kvp.Key.Path.StartsWith("special_"))
                    result.Add(kvp.Value);
            }
            return result.AsReadOnly();
        }

        /// <summary>获取全部已注册的特殊配对。</summary>
        public static IReadOnlyList<SpecialCatchEntry> GetAllSpecialCatches()
        {
            return _registry.GetSpecialCatches();
        }

        /// <summary>按鱼饵 ID 获取匹配的特殊配对条目。</summary>
        public static IReadOnlyList<SpecialCatchEntry> GetSpecialCatchesForBait(int baitTypeId)
        {
            var result = new List<SpecialCatchEntry>();
            foreach (var entry in _registry.GetSpecialCatches())
            {
                // 解析 bait Identifier → typeId
                if (ResolveFishTypeId(entry.BaitId, out var resolvedTypeId) && resolvedTypeId == baitTypeId)
                {
                    result.Add(entry);
                }
            }
            return result.AsReadOnly();
        }

        /// <summary>从随机池中选择鱼物品。返回选中的 fishTypeId，失败返回 -1。</summary>
        internal static int TrySelectFromPools(int baitTypeId, float luck)
        {
            // 先检查特殊配对
            var specialCatches = GetSpecialCatchesForBait(baitTypeId);
            foreach (var entry in specialCatches)
            {
                if (UnityEngine.Random.Range(0f, 1f) < entry.Chance)
                {
                    if (ResolveFishTypeId(entry.FishId, out var typeId))
                    {
                        EventBusManager.Instance.Sync.Post(new FishCaughtEvent(entry.FishId, null));
                        return typeId;
                    }
                }
            }

            // 遍历所有钓鱼池的 entries 做加权随机
            foreach (var kvp in _registry)
            {
                var pool = kvp.Value;
                if (pool == null || pool.Entries == null || pool.Entries.Length == 0) continue;

                float totalWeight = 0f;
                foreach (var e in pool.Entries) totalWeight += e.Weight;

                if (totalWeight <= 0f) continue;

                float roll = UnityEngine.Random.Range(0f, totalWeight);
                float accumulated = 0f;
                foreach (var e in pool.Entries)
                {
                    accumulated += e.Weight;
                    if (roll <= accumulated)
                    {
                        if (ResolveFishTypeId(e.FishId, out var typeId))
                        {
                            EventBusManager.Instance.Sync.Post(new FishCaughtEvent(e.FishId, null));
                            return typeId;
                        }
                        break;
                    }
                }
            }

            return -1;
        }

        // ===== 钓鱼属性查询 =====

        /// <summary>获取玩家的钓鱼时间属性（影响 ring 缩小速度）。</summary>
        public static float GetFishingTime(CharacterMainControl character)
        {
            if (character == null || character.CharacterItem == null) return 1.0f;
            return character.CharacterItem.GetStatValue(_hashFishingTime);
        }

        /// <summary>获取鱼物品的钓鱼难度属性（影响 ring 缩小速度）。</summary>
        public static float GetFishingDifficulty(Item fish)
        {
            if (fish == null) return 1.0f;
            return fish.GetStatValue(_hashFishingDifficulty);
        }

        /// <summary>获取玩家的钓鱼品质因子属性（影响上钩品质）。</summary>
        public static float GetFishingQualityFactor(CharacterMainControl character)
        {
            if (character == null || character.CharacterItem == null) return 1.0f;
            return character.CharacterItem.GetStatValue(_hashFishingQualityFactor);
        }

        // ===== 内部 =====

        private static bool ResolveFishTypeId(Identifier id, out int typeId)
        {
            // 先从缓存查
            if (_registry.TryGetCachedFishId(id, out typeId))
                return true;

            // 尝试通过 ItemUtils 解析
            if (ItemUtils.TryResolveTypeId(id, out typeId))
            {
                _registry.CacheFishId(id, typeId);
                return true;
            }

            return false;
        }
    }

    /// <summary>鱼被钓起事件。在 FML 钓鱼池命中时触发。</summary>
    public class FishCaughtEvent : FmlEvent
    {
        public Identifier FishId { get; }
        public Item? FishItem { get; }
        public FishCaughtEvent(Identifier fishId, Item? fishItem) { FishId = fishId; FishItem = fishItem; }
    }
}
