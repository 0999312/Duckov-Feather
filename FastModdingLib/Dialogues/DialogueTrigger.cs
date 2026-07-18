using Cysharp.Threading.Tasks;
using Duckov.Quests;
using FeatherMod.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// 对话触发系统。提供接近位置、接受任务、完成任务等对话触发器。
    /// Quest 事件通过游戏原生 Quest.onQuestActivated / onQuestCompleted 订阅。
    /// </summary>
    public static class DialogueTrigger
    {
        private static bool _initialized;

        private static readonly Dictionary<Identifier, List<QuestTriggerEntry>> s_questCompletedTriggers = new();
        private static readonly Dictionary<Identifier, List<QuestTriggerEntry>> s_questAcceptedTriggers = new();

        /// <summary>初始化 Quest 事件订阅（幂等）。</summary>
        internal static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            // C# 编译器对 field-like event 生成的 backing field 是 private static，
            // 必须同时包含 Public 和 NonPublic 才能通过 GetField 找到。
            var bfs = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

            // onQuestActivated
            try
            {
                var fiActivated = typeof(Quest).GetField("onQuestActivated", bfs);
                if (fiActivated != null)
                {
                    var evt = fiActivated.GetValue(null) as Action<Quest>;
                    evt += OnQuestActivated;
                    fiActivated.SetValue(null, evt);
                }
                else
                {
                    Debug.LogWarning("[FML DialogueTrigger] Quest.onQuestActivated field not found — quest-accept triggers disabled.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FML DialogueTrigger] Failed to subscribe Quest.onQuestActivated: {ex.Message}");
            }

            // onQuestCompleted
            try
            {
                var fiCompleted = typeof(Quest).GetField("onQuestCompleted", bfs);
                if (fiCompleted != null)
                {
                    var evt = fiCompleted.GetValue(null) as Action<Quest>;
                    evt += OnQuestCompleted;
                    fiCompleted.SetValue(null, evt);
                }
                else
                {
                    Debug.LogWarning("[FML DialogueTrigger] Quest.onQuestCompleted field not found — quest-complete triggers disabled.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FML DialogueTrigger] Failed to subscribe Quest.onQuestCompleted: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════
        //  Public API
        // ═══════════════════════════════════════════════════════

        /// <summary>注册接近触发对话。</summary>
        public static void OnProximity(Identifier npcId, float distance, DialogueLine[] lines,
            DialogueTriggerMode mode = DialogueTriggerMode.Once)
        {
            if (!FriendlyNpcUtils.Registry.TryGet(npcId, out var go) || go == null)
            {
                Debug.LogWarning($"[FML DialogueTrigger] NPC '{npcId}' not found.");
                return;
            }

            var trigger = go.GetComponent<NpcProximityTrigger>() ?? go.AddComponent<NpcProximityTrigger>();
            trigger.NpcId = npcId;
            trigger.Distance = distance;
            trigger.Lines = lines;
            trigger.Mode = mode;
        }

        /// <summary>注册接近触发对话（使用序列对象）。</summary>
        public static void OnProximity(Identifier npcId, DialogueSequence sequence)
        {
            if (!sequence.HasContent) return;
            OnProximity(npcId, sequence.ProximityDistance > 0 ? sequence.ProximityDistance : 3f,
                sequence.Lines, sequence.Mode);
        }

        /// <summary>注册接受任务时触发对话。</summary>
        /// <param name="actorId">DuckovDialogueActor.id。为空时依次回退：NPC 配置 ActorId → npcId.Path。</param>
        public static void OnQuestAccepted(Identifier questId, Identifier npcId, DialogueLine[] lines,
            string? actorId = null, DialogueTriggerMode mode = DialogueTriggerMode.Once)
        {
            Init();
            var entry = new QuestTriggerEntry { QuestId = questId, NpcId = npcId, Lines = lines, Mode = mode, ActorId = actorId };
            if (!s_questAcceptedTriggers.TryGetValue(npcId, out var list))
                s_questAcceptedTriggers[npcId] = list = new List<QuestTriggerEntry>();
            list.Add(entry);
        }

        /// <summary>注册完成任务时触发对话。</summary>
        /// <param name="actorId">DuckovDialogueActor.id。为空时依次回退：NPC 配置 ActorId → npcId.Path。</param>
        public static void OnQuestCompleted(Identifier questId, Identifier npcId, DialogueLine[] lines,
            string? actorId = null, DialogueTriggerMode mode = DialogueTriggerMode.Once)
        {
            Init();
            var entry = new QuestTriggerEntry { QuestId = questId, NpcId = npcId, Lines = lines, Mode = mode, ActorId = actorId };
            if (!s_questCompletedTriggers.TryGetValue(npcId, out var list))
                s_questCompletedTriggers[npcId] = list = new List<QuestTriggerEntry>();
            list.Add(entry);
        }

        /// <summary>移除指定 NPC 的所有对话触发器。</summary>
        public static void RemoveAllTriggers(Identifier npcId)
        {
            s_questAcceptedTriggers.Remove(npcId);
            s_questCompletedTriggers.Remove(npcId);

            if (FriendlyNpcUtils.Registry.TryGet(npcId, out var go) && go != null)
            {
                var trigger = go.GetComponent<NpcProximityTrigger>();
                if (trigger != null)
                    UnityEngine.Object.Destroy(trigger);
            }
        }

        // ═══════════════════════════════════════════════════════
        //  Quest 事件回调
        // ═══════════════════════════════════════════════════════

        private static void OnQuestActivated(Quest quest)
        {
            var questId = ResolveQuestId(quest);
            if (questId == null) return;
            HandleQuestTrigger(s_questAcceptedTriggers, questId);
        }

        private static void OnQuestCompleted(Quest quest)
        {
            var questId = ResolveQuestId(quest);
            if (questId == null) return;
            HandleQuestTrigger(s_questCompletedTriggers, questId);
        }

        private static void HandleQuestTrigger(Dictionary<Identifier, List<QuestTriggerEntry>> triggers,
            Identifier questId)
        {
            var toRemove = new List<(Identifier npcId, QuestTriggerEntry entry)>();

            foreach (var (npcId, entries) in triggers)
            {
                foreach (var entry in entries)
                {
                    if (!entry.QuestId.Equals(questId)) continue;
                    if (!string.IsNullOrEmpty(entry.NpcId.Path))
                        PlayDialogueForNpc(entry).Forget();
                    if (entry.Mode == DialogueTriggerMode.Once)
                        toRemove.Add((npcId, entry));
                }
            }

            foreach (var (npcId, entry) in toRemove)
            {
                if (triggers.TryGetValue(npcId, out var list))
                {
                    list.Remove(entry);
                    if (list.Count == 0) triggers.Remove(npcId);
                }
            }
        }

        private static async UniTask PlayDialogueForNpc(QuestTriggerEntry entry)
        {
            // 优先用注册时显式传入的 ActorId；未传入时查询 NPC 配置中的 ActorId
            // （而非 NpcId.Path——DuckovDialogueActor 按 FriendlyNpcConfig.ActorId 注册，
            // 用 NpcId.Path 会查不到 actor，导致对话无发言者显示）。
            var actorId = entry.ActorId;
            if (string.IsNullOrEmpty(actorId) && !FriendlyNpcUtils.TryGetNpcActorId(entry.NpcId, out actorId))
                actorId = entry.NpcId.Path;
            await DialogueManager.PlayDialogue(actorId, entry.Lines);
        }

        private static Identifier? ResolveQuestId(Quest quest)
        {
            // 🆕 Bug Fix: 优先使用 quest.ID 匹配（游戏激活 Quest 时 Instantiate 克隆体，
            // Unity 会在克隆体名称追加 "(Clone)"，导致 quest.name 与 Registry 中的 Path 不匹配）。
            // TryGetQuestIdentifier 通过数字 ID 反查 Identifier（O(1)），不受名称影响。
            if (QuestUtils.TryGetQuestIdentifier(quest.ID, out var id))
                return id;

            // 回退：名称匹配（兼容 quest.name 不含 "(Clone)" 的情况）
            var questName = quest.name;
            if (!string.IsNullOrEmpty(questName))
            {
                foreach (var kvp in QuestUtils.Registry)
                    if (kvp.Key.Path == questName)
                        return kvp.Key;
            }

            return null;
        }

        private class QuestTriggerEntry
        {
            public Identifier QuestId = default;
            public Identifier NpcId = default;
            public string? ActorId;
            public DialogueLine[] Lines = Array.Empty<DialogueLine>();
            public DialogueTriggerMode Mode;
        }
    }
}
