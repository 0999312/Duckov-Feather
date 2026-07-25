using Duckov.PerkTrees;
using HarmonyLib;

namespace FeatherMod.PerkTrees.Patches
{
    /// <summary>
    /// PerkTree.Load 延迟补丁。
    /// Awake() 中 Load() 被调用时 perks 列表为空，存档数据无法应用到尚不存在的 Perk。
    /// 此补丁在 perks 未就绪时拦截 Load，待 AddPerk 填充 perks 后再由 AddPerk 手动触发恢复。
    /// </summary>
    [HarmonyPatch(typeof(PerkTree), "Load")]
    internal static class PerkTreeLoadDeferPatch
    {
        [HarmonyPrefix]
        static bool Prefix(PerkTree __instance)
        {
            // 仅处理 FML 注册的自定义 PerkTree
            if (!PerkTreeUtils.IsFMLTree(__instance.perkTreeID))
                return true;

            // perks 已就绪，正常执行 Load
            if (__instance.perks.Count > 0)
                return true;

            // perks 为空，跳过本次 Load — 由 AddPerk 在填充完后补触发
            return false;
        }
    }
}
