using Cysharp.Threading.Tasks;
using FeatherMod.Entities;
using FeatherMod.Register;
using FeatherMod.Utils;
using HarmonyLib;
using System;
using System.Linq;
using UnityEngine;

namespace FeatherMod.Entities.Patches
{
    /// <summary>
    /// Phase 3 EnemyUtils 剩余 Harmony patch 点（PLAN-EnemyUtils §4.1 表）。
    ///
    /// ⚠ 类级 [HarmonyPatch] 必须存在：Harmony 2.4.x 的 PatchAll 只处理带类级
    /// Harmony 特性的类型，方法级 [HarmonyPatch] 不会被扫描（历史版本此文件
    /// 缺少类级特性导致全部补丁静默失效）。
    ///
    /// 每个 patch 独立 try/catch 包裹，失败时仅 log 错误不崩溃游戏。
    ///
    /// Patch #6 (AICharacterController.Init) 和 transpiler (GraphSerializationFix)
    /// 在各自独立文件中，不在此处。
    /// </summary>
    [HarmonyPatch]
    public static class OtherPatches
    {
        // ======================================================================
        // Patch #4: RandomCharacterSpawner.GetAPresetByWeight Postfix
        // 当游戏原生权重选择无法返回 preset 时，从 FML EnemyRegistry 中
        // 按权重回调候选 preset。
        // 原生签名：private CharacterRandomPresetInfo GetAPresetByWeight()
        // （struct 返回——不能用 __result != null 判断，用 randomPreset 字段）
        // ======================================================================
        [HarmonyPatch(typeof(RandomCharacterSpawner), "GetAPresetByWeight")]
        [HarmonyPostfix]
        public static void GetAPresetByWeightPostfix(RandomCharacterSpawner __instance, ref CharacterRandomPresetInfo __result)
        {
            try
            {
                // 原生已返回有效 preset（randomPreset 非空）时不干预
                if (__result.randomPreset != null) return;

                // 从元表获取 EnemyRegistry
                var meta = RegistryManager.Instance.Registry;
                if (!meta.TryGet(new Identifier(FMLConstants.Domain, "enemy"), out ERegistry raw) ||
                    !(raw is EnemyRegistry enemyReg))
                    return;

                // 收集所有已注册且有对应 weight 配置的 preset
                var candidates = enemyReg.ToArray();
                if (candidates.Length == 0) return;

                // 简单策略：从 FML 注册的 preset 中随机选一个
                // （后续可扩展为按 GameplayDataSettings 权重配置筛选）
                var selected = candidates[UnityEngine.Random.Range(0, candidates.Length)].Value;
                if (selected != null)
                {
                    __result = new CharacterRandomPresetInfo { randomPreset = selected, weight = 1f };
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FML Patch #4] GetAPresetByWeightPostfix: {e}");
            }
        }

        // ======================================================================
        // Patch #10: LevelManager.InitLevel Postfix
        // 关卡初始化后，遍历 FML EnemyRegistry 中显式开启 AutoSpawn 的敌人，
        // 在玩家出生点附近生成。
        // 注意：InitLevel 是 async 方法，Harmony postfix 在同步段结束
        // （首个 await 前）即触发——延迟数帧等玩家出生后再生成。
        // 仅在 EnemyUtils.SetAutoSpawn(id, true) 开启时生成（默认不自动生成）。
        // ======================================================================
        [HarmonyPatch(typeof(LevelManager), "InitLevel")]
        [HarmonyPostfix]
        public static void InitLevelPostfix(LevelManager __instance)
        {
            try
            {
                _ = DelayedAutoSpawnAsync();
            }
            catch (Exception e)
            {
                Debug.LogError($"[FML Patch #10] InitLevelPostfix: {e}");
            }
        }

        private static async UniTask DelayedAutoSpawnAsync()
        {
            try
            {
                // 等待玩家出生点/关卡结构就绪
                await UniTask.DelayFrame(3);

                var meta = RegistryManager.Instance.Registry;
                if (!meta.TryGet(new Identifier(FMLConstants.Domain, "enemy"), out ERegistry raw) ||
                    !(raw is EnemyRegistry enemyReg))
                    return;

                var playerSpawn = GameObject.FindWithTag("Player");
                Vector3 basePos = playerSpawn != null
                    ? playerSpawn.transform.position + new Vector3(5f, 0f, 5f)
                    : Vector3.zero;

                int sceneBuildIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
                int spawnCount = 0;

                foreach (var kvp in enemyReg)
                {
                    // 仅生成显式开启 AutoSpawn 的敌人
                    if (!enemyReg.IsAutoSpawn(kvp.Key)) continue;
                    var preset = kvp.Value;
                    if (preset == null) continue;

                    Vector3 spawnPos = basePos + new Vector3(
                        UnityEngine.Random.Range(-3f, 3f),
                        0f,
                        UnityEngine.Random.Range(-3f, 3f));

                    // public 方法直接调用，零反射（fire-and-forget）
                    _ = preset.CreateCharacterAsync(spawnPos, Vector3.zero, sceneBuildIndex, null, false);
                    spawnCount++;
                }

                if (spawnCount > 0)
                {
                    Debug.Log($"[FML Patch #10] InitLevel: auto-spawned {spawnCount} FML registered enemies.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FML Patch #10] DelayedAutoSpawnAsync: {e}");
            }
        }
    }
}
