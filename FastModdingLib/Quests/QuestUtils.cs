using Duckov.Quests;
using Duckov.Quests.Relations;
using Duckov.Utilities;
using FastModdingLib.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace FastModdingLib
{
    public static class QuestUtils
    {
        private static readonly QuestRegistry _questRegistry = new QuestRegistry();
        private static int _nextQuestId = 1000;  // 自定义任务 ID 从 1000 起

        /// <summary>暴露给外部用于元注册表注册等场景。</summary>
        public static QuestRegistry Registry => _questRegistry;

        // ═══════════════════════════════════════════════════
        //  ID 反查
        // ═══════════════════════════════════════════════════

        /// <summary>按 quest 数字 ID 反查 Identifier。</summary>
        public static bool TryGetQuestIdentifier(int questId, out Identifier id)
        {
            foreach (var kvp in _questRegistry)
            {
                if (kvp.Value != null && kvp.Value.id == questId)
                {
                    id = kvp.Key;
                    return true;
                }
            }
            id = default;
            return false;
        }

        /// <summary>按 Identifier 解析为 quest 数字 ID。</summary>
        public static bool TryGetQuestId(Identifier id, out int questId)
        {
            if (_questRegistry.TryGet(id, out var quest) && quest != null)
            {
                questId = quest.id;
                return true;
            }
            questId = -1;
            return false;
        }

        // ═══════════════════════════════════════════════════
        //  注册
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 注册自定义任务。使用 <see cref="Identifier"/> 标识任务，
        /// domain 自动推导为 owner modid。
        /// </summary>
        public static void RegisterQuest(Identifier id, QuestData data)
        {
            string modid = id.Domain;
            RegisterQuestInternal(id, data, modid);
        }

        /// <summary>
        /// 注册自定义任务（兼容旧 API）。
        /// </summary>
        public static void RegisterQuest(QuestData data, string modid = "FastModdingLib")
        {
            // 优先使用 data.Id（如果设置），否则使用旧式 Identifier
            Identifier id = data.Id ?? new Identifier(modid, $"quest_{data.ID}");
            RegisterQuestInternal(id, data, modid);
        }

        private static void RegisterQuestInternal(Identifier id, QuestData data, string modid)
        {
            // 解析 task / reward 中的 itemIdentifier → 内部 itemTypeID
            ResolveTaskItemRefs(data.tasks);
            ResolveRewardItemRefs(data.rewards);

            // 自分配 Quest ID（从 1000 起递增）
            data.ID = _nextQuestId++;

            // 自分配 Task/Reward 的 id（从 1 开始递增，游戏原生 API 要求唯一）
            int nextId = 1;
            foreach (var task in data.tasks)
                task.id = nextId++;
            foreach (var reward in data.rewards)
                reward.id = nextId++;

            Quest quest = new GameObject($"Quest_{data.displayName}").AddComponent<Quest>();
            UnityEngine.Object.DontDestroyOnLoad(quest.gameObject);
            quest.gameObject.SetActive(false);
            quest.DisplayNameRaw = data.displayName;
            quest.DescriptionRaw = data.description;
            quest.ID = data.ID;
            quest.QuestGiverID = data.questGiver;
            quest.requireLevel = data.requireLevel;

            if (data.requireScene != null && data.requireScene != "")
            {
                quest.requireSceneID = data.requireScene;
            }

            if (data.requireItemID != -1)
            {
                quest.requiredItemID = data.requireItemID;
                quest.requiredItemCount = 1;
            }

            foreach (var taskData in data.tasks)
            {
                Task task = taskData.SetTask(quest);
                if (task != null)
                {
                    quest.tasks.Add(task);
                }
            }

            foreach (var rewardData in data.rewards)
            {
                Reward reward = rewardData.SetReward(quest);
                if (reward != null)
                {
                    quest.rewards.Add(reward);
                }
            }
            GameplayDataSettings.QuestCollection.Add(quest);
            // 使用传入的 Identifier（domain = modid）作为 registry key
            _questRegistry.Set(id, quest, modid);
            Debug.Log($"[FML] Registered quest: {data.displayName} (ID: {data.ID}) from mod: {modid} (identifier: {id})");
        }

        /// <summary>解析 task 列表中的 itemIdentifier → itemTypeID。</summary>
        private static void ResolveTaskItemRefs(List<TaskData> tasks)
        {
            foreach (var task in tasks)
            {
                switch (task)
                {
                    case TaskRequireItem tri:
                        tri.itemTypeID = ItemUtils.ResolveItemRef(tri.itemIdentifier, tri.itemTypeID);
                        break;
                    case TaskRequireUseItem tui:
                        tui.itemTypeID = ItemUtils.ResolveItemRef(tui.itemIdentifier, tui.itemTypeID);
                        break;
                    case TaskKillCount tkc:
                        tkc.weaponTypeID = ItemUtils.ResolveItemRef(tkc.weaponIdentifier, tkc.weaponTypeID);
                        break;
                }
            }
        }

        /// <summary>解析 reward 列表中的 itemIdentifier → itemTypeID。</summary>
        private static void ResolveRewardItemRefs(List<RewardData> rewards)
        {
            foreach (var reward in rewards)
            {
                switch (reward)
                {
                    case RewardGiveItem rgi:
                        rgi.itemTypeID = ItemUtils.ResolveItemRef(rgi.itemIdentifier, rgi.itemTypeID);
                        break;
                    case RewardUnlockItem rui:
                        rui.itemTypeID = ItemUtils.ResolveItemRef(rui.itemIdentifier, rui.itemTypeID);
                        break;
                }
            }
        }

        internal static void UnregisterQuest(int ID)
        {
            foreach (var entry in _questRegistry)
            {
                if (entry.Value.id == ID)
                {
                    _questRegistry.Remove(entry.Key);
                    Debug.Log($"Unregistered custom quest: {ID}");
                    break;
                }
            }
        }

        /// <summary>按 Identifier 卸载任务。</summary>
        public static bool UnregisterQuest(Identifier id)
        {
            return _questRegistry.Remove(id);
        }

        public static void UnregisterQuestAll(string modID)
        {
            _questRegistry.RemoveAllByOwner(modID);
        }

        internal static void AddQuestRelation(int id, int before = -1, int after = -1)
        {
            QuestRelationNode item = new QuestRelationNode
            {
                questID = id
            };
            if (before != -1)
            {
                QuestRelationNode source = new QuestRelationNode
                {
                    questID = before
                };
                item.inConnections.Add(QuestRelationConnection.Create(source, item));
            }

            if (after != -1)
            {
                QuestRelationNode target = new QuestRelationNode
                {
                    questID = after
                };
                item.outConnections.Add(QuestRelationConnection.Create(item, target));
            }

            GameplayDataSettings.QuestRelation.allNodes.Add(item);
        }

        /// <summary>按 Identifier 添加任务前后置关系。</summary>
        public static void AddQuestRelation(Identifier id, Identifier? before = null, Identifier? after = null)
        {
            if (!TryGetQuestId(id, out var questId)) return;

            var beforeId = -1;
            if (before != null) TryGetQuestId(before, out beforeId);

            var afterId = -1;
            if (after != null) TryGetQuestId(after, out afterId);

            AddQuestRelation(questId, beforeId, afterId);
        }

    }
}