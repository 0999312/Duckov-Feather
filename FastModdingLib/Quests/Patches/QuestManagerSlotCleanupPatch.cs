using Duckov.Quests;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FeatherMod.Quests.Patches
{
    /// <summary>
    /// QuestManager.SetupSaveData 前缀补丁：读档前清空 activeQuests / historyQuests。
    ///
    /// 问题：游戏原生 QuestManager.SetupSaveData() 仅对 completedQuests 调用了 Clear()，
    /// 但 activeQuests 和 historyQuests 是直接 Add 新条目而不清空旧数据。
    /// 当 SavesSystem.OnSetFile 触发 Load() → SetupSaveData() 时（槽位切换），
    /// 旧槽位的 quest 实例仍残留在列表中，与新槽位的 quest 实例叠加，导致"存档混乱"。
    ///
    /// 修复：在 SetupSaveData 执行前，通过反射清空 activeQuests 和 historyQuests 列表，
    /// 确保每次读档都从干净的状态开始恢复。completedQuests 已有原生 Clear()，无需干预。
    ///
    /// 仅清空实例引用（不 Destroy GameObject），避免影响 DontDestroyOnLoad 的 quest 模板。
    /// </summary>
    [HarmonyPatch(typeof(QuestManager), "SetupSaveData")]
    internal static class QuestManagerSlotCleanupPatch
    {
        private static readonly FieldInfo? _activeQuestsField =
            typeof(QuestManager).GetField("activeQuests",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? _historyQuestsField =
            typeof(QuestManager).GetField("historyQuests",
                BindingFlags.Instance | BindingFlags.NonPublic);

        [HarmonyPrefix]
        static void Prefix(QuestManager __instance)
        {
            int activeCleared = 0;
            int historyCleared = 0;

            if (_activeQuestsField?.GetValue(__instance) is List<Quest> activeQuests
                && activeQuests.Count > 0)
            {
                activeCleared = activeQuests.Count;
                activeQuests.Clear();
            }

            if (_historyQuestsField?.GetValue(__instance) is List<Quest> historyQuests
                && historyQuests.Count > 0)
            {
                historyCleared = historyQuests.Count;
                historyQuests.Clear();
            }

            if (activeCleared > 0 || historyCleared > 0)
                Debug.Log($"[FML Quest] SlotCleanup: cleared {activeCleared} active + {historyCleared} history quest(s) before reload.");
        }
    }
}
