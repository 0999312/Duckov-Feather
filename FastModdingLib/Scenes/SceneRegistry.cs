using FeatherMod.Register;
using FeatherMod.Utils;
using System.Collections.Generic;

namespace FeatherMod
{
    /// <summary>
    /// 场景注册表。维护 Identifier → 游戏原生 sceneID 映射。
    /// </summary>
    public sealed class SceneRegistry : SimpleRegistry<object>
    {
        /// <summary>Identifier → 游戏原生 sceneID 映射。</summary>
        private readonly Dictionary<Identifier, string> _sceneIdMap = new Dictionary<Identifier, string>();

        /// <summary>sceneID → Identifier 反向映射。</summary>
        private readonly Dictionary<string, Identifier> _reverseMap = new Dictionary<string, Identifier>();

        /// <summary>注册场景映射。</summary>
        public void Register(Identifier id, string sceneId, string modid)
        {
            // 使用 null 作为占位值（场景本身不是 Unity 对象）
            Set(id, null!, modid);
            _sceneIdMap[id] = sceneId;
            _reverseMap[sceneId] = id;
        }

        /// <summary>按 Identifier 解析游戏原生 sceneID。</summary>
        public bool TryResolve(Identifier id, out string sceneId)
        {
            return _sceneIdMap.TryGetValue(id, out sceneId);
        }

        /// <summary>按游戏原生 sceneID 反查 Identifier。</summary>
        public bool TryGetIdentifier(string sceneId, out Identifier id)
        {
            return _reverseMap.TryGetValue(sceneId, out id);
        }

        protected override void OnRemoved(Identifier id, object value, string? modid)
        {
            if (_sceneIdMap.TryGetValue(id, out var sceneId))
            {
                _sceneIdMap.Remove(id);
                _reverseMap.Remove(sceneId);
            }
        }

        public new void Clear()
        {
            _sceneIdMap.Clear();
            _reverseMap.Clear();
            base.Clear();
        }
    }
}
