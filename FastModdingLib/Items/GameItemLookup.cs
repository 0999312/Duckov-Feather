using FastModdingLib.Utils;

using ItemStatsSystem;

using System.Collections.Generic;

using UnityEngine;

namespace FastModdingLib.Items
{
    /// <summary>
    /// 原版游戏物品反查表。建立 <c>Identifier("duckov", displayName)</c> ↔ <c>int TypeID</c> 的双向映射。
    /// 仅覆盖游戏原生静态物品，不包含 FML 或其他模组动态注册的物品。
    /// </summary>
    public static class GameItemLookup
    {
        private static readonly Dictionary<int, Identifier> _typeIdToIdentifier = new Dictionary<int, Identifier>();
        private static readonly Dictionary<Identifier, int> _identifierToTypeId = new Dictionary<Identifier, int>();
        private static readonly Dictionary<string, List<Identifier>> _tagIndex = new Dictionary<string, List<Identifier>>();
        private static bool _initialized;

        /// <summary>
        /// 扫描 ItemAssetsCollection 中所有原生物品，建立反查表。幂等。
        /// 由 FMLBootstrap.EnsureInit() 在加载阶段调用。
        /// </summary>
        internal static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            const int maxScanTypeId = 50000;
            int found = 0;

            for (int tid = 0; tid < maxScanTypeId; tid++)
            {
                try
                {
                    var entry = ItemAssetsCollection.Instance.GetEntry(tid);
                    if (entry == null) continue;
                }
                catch
                {
                    continue;
                }

                Item prefab;
                try
                {
                    prefab = ItemAssetsCollection.GetPrefab(tid);
                }
                catch
                {
                    continue;
                }
                if (prefab == null) continue;

                string displayName = prefab.DisplayName;
                if (string.IsNullOrEmpty(displayName)) continue;

                string safePath = SanitizePath(displayName);
                var id = new Identifier(FMLConstants.DuckovDomain, safePath);

                if (_identifierToTypeId.ContainsKey(id))
                {
                    id = new Identifier(FMLConstants.DuckovDomain, safePath + "_" + tid);
                }

                _typeIdToIdentifier[tid] = id;
                _identifierToTypeId[id] = tid;

                // 建立标签索引
                if (prefab.Tags != null)
                {
                    foreach (var tag in prefab.Tags)
                    {
                        if (tag == null) continue;
                        if (!_tagIndex.TryGetValue(tag.name, out var list))
                        {
                            list = new List<Identifier>();
                            _tagIndex[tag.name] = list;
                        }
                        list.Add(id);
                    }
                }

                found++;
            }

            Debug.Log($"[GameItemLookup] Indexed {found} vanilla items, {_tagIndex.Count} tags.");
        }

        // ═══════════════════════════════════════════════════
        //  Public Discovery API（modder 可见）
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 将物品 displayName 转换为 duckov Identifier。
        /// 如 <c>"AK-47"</c> → <c>Identifier("duckov", "AK-47")</c>。
        /// 返回 false 表示该名称无对应原版物品（可能不存在或 displayName 映射失败）。
        /// </summary>
        public static bool TryGetIdentifier(string displayName, out Identifier id)
        {
            string safePath = SanitizePath(displayName);
            var candidate = new Identifier(FMLConstants.DuckovDomain, safePath);
            if (_identifierToTypeId.ContainsKey(candidate))
            {
                id = candidate;
                return true;
            }
            id = default;
            return false;
        }

        /// <summary>
        /// 按标签搜索原版物品，返回所有匹配的 duckov Identifier。
        /// 如 <c>"Gun"</c> → 所有枪械的 Identifier 列表。
        /// </summary>
        public static bool TryFindByTag(string tag, out IReadOnlyList<Identifier> results)
        {
            if (_tagIndex.TryGetValue(tag, out var list))
            {
                results = list.AsReadOnly();
                return true;
            }
            results = System.Array.Empty<Identifier>();
            return false;
        }

        /// <summary>
        /// 获取所有已索引的原版物品 Identifier 列表。
        /// </summary>
        public static IReadOnlyList<Identifier> GetAllIdentifiers()
        {
            var list = new List<Identifier>(_identifierToTypeId.Keys);
            return list.AsReadOnly();
        }

        /// <summary>
        /// 获取已索引的原版物品总数。
        /// </summary>
        public static int Count => _identifierToTypeId.Count;

        // ═══════════════════════════════════════════════════
        //  Public API（modder 可见）
        // ═══════════════════════════════════════════════════

        /// <summary>按原版 TypeID 反查 duckov Identifier。如 1001 → Identifier("duckov", "AK-47")。</summary>
        public static bool TryGetIdentifier(int typeId, out Identifier id)
        {
            return _typeIdToIdentifier.TryGetValue(typeId, out id);
        }

        // ═══════════════════════════════════════════════════
        //  Internal API（FML 内部使用）
        // ═══════════════════════════════════════════════════

        /// <summary>按 duckov Identifier 解析为原版 TypeID。</summary>
        internal static bool TryResolve(Identifier id, out int typeId)
        {
            return _identifierToTypeId.TryGetValue(id, out typeId);
        }

        /// <summary>检查给定 Identifier 是否指向已知的原版物品。</summary>
        internal static bool IsVanillaItem(Identifier id)
        {
            return _identifierToTypeId.ContainsKey(id);
        }

        /// <summary>
        /// 清理字符串使其适合作为 Identifier path。
        /// </summary>
        private static string SanitizePath(string raw)
        {
            return raw
                .Replace("\\", "_")
                .Replace(":", "_")
                .Replace("..", "__")
                .Replace("/", "_");
        }
    }
}
