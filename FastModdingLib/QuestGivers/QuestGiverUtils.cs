using Duckov.Quests;
using Duckov.Utilities;
using FeatherMod.Register;
using FeatherMod.Utils;
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
        }

        // ═══════════════════════════════════════════════════
        //  注册（仅分配 ID，不创建 GO）
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 注册自定义 QuestGiver，自动分配唯一 questGiverID (int >= 50)。
        /// 不创建 GameObject——交互点通过 <see cref="CreateQuestGiver"/> 或
        /// <see cref="FriendlyNpcUtils.BindQuestGiver"/> 挂载。
        /// </summary>
        /// <param name="id">QuestGiver 的 Identifier。</param>
        /// <param name="modid">owner modid；null 时从 id.Domain 推导。</param>
        /// <returns>分配的自定义 questGiverID (int)。</returns>
        public static int RegisterQuestGiver(Identifier id, string? modid = null)
        {
            Init();
            string owner = modid ?? id.Domain;

            int questGiverId = _registry.Register(id, owner);

            Debug.Log($"[FML] Registered quest giver: {id} (custom ID: {questGiverId}) from mod: {owner}");
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
                    Debug.Log($"[FML] Bound quest {questId} to quest giver {questGiverId}");
                    return true;
                }
            }
            return false;
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
