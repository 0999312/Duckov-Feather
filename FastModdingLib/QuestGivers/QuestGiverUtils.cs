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
    /// QuestGiver 模块公共 API。提供自定义任务发放者的注册、生成、任务绑定、
    /// 查询和卸载功能。所有 public API 统一使用 <see cref="Identifier"/> 作为标识符。
    ///
    /// 设计思路：由于游戏原生 <see cref="QuestGiverID"/> 是固定 enum，不可扩展，
    /// FML 使用 Harmony 补丁（<see cref="QuestGiverRegistry"/>.GetQuestsByCustomId）
    /// 来支持自定义任务发放者 ID。自定义 ID 从 1000 起分配，与原生枚举值（0~11）
    /// 无冲突。
    /// </summary>
    /// <example>
    /// <code>
    /// // 注册任务发放者
    /// var config = new QuestGiverConfig
    /// {
    ///     DisplayNameKey = "NPC_Giver_Daily",
    ///     ActorId = "dialogue_daily_giver",
    ///     BoundQuests = new[] { new Identifier("mymod", "daily_quest_01") }
    /// };
    /// QuestGiverUtils.RegisterQuestGiver(new Identifier("mymod", "daily_giver"), config);
    ///
    /// // 生成 NPC
    /// QuestGiverUtils.SpawnQuestGiver(new Identifier("mymod", "daily_giver"), new Vector3(10, 0, 5));
    ///
    /// // 查询
    /// if (QuestGiverUtils.TryGetQuestGiver(new Identifier("mymod", "daily_giver"), out var go))
    ///     Debug.Log("NPC found at " + go.transform.position);
    /// </code>
    /// </example>
    public static class QuestGiverUtils
    {
        private static readonly QuestGiverRegistry _registry = new QuestGiverRegistry();
        private static bool _initialized;

        /// <summary>暴露给 RegisterBootstrap 用于注册到元表。</summary>
        public static QuestGiverRegistry Registry => _registry;

        // ═══════════════════════════════════════════════════
        //  Init（幂等）
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 初始化：将 QuestGiverRegistry 注册到
        /// <see cref="RegistryManager.Registry"/> 元表。
        /// 由 RegisterBootstrap.Init() 调用（幂等）。
        /// </summary>
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
        //  注册
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 注册自定义任务发放者。自动分配唯一 questGiverID，
        /// 创建 QuestGiver GameObject（不在此处生成到场景——调用
        /// <see cref="SpawnQuestGiver"/> 进行世界空间实例化）。
        /// </summary>
        /// <param name="id">
        /// QuestGiver 的 Identifier（domain = modid, path = 名称）。
        /// </param>
        /// <param name="config">QuestGiver 配置。</param>
        /// <param name="modid">owner modid；null 时从 id.Domain 推导。</param>
        /// <returns>分配的自定义 questGiverID (int)；供内部使用。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> 为 null。</exception>
        public static int RegisterQuestGiver(Identifier id, QuestGiverConfig config, string? modid = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            Init();
            string owner = modid ?? id.Domain;

            // 创建 QuestGiver GameObject（隐藏，SpawnQuestGiver 时激活/克隆）
            var go = new GameObject($"QuestGiver_{id.Domain}_{id.Path}");
            go.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(go);

            // 添加 QuestGiver 组件
            var qg = go.AddComponent<QuestGiver>();
            if (config.SpawnPOI)
            {
                SetSpawnPOI(qg, true);
            }

            // 添加对话角色组件
            if (!string.IsNullOrEmpty(config.ActorId))
            {
                var actor = go.AddComponent<global::DuckovDialogueActor>();
                SetActorId(actor, config.ActorId);
            }

            // 注册到 Registry（自动分配 questGiverID + 存储位置/旋转配置）
            int questGiverId = _registry.Register(id, go, owner, config.BoundQuests,
                config.SpawnPosition, config.SpawnRotation);

            Debug.Log($"[FML] Registered quest giver: {id} (custom ID: {questGiverId}) from mod: {owner}");
            return questGiverId;
        }

        // ═══════════════════════════════════════════════════
        //  生成
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 在指定世界位置生成已注册的 QuestGiver NPC。
        /// 内部克隆已注册的 GameObject 模板，激活并放置到场景中。
        /// </summary>
        /// <param name="id">QuestGiver 的 Identifier。</param>
        /// <param name="position">
        /// 生成位置。为 null 时使用注册配置中的 SpawnPosition。
        /// </param>
        /// <param name="rotation">
        /// 生成旋转。为 null 时使用注册配置中的 SpawnRotation。
        /// </param>
        /// <returns>生成的 GameObject；注册表中无此 Identifier 返回 null。</returns>
        public static GameObject? SpawnQuestGiver(Identifier id, Vector3? position = null, Quaternion? rotation = null)
        {
            Init();
            if (!_registry.TryGet(id, out var template) || template == null) return null;

            var go = UnityEngine.Object.Instantiate(template);
            go.name = template.name + "_Instance";

            // 优先使用调用方指定的位置/旋转，否则回退到注册时配置的值
            Vector3 cfgPos = Vector3.zero;
            Quaternion cfgRot = Quaternion.identity;
            if (!position.HasValue)
                _registry.TryGetSpawnPosition(id, out cfgPos);
            if (!rotation.HasValue)
                _registry.TryGetSpawnRotation(id, out cfgRot);

            go.transform.position = position ?? cfgPos;
            go.transform.rotation = rotation ?? cfgRot;
            go.SetActive(true);

            Debug.Log($"[FML] Spawned quest giver: {id} at {go.transform.position}");
            return go;
        }

        // ═══════════════════════════════════════════════════
        //  任务绑定
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 将任务绑定到指定 QuestGiver。绑定的任务会出现在此 QuestGiver 的发放列表中。
        /// 任务的 QuestData.questGiver 必须在注册时设为与此 QuestGiver 对应的值。
        /// </summary>
        /// <param name="questGiverId">QuestGiver 的 Identifier。</param>
        /// <param name="questId">任务的 Identifier（与 RegisterQuest 时使用的 id 一致）。</param>
        /// <returns>绑定成功返回 true；QuestGiver 不存在或任务无法解析返回 false。</returns>
        public static bool BindQuest(Identifier questGiverId, Identifier questId)
        {
            Init();
            if (!_registry.TryGet(questGiverId, out _)) return false;

            // 解析任务 Identifier → quest ID
            if (!QuestUtils.TryGetQuestId(questId, out int questIntId)) return false;

            // 获取所属 QuestGiverID
            if (!_registry.TryGetQuestGiverId(questGiverId, out int giverIntId)) return false;

            // 更新 Quest 的 QuestGiverID（通过 QuestCollection 查找）
            foreach (var quest in GameplayDataSettings.QuestCollection)
            {
                if (quest != null && quest.ID == questIntId)
                {
                    SetQuestQuestGiverId(quest, giverIntId);
                    Debug.Log($"[FML] Bound quest {questId} to quest giver {questGiverId}");
                    return true;
                }
            }
            return false;
        }

        // ═══════════════════════════════════════════════════
        //  查询
        // ═══════════════════════════════════════════════════

        /// <summary>按 Identifier 查询 QuestGiver GameObject。</summary>
        /// <returns>存在返回 true，go 为 GameObject；不存在返回 false。</returns>
        public static bool TryGetQuestGiver(Identifier id, out GameObject go)
        {
            return _registry.TryGet(id, out go);
        }

        /// <summary>按 Identifier 查询自定义 questGiverID (int)。</summary>
        /// <returns>存在返回 true；不存在返回 false。</returns>
        public static bool TryGetQuestGiverId(Identifier id, out int questGiverId)
        {
            return _registry.TryGetQuestGiverId(id, out questGiverId);
        }

        /// <summary>检查指定 int 值是否为 FML 注册的自定义 questGiverID。</summary>
        public static bool IsCustomQuestGiverId(int questGiverId)
        {
            return _registry.IsCustomQuestGiverId(questGiverId);
        }

        /// <summary>
        /// 按自定义 questGiverID (int) 获取属于此发放者的全部任务。
        /// 由 Harmony 补丁调用。
        /// </summary>
        internal static IEnumerable<Quest> GetQuestsByCustomId(int questGiverId)
        {
            return _registry.GetQuestsByCustomId(questGiverId);
        }

        // ═══════════════════════════════════════════════════
        //  卸载
        // ═══════════════════════════════════════════════════

        /// <summary>按 Identifier 卸载 QuestGiver（销毁 GameObject + 清理映射）。</summary>
        public static bool UnregisterQuestGiver(Identifier id)
        {
            return _registry.Remove(id);
        }

        /// <summary>批量卸载指定 mod 的全部 QuestGiver。</summary>
        public static int UnregisterAllQuestGivers(string modid)
        {
            return _registry.RemoveAllByOwner(modid);
        }

        // ═══════════════════════════════════════════════════
        //  反射辅助
        // ═══════════════════════════════════════════════════

        /// <summary>DuckovDialogueActor.id 经 Publicizer 已公开，直接赋值。</summary>
        private static void SetActorId(global::DuckovDialogueActor actor, string actorId)
        {
            actor.id = actorId;
        }

        /// <summary>QuestGiver.spawnPOI 已是 public 字段，直接赋值。</summary>
        private static void SetSpawnPOI(QuestGiver qg, bool value)
        {
            qg.spawnPOI = value;
        }

        /// <summary>Quest.questGiverID 经 Publicizer 已公开，直接赋值。</summary>
        private static void SetQuestQuestGiverId(Quest quest, int questGiverId)
        {
            quest.questGiverID = (QuestGiverID)questGiverId;
        }
    }
}
