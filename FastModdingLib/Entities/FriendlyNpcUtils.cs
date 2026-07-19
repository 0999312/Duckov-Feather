using Cysharp.Threading.Tasks;
using Duckov.Buffs;
using Duckov.PerkTrees.Interactable;
using Duckov.Quests;
using Duckov.UI.DialogueBubbles;
using Duckov.Utilities;
using FeatherMod.Events;
using FeatherMod.Events.GameEvents;
using FeatherMod.Interaction;
using FeatherMod.Register;
using FeatherMod.Utils;
using FmlEvent = FeatherMod.Events.Event;
using Saves;
using SodaCraft.Localizations;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// 友善 NPC 系统公共 API。通过 <see cref="CharacterRandomPreset.CreateCharacterAsync"/>
    /// 生成完整的可见角色（自带 CharacterModel / CustomFaceInstance / Animator / Collider），
    /// 替代旧版 bare GameObject 路径。
    /// </summary>
    public static class FriendlyNpcUtils
    {
        private static SimpleRegistry<GameObject> _registry;           // 运行时 NPC GameObject 注册表
        private static SimpleRegistry<CharacterRandomPreset> _presetReg; // NPC 预设注册表
        private static Dictionary<Identifier, FriendlyNpcConfig> _configCache; // 配置缓存（用于生成后挂载交互组件）
        private static Dictionary<Identifier, string> _ownerCache;
        private static bool _initialized;

        // ── 交互标记偏移（参照原版小明 Interact_Quest ≈1.37m 微调）──
        private static readonly Vector3 ShopMarkerOffset = new Vector3(0f, 1.4f, 0f);   // 主交互（商店）头顶标记
        private static readonly Vector3 QuestMarkerOffset = new Vector3(0f, 1.4f, 0f);  // 任务给予者标记（"!" 在其上再 +0.5m）
        private static readonly Vector3 PerkMarkerOffset = new Vector3(0f, 1.4f, 0f);   // 技能树标记
        private const long DefaultShopRefreshTicks = 6000000000L; // 原版小明：库存刷新间隔 10 分钟

        // NPC save/restore persistence
        private const string NpcSaveKey = "fml_friendly_npc_spawns";
        private static bool _saveRestoreHooked;

        [Serializable]
        private struct NpcSpawnEntry
        {
            public string domain;
            public string path;
            public float posX, posY, posZ;
            public float rotX, rotY, rotZ, rotW;
            /// <summary>生成时所属场景（子场景 ID；空串 = 主场景/基地）。旧存档无此字段 → null，按主场景处理。</summary>
            public string scene;
        }

        public static SimpleRegistry<GameObject> Registry => _registry;

        /// <summary>初始化（幂等）。</summary>
        public static void Init()
        {
            if (_initialized)
            {
                Debug.Log($"[FML FriendlyNpc] Init called but already initialized (no-op). Stack: {Environment.StackTrace}");
                return;
            }
            _initialized = true;

            Debug.Log("[FML FriendlyNpc] Init: creating new registries (first-time or domain reload).");

            _registry = new SimpleRegistry<GameObject>();
            _presetReg = new SimpleRegistry<CharacterRandomPreset>();
            _configCache = new Dictionary<Identifier, FriendlyNpcConfig>();
            _ownerCache = new Dictionary<Identifier, string>();

            RegistryManager.Instance.Registry.SetIfAbsent(
                new Identifier(FMLConstants.Domain, "friendly_npc"),
                _registry,
                RegistryManager.CurrentModid);

            HookSaveRestore();
        }

        private static void HookSaveRestore()
        {
            if (_saveRestoreHooked) return;
            _saveRestoreHooked = true;
            try
            {
                EventBusManager.Instance.Sync.Register<CollectSaveDataEvent>(OnCollectSaveData);
                EventBusManager.Instance.Sync.Register<LevelInitializedEvent>(OnLevelInitialized);
                // 读档 / 场景切换恢复：LevelInitializedEvent 仅新游戏触发；
                // 主场景加载完成（读档回基地）与进入子场景（进出建筑）也需恢复 NPC，
                // 对齐原版 XiaoMing——进入存档时自动在建筑确定的原生成点位重新出现。
                EventBusManager.Instance.Sync.Register<MainSceneLoadedEvent>(OnMainSceneLoaded);
                EventBusManager.Instance.Sync.Register<SceneLoadFinishedEvent>(OnSubSceneLoaded);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FML FriendlyNpc] Failed to hook save/restore: {ex.Message}");
            }
        }

        private static void OnLevelInitialized(LevelInitializedEvent evt) { RestoreNpcSpawns(); }
        private static void OnMainSceneLoaded(MainSceneLoadedEvent evt) { RestoreNpcSpawns(); }
        private static void OnSubSceneLoaded(SceneLoadFinishedEvent evt) { RestoreNpcSpawns(); }
        private static void OnCollectSaveData(CollectSaveDataEvent evt) { /* PersistNpcSpawn already saves in real-time */ }

        // ═══════════════════════════════════════════════════
        //  新版 API：Register → SpawnAsync（推荐）
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 注册友善 NPC 的预设数据。内部创建 <see cref="CharacterRandomPreset"/> 并注入游戏全局列表。
        /// 注册后可通过 <see cref="SpawnFriendlyNpcAsync"/> 在世界中生成。
        /// </summary>
        /// <param name="id">NPC 标识符。</param>
        /// <param name="config">NPC 配置（模型、捏脸、角色类型、装备等）。</param>
        /// <param name="modid">所属 mod，默认从 id.Domain 推导。</param>
        /// <returns>创建的 CharacterRandomPreset（可用于进一步修改）。</returns>
        public static CharacterRandomPreset RegisterFriendlyNpc(Identifier id, FriendlyNpcConfig config, string? modid = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            Init();
            string owner = modid ?? id.Domain;

            var preset = BuildFriendlyPreset(id, config);
            _presetReg.Set(id, preset, owner);
            _configCache[id] = config;
            _ownerCache[id] = owner;

            // 注入游戏全局 preset 列表
            var presets = GameplayDataSettings.CharacterRandomPresetData.presets;
            if (presets != null && !presets.Contains(preset))
            {
                presets.Add(preset);
            }

            Debug.Log($"[FML FriendlyNpc] Registered preset '{id}' (owner: {owner})");
            return preset;
        }

        /// <summary>
        /// 异步生成已注册的友善 NPC。使用 <see cref="CharacterRandomPreset.CreateCharacterAsync"/>
        /// 创建完整的可见角色，并在生成后自动挂载对话角色和交互组件。
        /// </summary>
        /// <param name="id">NPC 标识符（需已通过 RegisterFriendlyNpc 注册）。</param>
        /// <param name="position">可选：覆盖 config 中的 SpawnPosition。</param>
        /// <param name="rotation">可选：覆盖 config 中的 SpawnRotation。</param>
        /// <returns>生成的 NPC GameObject，失败时返回 null。</returns>
        public static async UniTask<GameObject?> SpawnFriendlyNpcAsync(
            Identifier id, Vector3? position = null, Quaternion? rotation = null)
        {
            if (!_presetReg.TryGet(id, out var preset))
            {
                bool configExists = _configCache.TryGetValue(id, out _);
                bool ownerExists = _ownerCache.TryGetValue(id, out _);
                Debug.LogError($"[FML FriendlyNpc] Preset '{id}' not registered. " +
                    $"(configInCache={configExists}, ownerInCache={ownerExists}, initialized={_initialized}). " +
                    $"Call RegisterFriendlyNpc first.");
                return null;
            }
            if (!_configCache.TryGetValue(id, out var config))
            {
                Debug.LogError($"[FML FriendlyNpc] Config for '{id}' not found.");
                return null;
            }

            var pos = position ?? config.SpawnPosition;
            var rot = rotation ?? config.SpawnRotation;
            string owner = _ownerCache.TryGetValue(id, out var o) ? o : id.Domain;

            try
            {
                // 将 Quaternion 旋转转换为方向向量
                Vector3 dir = rot * Vector3.forward;

                // 获取当前场景 buildIndex
                int sceneBuildIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;

                var method = typeof(CharacterRandomPreset).GetMethod("CreateCharacterAsync",
                    new Type[] { typeof(Vector3), typeof(Vector3), typeof(int), typeof(CharacterSpawnerGroup), typeof(bool) });

                if (method == null)
                {
                    Debug.LogError("[FML FriendlyNpc] CreateCharacterAsync not found via reflection.");
                    return null;
                }

                // 反射调用返回 UniTask<CharacterMainControl>（boxed），直接 await
                var uniTaskObj = method.Invoke(preset, new object[] { pos, dir, sceneBuildIndex, null, false });
                var character = await (UniTask<CharacterMainControl>)uniTaskObj;

                // 生成成功后挂载交互组件
                AttachInteractionComponents(character.gameObject, id, config);

                // 设置无敌（默认 true，通过 Health.invincible 实现）
                if (config.Invincible)
                {
                    var health = character.GetComponent<global::Health>();
                    if (health != null)
                        health.invincible = true;
                }

                _registry.Set(id, character.gameObject, owner);
                PersistNpcSpawn(id, pos, rot);
                EventBusManager.Instance.Sync.Post(new NpcCreatedEvent(id));

                Debug.Log($"[FML FriendlyNpc] Spawned '{id}' with CharacterModel at {pos}");
                return character.gameObject;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FML FriendlyNpc] Failed to spawn '{id}': {ex}");
                return null;
            }
        }

        // ═══════════════════════════════════════════════════
        //  旧版 API（保持兼容）
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// [Obsolete] 创建友善 NPC。此同步版本内部注册 preset 并尝试异步生成，
        /// 可能无法在同一帧返回有效的 GameObject。请改用
        /// <see cref="RegisterFriendlyNpc"/> + <see cref="SpawnFriendlyNpcAsync"/>。
        /// </summary>
        [Obsolete("Use RegisterFriendlyNpc() + SpawnFriendlyNpcAsync() for visible NPCs with CharacterModel.")]
        public static GameObject CreateFriendlyNpc(Identifier id, FriendlyNpcConfig config, string? modid = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            Init();
            string owner = modid ?? id.Domain;

            // 注册预设
            RegisterFriendlyNpc(id, config, owner);

            // 尝试同步生成（fire-and-forget，旧行为兼容）
            var go = new GameObject($"Npc_{id.Domain}_{id.Path}");
            go.transform.position = config.SpawnPosition;
            go.transform.rotation = config.SpawnRotation;

            // 设置碰撞体和交互图层（防止 InteractableBase.Awake NRE）
            var collider = go.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            int interactLayer = LayerMask.NameToLayer("Interactable");
            if (interactLayer != -1) go.layer = interactLayer;

            // 异步生成真实角色——fire and forget
            SpawnFriendlyNpcAsync(id, config.SpawnPosition, config.SpawnRotation).Forget();

            _registry.Set(id, go, owner);
            return go;
        }

        // ═══════════════════════════════════════════════════
        //  公共工具方法
        // ═══════════════════════════════════════════════════

        /// <summary>显示世界空间对话气泡。</summary>
        public static void ShowBubble(Identifier npcId, string text, float duration = 2f)
        {
            if (!_registry.TryGet(npcId, out var go) || go == null) return;
            DialogueBubblesManager.Show(text, go.transform, 1.5f, false, false, -1f, duration).Forget();
        }

        /// <summary>显示世界空间对话气泡（通过本地化键）。</summary>
        public static void ShowBubbleLocalized(Identifier npcId, string key, float duration = 2f)
        {
            var text = key.ToPlainText();
            ShowBubble(npcId, text, duration);
        }

        /// <summary>为 NPC 绑定商店。</summary>
        public static void BindShop(Identifier npcId, Identifier shopId)
        {
            EventBusManager.Instance.Sync.Post(new NpcShopBoundEvent(npcId, shopId));
        }

        /// <summary>查询 NPC 注册配置中的 DuckovDialogueActor.id（对话 ActorId）。</summary>
        /// <returns>配置存在且 ActorId 非空时返回 true。</returns>
        public static bool TryGetNpcActorId(Identifier npcId, out string actorId)
        {
            if (_configCache != null && _configCache.TryGetValue(npcId, out var cfg) && !string.IsNullOrEmpty(cfg.ActorId))
            {
                actorId = cfg.ActorId;
                return true;
            }
            actorId = string.Empty;
            return false;
        }

        /// <summary>
        /// 让 NPC 面向固定的世界方向（仅水平旋转，经游戏原生瞄准管线平滑转向）。
        /// 会挂载（或复用）<see cref="NpcFacePlayer"/> 组件并切换到固定朝向模式，
        /// 覆盖 AutoFacePlayer 的跟随玩家行为。
        /// </summary>
        public static void SetNpcFaceDirection(Identifier npcId, Vector3 direction)
        {
            if (!_registry.TryGet(npcId, out var go) || go == null) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return;
            var fp = go.GetComponent<NpcFacePlayer>();
            if (fp == null)
            {
                fp = go.AddComponent<NpcFacePlayer>();
                if (_configCache.TryGetValue(npcId, out var cfg))
                    fp.FollowRange = cfg.FacePlayerRange;
            }
            fp.FixedDirection = direction.normalized;
        }

        /// <summary>让 NPC 面向固定水平角度（度，0 = 世界 +Z 方向，顺时针）。</summary>
        public static void SetNpcFaceAngle(Identifier npcId, float yAngle)
        {
            SetNpcFaceDirection(npcId, Quaternion.Euler(0f, yAngle, 0f) * Vector3.forward);
        }

        /// <summary>
        /// 清除固定朝向。若该 NPC 配置开启了 AutoFacePlayer 则恢复跟随玩家；
        /// 否则移除朝向组件，NPC 保持当前朝向不动。
        /// </summary>
        public static void ClearNpcFaceDirection(Identifier npcId)
        {
            if (!_registry.TryGet(npcId, out var go) || go == null) return;
            var fp = go.GetComponent<NpcFacePlayer>();
            if (fp == null) return;
            bool autoFollow = _configCache.TryGetValue(npcId, out var cfg) && cfg.AutoFacePlayer;
            if (autoFollow)
            {
                fp.FixedDirection = null;
            }
            else
            {
                // 冻结当前朝向：瞄准自身位置使 UpdateAiming 不再改写朝向
                var cc = go.GetComponent<CharacterMainControl>();
                if (cc != null) cc.SetAimPoint(go.transform.position);
                UnityEngine.Object.Destroy(fp);
            }
        }

        /// <summary>[Obsolete] 为 NPC 绑定任务发放（string 格式，已废弃）。请使用 Identifier 版本。</summary>
        [Obsolete("Use BindQuestGiver(Identifier npcId, Identifier questGiverId) instead.")]
        public static void BindQuestGiver(Identifier npcId, string questGiverId)
        {
            if (!_registry.TryGet(npcId, out var go) || go == null) return;
            var qg = go.GetComponentInChildren<QuestGiver>(includeInactive: true);
            if (qg == null) return;
            SetQuestGiverId(qg, questGiverId);
        }

        /// <summary>为 NPC 绑定已注册的 QuestGiver（Identifier → int ID）。需先通过 QuestGiverUtils.RegisterQuestGiver() 注册。</summary>
        public static void BindQuestGiver(Identifier npcId, Identifier questGiverId)
        {
            if (!_registry.TryGet(npcId, out var go) || go == null) return;
            if (!QuestGiverUtils.TryGetQuestGiverId(questGiverId, out int giverIntId))
            {
                Debug.LogWarning($"[FML] QuestGiver '{questGiverId}' not registered. Call QuestGiverUtils.RegisterQuestGiver() first.");
                return;
            }
            // 查找 NPC 或其子对象上的 QuestGiver 组件
            var qg = go.GetComponentInChildren<QuestGiver>(includeInactive: true);
            if (qg == null)
            {
                Debug.LogWarning($"[FML] NPC '{npcId}' has no QuestGiver component. Ensure NpcRole includes QuestGiver.");
                return;
            }
            SetQuestGiverId(qg, giverIntId.ToString());
        }

        /// <summary>按 Identifier 销毁 NPC 运行时实例（保留 preset/config，允许重建）。</summary>
        public static bool RemoveNpc(Identifier id)
        {
            Debug.Log($"[FML FriendlyNpc] RemoveNpc '{id}' called. Stack: {Environment.StackTrace}");
            if (_registry.TryGet(id, out var go) && go != null)
            {
                // DuckovDialogueActor.OnDisable() 自动 Unregister——销毁 GO 即可
                UnityEngine.Object.Destroy(go);
            }
            _registry.Remove(id);
            RemoveNpcFromSave(id);
            // 注意：不删除 _presetReg / _configCache / _ownerCache。
            // preset 是注册数据，应在 mod 生命周期内持久存在；
            // 建筑回收/重放时不应丢失 preset（否则重建时无法生成 NPC）。
            // 批量清理由 RemoveAllNpcs 在 mod 卸载时负责。
            return true;
        }

        /// <summary>批量卸载指定 mod 的全部 NPC。</summary>
        public static int RemoveAllNpcs(string modid)
        {
            Debug.Log($"[FML FriendlyNpc] RemoveAllNpcs modid='{modid}' called. Stack: {Environment.StackTrace}");
            int count = _registry.RemoveAllByOwner(modid);
            int presetRemoved = _presetReg.RemoveAllByOwner(modid);
            Debug.Log($"[FML FriendlyNpc] RemoveAllNpcs modid='{modid}': removed {count} GO instances, {presetRemoved} presets.");
            // 清理 config/owner 缓存
            var toRemove = new List<Identifier>();
            foreach (var kvp in _ownerCache)
                if (kvp.Value == modid) toRemove.Add(kvp.Key);
            foreach (var key in toRemove)
            {
                _configCache.Remove(key);
                _ownerCache.Remove(key);
            }
            return count;
        }

        // ═══════════════════════════════════════════════════
        //  NPC 存档/读档持久化
        // ═══════════════════════════════════════════════════

        private static void PersistNpcSpawn(Identifier id, Vector3 pos, Quaternion rot)
        {
            try
            {
                var entries = LoadNpcSpawnEntries();
                entries.RemoveAll(e => e.domain == id.Domain && e.path == id.Path);
                entries.Add(new NpcSpawnEntry
                {
                    domain = id.Domain, path = id.Path,
                    posX = pos.x, posY = pos.y, posZ = pos.z,
                    rotX = rot.x, rotY = rot.y, rotZ = rot.z, rotW = rot.w,
                    scene = GetCurrentSceneKey()
                });
                SavesSystem.Save(NpcSaveKey, entries);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FML FriendlyNpc] Failed to persist spawn data for '{id}': {ex.Message}");
            }
        }

        /// <summary>当前场景标识：子场景 ID（MultiSceneCore.ActiveSubSceneID）；主场景返回空串。</summary>
        private static string GetCurrentSceneKey()
        {
            try
            {
                var sub = Duckov.Scenes.MultiSceneCore.ActiveSubSceneID;
                if (!string.IsNullOrEmpty(sub)) return sub;
            }
            catch { }
            return string.Empty;
        }

        private static void RemoveNpcFromSave(Identifier id)
        {
            try
            {
                var entries = LoadNpcSpawnEntries();
                entries.RemoveAll(e => e.domain == id.Domain && e.path == id.Path);
                SavesSystem.Save(NpcSaveKey, entries);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FML FriendlyNpc] Failed to remove spawn data for '{id}': {ex.Message}");
            }
        }

        private static List<NpcSpawnEntry> LoadNpcSpawnEntries()
        {
            try
            {
                if (SavesSystem.KeyExisits(NpcSaveKey))
                    return SavesSystem.Load<List<NpcSpawnEntry>>(NpcSaveKey) ?? new List<NpcSpawnEntry>();
            }
            catch { }
            return new List<NpcSpawnEntry>();
        }

        private static void RestoreNpcSpawns()
        {
            try
            {
                var entries = LoadNpcSpawnEntries();
                if (entries.Count == 0) return;
                string currentScene = GetCurrentSceneKey();
                int restored = 0;
                foreach (var entry in entries)
                {
                    // 场景过滤：仅在 NPC 所属场景加载时生成。
                    // 旧存档无 scene 字段（null/空）→ 视为基地（主场景）NPC，仅在主场景恢复。
                    bool sceneMatch = string.IsNullOrEmpty(entry.scene)
                        ? string.IsNullOrEmpty(currentScene)
                        : entry.scene == currentScene;
                    if (!sceneMatch) continue;

                    var id = new Identifier(entry.domain, entry.path);
                    if (_registry != null && _registry.TryGet(id, out var existingGo) && existingGo != null)
                        continue;

                    var pos = new Vector3(entry.posX, entry.posY, entry.posZ);
                    var rot = new Quaternion(entry.rotX, entry.rotY, entry.rotZ, entry.rotW);
                    SpawnFriendlyNpcAsync(id, pos, rot).Forget();
                    restored++;
                }
                if (restored > 0)
                    Debug.Log($"[FML FriendlyNpc] Restored {restored} NPC(s) in scene '{(currentScene == "" ? "<main>" : currentScene)}'.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FML FriendlyNpc] Failed to restore NPC spawns: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════
        //  内部方法
        // ═══════════════════════════════════════════════════

        /// <summary>从 FriendlyNpcConfig 构建 CharacterRandomPreset。</summary>
        /// <remarks>
        /// 基于游戏原生友好 NPC（Ming/Fo）的 CharacterRandomPreset 字段值。
        /// 所有字段（含 [SerializeField] private）经 Krafs.Publicizer 已公开，直接赋值无需反射。
        /// </remarks>
        private static CharacterRandomPreset BuildFriendlyPreset(Identifier id, FriendlyNpcConfig config)
        {
            var preset = ScriptableObject.CreateInstance<CharacterRandomPreset>();

            // ═══ Serialized collections — ScriptableObject.CreateInstance 后均为 null，必须显式初始化 ═══
            preset.itemsToGenerate = new System.Collections.Generic.List<RandomItemGenerateDescription>();
            preset.setStats = new System.Collections.Generic.List<CharacterRandomPreset.SetCharacterStatInfo>();
            preset.buffs = new System.Collections.Generic.List<Buff>();
            preset.buffResist = new System.Collections.Generic.List<Buff.BuffExclusiveTags>();
            preset.specialAttachmentBases = new System.Collections.Generic.List<AISpecialAttachmentBase>();

            // 基础标识（参考 Ming: nameKey=Character_Ming；modder 可用 DisplayNameKey 指定本地化键，
            // 游戏对 nameKey 做 ToPlainText 翻译，未设置时回退 id.Path）
            preset.nameKey = !string.IsNullOrEmpty(config.DisplayNameKey) ? config.DisplayNameKey : id.Path;

            // 生存属性（参考 Ming: health=800, exp=100, hasSoul=1）
            preset.health = 100f;
            preset.exp = 100;
            preset.hasSoul = false;

            // 显示（参考 Ming: showHealthBar=0, showName=0）
            preset.showHealthBar = false;
            preset.showName = true;
            preset.isBoss = false;

            // 对话与移动（参考 Ming: canTalk=1, canDash=0, defaultWeaponOut=0）
            preset.canTalk = true;
            preset.canDash = false;
            preset.defaultWeaponOut = false;

            // 阵营与生存（参考 Ming: team=0, canDieIfNotRaidMap=0）
            preset.team = config.Team;
            preset.canDieIfNotRaidMap = false;

            // 性能优化（参考 Ming: setActiveByPlayerDistance=1）
            preset.setActiveByPlayerDistance = true;

            // AI 参数（友善 NPC 不战斗；sightDistance 用配置值以支持自然朝向玩家）
            preset.patrolRange = 0f;
            preset.combatMoveRange = 0f;
            preset.sightDistance = config.SightDistance;

            // 捏脸（facePreset/usePlayerPreset 经 Publicizer 已公开，直接赋值）
            switch (config.Face.Mode)
            {
                case FaceRefMode.PlayerFace:
                    preset.usePlayerPreset = true;
                    break;
                case FaceRefMode.Preset when config.Face.PresetName != null:
                    var facePreset = FindFacePresetByName(config.Face.PresetName);
                    if (facePreset != null)
                        preset.facePreset = facePreset;
                    break;
                case FaceRefMode.Custom:
                    var customPreset = CreateCustomFacePreset(config.Face.CustomParts);
                    if (customPreset != null)
                        preset.facePreset = customPreset;
                    break;
                case FaceRefMode.FromJson when !string.IsNullOrEmpty(config.Face.FaceJson):
                    if (global::CustomFaceSettingData.JsonToData(config.Face.FaceJson, out var jsonData))
                    {
                        var jsonPreset = ScriptableObject.CreateInstance<CustomFacePreset>();
                        jsonData.savedSetting = true;
                        jsonPreset.settings = jsonData;
                        preset.facePreset = jsonPreset;
                    }
                    break;
            }

            // 模型：解析 config.Model → CharacterModel prefab，回退到 DefaultCharacterModel
            preset.characterModel = !string.IsNullOrEmpty(config.Model.GamePrefabName)
                ? (Resources.Load<CharacterModel>(config.Model.GamePrefabName)
                   ?? GameplayDataSettings.Prefabs.DefaultCharacterModel)
                : GameplayDataSettings.Prefabs.DefaultCharacterModel;

            // 装备注入：从 FriendlyNpcConfig 和 EquipmentUtils 待处理队列注入
            if (config.HeadEquipment.HasValue)
                EquipmentUtils.ConfigureNpcEquipment(id, EquipmentSlot.Head, config.HeadEquipment.Value);
            if (config.BodyEquipment.HasValue)
                EquipmentUtils.ConfigureNpcEquipment(id, EquipmentSlot.Body, config.BodyEquipment.Value);
            EquipmentUtils.InjectEquipmentToPreset(preset, id);

            return preset;
        }

        /// <summary>在生成的 NPC 上挂载对话角色和交互组件。</summary>
        /// <remarks>
        /// 严格参照原版 SpecialAttachment_XiaoMing.prefab 结构：
        ///   - 唯一主交互体（interactableGroup=true），QuestGiver / PerkTreeUIInvoker
        ///     通过 otherInterablesInGroup 组装，成员的碰撞体禁用——游戏的
        ///     CA_Interact.SearchInteractableAround 只会选择最近的一个 InteractableBase，
        ///     多独立碰撞体会导致只能交互其中一个（复合角色失效的根因）。
        ///   - 主交互体标记偏移 0.66m（原版头顶标记）；任务 "!" 指示器 1.87m。
        ///   - StockShop 除 merchantID 外还需配置 DisplayNameKey / accountAvaliable /
        ///     returnCash / sellFactor / refreshAfterTimeSpan，否则店名空白、
        ///     永远显示"只收现金"、每次开店都刷新库存。
        /// </remarks>
        private static void AttachInteractionComponents(GameObject go, Identifier id, FriendlyNpcConfig config)
        {
            // 对话角色：id 用于查找，nameKey 用于对话 UI 显示名（游戏经 ToPlainText 本地化翻译）。
            // nameKey 取 DisplayNameKey，未设置时回退 ActorId 本身（modder 按 ActorId 注册翻译）。
            if (!string.IsNullOrEmpty(config.ActorId))
            {
                var actor = go.AddComponent<DuckovDialogueActor>();
                actor.id = config.ActorId;
                actor.nameKey = !string.IsNullOrEmpty(config.DisplayNameKey) ? config.DisplayNameKey : config.ActorId;
                // 原版 Actor_Jeff offset={0,1.25,0}——对话 UI 指示器（气泡）挂点。
                // 不设置时默认 (0,0,0)，对话标识会贴在 NPC 脚部。
                actor.offset = config.DialogueOffset ?? new Vector3(0f, 1.25f, 0f);
            }

            try
            {
                var role = config.Role;
                bool isMerchant = role.HasFlag(NpcRole.Merchant);
                bool isQuestGiver = role.HasFlag(NpcRole.QuestGiver);
                bool hasPerkTree = config.PerkTreeId != null;

                NpcShopInteract? shopInteract = null;
                QuestGiver? questGiver = null;
                PerkTreeUIInvoker? perkInvoker = null;

                // ── Merchant：StockShop 数据组件 + NpcShopInteract 交互入口 ──
                if (isMerchant)
                {
                    var shop = go.AddComponent<global::Duckov.Economy.StockShop>();
                    if (!string.IsNullOrEmpty(config.ShopId))
                    {
                        shop.merchantID = config.ShopId;
                        // 联动校验：merchant profile 需先经 ShopUtils.CreateMerchantProfile 注册
                        if (GameplayDataSettings.StockshopDatabase == null
                            || GameplayDataSettings.StockshopDatabase.GetMerchantProfile(config.ShopId) == null)
                        {
                            Debug.LogWarning($"[FML FriendlyNpc] MerchantProfile '{config.ShopId}' not found for '{id}'. " +
                                "Call ShopUtils.CreateMerchantProfile() before SpawnFriendlyNpcAsync, or the shop will have no goods.");
                        }
                    }
                    // 原版小明字段值（SpecialAttachment_XiaoMing.prefab）：
                    shop.DisplayNameKey = ResolveShopNameKey(config);
                    shop.accountAvaliable = config.ShopAccountAvaliable;
                    shop.returnCash = config.ShopReturnCash;
                    shop.sellFactor = config.ShopSellFactor;
                    shop.refreshAfterTimeSpan = DefaultShopRefreshTicks;
                    shop.refreshStockOnStart = false;
                    // AddComponent 时 Awake 已用默认 merchantID("Albert") 跑过一次
                    // InitializeEntries（查无此商人，entries 为空），此处按正确 merchantID 重初始化。
                    shop.entries.Clear();
                    shop.InitializeEntries();

                    shopInteract = go.AddComponent<NpcShopInteract>();
                    ConfigureShopInteract(shopInteract, go);
                }

                // ── QuestGiver：独立子 GO 交互点（参照原版 Interact_Quest 子对象）──
                if (isQuestGiver)
                {
                    try
                    {
                        var questGo = CreateInteractChild(go, "Interact_Quest");
                        // 先禁用再挂组件：对齐原版 prefab 语义——字段全部就绪后才触发 Awake/Start。
                        // 否则 Awake 会以默认 interactMarkerOffset(0) 把 inspectionIndicator
                        // 生成在 0.5m 膝盖处埋进模型，且 PossibleQuests 以默认 questGiverID
                        // 缓存错误列表，导致任务 "!" 指示器不显示。
                        questGo.SetActive(false);
                        questGiver = questGo.AddComponent<QuestGiver>();
                        questGiver.spawnPOI = false; // 与原版小明一致（spawnPOI 是地图 POI，非头顶 "!"）
                        questGiver.interactMarkerOffset = QuestMarkerOffset;
                        questGiver.overrideInteractName = true;
                        questGiver._overrideInteractNameKey = "UI_Interact_Quest";
                        questGiver.interactTime = 0f;
                        questGiver.finishWhenTimeOut = false;
                        if (config.QuestGiverId != null)
                            BindQuestGiverToComponent(questGiver, config.QuestGiverId);
                        // 激活后 Awake/Start 以正确字段执行：inspectionIndicator 自动定位到
                        // interactMarkerOffset+0.5m 头顶处，RefreshInspectionIndicator 用正确 giverID 刷新。
                        questGo.SetActive(true);
                    }
                    catch (Exception ex)
                    {
                        questGiver = null;
                        Debug.LogWarning($"[FML FriendlyNpc] Failed to add QuestGiver for '{id}': {ex.Message}. " +
                            "QuestManager may not be initialized yet. Quest interaction will be unavailable.");
                    }
                }

                // ── PerkTree：独立子 GO 交互点（参照原版 Interact_Skill 子对象）──
                if (hasPerkTree)
                {
                    try
                    {
                        // 联动校验：技能树需已注册（PerkTreeUtils.RegisterPerkTree 或原版树），
                        // 否则交互时 PerkTreeView.Show(null) 会在 vanilla 代码内 NRE
                        if (!IsPerkTreeAvailable(config.PerkTreeId!.Path))
                        {
                            Debug.LogWarning($"[FML FriendlyNpc] PerkTree '{config.PerkTreeId!.Path}' not found in PerkTreeManager for '{id}'. " +
                                "Register it via PerkTreeUtils.RegisterPerkTree() first, or reference a vanilla tree (e.g. Identifier(\"duckov\", \"PerkTree_Hacker\")).");
                        }
                        var perkGo = CreateInteractChild(go, "Interact_Skill");
                        // 同 QuestGiver：先禁用再挂组件，字段就绪后再激活触发 Awake/Start。
                        perkGo.SetActive(false);
                        perkInvoker = perkGo.AddComponent<PerkTreeUIInvoker>();
                        perkInvoker.perkTreeID = config.PerkTreeId!.Path;
                        perkInvoker.interactMarkerOffset = PerkMarkerOffset;
                        perkInvoker.overrideInteractName = true;
                        perkInvoker._overrideInteractNameKey = config.PerkTreeId!.Path;
                        perkInvoker.interactTime = 0f;
                        perkInvoker.finishWhenTimeOut = true;
                        perkInvoker.coolTime = 0.2f;
                        perkGo.SetActive(true);
                    }
                    catch (Exception ex)
                    {
                        perkInvoker = null;
                        Debug.LogWarning($"[FML FriendlyNpc] Failed to add PerkTreeUIInvoker for '{id}': {ex.Message}.");
                    }
                }

                // ── 复合交互组装（原版 interactableGroup 模式）──
                SetupInteractionGroup(shopInteract, questGiver, perkInvoker);

                // Companion：挂载玩家交互组件
                if (role.HasFlag(NpcRole.Companion))
                {
                    go.AddComponent<global::InteractablePMC>();
                }

                // ── 运行时行为组件 ──

                if (config.ProximityDialogue != null && config.ProximityDialogue.Lines.Length > 0)
                {
                    var proxTrigger = go.AddComponent<NpcProximityTrigger>();
                    proxTrigger.NpcId = id;
                    proxTrigger.ActorId = config.ActorId;
                    proxTrigger.Distance = config.ProximityDialogue.ProximityDistance > 0
                        ? config.ProximityDialogue.ProximityDistance : 3f;
                    proxTrigger.Lines = config.ProximityDialogue.Lines;
                    proxTrigger.Mode = config.ProximityDialogue.Mode;
                    Debug.Log($"[FML FriendlyNpc] ProximityTrigger for '{id}': NpcId.Path='{id.Path}', ActorId='{config.ActorId}'");
                }

                if (config.AutoFacePlayer)
                {
                    var facePlayer = go.AddComponent<NpcFacePlayer>();
                    facePlayer.FollowRange = config.FacePlayerRange;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FML FriendlyNpc] Failed to add interaction component for '{id}' (Role={config.Role}): {ex.Message}");
            }
        }

        /// <summary>按原版小明配置商店主交互体：标记偏移、交互名、缩放、冷却与检测碰撞体。</summary>
        private static void ConfigureShopInteract(NpcShopInteract interact, GameObject go)
        {
            interact.interactMarkerOffset = ShopMarkerOffset;
            interact.overrideInteractName = true;
            interact._overrideInteractNameKey = "UI_Trade";
            interact.zoomIn = false;
            interact.interactTime = 0.2f;
            interact.coolTime = 0.2f;
            // 原版使用 radius=4 的专用 SphereCollider 做交互检测。
            // 显式指定，避免 Awake 回退拾取到角色自身胶囊体。
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = false;
            col.radius = 4f;
            col.center = Vector3.zero;
            interact.interactCollider = col;
        }

        /// <summary>
        /// 复合交互组装：指定唯一主交互体（商店 &gt; 任务 &gt; 技能树优先级），
        /// 其余经 otherInterablesInGroup 挂入交互组并禁用其独立碰撞体。
        /// 单一交互时保持独立碰撞体，直接检测。
        /// </summary>
        private static void SetupInteractionGroup(InteractableBase? shop, QuestGiver? quest, PerkTreeUIInvoker? perk)
        {
            InteractableBase? primary = shop != null ? shop : quest != null ? quest : (InteractableBase?)perk;
            if (primary == null) return;

            var members = new List<InteractableBase>(2);
            if (primary != shop && shop != null) members.Add(shop);
            if (primary != quest && quest != null) members.Add(quest);
            if (primary != perk && perk != null) members.Add(perk);
            if (members.Count == 0) return; // 单交互——无需组

            primary.interactableGroup = true;
            primary.otherInterablesInGroup = members;

            // 复刻 InteractableBase.Awake 的组同步（运行时挂载晚于 Awake，需手动执行）：
            foreach (var member in members)
            {
                member.MarkerActive = false;
                member.transform.SetPositionAndRotation(primary.transform.position, primary.transform.rotation);
                member.interactMarkerOffset = primary.interactMarkerOffset;
                if (member.interactCollider != null)
                    member.interactCollider.enabled = false;
            }
        }

        /// <summary>解析商店显示名本地化键：DisplayNameKey → ActorId → 原版 MerchantName_{merchantID} 惯例。</summary>
        private static string ResolveShopNameKey(FriendlyNpcConfig config)
        {
            if (!string.IsNullOrEmpty(config.DisplayNameKey)) return config.DisplayNameKey;
            if (!string.IsNullOrEmpty(config.ActorId)) return config.ActorId;
            return $"MerchantName_{config.ShopId}";
        }

        /// <summary>检查 perkTreeID 是否已存在于 PerkTreeManager（原版树或 FML 注册树）。管理器未就绪时不误报。</summary>
        private static bool IsPerkTreeAvailable(string treeId)
        {
            var mgr = PerkTreeManager.Instance;
            if (mgr == null || mgr.perkTrees == null) return true;
            foreach (var tree in mgr.perkTrees)
            {
                if (tree != null && tree.ID == treeId)
                    return true;
            }
            return false;
        }

        /// <summary>创建交互点子 GameObject（参照原版 Interact_Quest 子对象）。</summary>
        private static GameObject CreateInteractChild(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;

            int interactLayer = LayerMask.NameToLayer("Interactable");
            if (interactLayer != -1) child.layer = interactLayer;

            // 必须有 Collider，否则物理交互检测（OverlapSphere/Raycast）
            // 无法发现此子对象，导致 QuestGiver 等交互组件不可用。
            // 尺寸对齐原版小明 Interact_Quest 子对象：size (2, 1.3, 2)，center (0, 0.5, 0)。
            var col = child.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.5f, 0f);
            col.size = new Vector3(2f, 1.3f, 2f);

            return child;
        }

        /// <summary>按 nameKey 查找已生成的 CharacterMainControl（回退方案）。</summary>
        private static CharacterMainControl? FindSpawnedCharacter(Identifier id)
        {
            var all = GameObject.FindObjectsOfType<CharacterMainControl>();
            foreach (var c in all)
            {
                if (c.name.Contains(id.Path) || c.gameObject.name.Contains(id.Path))
                    return c;
            }
            return null;
        }

        // ═══════════════════════════════════════════════════
        //  反射辅助
        // ═══════════════════════════════════════════════════

        private static MethodInfo? _cachedCreateAsync;
        private static readonly object _cacheLock = new object();

        private static MethodInfo? GetCreateCharacterAsyncMethod()
        {
            if (_cachedCreateAsync != null) return _cachedCreateAsync;
            lock (_cacheLock)
            {
                if (_cachedCreateAsync != null) return _cachedCreateAsync;
                _cachedCreateAsync = typeof(CharacterRandomPreset).GetMethod("CreateCharacterAsync",
                    new Type[] { typeof(Vector3), typeof(Vector3), typeof(int), typeof(CharacterSpawnerGroup), typeof(bool) });
                if (_cachedCreateAsync == null)
                    Debug.LogError("[FML FriendlyNpc] CreateCharacterAsync method not found.");
                return _cachedCreateAsync;
            }
        }

        // Publicizer 已将所有 [SerializeField] private 字段变为 public，
        // 无需反射即可直接访问 CharacterRandomPreset / DuckovDialogueActor /
        // StockShop / InteractableBase / QuestGiver / PerkTreeUIInvoker 的字段与方法。

        private static CustomFacePreset? FindFacePresetByName(string name)
        {
            return Resources.Load<CustomFacePreset>($"CustomFacePreset_{name}");
        }

        private static CustomFacePreset? CreateCustomFacePreset(FacePartIds parts)
        {
            var preset = ScriptableObject.CreateInstance<CustomFacePreset>();

            var data = preset.settings;
            data.savedSetting = true;
            if (!string.IsNullOrEmpty(parts.HairId)) data.hairID = int.TryParse(parts.HairId, out var h) ? h : 0;
            if (!string.IsNullOrEmpty(parts.EyeId)) data.eyeID = int.TryParse(parts.EyeId, out var e) ? e : 0;
            if (!string.IsNullOrEmpty(parts.MouthId)) data.mouthID = int.TryParse(parts.MouthId, out var m) ? m : 0;
            if (!string.IsNullOrEmpty(parts.EyebrowId)) data.eyebrowID = int.TryParse(parts.EyebrowId, out var eb) ? eb : 0;
            if (!string.IsNullOrEmpty(parts.TailId)) data.tailID = int.TryParse(parts.TailId, out var t) ? t : 0;
            if (!string.IsNullOrEmpty(parts.FootId)) data.footID = int.TryParse(parts.FootId, out var f) ? f : 0;
            if (!string.IsNullOrEmpty(parts.WingId)) data.wingID = int.TryParse(parts.WingId, out var w) ? w : 0;
            preset.settings = data;

            return preset;
        }

        private static void SetQuestGiverId(QuestGiver qg, string questGiverId)
        {
            if (string.IsNullOrEmpty(questGiverId)) return;

            // questGiverID 字段经 Publicizer 已公开，直接赋值
            if (int.TryParse(questGiverId, out int customId) && customId >= 50)
            {
                qg.questGiverID = (QuestGiverID)customId;
                return;
            }

            try
            {
                qg.questGiverID = (QuestGiverID)Enum.Parse(typeof(QuestGiverID), questGiverId);
            }
            catch (ArgumentException)
            {
                Debug.LogWarning($"[FML] QuestGiverId '{questGiverId}' is not a valid enum name or custom int ID. " +
                    "Use QuestGiverUtils.RegisterQuestGiver() to register custom IDs first, or pass a valid QuestGiverID enum name (e.g. 'Ming', 'Fo', 'Albert').");
            }
        }

        /// <summary>通过 Identifier 查找已注册的 QuestGiver ID 并设置到组件上。</summary>
        private static void BindQuestGiverToComponent(QuestGiver qg, Identifier questGiverId)
        {
            if (!QuestGiverUtils.TryGetQuestGiverId(questGiverId, out int giverIntId))
            {
                Debug.LogWarning($"[FML] QuestGiver '{questGiverId}' not registered. Call QuestGiverUtils.RegisterQuestGiver() first.");
                return;
            }
            SetQuestGiverId(qg, giverIntId.ToString());
        }
    }

    /// <summary>NPC 创建事件。</summary>
    public class NpcCreatedEvent : FmlEvent
    {
        public Identifier NpcId { get; }
        public NpcCreatedEvent(Identifier npcId) { NpcId = npcId; }
    }

    /// <summary>NPC 商店绑定事件。</summary>
    public class NpcShopBoundEvent : FmlEvent
    {
        public Identifier NpcId { get; }
        public Identifier ShopId { get; }
        public NpcShopBoundEvent(Identifier npcId, Identifier shopId) { NpcId = npcId; ShopId = shopId; }
    }

    /// <summary>
    /// 友好 NPC 商店交互组件。替代旧版 <see cref="Duckov.NoteIndexs.NoteInteract"/>，
    /// 在 NPC 被交互时打开 <see cref="Duckov.Economy.StockShopView"/>。
    /// 由 <see cref="FriendlyNpcUtils.AttachInteractionComponents"/> 在生成时自动挂载。
    /// </summary>
    internal class NpcShopInteract : global::InteractableBase
    {
        protected override void OnInteractFinished()
        {
            var shop = GetComponent<global::Duckov.Economy.StockShop>();
            if (shop != null)
            {
                shop.ShowUI();
            }
            else
            {
                Debug.LogWarning($"[FML] NpcShopInteract: StockShop component not found on {gameObject.name}");
            }
        }
    }
}
