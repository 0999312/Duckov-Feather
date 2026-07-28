using Duckov.Quests;
using Duckov.Utilities;
using FeatherMod.Events;
using FeatherMod.Events.GameEvents;
using FeatherMod.Register;
using FeatherMod.Saves;
using FeatherMod.Utils;
using SodaCraft.Localizations;
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
    /// <remarks>
    /// <para><b>ID 持久化</b>：Identifier → int 映射通过 <see cref="SaveUtils"/>
    /// 持久化到当前存档槽位的 <c>.sav</c> 文件（键 <c>feather:questgiver_id_map</c>），
    /// 而非全局 JSON。同一 Identifier 在同一槽位始终获得相同 int ID，
    /// 使 <c>Character_{int}</c> 本地化键跨会话稳定；删除存档时映射随槽位
    /// <c>.sav</c> 一起被擦除，不会泄漏到其他槽位。</para>
    /// <para><b>存档删除</b>：订阅 <see cref="SaveDeletedEvent"/>，删除槽位时
    /// 清空内存索引与持久化缓存，避免新存档继承旧档 ID 映射导致 Quest 链断裂。</para>
    /// </remarks>
    public sealed class QuestGiverRegistry : SimpleRegistry<GameObject>
    {
        public const int MinCustomQuestGiverId = 50;

        private readonly Dictionary<Identifier, int> _questGiverIdIndex = new Dictionary<Identifier, int>();
        private readonly Dictionary<int, Identifier> _reverseIdIndex = new Dictionary<int, Identifier>();
        private readonly Dictionary<int, string> _displayNameKeyCache = new Dictionary<int, string>();
        private int _nextQuestGiverId = MinCustomQuestGiverId;

        // ── ID 持久化（按存档槽位）──
        // 游戏对 QuestGiver 显示名使用本地化键 Character_{questGiverID}。
        // questGiverID 由框架动态分配（50 起递增），若每次会话都变化，modder
        // 无法为该键编写稳定翻译。此处把 Identifier → int 映射通过 SaveUtils
        // 持久化到当前存档槽位的 .sav 文件，保证同一 Identifier 在同一槽位
        // 始终分配相同 int ID，使 Character_{int} 键跨会话稳定。
        // 删除存档时映射随槽位 .sav 一起被擦除，不会泄漏到其他槽位。
        private static readonly Identifier PersistSaveId = new Identifier(FMLConstants.Domain, "questgiver_id_map");

        private PersistData? _persisted;
        private bool _persistLoaded;
        private bool _saveDeletedHooked;

        [Serializable]
        public class PersistData
        {
            public int nextId = MinCustomQuestGiverId;
            public List<PersistEntry> entries = new List<PersistEntry>();
        }

        [Serializable]
        public struct PersistEntry
        {
            public string id;
            public int intId;
        }

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
            EnsurePersistLoaded();
            EnsureSaveDeletedHooked();

            // 重复注册同一 Identifier：直接复用已有 ID（避免本地化键跳变）
            if (_questGiverIdIndex.TryGetValue(id, out int existing))
                return existing;

            string key = id.ToString();

            // 持久化恢复：同一 Identifier 在同一存档槽位跨会话保持相同 int ID
            if (_persisted != null)
            {
                foreach (var e in _persisted.entries)
                {
                    if (e.id == key && !_reverseIdIndex.ContainsKey(e.intId))
                    {
                        _questGiverIdIndex[id] = e.intId;
                        _reverseIdIndex[e.intId] = id;
                        if (e.intId >= _nextQuestGiverId) _nextQuestGiverId = e.intId + 1;
                        return e.intId;
                    }
                }
            }

            int questGiverId = _nextQuestGiverId++;
            while (_reverseIdIndex.ContainsKey(questGiverId))
                questGiverId = _nextQuestGiverId++;

            _questGiverIdIndex[id] = questGiverId;
            _reverseIdIndex[questGiverId] = id;
            _persisted ??= new PersistData();
            _persisted.entries.Add(new PersistEntry { id = key, intId = questGiverId });
            SavePersisted();

            return questGiverId;
        }

        // ═══════════════════════════════════════════════════
        //  ID 持久化读写（按存档槽位，走 SaveUtils / ES3）
        // ═══════════════════════════════════════════════════

        private void EnsurePersistLoaded()
        {
            if (_persistLoaded) return;
            _persistLoaded = true;
            try
            {
                _persisted = SaveUtils.Load<PersistData>(PersistSaveId);
                if (_persisted != null && _persisted.nextId > _nextQuestGiverId)
                    _nextQuestGiverId = _persisted.nextId;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FML QuestGiver] Failed to load persisted ID map: {ex.Message}");
            }
        }

        private void SavePersisted()
        {
            if (!_persistLoaded || _persisted == null) return;
            try
            {
                _persisted.nextId = _nextQuestGiverId;
                SaveUtils.Save(PersistSaveId, _persisted);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FML QuestGiver] Failed to persist ID map: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════
        //  存档删除清理
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 首次注册时订阅 <see cref="SaveDeletedEvent"/>（幂等 EnsureHook 模式，
        /// 参照 PerkTreeUtils.HookOnSetFileCleanup / BuildingUtils.HookSceneLoadEvent）。
        /// </summary>
        private void EnsureSaveDeletedHooked()
        {
            if (_saveDeletedHooked) return;
            _saveDeletedHooked = true;
            EventBusManager.Instance.Sync.Register<SaveDeletedEvent>(OnSaveDeleted);
        }

        /// <summary>
        /// 存档槽位被删除时清空内存索引与持久化缓存，避免新存档继承旧档 ID 映射
        /// 导致 Quest 链断裂。磁盘上的 <c>.sav</c> 已由 SavesSystem 删除，
        /// 此处仅清理内存状态。
        /// </summary>
        private void OnSaveDeleted(SaveDeletedEvent evt)
        {
            _questGiverIdIndex.Clear();
            _reverseIdIndex.Clear();
            _displayNameKeyCache.Clear();
            _nextQuestGiverId = MinCustomQuestGiverId;
            _persisted = null;
            _persistLoaded = false;
        }

        /// <summary>
        /// 缓存 QuestGiver 的显示名本地化键，供语言切换时重新解析。
        /// 由 <see cref="QuestGiverUtils.RegisterQuestGiver"/> 内部调用。
        /// </summary>
        internal void CacheDisplayNameKey(int questGiverId, string displayNameKey)
        {
            _displayNameKeyCache[questGiverId] = displayNameKey;
        }

        /// <summary>
        /// 刷新所有已缓存 QuestGiver 的本地化重定向。
        /// 在语言切换时调用，确保 <c>Character_{ID}</c> 显示名随语言更新。
        /// </summary>
        internal void RefreshDisplayNameOverrides()
        {
            foreach (var kvp in _displayNameKeyCache)
            {
                var displayText = kvp.Value.ToPlainText();
                if (!displayText.StartsWith("*") || !displayText.EndsWith("*"))
                {
                    LocalizationManager.SetOverrideText($"Character_{kvp.Key}", displayText);
                }
            }
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
    }
}
