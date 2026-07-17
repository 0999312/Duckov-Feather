using Cysharp.Threading.Tasks;
using Duckov.Buffs;
using Duckov.NoteIndexs;
using Duckov.Quests;
using Duckov.UI.DialogueBubbles;
using Duckov.Utilities;
using FeatherMod.Events;
using FeatherMod.Register;
using FeatherMod.Utils;
using FmlEvent = FeatherMod.Events.Event;
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

        public static SimpleRegistry<GameObject> Registry => _registry;

        /// <summary>初始化（幂等）。</summary>
        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            _registry = new SimpleRegistry<GameObject>();
            _presetReg = new SimpleRegistry<CharacterRandomPreset>();
            _configCache = new Dictionary<Identifier, FriendlyNpcConfig>();
            _ownerCache = new Dictionary<Identifier, string>();

            RegistryManager.Instance.Registry.SetIfAbsent(
                new Identifier(FMLConstants.Domain, "friendly_npc"),
                _registry,
                RegistryManager.CurrentModid);
        }

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
                Debug.LogError($"[FML FriendlyNpc] Preset '{id}' not registered. Call RegisterFriendlyNpc first.");
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

                // 游戏实际唯一重载：
                //   UniTask<CharacterMainControl> CreateCharacterAsync(
                //       Vector3 pos, Vector3 dir, int relatedScene,
                //       CharacterSpawnerGroup group, bool isLeader)
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

                _registry.Set(id, character.gameObject, owner);
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
            int interactLayer = LayerMask.NameToLayer("Interact");
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

        /// <summary>为 NPC 绑定任务发放（string 格式，兼容旧 API）。</summary>
        public static void BindQuestGiver(Identifier npcId, string questGiverId)
        {
            if (!_registry.TryGet(npcId, out var go) || go == null) return;
            var qg = go.GetComponent<QuestGiver>();
            if (qg == null) qg = go.AddComponent<QuestGiver>();
            SetQuestGiverId(qg, questGiverId);
        }

        /// <summary>为 NPC 绑定已注册的 QuestGiver（Identifier 版本）。</summary>
        public static void BindQuestGiver(Identifier npcId, Identifier questGiverId)
        {
            if (!_registry.TryGet(npcId, out var go) || go == null) return;
            if (QuestGiverUtils.TryGetQuestGiverId(questGiverId, out int giverIntId))
            {
                var qg = go.GetComponent<QuestGiver>();
                if (qg == null) qg = go.AddComponent<QuestGiver>();
                SetQuestGiverId(qg, giverIntId.ToString());
            }
        }

        /// <summary>按 Identifier 销毁 NPC（含预设清理）。</summary>
        public static bool RemoveNpc(Identifier id)
        {
            if (_registry.TryGet(id, out var go) && go != null)
            {
                // DuckovDialogueActor.OnDisable() 自动 Unregister——销毁 GO 即可
                UnityEngine.Object.Destroy(go);
            }
            _registry.Remove(id);
            _presetReg.Remove(id);
            _configCache.Remove(id);
            _ownerCache.Remove(id);
            return true;
        }

        /// <summary>批量卸载指定 mod 的全部 NPC。</summary>
        public static int RemoveAllNpcs(string modid)
        {
            int count = _registry.RemoveAllByOwner(modid);
            _presetReg.RemoveAllByOwner(modid);
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
            // 否则 CreateCharacterAsync 内部 foreach/for 循环会在 null 上 NRE：
            //   itemsToGenerate     → GenerateItems() :489 foreach
            //   setStats            → CreateCharacterAsync() :261 for
            //   buffs / buffResist  → CreateCharacterAsync() :375 foreach / :379 for
            //   specialAttachmentBases → CreateCharacterAsync() :387 .Count
            //   bulletQualityDistribution / bulletFilter / bulletExclusiveTags → AddBullet() :519-523
            preset.itemsToGenerate = new System.Collections.Generic.List<RandomItemGenerateDescription>();
            preset.setStats = new System.Collections.Generic.List<CharacterRandomPreset.SetCharacterStatInfo>();
            preset.buffs = new System.Collections.Generic.List<Buff>();
            preset.buffResist = new System.Collections.Generic.List<Buff.BuffExclusiveTags>();
            preset.specialAttachmentBases = new System.Collections.Generic.List<AISpecialAttachmentBase>();
            // bulletQualityDistribution: RandomContainer<int> 在无武器时不会被调用（AddBullet 提前 return）
            // bulletFilter / bulletExclusiveTags 同上——友善 NPC 不装备武器，AddBullet 跳过

            // 基础标识（参考 Ming: nameKey=Character_Ming）
            preset.nameKey = id.Path;

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

            // AI 参数（友善 NPC 不战斗，最小化巡逻范围）
            preset.patrolRange = 0f;
            preset.combatMoveRange = 0f;
            preset.sightDistance = 0f;

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
        private static void AttachInteractionComponents(GameObject go, Identifier id, FriendlyNpcConfig config)
        {
            // 对话角色
            if (!string.IsNullOrEmpty(config.ActorId))
            {
                var actor = go.AddComponent<DuckovDialogueActor>();
                SetActorId(actor, config.ActorId);
            }

            // 根据角色类型绑定交互行为
            try
            {
                switch (config.Role)
                {
                    case NpcRole.Merchant:
                        var merchInteract = go.AddComponent<NoteInteract>();
                        merchInteract.noteKey = $"npc_{id.Path}";
                        break;

                    case NpcRole.QuestGiver:
                        var qg = go.GetComponent<QuestGiver>() ?? go.AddComponent<QuestGiver>();
                        if (!string.IsNullOrEmpty(config.QuestGiverId))
                            SetQuestGiverId(qg, config.QuestGiverId);
                        break;

                    case NpcRole.Companion:
                        go.AddComponent<global::InteractablePMC>();
                        break;

                    case NpcRole.DialogueOnly:
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FML FriendlyNpc] Failed to add interaction component for '{id}' (Role={config.Role}): {ex.Message}");
            }
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
        // 无需反射即可直接访问 CharacterRandomPreset / DuckovDialogueActor 的字段。

        private static CustomFacePreset? FindFacePresetByName(string name)
        {
            return Resources.Load<CustomFacePreset>($"CustomFacePreset_{name}");
        }

        private static CustomFacePreset? CreateCustomFacePreset(FacePartIds parts)
        {
            var preset = ScriptableObject.CreateInstance<CustomFacePreset>();

            // CustomFacePreset.settings 是 public CustomFaceSettingData，直接赋值
            // CustomFaceSettingData 全部字段都是 public int
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

        /// <summary>
        /// 设置 DuckovDialogueActor.id 并确保注册。
        /// OnEnable 在 AddComponent 时同步触发，此时 actor.id 尚未赋值（为空），
        /// Register(this) 以空 ID 加入 ActiveActors。Get(id) 动态检查 ID 属性，
        /// 设置 id 后 Get 可正常找到，但若 GameObject 因 SetRelatedScene 停用，
        /// OnEnable 未触发则需手动注册。
        /// </summary>
        private static void SetActorId(DuckovDialogueActor actor, string actorId)
        {
            actor.id = actorId;
            // 确保注册——OnEnable 可能因 GameObject inactive 而未触发
            // Register 内部 Contains 检查已注册场景为 no-op（仅 warning log）
            DuckovDialogueActor.Register(actor);
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
                Debug.LogWarning($"[FML] Unknown QuestGiverId '{questGiverId}' — " +
                    "not a valid enum name or custom int ID.");
            }
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
}
