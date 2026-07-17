using Duckov.NoteIndexs;
using FeatherMod.Register;
using FeatherMod.Utils;
using System.Collections.Generic;

namespace FeatherMod
{
    /// <summary>
    /// 笔记注册表。维护 Identifier → Note 主映射和 Identifier → key 反向索引。
    /// OnRemoved 时从 <see cref="NoteIndex.Notes"/> 列表中移除笔记条目。
    /// </summary>
    public sealed class NoteRegistry : SimpleRegistry<Note>
    {
        /// <summary>Identifier → 游戏原生 key 映射。</summary>
        private readonly Dictionary<Identifier, string> _keyIndex = new Dictionary<Identifier, string>();

        /// <summary>
        /// 注册笔记（写入主字典 + owner 索引 + key 索引 + 游戏原生列表）。
        /// </summary>
        public void Register(Identifier id, Note note, string modid)
        {
            Set(id, note, modid);
            _keyIndex[id] = note.key;

            // 注入到游戏原生 NoteIndex（如果 NoteIndex 已就绪）
            if (NoteIndex.Instance != null)
            {
                NoteIndex.Instance.Notes.Add(note);
                NoteIndex.SetNoteDynamic(note);
            }
        }

        /// <summary>按 Identifier 查询游戏原生 key。</summary>
        public bool TryGetKey(Identifier id, out string key)
        {
            return _keyIndex.TryGetValue(id, out key);
        }

        /// <summary>获取全部已注册的 key（供 Patch 层遍历）。</summary>
        public IEnumerable<KeyValuePair<Identifier, string>> GetAllKeys()
        {
            foreach (var kvp in _keyIndex)
            {
                yield return kvp;
            }
        }

        protected override void OnRemoved(Identifier id, Note value, string? modid)
        {
            _keyIndex.Remove(id);

            // 从游戏原生 notes 列表移除
            if (NoteIndex.Instance != null && value != null)
            {
                NoteIndex.Instance.Notes.Remove(value);
            }
        }

        public new void Clear()
        {
            _keyIndex.Clear();
            base.Clear();
        }
    }
}
