using Duckov.PerkTrees;
using HarmonyLib;

namespace FeatherMod.PerkTrees.Patches
{
    /// <summary>
    /// Perk.Unlocked setter 批量事件抑制。
    ///
    /// 问题：PerkTree.SetupSaveData() 在存档槽位切换时被 PerkTree.Load() 调用，
    /// 遍历所有 perk 调用 perk.Unlocked = false 再调用 perk.Unlocked = restored，
    /// 每个 perk 触发两次 onUnlockStateChanged → PerkDetails.Refresh()。
    /// 主菜单场景下 PerkDetails UI 组件未完全初始化 → NRE 崩溃 → 存档无法选择。
    ///
    /// 修复：SetupSaveData 是批量操作，逐 perk 通知 UI 无意义。
    /// 在操作期间设 SuppressPerkEvents=true，Perk.set_Unlocked Prefix 直接写
    /// _unlocked 字段并跳过原 setter（不触发 onUnlockStateChanged）。
    /// 正常流程（ForceUnlock / ConfirmUnlock）不走 SetupSaveData，不受影响。
    ///
    /// ⚠ 类级 [HarmonyPatch] 必须存在：Harmony 2.4.x 的 PatchAll 只扫描带类级
    /// Harmony 特性的类型（历史版本缺少类级特性导致本补丁从未生效）。
    /// </summary>
    [HarmonyPatch]
    internal static class PerkSetupSaveDataSuppressPatch
    {
        // ═══════════════════════════════════════════════════
        //  PerkTree.SetupSaveData — 控制抑制窗口
        // ═══════════════════════════════════════════════════

        [HarmonyPatch(typeof(PerkTree), "SetupSaveData")]
        [HarmonyPrefix]
        static void SetupSaveData_Prefix()
        {
            PerkTreeUtils.SuppressPerkEvents = true;
        }

        [HarmonyPatch(typeof(PerkTree), "SetupSaveData")]
        [HarmonyPostfix]
        static void SetupSaveData_Postfix()
        {
            PerkTreeUtils.SuppressPerkEvents = false;
        }

        // ═══════════════════════════════════════════════════
        //  Perk.set_Unlocked — 抑制期间跳过事件触发
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// Prefix：抑制期间直接写 _unlocked 字段，跳过原 setter（不触发 onUnlockStateChanged）。
        /// _unlocked 经 Krafs.Publicizer 已公开，直接赋值无需反射。
        /// </summary>
        [HarmonyPatch(typeof(Perk), "set_Unlocked")]
        [HarmonyPrefix]
        static bool PerkSetUnlocked_Prefix(Perk __instance, bool value)
        {
            if (!PerkTreeUtils.SuppressPerkEvents)
                return true; // 正常流程：走原 setter

            __instance._unlocked = value;
            return false; // 跳过原 setter（不触发 onUnlockStateChanged）
        }
    }
}
