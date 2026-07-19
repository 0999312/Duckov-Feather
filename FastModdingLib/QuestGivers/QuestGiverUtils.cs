using Duckov.Quests;
using Duckov.Utilities;
using FeatherMod.Events;
using FeatherMod.Events.GameEvents;
using FeatherMod.Register;
using FeatherMod.Utils;
using SodaCraft.Localizations;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// QuestGiver 模块公共 API。提供自定义 QuestGiver ID 注册、交互点创建、
    /// 任务绑定和卸载。所有 public API 统一使用 <see cref="Identifier"/>。
    ///
    /// 参照原版 SpecialAttachment_XiaoMing.prefab：QuestGiver 是独立的子 GO 交互点，
    /// 只包含 <see cref="QuestGiver"/> 组件（继承 InteractableBase），
    /// 模型/捏脸/ActorId 等显示层属性由 <see cref="FriendlyNpcConfig"/> 管理。
    /// </summary>
    public static class QuestGiverUtils
    {
        private static readonly QuestGiverRegistry _registry = new QuestGiverRegistry();
        private static bool _initialized;

        /// <summary>暴露给 RegisterBootstrap 用于注册到元表。</summary>
        public static QuestGiverRegistry Registry => _registry;

        internal static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            var meta = RegistryManager.Instance.Registry;
            var id = new Identifier(FMLConstants.Domain, "quest_giver");
            if (meta is NonAlterableSimpleRegistry<ERegistry> nonAlt)
                nonAlt.SetIfAbsent(id, _registry, RegistryManager.CurrentModid);
            else
                meta.Set(id, _registry, RegistryManager.CurrentModid);

            // 订阅语言切换事件：QuestGiver 显示名本地化键需随语言刷新
            EventBusManager.Instance.Sync.Register<LanguageChangedEvent>(OnLanguageChanged);
            // 订阅语言文件加载完成：注册时若翻译未就绪（ToPlainText 返回 *key* 包裹形式），
            // override 会被推迟——I18n 加载语言文件后补一次刷新，确保 Character_{int} 及时生效。
            I18n.OnLanguageFileLoaded += OnI18nLoaded;
        }

        private static void OnLanguageChanged(LanguageChangedEvent evt)
        {
            _registry.RefreshDisplayNameOverrides();
        }

        private static void OnI18nLoaded()
        {
            _registry.RefreshDisplayNameOverrides();
        }

        // ═══════════════════════════════════════════════════
        //  注册（仅分配 ID，不创建 GO）
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 注册自定义 QuestGiver，自动分配唯一 questGiverID (int >= 50)。
        /// 不创建 GameObject——交互点通过 <see cref="CreateQuestGiver"/> 或
        /// <see cref="FriendlyNpcUtils.BindQuestGiver"/> 挂载。
        ///
        /// 游戏对 QuestGiver 的显示名使用本地化键 <c>Character_{questGiverID}</c>
        /// （如 <c>Character_Jeff</c>）。由于自定义 ID 是动态分配的，modder 无法预知具体数值，
        /// 因此框架自动注册本地化重定向：<c>Character_{assignedID}</c> → <paramref name="displayNameKey"/>。
        /// </summary>
        /// <param name="id">QuestGiver 的 Identifier。</param>
        /// <param name="displayNameKey">
        /// QuestGiver 显示名的本地化键（如 "NPC_JeffCustom_Name"）。
        /// 为空时使用 <paramref name="id"/>.<see cref="Identifier.Path"/> 作为显示文本。
        /// 框架自动将游戏内键 <c>Character_{assignedID}</c> 映射到此键的翻译值。
        /// </param>
        /// <param name="modid">owner modid；null 时从 id.Domain 推导。</param>
        /// <returns>分配的自定义 questGiverID (int)。</returns>
        public static int RegisterQuestGiver(Identifier id, string? displayNameKey = null, string? modid = null)
        {
            Init();
            string owner = modid ?? id.Domain;

            int questGiverId = _registry.Register(id, owner);

            // 自动注册本地化重定向：Character_{questGiverID} → displayNameKey
            // 游戏 Quest UI 使用 Character_{QuestGiverID} 作为显示名本地化键，
            // 动态分配的 int ID 对 modder 不可预知，由框架自动桥接。
            if (!string.IsNullOrEmpty(displayNameKey))
            {
                var displayText = displayNameKey.ToPlainText();
                // 如果 ToPlainText 返回了 *key* 包裹形式（未找到翻译），说明 modder 尚未注册翻译，
                // 此时暂不设置 override（等 I18n 加载后再补）。
                if (!displayText.StartsWith("*") || !displayText.EndsWith("*"))
                {
                    LocalizationManager.SetOverrideText($"Character_{questGiverId}", displayText);
                }
                // 缓存映射：语言切换时 I18n 可通过此字典重新解析
                _registry.CacheDisplayNameKey(questGiverId, displayNameKey);
            }
            else
            {
                // 无 displayNameKey → 使用 id.Path 作为显示名
                LocalizationManager.SetOverrideText($"Character_{questGiverId}", id.Path);
            }

            Debug.Log($"[FML] Registered quest giver: {id} (custom ID: {questGiverId}, display key: {displayNameKey ?? id.Path}) from mod: {owner}");
            return questGiverId;
        }

        // ═══════════════════════════════════════════════════
        //  创建交互点
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 在指定世界位置创建独立的 QuestGiver 交互点 GO。
        /// 参照原版：一个带 Collider 的 GO + <see cref="QuestGiver"/> 组件（InteractableBase）。
        /// </summary>
        /// <param name="id">已注册的 QuestGiver Identifier。</param>
        /// <param name="position">世界空间位置。</param>
        /// <param name="spawnPOI">是否生成 POI 标记。默认 true。</param>
        /// <returns>创建的交互点 GameObject；未注册返回 null。</returns>
        public static GameObject? CreateQuestGiver(Identifier id, Vector3 position, bool spawnPOI = true)
        {
            Init();
            if (!_registry.TryGetQuestGiverId(id, out int giverIntId))
            {
                Debug.LogWarning($"[FML] QuestGiver '{id}' not registered. Call RegisterQuestGiver() first.");
                return null;
            }

            var go = new GameObject($"QuestGiver_{id.Domain}_{id.Path}");
            go.transform.position = position;
            UnityEngine.Object.DontDestroyOnLoad(go);

            int interactLayer = LayerMask.NameToLayer("Interactable");
            if (interactLayer != -1) go.layer = interactLayer;

            var qg = go.AddComponent<QuestGiver>();
            qg.spawnPOI = spawnPOI;
            qg.questGiverID = (QuestGiverID)giverIntId;

            _registry.SetRegistered(id, go, id.Domain);

            Debug.Log($"[FML] Created quest giver interact point: {id} at {position}");
            return go;
        }

        // ═══════════════════════════════════════════════════
        //  任务绑定
        // ═══════════════════════════════════════════════════

        /// <summary>将任务绑定到指定 QuestGiver。</summary>
        public static bool BindQuest(Identifier questGiverId, Identifier questId)
        {
            Init();
            if (!_registry.TryGetQuestGiverId(questGiverId, out int giverIntId)) return false;
            if (!QuestUtils.TryGetQuestId(questId, out int questIntId)) return false;

            foreach (var quest in GameplayDataSettings.QuestCollection)
            {
                if (quest != null && quest.ID == questIntId)
                {
                    quest.questGiverID = (QuestGiverID)giverIntId;
                    RefreshQuestGiverIndicators(giverIntId);
                    Debug.Log($"[FML] Bound quest {questId} to quest giver {questGiverId}");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 刷新场景中该 giver 的 QuestGiver 组件的任务缓存与 "!" 指示器。
        /// <see cref="QuestGiver._possibleQuests"/> 为 lazy 缓存且不会自行失效，
        /// BindQuest 后若不清理，<c>AnyQuestAvaliable()</c> 永远拿到旧列表导致指示器不显示。
        /// </summary>
        private static void RefreshQuestGiverIndicators(int giverIntId)
        {
            foreach (var qg in UnityEngine.Object.FindObjectsOfType<QuestGiver>(true))
            {
                if (qg == null || (int)qg.questGiverID != giverIntId) continue;
                // Publicizer 已公开字段，直接清缓存（非反射）
                qg._possibleQuests = null;
                // 重启激活状态触发 Start() → RefreshInspectionIndicator()
                var go = qg.gameObject;
                if (go.activeSelf)
                {
                    go.SetActive(false);
                    go.SetActive(true);
                }
            }
        }

        // ═══════════════════════════════════════════════════
        //  查询
        // ═══════════════════════════════════════════════════

        public static bool TryGetQuestGiver(Identifier id, out GameObject go)
            => _registry.TryGet(id, out go);

        public static bool TryGetQuestGiverId(Identifier id, out int questGiverId)
            => _registry.TryGetQuestGiverId(id, out questGiverId);

        public static bool IsCustomQuestGiverId(int questGiverId)
            => _registry.IsCustomQuestGiverId(questGiverId);

        internal static IEnumerable<Quest> GetQuestsByCustomId(int questGiverId)
            => _registry.GetQuestsByCustomId(questGiverId);

        // ═══════════════════════════════════════════════════
        //  卸载
        // ═══════════════════════════════════════════════════

        public static bool UnregisterQuestGiver(Identifier id)
            => _registry.Remove(id);

        public static int UnregisterAllQuestGivers(string modid)
            => _registry.RemoveAllByOwner(modid);
    }
}
