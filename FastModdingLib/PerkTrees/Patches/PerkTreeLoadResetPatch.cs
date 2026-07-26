using Duckov.PerkTrees;
using HarmonyLib;
using Saves;
using UnityEngine;

namespace FeatherMod.PerkTrees.Patches
{
    /// <summary>
    /// PerkTree.Load 后置补丁：无存档数据时重置 Perk 为默认状态。
    ///
    /// 问题：游戏原生 PerkTree.Load() 仅当 SavesSystem.KeyExisits(SaveKey) 为 true 时
    /// 才调用 SetupSaveData() 恢复存档状态。若当前槽位从未保存过该 PerkTree，
    /// Load() 直接返回，perk 保持 DontDestroyOnLoad 残留的旧槽位解锁状态。
    ///
    /// 修复：Load() 执行后，检查 loaded 字段。若为 false（无存档数据被加载），
    /// 遍历所有 perk 重置为默认状态（defaultUnlocked → ForceUnlock，否则 Unlocked=false）。
    ///
    /// 兼容性：PerkTreeLoadDeferPatch（Prefix）在 perks.Count==0 时返回 false，
    /// 会同时跳过本 Postfix——空树时不会触发重置逻辑，由 AddPerk 的 tree.Load() 兜底。
    /// </summary>
    [HarmonyPatch(typeof(PerkTree), "Load")]
    internal static class PerkTreeLoadResetPatch
    {
        /// <summary>
        /// Postfix：Load() 完成后若无存档数据被加载，重置 Perk 为默认状态。
        /// </summary>
        [HarmonyPostfix]
        static void Postfix(PerkTree __instance)
        {
            // 仅处理 FML 注册的自定义 PerkTree
            if (!PerkTreeUtils.IsFMLTree(__instance.perkTreeID))
                return;

            // perks 为空时不操作（由 PerkTreeLoadDeferPatch 的 Prefix 保护，
            // 此处二次防御：即使 Prefix 未跳过，空列表也无需重置）
            if (__instance.perks.Count == 0)
                return;

            // 重建 SaveKey 检查存档 key 是否存在
            string saveKey = "PerkTree_" + __instance.perkTreeID;

            // 若存档 key 存在，说明 Load() 已通过 SetupSaveData 正确恢复状态，无需干预
            if (SavesSystem.KeyExisits(saveKey))
                return;

            // 无存档数据：重置所有 perk 为默认状态
            int resetCount = 0;
            foreach (var perk in __instance.perks)
            {
                if (perk == null) continue;

                if (perk.defaultUnlocked)
                {
                    // 默认解锁的 perk 需确保为解锁状态
                    if (!perk.Unlocked)
                    {
                        perk.ForceUnlock();
                        resetCount++;
                    }
                }
                else
                {
                    // 非默认解锁的 perk 需重置为锁定状态
                    if (perk.Unlocked)
                    {
                        perk.Unlocked = false;
                        resetCount++;
                    }
                }
            }

            if (resetCount > 0)
                Debug.Log($"[FML PerkTree] LoadReset: '{__instance.perkTreeID}' — {resetCount} perk(s) reset to defaults (no save data).");
        }
    }
}
