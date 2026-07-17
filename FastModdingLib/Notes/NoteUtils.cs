using Duckov.NoteIndexs;
using Duckov.UI;
using FeatherMod.Events;
using FeatherMod.Register;
using FeatherMod.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// 笔记系统公共 API。提供笔记的注册、解锁、查询和世界空间拾取物生成。
    /// 所有 public API 使用 <see cref="Identifier"/> 作为笔记标识符。
    /// </summary>
    /// <example>
    /// <code>
    /// NoteUtils.RegisterNote(
    ///     new Identifier("mymod", "lore_01"),
    ///     new NoteConfig
    ///     {
    ///         TitleKey = "Note_lore_01_Title",
    ///         ContentKey = "Note_lore_01_Content",
    ///         Hidden = false
    ///     });
    /// NoteUtils.Unlock(new Identifier("mymod", "lore_01"));
    /// </code>
    /// </example>
    public static class NoteUtils
    {
        private static NoteRegistry _registry;
        private static bool _initialized;

        public static NoteRegistry Registry => _registry;

        /// <summary>
        /// 初始化笔记模块（幂等）。将注册表注册到 <see cref="RegistryManager"/> 元表。
        /// </summary>
        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            _registry = new NoteRegistry();
            RegistryManager.Instance.Registry.SetIfAbsent(
                new Identifier(FMLConstants.Domain, "note"),
                _registry,
                RegistryManager.CurrentModid);

            // 应用 Harmony 补丁（事件桥接）
            NoteEventPatch.EnsurePatched();
        }

        /// <summary>
        /// 注册一条笔记到 FML 注册表并注入到游戏原生 <see cref="NoteIndex"/>。
        /// </summary>
        /// <param name="id">笔记标识符。Path 将作为游戏原生 key。</param>
        /// <param name="config">笔记内容配置。</param>
        /// <param name="modid">所属 mod 标识，默认使用 id.Domain。</param>
        public static void RegisterNote(Identifier id, NoteConfig config, string? modid = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            Init();
            string owner = modid ?? id.Domain;

            var note = CreateNativeNote(id, config);
            _registry.Register(id, note, owner);

            // 延迟注入：若 NoteIndex 尚未就绪（游戏启动时序），
            // 后续通过 NoteEventPatch 或重复调用 RegisterNote 自行补注。
            if (NoteIndex.Instance != null && !NoteIndex.Instance.Notes.Contains(note))
            {
                NoteIndex.Instance.Notes.Add(note);
                NoteIndex.SetNoteDynamic(note);
            }

            EventBusManager.Instance.Sync.Post(new NoteRegisteredEvent(id));
        }

        /// <summary>按 Identifier 移除已注册的笔记。</summary>
        public static bool UnregisterNote(Identifier id) => _registry.Remove(id);

        /// <summary>批量卸载指定 mod 注册的全部笔记。</summary>
        public static int UnregisterAllNotes(string modid) => _registry.RemoveAllByOwner(modid);

        // ===== 状态操作 =====

        /// <summary>解锁笔记。内部将 Identifier 映射到游戏原生 key 后调用 SetNoteUnlocked。</summary>
        public static void Unlock(Identifier id)
        {
            if (_registry.TryGetKey(id, out var key))
            {
                NoteIndex.SetNoteUnlocked(key);
                EventBusManager.Instance.Sync.Post(new NoteUnlockedEvent(id));
            }
        }

        /// <summary>解锁笔记并打开笔记 UI。内部调用 Unlock + NoteIndexView.ShowNote。</summary>
        public static void UnlockAndShow(Identifier id)
        {
            if (_registry.TryGetKey(id, out var key))
            {
                NoteIndex.SetNoteUnlocked(key);
                NoteIndexView.ShowNote(key);
                EventBusManager.Instance.Sync.Post(new NoteUnlockedEvent(id));
            }
        }

        /// <summary>查询笔记是否已解锁。</summary>
        public static bool IsUnlocked(Identifier id)
        {
            return _registry.TryGetKey(id, out var key) && NoteIndex.GetNoteUnlocked(key);
        }

        /// <summary>查询笔记是否已阅读。</summary>
        public static bool IsRead(Identifier id)
        {
            return _registry.TryGetKey(id, out var key) && NoteIndex.GetNoteRead(key);
        }

        // ===== 统计 =====

        /// <summary>获取笔记总数（不含隐藏笔记）。</summary>
        public static int GetTotalCount()
        {
            return NoteIndex.GetTotalNoteCount();
        }

        /// <summary>获取已解锁笔记数。</summary>
        public static int GetUnlockedCount()
        {
            return NoteIndex.GetUnlockedNoteCount();
        }

        /// <summary>获取指定 mod 注册的全部笔记 Identifier。</summary>
        public static IReadOnlyList<Identifier> GetAllNotes(string modid)
        {
            return _registry.GetAllByOwner(modid);
        }

        // ===== 世界空间拾取物 =====

        /// <summary>
        /// 在世界空间生成笔记拾取物。交互后自动解锁笔记并打开 UI。
        /// </summary>
        /// <param name="id">笔记标识符。</param>
        /// <param name="position">世界空间坐标。</param>
        /// <param name="sceneId">目标场景（null = 当前活动场景）。</param>
        /// <returns>创建的 NoteInteract 组件。</returns>
        public static NoteInteract SpawnPickup(Identifier id, Vector3 position, string? sceneId = null)
        {
            if (!_registry.TryGetKey(id, out var key))
                return null;

            var go = new GameObject($"Note_{key}");
            go.transform.position = position;

            // 在添加 InteractableBase 子类前，设置碰撞体和交互图层
            // 防止游戏原生 InteractableBase.Awake() 因缺失 Collider 而 NRE
            var collider = go.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            int interactLayer = LayerMask.NameToLayer("Interact");
            if (interactLayer != -1) go.layer = interactLayer;

            var interact = go.AddComponent<NoteInteract>();
            interact.noteKey = key;

            return interact;
        }

        // ===== 内部：DTO → 游戏原生类型 =====

        private static Note CreateNativeNote(Identifier id, NoteConfig config)
        {
            string key = id.Path;
            return new Note
            {
                key = key,
                image = config.Image,
                hide = config.Hidden
            };
        }
    }
}
