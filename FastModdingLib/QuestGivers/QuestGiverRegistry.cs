using Duckov.Quests;
using Duckov.Utilities;
using FeatherMod.Register;
using FeatherMod.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// QuestGiver 注册表。维护 Identifier ↔ 自定义 questGiverID (int) 的映射。
    /// 自定义 questGiverID 从 <see cref="MinCustomQuestGiverId"/> (50) 起分配
    /// （游戏原生 QuestGiverID 枚举值为 0~11，避免冲突）。
    /// </summary>
    public sealed class QuestGiverRegistry : SimpleRegistry<GameObject>
    {
        public const int MinCustomQuestGiverId = 50;

        private readonly Dictionary<Identifier, int> _questGiverIdIndex = new Dictionary<Identifier, int>();
        private readonly Dictionary<int, Identifier> _reverseIdIndex = new Dictionary<int, Identifier>();
        private int _nextQuestGiverId = MinCustomQuestGiverId;

        /// <summary>禁止直接调用基类 Set()——必须通过 Register() 确保 questGiverID 分配。</summary>
        public new void Set(Identifier id, GameObject value, string modid)
        {
            throw new InvalidOperationException(
                $"QuestGiverRegistry.Set() is blocked. Use Register() for new IDs or SetRegistered() for existing.");
        }

        /// <summary>内部使用：为已注册的 QuestGiver 关联 GO（CreateQuestGiver 时）。</summary>
        internal void SetRegistered(Identifier id, GameObject go, string modid)
        {
            base.Set(id, go, modid);
        }

        // ═══════════════════════════════════════════════════
        //  注册
        // ═══════════════════════════════════════════════════

        /// <summary>注册自定义 QuestGiver Identifier，分配唯一 questGiverID (int)。</summary>
        /// <returns>分配的自定义 questGiverID (int)。</returns>
        internal int Register(Identifier id, string modid)
        {
            int questGiverId = _nextQuestGiverId++;
            while (_reverseIdIndex.ContainsKey(questGiverId))
                questGiverId = _nextQuestGiverId++;

            _questGiverIdIndex[id] = questGiverId;
            _reverseIdIndex[questGiverId] = id;

            return questGiverId;
        }

        // ═══════════════════════════════════════════════════
        //  查询
        // ═══════════════════════════════════════════════════

        internal bool TryGetQuestGiverId(Identifier id, out int questGiverId)
            => _questGiverIdIndex.TryGetValue(id, out questGiverId);

        internal bool TryGetIdentifier(int questGiverId, out Identifier id)
            => _reverseIdIndex.TryGetValue(questGiverId, out id);

        internal bool IsCustomQuestGiverId(int questGiverId)
            => _reverseIdIndex.ContainsKey(questGiverId);

        internal IEnumerable<Quest> GetQuestsByCustomId(int questGiverId)
        {
            foreach (var quest in GameplayDataSettings.QuestCollection)
            {
                if (quest != null && (int)quest.QuestGiverID == questGiverId)
                    yield return quest;
            }
        }

        // ═══════════════════════════════════════════════════
        //  native 善后
        // ═══════════════════════════════════════════════════

        protected override void OnRemoved(Identifier id, GameObject value, string? modid)
        {
            if (_questGiverIdIndex.TryGetValue(id, out int questGiverId))
            {
                _reverseIdIndex.Remove(questGiverId);
                _questGiverIdIndex.Remove(id);
            }
            if (value != null)
                UnityEngine.Object.Destroy(value);
        }

        /// <summary>QuestGiver.questGiverID 通过反射设置（兼容自定义 int ID）。</summary>
        private static void SetQuestGiverId(QuestGiver qg, int questGiverId)
        {
            var field = typeof(QuestGiver).GetField("questGiverID",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            if (field != null)
                field.SetValue(qg, Enum.ToObject(field.FieldType, questGiverId));
        }
    }
}
