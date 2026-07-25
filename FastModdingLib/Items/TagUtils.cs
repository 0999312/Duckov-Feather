using Duckov.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FeatherMod.Items
{
    /// <summary>
    /// Tags 是 FML 中唯一不走 Identifier 的系统——所有 Tag 均视为 Common Tag，
    /// 以纯字符串名称标识，Identifier 在此系统上无意义。
    /// </summary>
    public static class TagUtils
    {
        /// <summary>运行时创建的 Tag 缓存。key = tag name，value = Tag 实例。</summary>
        private static readonly Dictionary<string, Tag> _customTags = new(StringComparer.OrdinalIgnoreCase);

        // ═══════════════════════════════════════════════════
        //  Public API — 数据驱动注册
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 注册（创建）一个 Tag。数据驱动，用于人工添加 Tag 和合成台 Tag 配置。
        /// 若 Tag 已存在（原生或已注册），返回已有实例；否则创建新的 ScriptableObject 并注册到
        /// <see cref="GameplayDataSettings.Tags"/>。
        /// </summary>
        /// <param name="tagName">标签名称（大小写不敏感，作为 ScriptableObject.name）。</param>
        /// <param name="config">可选配置：show、color、priority 等。为 null 时使用默认值。</param>
        /// <returns>Tag 实例。</returns>
        /// <example>
        /// <code>
        /// // 简单注册
        /// TagUtils.RegisterTag("DrinkStation");
        ///
        /// // 带配置注册
        /// TagUtils.RegisterTag("CoffeeBean", new TagConfig
        /// {
        ///     Show = true,
        ///     Color = new Color(0.6f, 0.3f, 0.1f),
        ///     Priority = 10,
        /// });
        /// </code>
        /// </example>
        public static Tag RegisterTag(string tagName, TagConfig? config = null)
        {
            if (string.IsNullOrEmpty(tagName))
                throw new ArgumentNullException(nameof(tagName));

            // 1. 检查是否已存在
            var existing = FindExisting(tagName);
            if (existing != null)
            {
                // 如果提供了配置，更新已有 Tag 的字段
                if (config.HasValue)
                    ApplyConfig(existing, config.Value);
                return existing;
            }

            // 2. 创建新 Tag ScriptableObject
            Tag tag;
            try
            {
                tag = ScriptableObject.CreateInstance<Tag>();
                tag.name = tagName;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(
                    $"[TagUtils] Failed to create Tag '{tagName}'. " +
                    $"Ensure Tag type is accessible (ScriptableObject). Error: {e.Message}", e);
            }

            // 3. 应用配置
            if (config.HasValue)
                ApplyConfig(tag, config.Value);

            // 4. 注册到游戏原生 Tag 数据库
            RegisterToNative(tag);

            // 5. 缓存
            _customTags[tagName] = tag;
            Debug.Log($"[TagUtils] Registered Tag: '{tagName}' (show={tag.Show}, priority={tag.Priority}, color={tag.Color})");
            return tag;
        }

        /// <summary>
        /// 查找指定名称的 Tag（仅查询，不创建）。
        /// 先查游戏原生数据库，再查自定义缓存。
        /// </summary>
        /// <returns>Tag 实例，未找到时返回 null。</returns>
        public static Tag? GetTag(string tagName)
        {
            if (string.IsNullOrEmpty(tagName)) return null;
            return FindExisting(tagName);
        }

        /// <summary>
        /// 检查指定名称的 Tag 是否存在。
        /// </summary>
        public static bool TagExists(string tagName)
        {
            if (string.IsNullOrEmpty(tagName)) return false;
            return FindExisting(tagName) != null;
        }

        /// <summary>
        /// 获取自定义注册的 Tag 名称列表（快照）。
        /// </summary>
        public static IReadOnlyList<string> GetCustomTagNames()
        {
            var names = new string[_customTags.Count];
            _customTags.Keys.CopyTo(names, 0);
            return names;
        }

        // ═══════════════════════════════════════════════════
        //  Internal
        // ═══════════════════════════════════════════════════

        /// <summary>查找已存在的 Tag（先原生，后自定义缓存）。</summary>
        private static Tag? FindExisting(string tagName)
        {
            // 原生数据库查找
            try
            {
                if (GameplayDataSettings.Tags != null)
                {
                    // 优先通过 AllTags 遍历匹配（TagCollection 有 public List<Tag> list）
                    var allTags = GameplayDataSettings.Tags.AllTags;
                    if (allTags != null)
                    {
                        var match = allTags.FirstOrDefault(t => t != null && t.name == tagName);
                        if (match != null) return match;
                    }
                }
            }
            catch { }

            // 自定义缓存查找
            if (_customTags.TryGetValue(tagName, out var cached))
                return cached;

            return null;
        }

        /// <summary>应用 TagConfig 到 Tag 实例（通过 Publicizer 公开的字段）。</summary>
        private static void ApplyConfig(Tag tag, TagConfig config)
        {
            tag.priority = config.Priority;
            tag.show = config.Show;
            tag.showDescription = config.ShowDescription;
            tag.color = config.Color != null ? config.Color.Value : Color.gray;
        }

        /// <summary>将 Tag 注册到游戏原生 GameplayDataSettings.Tags。</summary>
        private static void RegisterToNative(Tag tag)
        {
            if (GameplayDataSettings.Tags == null)
            {
                Debug.LogWarning($"[TagUtils] GameplayDataSettings.Tags is null, Tag '{tag.name}' will be available via custom cache only.");
                return;
            }
            GameplayDataSettings.Tags.allTags.Add(tag);
        }

        /// <summary>清理所有自定义 Tag。由 FML 自动卸载流程调用。</summary>
        internal static void ClearCustomTags()
        {
            foreach (var kvp in _customTags)
            {
                try
                {
                    if (kvp.Value != null)
                        UnityEngine.Object.Destroy(kvp.Value);
                }
                catch { }
            }
            _customTags.Clear();
        }
    }

    /// <summary>
    /// Tag 创建配置。用于 <see cref="TagUtils.RegisterTag"/> 的数据驱动参数。
    /// 所有字段均为可选，未设置时使用 Tag 默认值。
    /// </summary>
    public struct TagConfig
    {
        /// <summary>是否在物品 Tooltip 中显示此 Tag。默认 false。</summary>
        public bool Show = true;

        /// <summary>是否显示 Tag 的描述文本。默认 false。</summary>
        public bool ShowDescription = false;

        /// <summary>Tag 图标/文字颜色。默认 Color.black。</summary>
        public Color? Color;

        /// <summary>Tag 显示优先级（数值越大越靠前）。默认 0。</summary>
        public int Priority = 0;

        public TagConfig()
        {
        }
    }
}
