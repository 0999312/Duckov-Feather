using Duckov.Quests;
using Duckov.Utilities;
using FeatherMod.Register;
using FeatherMod.Utils;
using SodaCraft.Localizations;
using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly Dictionary<int, string> _displayNameKeyCache = new Dictionary<int, string>();
        private int _nextQuestGiverId = MinCustomQuestGiverId;

        // ── ID 持久化 ──
        // 游戏对 QuestGiver 显示名使用本地化键 Character_{questGiverID}。
        // questGiverID 由框架动态分配（50 起递增），若每次会话都变化，modder
        // 无法为该键编写稳定翻译。此处把 Identifier → int 映射持久化到全局文件
        // （跨存档槽共享），保证同一 Identifier 永远分配到相同 int ID，
        // 使 Character_{int} 键跨会话稳定。
        private static string PersistFilePath =>
            System.IO.Path.Combine(Application.persistentDataPath, "FML", "questgiver_id_map.json");
        private Dictionary<string, int>? _persisted;
        private bool _persistLoaded;

        [Serializable]
        private class PersistData
        {
            public int nextId = MinCustomQuestGiverId;
            public List<PersistEntry> entries = new List<PersistEntry>();
        }

        [Serializable]
        private struct PersistEntry
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

            // 重复注册同一 Identifier：直接复用已有 ID（避免本地化键跳变）
            if (_questGiverIdIndex.TryGetValue(id, out int existing))
                return existing;

            // 持久化恢复：同一 Identifier 跨会话保持相同 int ID
            string key = id.ToString();
            if (_persisted!.TryGetValue(key, out int persistedId) && !_reverseIdIndex.ContainsKey(persistedId))
            {
                _questGiverIdIndex[id] = persistedId;
                _reverseIdIndex[persistedId] = id;
                if (persistedId >= _nextQuestGiverId) _nextQuestGiverId = persistedId + 1;
                return persistedId;
            }

            int questGiverId = _nextQuestGiverId++;
            while (_reverseIdIndex.ContainsKey(questGiverId))
                questGiverId = _nextQuestGiverId++;

            _questGiverIdIndex[id] = questGiverId;
            _reverseIdIndex[questGiverId] = id;
            _persisted[key] = questGiverId;
            SavePersisted();

            return questGiverId;
        }

        // ═══════════════════════════════════════════════════
        //  ID 持久化读写
        // ═══════════════════════════════════════════════════

        private void EnsurePersistLoaded()
        {
            if (_persistLoaded) return;
            _persistLoaded = true;
            _persisted = new Dictionary<string, int>();
            try
            {
                if (File.Exists(PersistFilePath))
                {
                    var data = JsonUtility.FromJson<PersistData>(File.ReadAllText(PersistFilePath));
                    if (data != null)
                    {
                        if (data.nextId > _nextQuestGiverId) _nextQuestGiverId = data.nextId;
                        foreach (var e in data.entries)
                            _persisted[e.id] = e.intId;
                    }
                }
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
                var data = new PersistData { nextId = _nextQuestGiverId };
                foreach (var kvp in _persisted)
                    data.entries.Add(new PersistEntry { id = kvp.Key, intId = kvp.Value });
                var dir = Path.GetDirectoryName(PersistFilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(PersistFilePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FML QuestGiver] Failed to persist ID map: {ex.Message}");
            }
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
