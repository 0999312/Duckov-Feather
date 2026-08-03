using Duckov.PerkTrees;
using HarmonyLib;
using UnityEngine;

namespace FeatherMod.PerkTrees.Patches
{
    /// <summary>
    /// PerkTreeManager.GetPerkTree 前缀 + 后缀补丁。
    ///
    /// 问题：FML 在 PerkTreeManager.Instance 为 null 时注册的 PerkTree
    /// 无法注入到 <c>perkTrees</c> 列表，导致游戏原生代码（如 PerkTreeUIInvoker）
    /// 调用 <c>GetPerkTree</c> 时返回 null 并打印错误日志。
    ///
    /// Prefix 优先拦截 FML 树命中 → 跳过原版方法（避免错误日志）；
    /// Postfix 处理回退查找（策略 2/3）及诊断输出。
    /// 命中后自动补注入到 perkTrees 列表（使后续原生查找直接命中）。
    /// </summary>
    [HarmonyPatch(typeof(PerkTreeManager), "GetPerkTree")]
    public static class PerkTreeManagerGetPerkTreePatch
    {
        /// <summary>
        /// Prefix：优先通过 FML 内部缓存查找。命中则直接返回，跳过原版方法避免错误日志。
        /// </summary>
        [HarmonyPrefix]
        public static bool Prefix(string id, ref PerkTree __result)
        {
            // 仅拦截 FML 注册的树 → 命中直接返回，原版方法不执行
            if (PerkTreeUtils.IsFMLTree(id)
                && PerkTreeUtils.TryGetRegisteredTree(id, out var cached))
            {
                InjectIfMissing(cached);
                if (cached.relationGraphOwner?.graph is PerkRelationGraph g)
                {
                    // UI 打开前清理悬空连接：原生 PerkTreeView.RefreshConnections
                    // 对 targetNode 为 null 的连接无空检查，会直接 NRE
                    PerkTreeUtils.PruneDanglingConnections(g);
                    PerkTreeUtils.TryLayoutGraph(g);
                }
                __result = cached;
                return false; // 跳过原版方法
            }
            return true; // 非 FML 树，走原版逻辑
        }

        /// <summary>
        /// Postfix：原版方法返回 null 时，尝试策略 2/3 回退查找。
        /// </summary>
        [HarmonyPostfix]
        public static void Postfix(string id, ref PerkTree __result)
        {
            if (__result != null) return;

            var fallback = TryFindFallback(id);
            if (fallback != null)
                __result = fallback;
        }

        private static PerkTree? TryFindFallback(string id)
        {
            var instance = PerkTreeManager.Instance;

            // 策略 2：遍历 PerkTreeManager.perkTrees 匹配（FML 前缀 或 原版 ID）
            if (instance?.perkTrees != null)
            {
                var prefixName = PerkTreeUtils.FML_TREE_PREFIX + id;
                foreach (var t in instance.perkTrees)
                {
                    if (t != null && (t.name == prefixName || t.ID == id))
                        return t;
                }
            }

            // 策略 3：Resources.FindObjectsOfTypeAll（DontDestroyOnLoad + 原版树）
            var allTrees = Resources.FindObjectsOfTypeAll<PerkTree>();
            var fmlTargetName = PerkTreeUtils.FML_TREE_PREFIX + id;
            foreach (var t in allTrees)
            {
                if (t != null && (t.name == fmlTargetName || t.ID == id))
                {
                    InjectIfMissing(t);
                    return t;
                }
            }

            // 所有策略均失败 → 诊断输出
            Debug.LogWarning($"[FML PerkTreePatch] Tree '{id}' NOT found. "
                + $"cache={PerkTreeUtils.TryGetRegisteredTree(id, out _)}, "
                + $"isFML={PerkTreeUtils.IsFMLTree(id)}, "
                + $"instanceNull={instance == null}, "
                + $"perkTreesCount={instance?.perkTrees?.Count ?? -1}, "
                + $"allTreesInScene={allTrees?.Length ?? -1}");

            if (instance?.perkTrees != null)
            {
                var names = new System.Text.StringBuilder();
                foreach (var t in instance.perkTrees)
                    if (t != null) names.Append(t.name).Append(", ");
                Debug.LogWarning($"[FML PerkTreePatch] perkTrees names: [{names}]");
            }

            return null;
        }

        private static void InjectIfMissing(PerkTree tree)
        {
            var instance = PerkTreeManager.Instance;
            if (instance == null || instance.perkTrees == null) return;
            if (instance.perkTrees.Contains(tree)) return;
            instance.perkTrees.Add(tree);
        }
    }
}
