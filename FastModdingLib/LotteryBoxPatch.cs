using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FastModdingLib.Entities;
using FastModdingLib.Utils;
using HarmonyLib;
using UnityEngine;

namespace FastModdingLib
{
    /// <summary>
    /// Harmony Patch：在 InteractableBase.Awake() 时自动注入 FML 注册的物品到 LotteryBox.candidates 池。
    /// 封装全部对 private ItemTypeID 嵌套类的反射访问，对外零反射。
    /// </summary>
    [HarmonyPatch(typeof(InteractableBase), "Awake")]
    public static class LotteryBoxPatch
    {
        // ═══════════════════════════════════════════════════
        //  诊断：类加载 & Harmony 准备
        // ═══════════════════════════════════════════════════

        static LotteryBoxPatch()
        {
        }

        [HarmonyPrepare]
        public static bool Prepare()
        {
            return true;
        }

        // ═══════════════════════════════════════════════════
        //  静态反射缓存
        // ═══════════════════════════════════════════════════

        private static readonly Type ItemTypeIdType =
            typeof(Duckov.LotteryBox).GetNestedType("ItemTypeID", BindingFlags.NonPublic);

        private static readonly FieldInfo ItemTypeId_IdField = ItemTypeIdType?.GetField("id");

        private static readonly FieldInfo CandidatesField =
            typeof(Duckov.LotteryBox).GetField("candidates", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>按 containerType 缓存 entries FieldInfo。</summary>
        private static readonly Dictionary<Type, FieldInfo> EntriesFieldCache = new Dictionary<Type, FieldInfo>();

        /// <summary>按 containerType 缓存 AddEntry MethodInfo。</summary>
        private static readonly Dictionary<Type, MethodInfo> AddEntryMethodCache = new Dictionary<Type, MethodInfo>();

        /// <summary>cache 锁对象。</summary>
        private static readonly object CacheLock = new object();


        /// <summary>日志开关：reflection 失败时输出一次。</summary>
        private static bool _reflectionInitLogged;

        // ═══════════════════════════════════════════════════
        //  Harmony Patch: Awake Postfix
        //  地图加载时自动触发，无需玩家交互
        // ═══════════════════════════════════════════════════

        [HarmonyPostfix]
        public static void Awake_Postfix(InteractableBase __instance)
        {
            // 仅处理 LotteryBox 实例
            if (!(__instance is Duckov.LotteryBox lotteryBox)) return;

            var registry = LotteryBoxUtils.Registry;
            if (registry == null) return;

            foreach (var kvp in registry)
            {
                var rule = kvp.Value;
                if (rule == null || string.IsNullOrEmpty(rule.SceneNamePattern)) continue;

                // 已注入过此实例则跳过
                if (rule.Backups.Any(b => b.Box == lotteryBox)) continue;

                // 名称匹配
                if (!WildcardHelper.Match(rule.SceneNamePattern, lotteryBox.gameObject.name)) continue;

                // 执行注入
                InjectIntoBox(lotteryBox, rule);
            }
        }

        // ═══════════════════════════════════════════════════
        //  核心：反射注入（封装了 ItemTypeID 私有类型访问）
        // ═══════════════════════════════════════════════════

        private static void InjectIntoBox(Duckov.LotteryBox box, LotteryBoxData rule)
        {
            if (!EnsureReflectionReady()) return;

            var container = CandidatesField.GetValue(box);
            if (container == null)
            {
                Debug.LogWarning($"[FML] LotteryBox: candidates field is null on '{box.gameObject.name}'. Skipped.");
                return;
            }

            var containerType = container.GetType();
            var entriesField = GetOrCacheField(EntriesFieldCache, containerType, "entries");
            var addEntryMethod = GetOrCacheMethod(AddEntryMethodCache, containerType, "AddEntry");
            var entries = (IList)entriesField.GetValue(container);
            if (entries == null) return;

            int weaponTypeId = rule.Item.ResolveTypeId();
            WeaponClassifier.Kind injectKind = WeaponClassifier.Classify(weaponTypeId);

            if (injectKind == WeaponClassifier.Kind.None)
            {
                Debug.LogWarning($"[FML] LotteryBox: item typeID={weaponTypeId} is neither Gun nor Melee. Box '{box.gameObject.name}' skipped.");
                return;
            }

            WeaponClassifier.Kind boxKind = ClassifyBox(container, entriesField);
            if (boxKind == WeaponClassifier.Kind.None)
            {
                Debug.LogWarning($"[FML] LotteryBox: box '{box.gameObject.name}' contains no recognizable weapon items. Skipped.");
                return;
            }

            // 严格隔离：枪↔枪箱，刀↔刀箱
            if (injectKind != boxKind)
            {
                Debug.LogWarning($"[FML] LotteryBox: type mismatch — injecting {(injectKind == WeaponClassifier.Kind.Gun ? "Gun" : "Melee")} into {(boxKind == WeaponClassifier.Kind.Gun ? "Gun" : "Melee")} box '{box.gameObject.name}'. Skipped.");
                return;
            }

            // ── 备份现有 entries ──
            var snapshots = new LotteryBoxData.CandidateSnapshot[entries.Count];
            float originalTotalWeight = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var entryType = entry.GetType();
                var valueField = entryType.GetField("value");
                var weightField = entryType.GetField("weight");
                var value = valueField.GetValue(entry);
                var id = (int)ItemTypeId_IdField.GetValue(value);
                var w = (float)weightField.GetValue(entry);
                snapshots[i] = new LotteryBoxData.CandidateSnapshot { ItemTypeID = id, Weight = w };
                originalTotalWeight += w;
            }

            // ── 计算注入权重 = multiplier × 原生条目平均权重 ──
            float avgWeight = entries.Count > 0 ? originalTotalWeight / entries.Count : 1f;
            float injectWeight = Mathf.Max(0f, rule.Weight) * avgWeight;

            // ── 创建 ItemTypeID 实例并注入（只追加，不缩放原生） ──
            var newItemTypeId = Activator.CreateInstance(ItemTypeIdType);
            ItemTypeId_IdField.SetValue(newItemTypeId, weaponTypeId);
            addEntryMethod.Invoke(container, new[] { newItemTypeId, injectWeight });

            // ── 记录备份 ──
            rule.Backups.Add(new LotteryBoxData.CandidateBackup
            {
                Box = box,
                OriginalEntries = snapshots
            });

            Debug.Log($"[FML] LotteryBox: injected typeID={weaponTypeId} (multiplier={rule.Weight:F2}, weight={injectWeight:F2}, avg={avgWeight:F2}) into '{box.gameObject.name}' (kind={boxKind})");
        }

        // ═══════════════════════════════════════════════════
        //  恢复逻辑（由 LotteryBoxRegistry.OnRemoved 调用）
        // ═══════════════════════════════════════════════════

        internal static void RestoreCandidates(LotteryBoxData.CandidateBackup backup)
        {
            if (backup.Box == null) return;
            if (!EnsureReflectionReady()) return;

            var container = CandidatesField.GetValue(backup.Box);
            if (container == null) return;

            var containerType = container.GetType();
            var entriesField = GetOrCacheField(EntriesFieldCache, containerType, "entries");
            var addEntryMethod = GetOrCacheMethod(AddEntryMethodCache, containerType, "AddEntry");
            var entries = (IList)entriesField.GetValue(container);
            if (entries == null) return;

            entries.Clear();
            foreach (var snap in backup.OriginalEntries)
            {
                var newItemTypeId = Activator.CreateInstance(ItemTypeIdType);
                ItemTypeId_IdField.SetValue(newItemTypeId, snap.ItemTypeID);
                addEntryMethod.Invoke(container, new[] { newItemTypeId, snap.Weight });
            }
        }

        // ═══════════════════════════════════════════════════
        //  武器分类（委托给共享 WeaponClassifier）
        // ═══════════════════════════════════════════════════

        /// <summary>遍历 candidates.entries 判断 LotteryBox 的武器类型。</summary>
        private static WeaponClassifier.Kind ClassifyBox(object container, FieldInfo entriesField)
        {
            if (ItemTypeId_IdField == null) return WeaponClassifier.Kind.None;

            var entries = (IList)entriesField.GetValue(container);
            if (entries == null || entries.Count == 0) return WeaponClassifier.Kind.None;

            WeaponClassifier.Kind result = WeaponClassifier.Kind.None;
            foreach (var entry in entries)
            {
                var entryType = entry.GetType();
                var valueField = entryType.GetField("value");
                var value = valueField.GetValue(entry);
                var id = (int)ItemTypeId_IdField.GetValue(value);

                var kind = WeaponClassifier.Classify(id);
                if (kind == WeaponClassifier.Kind.Gun) return WeaponClassifier.Kind.Gun;
                if (kind == WeaponClassifier.Kind.Melee) result = WeaponClassifier.Kind.Melee;
            }
            return result;
        }

        // ═══════════════════════════════════════════════════
        //  反射辅助
        // ═══════════════════════════════════════════════════

        private static bool EnsureReflectionReady()
        {
            if (ItemTypeIdType != null && ItemTypeId_IdField != null && CandidatesField != null)
                return true;

            if (!_reflectionInitLogged)
            {
                _reflectionInitLogged = true;
                Debug.LogError("[FML] LotteryBox: reflection initialization failed. " +
                    $"ItemTypeIdType={(ItemTypeIdType != null ? "OK" : "NULL")}, " +
                    $"IdField={(ItemTypeId_IdField != null ? "OK" : "NULL")}, " +
                    $"CandidatesField={(CandidatesField != null ? "OK" : "NULL")}");
            }
            return false;
        }

        private static FieldInfo GetOrCacheField(Dictionary<Type, FieldInfo> cache, Type containerType, string fieldName)
        {
            lock (CacheLock)
            {
                if (!cache.TryGetValue(containerType, out var field))
                {
                    field = containerType.GetField(fieldName);
                    cache[containerType] = field;
                }
                return field;
            }
        }

        private static MethodInfo GetOrCacheMethod(Dictionary<Type, MethodInfo> cache, Type containerType, string methodName)
        {
            lock (CacheLock)
            {
                if (!cache.TryGetValue(containerType, out var method))
                {
                    method = containerType.GetMethod(methodName);
                    cache[containerType] = method;
                }
                return method;
            }
        }
    }
}
