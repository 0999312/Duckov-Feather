using Duckov.Endowment;
using HarmonyLib;
using UnityEngine;

namespace FeatherMod.Endowment.Patches
{
    /// <summary>
    /// Harmony 补丁：EndowmentManager 生命周期注入。
    /// Awake Postfix 将 FML 注册的自定义天赋注入到 EndowmentManager.entries 列表；
    /// SelectIndex Prefix 确保自定义 EndowmentIndex（≥10）不被拦截。
    /// </summary>
    [HarmonyPatch(typeof(EndowmentManager))]
    public static class EndowmentManagerPatch
    {
        /// <summary>
        /// Awake Postfix：遍历 EndowmentRegistry，为尚未分配 index 的天赋
        /// 分配 EndowmentIndex（≥10），注入到 entries 列表。
        /// 利用 Publicizer 公开的字段，无需反射。
        /// </summary>
        /// <remarks>
        /// 幂等：通过 <c>AllocateIndex</c> 的幂等性 + 检查 <c>entries.Contains</c>，
        /// 即使多次调用也不会重复注入。同时作为安全网兜底处理
        /// <c>RegisterEndowment</c> 调用早于 <c>Awake</c> 的极端时序场景。
        /// </remarks>
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        public static void Awake_Postfix(EndowmentManager __instance)
        {
            var registry = EndowmentUtils.Registry;
            if (registry == null) return;

            foreach (var kvp in registry)
            {
                var entry = kvp.Value;
                if (entry == null) continue;

                // 尝试注入——AllocateIndex 幂等（已分配则返回已有值）
                registry.TryInjectToManager(kvp.Key, entry);
            }
        }

        /// <summary>
        /// SelectIndex Prefix：确保自定义 EndowmentIndex（≥10）走原生逻辑。
        /// 原生 SelectIndex 不检查 index 范围，但保留此 Prefix 以兼容未来游戏版本变化。
        /// </summary>
        [HarmonyPatch("SelectIndex")]
        [HarmonyPrefix]
        public static bool SelectIndex_Prefix(EndowmentIndex index)
        {
            return true;
        }
    }
}
