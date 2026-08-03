using Duckov.Modding;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace FeatherMod.Modding
{
    /// <summary>
    /// Harmony 补丁集合。Hook <c>ModManager</c> 的排序逻辑，
    /// 注入 fml.json 声明的依赖排序策略。
    /// 排序策略：仅保证拓扑依赖（dependencies / loadAfter / loadBefore），
    /// 不强制按 fml.json priority 重排，尊重玩家手动排序。
    /// </summary>
    public static class ModManagerPatches
    {
        private static bool _sortPatchApplied;

        /// <summary>
        /// 确保补丁已应用。在 FML 自身 OnAfterSetup 中调用一次即可。
        /// 幂等：重复调用不重复 patch。
        /// </summary>
        public static void EnsurePatched()
        {
            if (_sortPatchApplied) return;
            _sortPatchApplied = true;

            var harmony = new Harmony("Feather.ModOrdering");
            FeatherMod.ModBehaviour.ExtraHarmonies.Add(harmony);
            harmony.Patch(
                original: typeof(ModManager).GetMethod("SortModInfosByPriority",
                    BindingFlags.NonPublic | BindingFlags.Static),
                postfix: new HarmonyMethod(typeof(ModManagerPatches), nameof(SortModInfosByPriority_Postfix)));
            harmony.Patch(
                original: typeof(ModManager).GetMethod(nameof(ModManager.Reorder),
                    BindingFlags.Public | BindingFlags.Static),
                postfix: new HarmonyMethod(typeof(ModManagerPatches), nameof(Reorder_Postfix)));
        }

        /// <summary>
        /// 后置修正 <c>SortModInfosByPriority</c>（被 Rescan 调用）。
        /// 原生排序已按 ES3 保存的 priority 恢复玩家手动顺序，
        /// 此 Postfix 在此基础上叠加依赖拓扑排序，确保 fml.json 依赖关系成立。
        /// 未声明依赖关系的 mod 保持原生排序结果（即玩家上次保存的顺序）。
        /// 排序完成后将修正后的顺序回写 ES3，确保下次 Rescan 时顺序不再丢失。
        /// </summary>
        public static void SortModInfosByPriority_Postfix()
        {
            RepairModsES3IfCorrupt();
            ModMetaCache.Clear();
            ModMetaCache.LoadAll(ModManager.modInfos);
            ModDependencyResolver.SortByDependencyOnly(ModManager.modInfos);
            PersistPriorities();
        }

        /// <summary>
        /// 后置修正 <c>Reorder</c>（玩家 UI 拖拽排序后触发）。
        /// Reorder 内部不调用 SortModInfosByPriority，故在此独立做依赖拓扑排序。
        /// 仅保证 fml.json 声明的依赖关系（dependencies / loadAfter / loadBefore），
        /// 不强制按 priority 重排。未声明依赖关系的 mod 顺序完全不受影响。
        /// 排序完成后将修正后的顺序回写 ES3，确保优先级与内存顺序一致。
        /// </summary>
        public static void Reorder_Postfix()
        {
            RepairModsES3IfCorrupt();
            ModMetaCache.Clear();
            ModMetaCache.LoadAll(ModManager.modInfos);
            ModDependencyResolver.SortByDependencyOnly(ModManager.modInfos);
            PersistPriorities();
            // ModManager.OnReorder 是 public static event——外部无法直接 Invoke；
            // backing 字段为编译器生成的 <OnReorder>k__BackingField（private static），
            // 只能通过反射读取（AGENTS.md 允许的 event backing field 场景）。
            var onReorder = typeof(ModManager)
                .GetField("<OnReorder>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static)
                ?.GetValue(null) as System.Action;
            onReorder?.Invoke();
        }

        /// <summary>
        /// 将当前 <c>ModManager.modInfos</c> 的顺序持久化到 ES3（Mods.ES3）。
        /// 调用原生 <c>ModManager.RegeneratePriorities()</c>（private static 经 Publicizer 已公开，直接调用零反射），
        /// 将每个 mod 的索引作为 priority_XXX 键写入。
        /// </summary>
        private static void PersistPriorities()
        {
            var modInfos = ModManager.modInfos;
            if (modInfos == null || modInfos.Count == 0) return;

            ModManager.RegeneratePriorities();
        }

        /// <summary>
        /// Mods.ES3 损坏自愈。原生 <c>ModManager.Load&lt;T&gt;</c> 读取该文件失败时
        /// 会刷屏 "Failed loading mod info." 且所有 mod 优先级退化为 int.MaxValue
        /// （顺序随机、玩家手动排序丢失）。此处探测文件可读性，损坏时将主文件与
        /// 备份一并隔离（重命名保留现场），后续 <see cref="PersistPriorities"/> 会重建干净文件。
        /// 安全性：Mods.ES3 仅存 mod priority 键，隔离无玩家数据损失。
        /// </summary>
        private static void RepairModsES3IfCorrupt()
        {
            var settings = new ES3Settings
            {
                location = ES3.Location.File,
                path = "Saves/Mods.ES3"
            };

            try
            {
                ES3.Load<int>("__fml_probe__", -1, settings);
                return; // 文件可读，健康
            }
            catch
            {
                // 损坏：隔离主文件与备份
            }

            string savesDir = Path.Combine(Application.persistentDataPath, "Saves");
            string stamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            foreach (var fileName in new[] { "Mods.ES3", "Mods.ES3.bac" })
            {
                string fullPath = Path.Combine(savesDir, fileName);
                if (!File.Exists(fullPath)) continue;
                try
                {
                    File.Move(fullPath, fullPath + ".corrupt-" + stamp);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[FML] Failed to quarantine corrupt file {fullPath}: {e.Message}");
                }
            }
            Debug.LogWarning("[FML] Saves/Mods.ES3 was corrupt; quarantined. Mod priorities will be regenerated.");
        }
    }
}
