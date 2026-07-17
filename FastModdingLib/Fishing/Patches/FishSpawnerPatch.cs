using FeatherMod.Utils;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// Harmony 补丁：在 FishSpawner.Awake 时将 FML 注册的特殊配对注入到
    /// 游戏原生的 specialPairs 列表中，使 FML 注册的鱼种可被原生钓鱼系统识别。
    /// 不拦截 Spawn 方法（因为其返回 async UniTask，Harmony Prefix 无法安全替换异步返回值）。
    /// </summary>
    internal static class FishSpawnerPatch
    {
        private static bool _patched;

        /// <summary>应用补丁（幂等）。</summary>
        internal static void EnsurePatched()
        {
            if (_patched) return;
            _patched = true;

            try
            {
                var harmony = new Harmony("Feather.Fishing");
                FeatherMod.ModBehaviour.ExtraHarmonies.Add(harmony);
                // Hook Awake via reflection since specialPairs is a private serialized field
                var awakeMethod = typeof(Duckov.Utilities.FishSpawner).GetMethod("Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (awakeMethod != null)
                {
                    harmony.Patch(
                        original: awakeMethod,
                        postfix: new HarmonyMethod(typeof(FishSpawnerPatch), nameof(AwakePostfix)));
                }
                else
                {
                    Debug.LogWarning("[FML Fishing] FishSpawner.Awake not found — special catch injection disabled.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FML Fishing] Failed to apply FishSpawner patches: {e}");
                _patched = false;
            }
        }

        /// <summary>
        /// FishSpawner.Awake 后注入 FML 注册的特殊配对。
        /// 通过反射访问 private List<SpecialPair> specialPairs 字段（Publicizer 已公开）。
        /// </summary>
        private static void AwakePostfix(Duckov.Utilities.FishSpawner __instance)
        {
            try
            {
                // 获取原生 specialPairs 字段
                var specialPairsField = typeof(Duckov.Utilities.FishSpawner).GetField("specialPairs",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (specialPairsField == null) return;

                // 获取原生列表
                var specialPairs = specialPairsField.GetValue(__instance) as System.Collections.IList;
                if (specialPairs == null) return;

                // 注入 FML 注册的特殊配对
                foreach (var entry in FishingUtils.GetAllSpecialCatches())
                {
                    if (ResolveBaitId(entry.BaitId, out var baitTypeId) &&
                        ResolveFishId(entry.FishId, out var fishTypeId))
                    {
                        // 构造游戏原生的 SpecialPair (private struct)
                        // 由于是 private struct，需要用反射创建
                        var specialPairType = typeof(Duckov.Utilities.FishSpawner).GetNestedType("SpecialPair",
                            BindingFlags.NonPublic);
                        if (specialPairType == null) continue;

                        var pair = Activator.CreateInstance(specialPairType);
                        specialPairType.GetField("baitID")?.SetValue(pair, baitTypeId);
                        specialPairType.GetField("fishID")?.SetValue(pair, fishTypeId);
                        specialPairType.GetField("chance")?.SetValue(pair, entry.Chance);

                        specialPairs.Add(pair);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FML Fishing] AwakePostfix failed: {e}");
            }
        }

        private static bool ResolveBaitId(Identifier id, out int typeId)
        {
            return ItemUtils.TryResolveTypeId(id, out typeId);
        }

        private static bool ResolveFishId(Identifier id, out int typeId)
        {
            return ItemUtils.TryResolveTypeId(id, out typeId);
        }
    }
}
