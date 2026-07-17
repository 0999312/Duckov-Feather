using Duckov.Buildings;
using Duckov.Utilities;
using FeatherMod.Register;
using FeatherMod.Utils;
using System.Collections.Generic;
using System.Linq;

namespace FeatherMod
{
    /// <summary>
    /// 建筑注册表。维护 Identifier → BuildingInfo 主映射和 id → prefab 字典。
    /// OnRemoved 时从 <see cref="BuildingDataCollection"/> 的 infos/prefabs 列表移除。
    /// </summary>
    public sealed class BuildingRegistry : SimpleRegistry<BuildingInfo>
    {
        private readonly Dictionary<string, Building> _prefabs;

        public BuildingRegistry()
        {
            _prefabs = new Dictionary<string, Building>();
        }

        /// <summary>注册建筑（写入主字典 + owner 索引 + prefab 索引）。</summary>
        public void Register(Identifier id, BuildingInfo info, Building prefab, string modid)
        {
            Set(id, info, modid);
            // 以 prefabName 为主 key（游戏通过 prefabName 查找），id 为备用
            _prefabs[info.prefabName] = prefab;
            if (info.id != info.prefabName && !string.IsNullOrEmpty(info.id))
                _prefabs[info.id] = prefab;
        }

        /// <summary>
        /// 按建筑 id 或 prefabName 查找预制体。
        /// 先按 buildingId 精确匹配；失败时遍历所有已注册 BuildingInfo 按 prefabName 回退匹配。
        /// </summary>
        public bool TryGetPrefab(string buildingId, out Building prefab)
        {
            // 精确匹配
            if (_prefabs.TryGetValue(buildingId, out prefab!))
                return true;

            // 回退：按 BuildingInfo.prefabName 遍历匹配
            // 适用于 modder 设置了 id ≠ prefabName 的场景
            if (!string.IsNullOrEmpty(buildingId))
            {
                foreach (var kvp in this)
                {
                    if (kvp.Value.prefabName == buildingId && kvp.Value.Valid)
                    {
                        if (_prefabs.TryGetValue(kvp.Value.prefabName, out prefab!))
                            return true;
                    }
                }
            }
            return false;
        }

        /// <summary>获取全部已注册的 BuildingInfo（供 Patch 层遍历）。</summary>
        public IEnumerable<BuildingInfo> GetAllInfos()
        {
            foreach (var kvp in this)
            {
                yield return kvp.Value;
            }
        }

        protected override void OnRemoved(Identifier id, BuildingInfo value, string? modid)
        {
            var collection = GameplayDataSettings.BuildingDataCollection;
            if (collection == null) return;

            // 从 infos 列表移除
            collection.infos?.Remove(value);

            // 从 prefabs 列表移除（同时清除 prefabName 和 id 两个 key）
            if (_prefabs.TryGetValue(value.prefabName, out var prefab))
            {
                collection.prefabs?.Remove(prefab);
                _prefabs.Remove(value.prefabName);
                // DontDestroyOnLoad 保护的对象需显式销毁
                if (prefab != null)
                    UnityEngine.Object.Destroy(prefab.gameObject);
            }
            if (value.prefabName != value.id && _prefabs.TryGetValue(value.id, out prefab))
            {
                collection.prefabs?.Remove(prefab);
                _prefabs.Remove(value.id);
                if (prefab != null)
                    UnityEngine.Object.Destroy(prefab.gameObject);
            }
        }

        public new void Clear()
        {
            _prefabs.Clear();
            base.Clear();
        }
    }
}
