using Cysharp.Threading.Tasks;
using Duckov.NoteIndexs;
using Duckov.Quests;
using Duckov.UI.DialogueBubbles;
using FeatherMod.Events;
using FeatherMod.Register;
using FeatherMod.Utils;
using FmlEvent = FeatherMod.Events.Event;
using SodaCraft.Localizations;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// 友善 NPC 系统公共 API。提供 NPC 创建、对话气泡、商店/任务绑定。
    /// </summary>
    public static class FriendlyNpcUtils
    {
        private static SimpleRegistry<GameObject> _registry;
        private static bool _initialized;

        public static SimpleRegistry<GameObject> Registry => _registry;

        /// <summary>初始化（幂等）。</summary>
        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            _registry = new SimpleRegistry<GameObject>();
            RegistryManager.Instance.Registry.SetIfAbsent(
                new Identifier(FMLConstants.Domain, "friendly_npc"),
                _registry,
                RegistryManager.CurrentModid);
        }

        /// <summary>
        /// 创建友善 NPC 并绑定交互行为。
        /// </summary>
        public static GameObject CreateFriendlyNpc(Identifier id, FriendlyNpcConfig config, string? modid = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            Init();
            string owner = modid ?? id.Domain;

            var go = new GameObject($"Npc_{id.Domain}_{id.Path}");
            go.transform.position = config.SpawnPosition;
            go.transform.rotation = config.SpawnRotation;

            // 设置交互碰撞体和图层，防止游戏原生 InteractableBase.Awake()
            // 因缺失 Collider 而导致 NullReferenceException
            var collider = go.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            int interactLayer = LayerMask.NameToLayer("Interact");
            if (interactLayer != -1) go.layer = interactLayer;

            // 对话角色
            if (!string.IsNullOrEmpty(config.ActorId))
            {
                var actor = go.AddComponent<DuckovDialogueActor>();
                // actor.id 通过反射设置（DuckovDialogueActor 字段为 private，Publicizer 已公开）
                SetActorId(actor, config.ActorId);
            }

            // 根据角色类型绑定交互行为
            try
            {
                switch (config.Role)
                {
                    case NpcRole.Merchant:
                        var merchInteract = go.AddComponent<NoteInteract>(); // 复用 InteractableBase 变体
                        merchInteract.noteKey = $"npc_{id.Path}";
                        // 商店绑定由 ShopUtils 处理
                        break;

                    case NpcRole.QuestGiver:
                        {
                            var qg = go.AddComponent<QuestGiver>();
                            if (!string.IsNullOrEmpty(config.QuestGiverId))
                            {
                                // 支持自定义 QuestGiverID（int 值）和原生枚举名称
                                SetQuestGiverId(qg, config.QuestGiverId);
                            }
                            else
                            {
                                // QuestGiverId 未指定 — 使用默认值 QuestGiverID.None (0)
                                // 可通过 BindQuestGiver(Identifier, Identifier) 后续绑定自定义 QuestGiver
                            }
                        }
                        break;

                    case NpcRole.Companion:
                        var pmc = go.AddComponent<global::InteractablePMC>();
                        // InteractablePMC 默认行为已包含跟随逻辑
                        break;

                    case NpcRole.DialogueOnly:
                        // 仅对话，不绑定额外交互
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FML FriendlyNpc] Failed to add interaction component for NPC '{id}' (Role={config.Role}): {ex.Message}");
            }

            // 捏脸（通过 FaceRefResolver）
            // 注意：裸 AddComponent<CharacterModel>() 不完整——CharacterModel 需要 Animator、
            // SkinnedMeshRenderer 等子结构，这些通常由 CharacterRandomPreset.CreateCharacterAsync
            // 或从 Prefab 实例化生成。当前 bare GameObject 路径将在 Phase 6 重构为
            // CharacterRandomPreset 生成路径，届时 CharacterModel 和 CustomFaceInstance 将自动就绪。
            var model = go.GetComponent<global::CharacterModel>();
            if (model != null && config.Face.Mode != FaceRefMode.None)
            {
                FaceRefResolver.ApplyToModel(model, config.Face);
            }

            _registry.Set(id, go, owner);
            EventBusManager.Instance.Sync.Post(new NpcCreatedEvent(id));

            return go;
        }

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
            // 商店注册由 ShopUtils 完成，此处仅记录绑定关系
            // 实际交互时由 InteractableBase.OnInteractFinished 触发 ShopUtils.OpenShop
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

        /// <summary>
        /// 为 NPC 绑定已注册的 QuestGiver（Identifier 版本）。
        /// 从 QuestGiverUtils 查找已注册的 QuestGiver，将其 ID 绑定到此 NPC。
        /// </summary>
        public static void BindQuestGiver(Identifier npcId, Identifier questGiverId)
        {
            if (!_registry.TryGet(npcId, out var go) || go == null) return;

            // 从 QuestGiverUtils 查询自定义 ID
            if (QuestGiverUtils.TryGetQuestGiverId(questGiverId, out int giverIntId))
            {
                var qg = go.GetComponent<QuestGiver>();
                if (qg == null) qg = go.AddComponent<QuestGiver>();
                SetQuestGiverId(qg, giverIntId.ToString());
            }
        }

        /// <summary>按 Identifier 销毁 NPC。</summary>
        public static bool RemoveNpc(Identifier id)
        {
            if (!_registry.TryGet(id, out var go)) return false;
            if (go != null) UnityEngine.Object.Destroy(go);
            return _registry.Remove(id);
        }

        /// <summary>批量卸载指定 mod 的全部 NPC。</summary>
        public static int RemoveAllNpcs(string modid) => _registry.RemoveAllByOwner(modid);

        // ===== 内部辅助 =====

        private static void SetActorId(DuckovDialogueActor actor, string actorId)
        {
            var field = typeof(DuckovDialogueActor).GetField("id",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            field?.SetValue(actor, actorId);
        }

        /// <summary>
        /// 设置 QuestGiver 组件的 questGiverID 字段。
        /// 支持三种格式：
        /// 1. 自定义 int 值（如 "1000"）→ 直接赋整数值（配合 QuestGiverUtils）
        /// 2. 原生枚举名称（如 "Jeff"）→ Enum.Parse 匹配
        /// 3. 空/null → 不设置
        /// </summary>
        private static void SetQuestGiverId(QuestGiver qg, string questGiverId)
        {
            if (string.IsNullOrEmpty(questGiverId)) return;

            var field = typeof(QuestGiver).GetField("questGiverID",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            if (field == null) return;

            // 优先尝试 int 值（自定义 QuestGiverID）
            if (int.TryParse(questGiverId, out int customId) && customId >= 50)
            {
                // 自定义 ID：通过 Enum.ToObject 转换为 QuestGiverID 枚举值
                field.SetValue(qg, Enum.ToObject(field.FieldType, customId));
                return;
            }

            // 尝试作为枚举名称
            try
            {
                var enumVal = Enum.Parse(typeof(QuestGiverID), questGiverId);
                field.SetValue(qg, enumVal);
            }
            catch (ArgumentException)
            {
                Debug.LogWarning($"[FML] Unknown QuestGiverId '{questGiverId}' — " +
                    "not a valid enum name or custom int ID. QuestGiver component will have default ID.");
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
