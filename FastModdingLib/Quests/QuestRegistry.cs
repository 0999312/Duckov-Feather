using Duckov.Quests;
using Duckov.Utilities;
using FeatherMod.Register;
using FeatherMod.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// 管理 <see cref="Quest"/> 注册表的 native 清理与反向索引。
    /// <see cref="SimpleRegistry{T}.OnRemoved"/> 在 registry 删除 entry 时善后：
    /// 从 <see cref="GameplayDataSettings.QuestCollection"/> 移除、<see cref="Object.Destroy"/> 游戏对象、
    /// 清理 <see cref="GameplayDataSettings.QuestRelation"/> 节点。
    /// 同时维护 quest 数字 ID ↔ Identifier 的 O(1) 反向索引，供冲突检测和反查使用。
    /// </summary>
    public class QuestRegistry : SimpleRegistry<Quest>
    {
        /// <summary>quest 数字 ID → Identifier 反向索引（O(1) 反查）。</summary>
        private readonly Dictionary<int, Identifier> _questIdIndex = new Dictionary<int, Identifier>();

        protected override void OnRemoved(Identifier id, Quest value, string? modid)
        {
            GameplayDataSettings.QuestCollection.Remove(value);
            Object.Destroy(value.gameObject);
            GameplayDataSettings.QuestRelation.RemoveNode(
                GameplayDataSettings.QuestRelation.GetNode(value.ID));
        }

        // ═══════════════════════════════════════════════════
        //  反向索引同步 — override 基类变异方法
        // ═══════════════════════════════════════════════════

        public override void Set(Identifier id, Quest value, string modid)
        {
            // 如果该 Identifier 之前已注册不同 quest，清理旧 ID 映射
            if (dict.TryGetValue(id, out var oldQuest) && oldQuest != null && oldQuest.id != value.ID)
            {
                _questIdIndex.Remove(oldQuest.id);
            }
            // 如果新 quest.ID 已被其他 Identifier 占用，清理旧映射（防冲突）
            if (_questIdIndex.TryGetValue(value.ID, out var existingId) && existingId != id)
            {
                _questIdIndex.Remove(value.ID);
            }
            _questIdIndex[value.ID] = id;
            base.Set(id, value, modid);
        }

        public override bool Remove(Identifier id)
        {
            if (dict.TryGetValue(id, out var quest) && quest != null)
            {
                _questIdIndex.Remove(quest.id);
            }
            return base.Remove(id);
        }

        public override int RemoveAllByOwner(string modid)
        {
            // 显式清理反向索引（基类 Remove 回调也会清理，但批量清理更安全）
            var owned = GetAllByOwner(modid);
            foreach (var id in owned)
            {
                if (dict.TryGetValue(id, out var quest) && quest != null)
                {
                    _questIdIndex.Remove(quest.id);
                }
            }
            return base.RemoveAllByOwner(modid);
        }

        public override void Clear()
        {
            _questIdIndex.Clear();
            base.Clear();
        }

        // ═══════════════════════════════════════════════════
        //  反查与冲突检测
        // ═══════════════════════════════════════════════════

        /// <summary>按 quest 数字 ID 反查 Identifier（O(1)）。</summary>
        public bool TryGetIdentifier(int questId, out Identifier id)
        {
            return _questIdIndex.TryGetValue(questId, out id);
        }

        /// <summary>检查指定 quest 数字 ID 是否已被本注册表占用。</summary>
        public bool IsQuestIdOccupied(int questId)
        {
            return _questIdIndex.ContainsKey(questId);
        }

        /// <summary>
        /// 检查指定 quest 数字 ID 是否已在 <see cref="GameplayDataSettings.QuestCollection"/>
        /// 中存在（包含原生游戏任务和已注册的 FML 任务）。
        /// </summary>
        public static bool IsQuestIdInCollection(int questId)
        {
            foreach (var quest in GameplayDataSettings.QuestCollection)
            {
                if (quest != null && quest.ID == questId)
                    return true;
            }
            return false;
        }
    }
}
