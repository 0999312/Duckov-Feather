using Duckov.PerkTrees;
using HarmonyLib;

namespace FeatherMod.PerkTrees.Patches
{
    /// <summary>
    /// PerkTree.Collect 前缀补丁。
    /// 游戏原生 <c>PerkTree.Collect()</c> 会重新扫描子 GameObject 并重建
    /// Perk 列表，这会将运行时通过 FML 注入的 Perk 清空。
    /// 此补丁对 FML 注册的 PerkTree 跳过 Collect 调用。
    /// 判定统一走 <see cref="PerkTreeUtils.IsFMLTree"/>（注册表），
    /// 与其它补丁（PerkTreeEnablePatch 等）同源，避免名称前缀双轨判定不一致。
    /// </summary>
    [HarmonyPatch(typeof(PerkTree), "Collect")]
    public static class PerkTreeCollectGuard
    {
        [HarmonyPrefix]
        public static bool Prefix(PerkTree __instance)
        {
            if (__instance != null && __instance.perkTreeID != null &&
                PerkTreeUtils.IsFMLTree(__instance.perkTreeID))
            {
                return false; // 跳过 Collect，保护运行时注入的 Perk
            }
            return true; // 游戏原生树，正常执行 Collect
        }
    }
}
