using Duckov.Endowment;
using FeatherMod.Register;
using FeatherMod.Utils;
using ItemStatsSystem.Stats;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// 天赋系统公共 API。所有注册、查询、选择操作均使用 <see cref="Identifier"/>
    /// 作为资源标识符。<see cref="EndowmentIndex"/> 枚举值由 FML 内部自动分配，
    /// modder 不直接接触。
    /// </summary>
    public static class EndowmentUtils
    {
        private static readonly EndowmentRegistry _endowmentRegistry = new EndowmentRegistry();
        private static bool _initialized;

        /// <summary>暴露给 RegisterBootstrap 和 Patch 层用于注册到元表和查询。</summary>
        public static EndowmentRegistry Registry => _endowmentRegistry;

        /// <summary>
        /// 初始化：将 EndowmentRegistry 注册到 <see cref="RegistryManager.Registry"/> 元表。
        /// 由 <c>RegisterBootstrap.Init()</c> 调用（幂等）。
        /// </summary>
        internal static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            var meta = RegistryManager.Instance.Registry;
            var id = new Identifier(FMLConstants.Domain, "endowment");
            if (meta is NonAlterableSimpleRegistry<ERegistry> nonAlt)
                nonAlt.SetIfAbsent(id, _endowmentRegistry, RegistryManager.CurrentModid);
            else
                meta.Set(id, _endowmentRegistry, RegistryManager.CurrentModid);
        }

        // ===== 注册 / 卸载（Identifier 优先） =====

        /// <summary>
        /// 【推荐】注册自定义天赋——modder 用纯 FML DTO 配置，无需接触游戏内部类型。
        /// FML 内部负责将 <see cref="EndowmentConfig"/> 转换为游戏原生的 <see cref="EndowmentEntry"/>。
        /// </summary>
        /// <param name="id">天赋 Identifier（Domain=modid, Path=天赋名称）。</param>
        /// <param name="config">天赋配置 DTO。参见 <see cref="EndowmentConfig"/>。</param>
        /// <param name="modid">注册者 mod 标识；null 时从 id.Domain 推导。</param>
        /// <example>
        /// <code>
        /// EndowmentUtils.RegisterEndowment(
        ///     new Identifier("mymod", "assassin"),
        ///     new EndowmentConfig
        ///     {
        ///         Modifiers = new[]
        ///         {
        ///             new EndowmentModifier { StatKey = "moveSpeed", Type = ModifierType.PercentageAdd, Value = 0.15f },
        ///             new EndowmentModifier { StatKey = "maxHealth", Type = ModifierType.PercentageAdd, Value = -0.1f }
        ///         },
        ///         UnlockedByDefault = false,
        ///         RequirementTextKey = "endowment_assassin_requirement"
        ///     });
        /// </code>
        /// </example>
        public static void RegisterEndowment(Identifier id, EndowmentConfig config, string? modid = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            Init();
            string owner = modid ?? id.Domain;
            var entry = CreateNativeEntry(config, id);
            _endowmentRegistry.Set(id, entry, owner);

            // 立即尝试注入到 EndowmentManager（处理 Awake 早于 PatchAll 的时序场景）
            _endowmentRegistry.TryInjectToManager(id, entry);
        }

        /// <summary>
        /// 【已废弃】直接注册 EndowmentEntry 实例。
        /// 请改用 <see cref="RegisterEndowment(Identifier, EndowmentConfig, string?)"/>。
        /// </summary>
        [Obsolete("Use RegisterEndowment(Identifier, EndowmentConfig, string?) instead.")]
        public static void RegisterEndowment(Identifier id, EndowmentEntry endowment, string? modid = null)
        {
            Init();
            string owner = modid ?? id.Domain;
            _endowmentRegistry.Set(id, endowment, owner);

            // 旧 API 同样需要支持延迟注入
            _endowmentRegistry.TryInjectToManager(id, endowment);
        }

        /// <summary>
        /// 【已废弃】通过 object[] 注册天赋。
        /// 请改用 <see cref="RegisterEndowment(Identifier, EndowmentConfig, string?)"/>。
        /// </summary>
        [Obsolete("Use RegisterEndowment(Identifier, EndowmentConfig, string?) instead.")]
        public static void RegisterEndowment(
            Identifier id,
            object[] modifiers,
            bool unlockedByDefault = false,
            string requirementText = "",
            string? modid = null)
        {
            Init();
            string owner = modid ?? id.Domain;

            var config = new EndowmentConfig
            {
                Modifiers = Array.ConvertAll(modifiers ?? Array.Empty<object>(), m =>
                {
                    // 尝试从游戏原生 ModifierDescription 转换
                    var modType = m.GetType();
                    return new EndowmentModifier
                    {
                        StatKey = (string)modType.GetField("statKey")?.GetValue(m) ?? "",
                        Type = (ItemStatsSystem.Stats.ModifierType)(modType.GetField("type")?.GetValue(m) ?? 0),
                        Value = (float)(modType.GetField("value")?.GetValue(m) ?? 0f)
                    };
                }),
                UnlockedByDefault = unlockedByDefault,
                RequirementTextKey = requirementText
            };
            var entry = CreateNativeEntry(config, id);
            _endowmentRegistry.Set(id, entry, owner);
        }

        /// <summary>
        /// 【兜底】使用强指定的 EndowmentIndex 注册天赋。
        /// 仅在需要与既有游戏内容共享枚举空间时使用。
        /// </summary>
        public static void RegisterEndowmentWithIndex(Identifier id, EndowmentEntry endowment,
            EndowmentIndex explicitIndex, string modid)
        {
            Init();
            _endowmentRegistry.Set(id, endowment, modid);
        }

        /// <summary>按 Identifier 移除已注册的天赋。</summary>
        public static bool UnregisterEndowment(Identifier id) => _endowmentRegistry.Remove(id);

        /// <summary>批量卸载指定 mod 注册的全部天赋。</summary>
        public static int UnregisterAllEndowments(string modid) => _endowmentRegistry.RemoveAllByOwner(modid);

        // ===== 查询（全部走 Identifier） =====

        /// <summary>获取已注册的天赋，未找到时返回 null。</summary>
        public static EndowmentEntry? GetEndowment(Identifier id)
        {
            return _endowmentRegistry.TryGet(id, out var entry) ? entry : null;
        }

        /// <summary>安全查询已注册的天赋。</summary>
        public static bool TryGetEndowment(Identifier id, out EndowmentEntry entry)
            => _endowmentRegistry.TryGet(id, out entry);

        /// <summary>列出指定 mod 注册的全部天赋 Identifier。</summary>
        public static IReadOnlyList<Identifier> GetAllEndowments(string modid)
        {
            return _endowmentRegistry.GetAllByOwner(modid);
        }

        // ===== 状态操作（Identifier → 内部映射到 EndowmentIndex） =====

        /// <summary>查询天赋是否已解锁。内部从 Identifier 映射到 EndowmentIndex 后调原生 API。</summary>
        public static bool IsEndowmentUnlocked(Identifier id)
        {
            if (!_endowmentRegistry.TryGetIndex(id, out var index)) return false;
            return EndowmentManager.GetEndowmentUnlocked(index);
        }

        /// <summary>解锁天赋。内部从 Identifier 映射到 EndowmentIndex 后调原生 API。</summary>
        public static bool UnlockEndowment(Identifier id)
        {
            if (!_endowmentRegistry.TryGetIndex(id, out var index)) return false;
            return EndowmentManager.UnlockEndowment(index);
        }

        /// <summary>选择/激活天赋。内部从 Identifier 映射到 EndowmentIndex 后调原生 SelectIndex。</summary>
        public static void SelectEndowment(Identifier id)
        {
            if (!_endowmentRegistry.TryGetIndex(id, out var index)) return;
            EndowmentManager.Instance.SelectIndex(index);
        }

        /// <summary>返回当前选中的天赋 Identifier，未选中时返回 null。</summary>
        public static Identifier? GetCurrentSelection()
        {
            var idx = EndowmentManager.CurrentIndex;
            if (_endowmentRegistry.TryGetIdentifier(idx, out var id))
                return id;

            return null;
        }

        // ===== 内部：DTO → 游戏原生类型转换（利用 Publicizer 公开的字段直接访问） =====

        /// <summary>
        /// 将 FML DTO 转换为游戏原生的 EndowmentEntry。
        /// 此方法利用了 Publicizer 公开的 <c>EndowmentEntry</c> private 字段，
        /// 无需反射。modder 永远不接触此方法。
        /// </summary>
        internal static EndowmentEntry CreateNativeEntry(EndowmentConfig config, Identifier id)
        {
            var go = new GameObject($"Endowment_{id.Path}");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var entry = go.AddComponent<EndowmentEntry>();

            // 利用 Publicizer 公开的字段直接赋值
            entry.requirementTextKey = config.RequirementTextKey;
            entry.unlockedByDefault = config.UnlockedByDefault;

            // 设置图标（null 时使用游戏默认图标）
            if (config.Icon != null)
                entry.icon = config.Icon;

            // 转换 EndowmentModifier[] → EndowmentEntry.ModifierDescription[]
            var nativeModifiers = new EndowmentEntry.ModifierDescription[config.Modifiers.Length];
            for (int i = 0; i < config.Modifiers.Length; i++)
            {
                nativeModifiers[i] = new EndowmentEntry.ModifierDescription
                {
                    statKey = config.Modifiers[i].StatKey,
                    type = config.Modifiers[i].Type,
                    value = config.Modifiers[i].Value
                };
            }
            entry.modifiers = nativeModifiers;

            return entry;
        }
    }
}
