using Duckov.PerkTrees;
using HarmonyLib;

namespace FeatherMod.PerkTrees.Patches
{
    /// <summary>
    /// PerkTree.Save 空树拦截补丁。
    /// 场景加载前 NotifySaveBeforeLoadScene → CollectSaveData 会触发 Save()，
    /// 此时若 perks 列表为空，Save 会写入全 locked 状态，永久覆写正常存档。
    /// 此补丁在 perks 未就绪时拦截 Save，防止空白存档损坏数据。
    /// </summary>
    [HarmonyPatch(typeof(PerkTree), "Save")]
    internal static class PerkTreeSaveGuardPatch
    {
        [HarmonyPrefix]
        static bool Prefix(PerkTree __instance)
        {
            if (PerkTreeUtils.IsFMLTree(__instance.perkTreeID)
                && __instance.perks.Count == 0)
            {
                return false; // 空树不存档，防止覆写
            }
            return true;
        }
    }
}
