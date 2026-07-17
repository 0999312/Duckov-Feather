using Duckov.Quests;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FeatherMod.QuestGivers.Patches
{
    /// <summary>
    /// Harmony 补丁：拦截 <see cref="QuestManager.GetAllQuestsByQuestGiverID"/>
    /// 以支持 FML 自定义 QuestGiverID（值 ≥ 50）。
    ///
    /// 游戏原生 <see cref="QuestGiverID"/> 是固定 enum（值 0~11），
    /// 不支持运行时扩展。此补丁在方法调用前检测 questGiverID 整数值，
    /// 如果属于 FML 管理的自定义 ID 范围，则返回 FML 内部维护的任务列表；
    /// 否则走原生逻辑。
    /// </summary>
    /// <remarks>
    /// 此补丁会被 <see cref="ModBehaviour.OnAfterSetup"/> 中的
    /// <c>_harmony.PatchAll(Assembly.GetExecutingAssembly())</c> 自动应用。
    /// 每个 Prefix 使用 try-catch 包裹，异常时放行原生逻辑以保证游戏稳定。
    /// </remarks>
    [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.GetAllQuestsByQuestGiverID))]
    public static class QuestGiverIDPatch
    {
        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        /// <summary>
        /// Prefix：判断传入的 questGiverID 是否为 FML 自定义值。
        /// 是 → 替换返回值为 FML 维护的任务列表，跳过原生方法。
        /// 否 → 执行原生逻辑。
        /// </summary>
        [HarmonyPrefix]
        public static bool GetAllQuestsByQuestGiverID_Prefix(
            QuestGiverID questGiverID,
            ref IEnumerable<Quest> __result)
        {
            try
            {
                int giverId = (int)questGiverID;

                if (giverId >= 50 && QuestGiverUtils.IsCustomQuestGiverId(giverId))
                {
                    var quests = QuestGiverUtils.GetQuestsByCustomId(giverId);
                    if (quests != null)
                    {
                        __result = quests;
                        return false;
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestGiverIDPatch] GetAllQuestsByQuestGiverID failed: {e.Message}");
                return true; // 放行原生
            }
        }

        /// <summary>
        /// Prefix：拦截 <see cref="QuestManager.GetActiveQuestsFromGiver"/>。
        /// 对自定义 QuestGiverID，从 ActiveQuests 中筛选。
        /// </summary>
        [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.GetActiveQuestsFromGiver))]
        [HarmonyPrefix]
        public static bool GetActiveQuestsFromGiver_Prefix(
            QuestGiverID giverID,
            ref List<Quest> __result)
        {
            try
            {
                int giverId = (int)giverID;

                if (giverId >= 50 && QuestGiverUtils.IsCustomQuestGiverId(giverId))
                {
                    var instance = QuestManager.Instance;
                    if (instance != null)
                    {
                        var activeField = typeof(QuestManager).GetField("activeQuests", Flags);
                        if (activeField != null)
                        {
                            var activeQuests = activeField.GetValue(instance) as List<Quest>;
                            __result = activeQuests?
                                .Where(e => e != null && (int)e.QuestGiverID == giverId)
                                .ToList() ?? new List<Quest>();
                            return false;
                        }
                    }
                    __result = new List<Quest>();
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestGiverIDPatch] GetActiveQuestsFromGiver failed: {e.Message}");
                return true;
            }
        }

        /// <summary>
        /// Prefix：拦截 <see cref="QuestManager.GetHistoryQuestsFromGiver"/>。
        /// 对自定义 QuestGiverID，从 historyQuests 中筛选。
        /// </summary>
        [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.GetHistoryQuestsFromGiver))]
        [HarmonyPrefix]
        public static bool GetHistoryQuestsFromGiver_Prefix(
            QuestGiverID giverID,
            ref List<Quest> __result)
        {
            try
            {
                int giverId = (int)giverID;

                if (giverId >= 50 && QuestGiverUtils.IsCustomQuestGiverId(giverId))
                {
                    var instance = QuestManager.Instance;
                    if (instance != null)
                    {
                        var historyField = typeof(QuestManager).GetField("historyQuests", Flags);
                        if (historyField != null)
                        {
                            var historyQuests = historyField.GetValue(instance) as List<Quest>;
                            __result = historyQuests?
                                .Where(e => e != null && (int)e.QuestGiverID == giverId)
                                .ToList() ?? new List<Quest>();
                            return false;
                        }
                    }
                    __result = new List<Quest>();
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestGiverIDPatch] GetHistoryQuestsFromGiver failed: {e.Message}");
                return true;
            }
        }

        /// <summary>
        /// Prefix：拦截 <see cref="QuestManager.AnyActiveQuestNeedsInspection"/>。
        /// 对自定义 QuestGiverID，检测是否有活跃任务需要检查。
        /// </summary>
        [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.AnyActiveQuestNeedsInspection))]
        [HarmonyPrefix]
        public static bool AnyActiveQuestNeedsInspection_Prefix(
            QuestGiverID giverID,
            ref bool __result)
        {
            try
            {
                int giverId = (int)giverID;

                if (giverId >= 50 && QuestGiverUtils.IsCustomQuestGiverId(giverId))
                {
                    var instance = QuestManager.Instance;
                    if (instance != null)
                    {
                        var activeField = typeof(QuestManager).GetField("activeQuests", Flags);
                        if (activeField != null)
                        {
                            var activeQuests = activeField.GetValue(instance) as List<Quest>;
                            __result = activeQuests?.Any(e =>
                                e != null && (int)e.QuestGiverID == giverId && e.NeedInspection) ?? false;
                            return false;
                        }
                    }
                    __result = false;
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestGiverIDPatch] AnyActiveQuestNeedsInspection failed: {e.Message}");
                return true;
            }
        }
    }
}
