using Duckov.Buildings;
using Duckov.Buildings.UI;
using Duckov.Utilities;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;

using UnityEngine;

namespace FeatherMod.Buildings.Patches
{
    /// <summary>
    /// Harmony Patch 集合，确保自定义建筑在游戏原生 UI 和数据查询中可见。
    /// 当原生 <see cref="BuildingDataCollection"/> 找不到对应的 Info/Prefab 时，
    /// 回退到 FML 的 <see cref="BuildingRegistry"/> 查找。
    /// </summary>
    [HarmonyPatch]
    public static class BuildingCollectionPatch
    {
        /// <summary>缓存 requireBuildings 字段反射信息（一次性）。</summary>
        private static readonly FieldInfo? _requireBuildingsField;
        private static readonly FieldInfo? _requireQuestsField;
        private static readonly FieldInfo? _alternativeForField;

        static BuildingCollectionPatch()
        {
            var infoType = typeof(BuildingInfo);
            _requireBuildingsField = infoType.GetField("requireBuildings",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _requireQuestsField = infoType.GetField("requireQuests",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _alternativeForField = infoType.GetField("alternativeFor",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        /// <summary>
        /// 修复 BuildingInfo 中可能为 null 的数组字段为 Empty，
        /// 防止 RequirementsSatisfied() / BuildingAreaData.Any() 遍历时 NRE。
        /// </summary>
        private static void Sanitize(ref BuildingInfo info)
        {
            if (_requireBuildingsField != null && _requireBuildingsField.GetValue(info) == null)
                _requireBuildingsField.SetValueDirect(__makeref(info), Array.Empty<string>());
            if (_requireQuestsField != null && _requireQuestsField.GetValue(info) == null)
                _requireQuestsField.SetValueDirect(__makeref(info), Array.Empty<int>());
            if (_alternativeForField != null && _alternativeForField.GetValue(info) == null)
                _alternativeForField.SetValueDirect(__makeref(info), Array.Empty<string>());
        }

        [HarmonyPatch(typeof(BuildingDataCollection), "GetInfo")]
        [HarmonyPostfix]
        static void GetInfo_Postfix(string id, ref BuildingInfo __result)
        {
            // 原生已找到有效结果，不干预（BuildingInfo 是值类型，通过 Valid 属性判断）
            if (__result.Valid)
                return;

            // null/空 id 无意义，不搜索（避免匹配到 id 同为 null 的 BuildingInfo）
            if (string.IsNullOrEmpty(id))
                return;

            // 从 BuildingRegistry 回退查找
            var registry = BuildingUtils.Registry;

            // 精确匹配
            foreach (var kvp in registry)
            {
                if (kvp.Value.id == id)
                {
                    __result = kvp.Value;
                    return;
                }
            }

            // 回退：游戏内部可能给 id 加了 "Building_" 前缀，
            // 而 modder 注册时用的是去掉前缀的名称
            const string prefix = "Building_";
            if (id.StartsWith(prefix))
            {
                var stripped = id.Substring(prefix.Length);
                foreach (var kvp in registry)
                {
                    if (kvp.Value.id == stripped)
                    {
                        __result = kvp.Value;
                        return;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(BuildingDataCollection), "GetPrefab")]
        [HarmonyPostfix]
        static void GetPrefab_Postfix(string prefabName, ref Building __result)
        {
            // 原生已找到，不干预
            if (__result != null)
                return;

            // null/空 prefabName 不搜索
            if (string.IsNullOrEmpty(prefabName))
                return;

            // 从 BuildingRegistry 回退查找（同时过滤已被 Destroy 的僵尸引用）
            var registry = BuildingUtils.Registry;

            // 精确匹配
            if (registry.TryGetPrefab(prefabName, out var prefab) && prefab != null)
            {
                __result = prefab;
                return;
            }

            // 回退：去掉 "Building_" 前缀再匹配
            const string prefix = "Building_";
            if (prefabName.StartsWith(prefix)
                && registry.TryGetPrefab(prefabName.Substring(prefix.Length), out prefab)
                && prefab != null)
            {
                __result = prefab;
            }
        }

        [HarmonyPatch(typeof(BuildingSelectionPanel), "GetBuildingsToDisplay")]
        [HarmonyPostfix]
        static void GetBuildingsToDisplay_Postfix(ref BuildingInfo[] __result)
        {
            var registry = BuildingUtils.Registry;
            var customInfos = registry.GetAllInfos().ToArray();
            if (customInfos.Length == 0)
                return;

            // 去重：跳过已存在于原生列表中的条目（防御性保留）
            var existingIds = new System.Collections.Generic.HashSet<string>();
            foreach (var info in __result)
                if (!string.IsNullOrEmpty(info.id))
                    existingIds.Add(info.id);

            var toAdd = new System.Collections.Generic.List<BuildingInfo>();
            for (int i = 0; i < customInfos.Length; i++)
            {
                if (!existingIds.Contains(customInfos[i].id))
                {
                    Sanitize(ref customInfos[i]);
                    toAdd.Add(customInfos[i]);
                }
            }

            if (toAdd.Count > 0)
                __result = __result.Concat(toAdd).ToArray();
        }
    }
}
