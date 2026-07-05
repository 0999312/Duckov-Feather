using System;
using System.Collections.Generic;
using System.Reflection;
using Duckov.Utilities;
using FastModdingLib.Entities;
using FastModdingLib.Register;
using FastModdingLib.Utils;
using UnityEngine;

namespace FastModdingLib
{
    /// <summary>
    /// NPC 武器注入 API。
    /// 零 Harmony Hook：直接修改 ScriptableObject 的 itemsToGenerate 数据。
    /// </summary>
    public static class WeaponInjectionUtils
    {
        internal static readonly WeaponInjectionRegistry Registry = new WeaponInjectionRegistry();
        private static bool _initialized;

        // ── 反射缓存 ──
        private static FieldInfo? _itemsToGenerateField;

        /// <summary>
        /// 暴露给 RegisterBootstrap 用于注册到元表和查询。
        /// </summary>
        public static WeaponInjectionRegistry WeaponRegistry => Registry;

        /// <summary>
        /// 初始化：将 WeaponInjectionRegistry 注册到 RegistryManager 元表。
        /// 由 RegisterBootstrap.Init() 调用（幂等）。
        /// </summary>
        internal static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            var meta = RegistryManager.Instance.Registry;
            var id = new Identifier(FMLConstants.Domain, "weapon_injection");
            if (meta is NonAlterableSimpleRegistry<ERegistry> nonAlt)
                nonAlt.SetIfAbsent(id, Registry, RegistryManager.CurrentModid);
            else
                meta.Set(id, Registry, RegistryManager.CurrentModid);
        }

        // ═══════════════════════════════════════════════════
        //  公开 API — 注册
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 替换指定 NPC 预设的手持武器。不追加，直接替换 PrimWeaponSlot。
        /// </summary>
        /// <param name="presetNameKey">目标预设 nameKey，支持前缀通配（"Cname_Scav*"）。</param>
        /// <param name="weapon">武器引用（Identifier 或 typeID）。</param>
        /// <param name="chance">概率（0-1），默认 0.3。</param>
        public static void AddWeaponToPreset(string presetNameKey, ItemEntry weapon, float chance = 0.3f)
        {
            Init();
            if (string.IsNullOrEmpty(presetNameKey))
                throw new ArgumentNullException(nameof(presetNameKey));

            var data = new WeaponInjectionData
            {
                Weapon = weapon,
                Chance = Mathf.Clamp01(chance),
                PresetNamePattern = presetNameKey,
                Team = null
            };

            InjectToMatchingPresets(data, preset => WildcardHelper.Match(presetNameKey, preset.nameKey));

            var modid = RegistryManager.CurrentModid;
            var id = new Identifier(modid, Guid.NewGuid().ToString());
            Registry.Set(id, data, modid);
            Debug.Log($"[FML] WeaponInjection: preset='{presetNameKey}' weapon={weapon.ResolveTypeId()} chance={chance:F2} mod={modid}");
        }

        /// <summary>
        /// 替换指定阵营所有 NPC 的手持武器。
        /// </summary>
        /// <param name="team">目标阵营。</param>
        /// <param name="weapon">武器引用（Identifier 或 typeID）。</param>
        /// <param name="chance">概率（0-1），默认 0.3。</param>
        public static void AddWeaponToTeam(Teams team, ItemEntry weapon, float chance = 0.3f)
        {
            Init();

            var data = new WeaponInjectionData
            {
                Weapon = weapon,
                Chance = Mathf.Clamp01(chance),
                PresetNamePattern = null,
                Team = team
            };

            InjectToMatchingPresets(data, preset => preset.team == team);

            var modid = RegistryManager.CurrentModid;
            var id = new Identifier(modid, Guid.NewGuid().ToString());
            Registry.Set(id, data, modid);
            Debug.Log($"[FML] WeaponInjection: team={team} weapon={weapon.ResolveTypeId()} chance={chance:F2} mod={modid}");
        }

        // ═══════════════════════════════════════════════════
        //  公开 API — 卸载
        // ═══════════════════════════════════════════════════

        /// <summary>移除指定预设名的武器注入规则。</summary>
        public static bool RemoveWeaponFromPreset(string presetNamePattern, ItemEntry weapon)
        {
            return RemoveMatching(data =>
                data.PresetNamePattern == presetNamePattern &&
                data.Weapon.ResolveTypeId() == weapon.ResolveTypeId());
        }

        /// <summary>移除指定阵营的武器注入规则。</summary>
        public static bool RemoveWeaponFromTeam(Teams team, ItemEntry weapon)
        {
            return RemoveMatching(data =>
                data.Team == team &&
                data.Weapon.ResolveTypeId() == weapon.ResolveTypeId());
        }

        /// <summary>批量卸载指定 mod 注册的全部武器注入。</summary>
        public static int UnregisterAllWeaponInjections(string modid)
        {
            return Registry.RemoveAllByOwner(modid);
        }

        // ═══════════════════════════════════════════════════
        //  内部 — 核心注入逻辑
        // ═══════════════════════════════════════════════════

        private static void InjectToMatchingPresets(WeaponInjectionData data, Func<CharacterRandomPreset, bool> predicate)
        {
            var presets = GameplayDataSettings.CharacterRandomPresetData?.presets;
            if (presets == null || presets.Count == 0)
            {
                Debug.LogWarning("[FML] WeaponInjection: CharacterRandomPresetData.presets is null or empty. Injection deferred?");
                return;
            }

            foreach (var preset in presets)
            {
                if (preset == null || !predicate(preset))
                    continue;

                InjectToPreset(preset, data);
            }
        }

        private static void InjectToPreset(CharacterRandomPreset preset, WeaponInjectionData data)
        {
            var itemsToGenerate = GetItemsToGenerate(preset);
            if (itemsToGenerate == null) return;

            int weaponTypeId = data.Weapon.ResolveTypeId();
            WeaponClassifier.Kind injectKind = WeaponClassifier.Classify(weaponTypeId);

            // 收集武器条目索引
            var gunIndices = new List<int>();
            var meleeIndices = new List<int>();

            for (int i = 0; i < itemsToGenerate.Count; i++)
            {
                var desc = itemsToGenerate[i];
                var kind = ClassifyDescription(desc);
                if (kind == WeaponClassifier.Kind.Gun) gunIndices.Add(i);
                else if (kind == WeaponClassifier.Kind.Melee) meleeIndices.Add(i);
            }

            // 按枪刀类型严格匹配（不兼容，不 fallback）
            int targetIdx = -1;
            if (injectKind == WeaponClassifier.Kind.Gun && gunIndices.Count > 0)
                targetIdx = gunIndices[0];
            else if (injectKind == WeaponClassifier.Kind.Melee && meleeIndices.Count > 0)
                targetIdx = meleeIndices[0];

            if (targetIdx < 0)
            {
                // 无兼容武器条目：跳过此 preset
                Debug.LogWarning($"[FML] WeaponInjection: preset '{preset.nameKey}' has no compatible weapon entry for {(injectKind == WeaponClassifier.Kind.Gun ? "Gun" : "Melee")}. Skipped.");
                return;
            }

            if (targetIdx >= 0)
            {
                // 修改已有条目
                var desc = itemsToGenerate[targetIdx];
                var pool = desc.itemPool;

                // 备份：转换为 PoolEntrySnapshot 避免嵌套泛型类型混淆
                var snapshots = new WeaponInjectionData.PoolEntrySnapshot[pool.entries.Count];
                for (int j = 0; j < pool.entries.Count; j++)
                {
                    snapshots[j] = new WeaponInjectionData.PoolEntrySnapshot
                    {
                        ItemTypeID = pool.entries[j].value.itemTypeID,
                        Weight = pool.entries[j].weight
                    };
                }

                var backup = new WeaponInjectionData.PoolBackup
                {
                    Preset = preset,
                    DescriptionIndex = targetIdx,
                    OriginalEntries = snapshots,
                    OriginalChance = desc.chance
                };

                // 注入权重先占位，原生权重等比缩放到剩余空间
                float injectWeight = Mathf.Clamp01(data.Chance);
                float originalTotalWeight = 0f;
                foreach (var entry in pool.entries)
                {
                    originalTotalWeight += entry.weight;
                }

                float remainingRatio = 1f - injectWeight;
                if (originalTotalWeight > 0f)
                {
                    for (int j = 0; j < pool.entries.Count; j++)
                    {
                        var entry = pool.entries[j];
                        entry.weight *= remainingRatio;
                        pool.entries[j] = entry;
                    }
                }

                // 添加注入武器 Entry
                pool.AddEntry(new RandomItemGenerateDescription.Entry { itemTypeID = weaponTypeId }, injectWeight);
                pool.RefreshPercent();

                itemsToGenerate[targetIdx] = desc;
                data.Backups.Add(backup);
            }
        }

        /// <summary>
        /// 从 PoolBackup 恢复原始数据。public 供 WeaponInjectionRegistry.OnRemoved 调用。
        /// </summary>
        internal static void RestorePool(WeaponInjectionData.PoolBackup backup)
        {
            if (backup.Preset == null) return;

            var itemsToGenerate = GetItemsToGenerate(backup.Preset);
            if (itemsToGenerate == null) return;

            if (backup.DescriptionIndex >= itemsToGenerate.Count) return;

            var desc = itemsToGenerate[backup.DescriptionIndex];
            var pool = desc.itemPool;

            // 恢复 entries：从 PoolEntrySnapshot 重建
            pool.entries.Clear();
            foreach (var snap in backup.OriginalEntries)
            {
                pool.AddEntry(new RandomItemGenerateDescription.Entry { itemTypeID = snap.ItemTypeID }, snap.Weight);
            }
            pool.RefreshPercent();

            // 恢复 chance
            desc.chance = backup.OriginalChance;
            itemsToGenerate[backup.DescriptionIndex] = desc;
        }

        // ═══════════════════════════════════════════════════
        //  内部 — 武器识别
        // ═══════════════════════════════════════════════════

        /// <summary>判断 RandomItemGenerateDescription 是否为武器条目及其类型。</summary>
        private static WeaponClassifier.Kind ClassifyDescription(RandomItemGenerateDescription desc)
        {
            if (desc.randomFromPool)
            {
                foreach (var containerEntry in desc.itemPool.entries)
                {
                    var kind = WeaponClassifier.Classify(containerEntry.value.itemTypeID);
                    if (kind != WeaponClassifier.Kind.None)
                        return kind;
                }
            }
            else
            {
                foreach (var tagEntry in desc.tags.entries)
                {
                    if (tagEntry.value == null) continue;
                    var tagName = tagEntry.value.name;
                    if (tagName == "Gun") return WeaponClassifier.Kind.Gun;
                    if (tagName == "MeleeWeapon") return WeaponClassifier.Kind.Melee;
                }
            }
            return WeaponClassifier.Kind.None;
        }

        // ═══════════════════════════════════════════════════
        //  内部 — 辅助
        // ═══════════════════════════════════════════════════

        /// <summary>通过反射获取 CharacterRandomPreset 的私有 itemsToGenerate 列表。</summary>
        private static List<RandomItemGenerateDescription>? GetItemsToGenerate(CharacterRandomPreset preset)
        {
            if (_itemsToGenerateField == null)
            {
                _itemsToGenerateField = typeof(CharacterRandomPreset).GetField("itemsToGenerate",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (_itemsToGenerateField == null)
                {
                    Debug.LogError("[FML] WeaponInjection: Cannot find 'itemsToGenerate' field on CharacterRandomPreset via reflection.");
                    return null;
                }
            }

            return _itemsToGenerateField.GetValue(preset) as List<RandomItemGenerateDescription>;
        }

        /// <summary>查找并移除匹配的注入规则。</summary>
        private static bool RemoveMatching(Func<WeaponInjectionData, bool> predicate)
        {
            foreach (var kvp in Registry)
            {
                if (predicate(kvp.Value))
                {
                    return Registry.Remove(kvp.Key);
                }
            }
            return false;
        }
    }
}
