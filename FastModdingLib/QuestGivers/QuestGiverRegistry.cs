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
    /// QuestGiver 模块注册表。继承 <see cref="SimpleRegistry{T}"/>，
    /// 维护 Identifier → Game.GameObject（QuestGiver 组件所在）的主映射、
    /// Identifier → 自定义 questGiverID (int) 的映射，以及
    /// Identifier → 绑定任务列表的上下文。
    ///
    /// <see cref="SimpleRegistry{T}.OnRemoved"/> 在 registry 删除 entry 时完成
    /// native 善后：销毁 UnityEngine.Object、清理自定义 ID 映射。
    /// </summary>
    /// <remarks>
    /// 自定义 questGiverID 从 50 起分配（游戏原生 QuestGiverID 枚举值为 0~11）。
    /// </remarks>
    public sealed class QuestGiverRegistry : SimpleRegistry<GameObject>
    {
        /// <summary>自定义 questGiverID 起始值（与 Harmony 补丁中的检测阈值一致）。</summary>
        internal const int MinCustomQuestGiverId = 50;
        /// <summary>Identifier → 自定义 questGiverID (int) 映射。</summary>
        private readonly Dictionary<Identifier, int> _questGiverIdIndex = new Dictionary<Identifier, int>();

        /// <summary>自定义 questGiverID (int) → Identifier 反向索引。</summary>
        private readonly Dictionary<int, Identifier> _reverseIdIndex = new Dictionary<int, Identifier>();

        /// <summary>下一个可分配的自定义 questGiverID。</summary>
        private int _nextQuestGiverId = MinCustomQuestGiverId;

        /// <summary>Identifier → 注册时设置的生成位置（SpawnQuestGiver 的默认位置）。</summary>
        private readonly Dictionary<Identifier, Vector3> _spawnPositions = new Dictionary<Identifier, Vector3>();

        /// <summary>Identifier → 注册时设置的生成旋转（SpawnQuestGiver 的默认旋转）。</summary>
        private readonly Dictionary<Identifier, Quaternion> _spawnRotations = new Dictionary<Identifier, Quaternion>();

        // ═══════════════════════════════════════════════════
        //  防止绕过 Register() 直接调用 Set()
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 禁止直接调用基类 Set()——必须通过 <see cref="Register"/> 以确保 questGiverID 分配和索引同步。
        /// </summary>
        public new void Set(Identifier id, GameObject value, string modid)
        {
            throw new InvalidOperationException(
                $"QuestGiverRegistry.Set() is blocked. Use Register() to ensure questGiverID allocation. " +
                $"Attempted to set '{id}' for mod '{modid}'.");
        }

        // ═══════════════════════════════════════════════════
        //  注册
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 注册自定义 QuestGiver。自动分配 questGiverID，写入主字典、
        /// owner 索引、ID 双向映射、任务绑定上下文、生成位置。
        /// </summary>
        /// <param name="id">QuestGiver 的 Identifier（domain = modid）。</param>
        /// <param name="go">QuestGiver 组件所在的 GameObject。</param>
        /// <param name="modid">owner modid。</param>
        /// <param name="boundQuests">可选的绑定任务 Identifier 列表。</param>
        /// <param name="spawnPosition">注册时的生成位置（SpawnQuestGiver 的默认值）。</param>
        /// <param name="spawnRotation">注册时的生成旋转（SpawnQuestGiver 的默认值）。</param>
        /// <returns>分配的自定义 questGiverID (int)。</returns>
        internal int Register(Identifier id, GameObject go, string modid, Identifier[]? boundQuests,
            Vector3 spawnPosition, Quaternion spawnRotation)
        {
            // 分配唯一 questGiverID
            int questGiverId = _nextQuestGiverId++;
            while (_reverseIdIndex.ContainsKey(questGiverId))
            {
                questGiverId = _nextQuestGiverId++;
            }

            base.Set(id, go, modid);
            _questGiverIdIndex[id] = questGiverId;
            _reverseIdIndex[questGiverId] = id;
            _spawnPositions[id] = spawnPosition;
            _spawnRotations[id] = spawnRotation;

            // 设置 QuestGiver 组件的 questGiverID
            var qg = go.GetComponent<QuestGiver>();
            if (qg != null)
            {
                SetQuestGiverId(qg, questGiverId);
            }

            return questGiverId;
        }

        // ═══════════════════════════════════════════════════
        //  查询
        // ═══════════════════════════════════════════════════

        /// <summary>按 Identifier 获取自定义 questGiverID (int)。</summary>
        internal bool TryGetQuestGiverId(Identifier id, out int questGiverId)
        {
            return _questGiverIdIndex.TryGetValue(id, out questGiverId);
        }

        /// <summary>按自定义 questGiverID (int) 反查 Identifier。</summary>
        internal bool TryGetIdentifier(int questGiverId, out Identifier id)
        {
            return _reverseIdIndex.TryGetValue(questGiverId, out id);
        }

        /// <summary>检查指定 int 值是否为 FML 注册的自定义 questGiverID。</summary>
        internal bool IsCustomQuestGiverId(int questGiverId)
        {
            return _reverseIdIndex.ContainsKey(questGiverId);
        }

        /// <summary>获取注册时设置的生成位置（SpawnQuestGiver 的默认值）。</summary>
        internal bool TryGetSpawnPosition(Identifier id, out Vector3 position)
        {
            return _spawnPositions.TryGetValue(id, out position);
        }

        /// <summary>获取注册时设置的生成旋转（SpawnQuestGiver 的默认值）。</summary>
        internal bool TryGetSpawnRotation(Identifier id, out Quaternion rotation)
        {
            return _spawnRotations.TryGetValue(id, out rotation);
        }

        /// <summary>
        /// 按自定义 questGiverID (int) 获取属于此发放者的全部任务。
        /// 从 QuestCollection 中筛选 Quest.QuestGiverID == questGiverId 的条目。
        /// </summary>
        internal IEnumerable<Quest> GetQuestsByCustomId(int questGiverId)
        {
            foreach (var quest in GameplayDataSettings.QuestCollection)
            {
                if (quest != null && (int)quest.QuestGiverID == questGiverId)
                {
                    yield return quest;
                }
            }
        }

        // ═══════════════════════════════════════════════════
        //  native 善后
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 从 registry 移除时，销毁 GameObject、清理 ID 映射和任务绑定。
        /// </summary>
        protected override void OnRemoved(Identifier id, GameObject value, string? modid)
        {
            // 清理 ID 映射
            if (_questGiverIdIndex.TryGetValue(id, out int questGiverId))
            {
                _reverseIdIndex.Remove(questGiverId);
                _questGiverIdIndex.Remove(id);
            }

            _spawnPositions.Remove(id);
            _spawnRotations.Remove(id);

            // 销毁 GameObject
            if (value != null)
            {
                UnityEngine.Object.Destroy(value);
            }
        }

        // ═══════════════════════════════════════════════════
        //  反射辅助
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 通过反射设置 QuestGiver 的 questGiverID 字段为自定义 int 值。
        /// 游戏原生 QuestGiverID 是 enum，通过 Enum.ToObject 将 int 转换为正确枚举类型。
        /// </summary>
        private static void SetQuestGiverId(QuestGiver qg, int questGiverId)
        {
            var field = typeof(QuestGiver).GetField("questGiverID",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(qg, Enum.ToObject(field.FieldType, questGiverId));
            }
        }
    }
}
