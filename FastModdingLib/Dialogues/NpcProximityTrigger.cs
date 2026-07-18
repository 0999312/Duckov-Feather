using Cysharp.Threading.Tasks;
using FeatherMod.Utils;
using Saves;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// NPC 接近触发对话组件。挂载到 NPC GameObject 上，
    /// 在 Update 中检测玩家距离，满足条件时通过 <see cref="DialogueManager.PlayDialogue"/>
    /// 播放对话（优先全屏面板，失败时降级为气泡）。
    /// 由 <see cref="FriendlyNpcUtils.AttachInteractionComponents"/> 根据
    /// <see cref="FriendlyNpcConfig.ProximityDialogue"/> 自动挂载。
    ///
    /// 🆕 Bug Fix: <see cref="DialogueTriggerMode.Once"/> 状态通过静态字典跨 NPC 实例持久化，
    /// 防止建筑重建/场景重载后对话重复触发。
    /// </summary>
    public class NpcProximityTrigger : MonoBehaviour
    {
        /// <summary>NPC Identifier。</summary>
        public Identifier NpcId;

        /// <summary>对话使用的 Actor ID（与 DuckovDialogueActor.id 对应）。为空时回退到 NpcId.Path。</summary>
        public string? ActorId;

        /// <summary>接近触发距离（米）。</summary>
        public float Distance = 3f;

        /// <summary>对话内容。</summary>
        public DialogueLine[] Lines = System.Array.Empty<DialogueLine>();

        /// <summary>触发模式。</summary>
        public DialogueTriggerMode Mode = DialogueTriggerMode.Once;

        // 🆕 Bug Fix: Once 模式触发状态由静态字典持久化（跨 NPC 实例/NPC 重生/NPC 销毁），
        // 避免因建筑重建、场景重载导致新的 NpcProximityTrigger 实例重置 _triggered。
        private static readonly Dictionary<Identifier, bool> s_triggeredOnceStates = new();

        private Transform? _playerTransform;
        private float _nextCheckTime;
        private const float CheckInterval = 0.5f;

        private bool AlreadyTriggered
        {
            get => Mode == DialogueTriggerMode.Once
                   && s_triggeredOnceStates.TryGetValue(NpcId, out var v) && v;
            set { if (Mode == DialogueTriggerMode.Once) s_triggeredOnceStates[NpcId] = value; }
        }

        private void Start()
        {
            FindPlayer();

            // ES3 跨会话持久化：Once 模式下，从存档恢复已触发状态
            if (Mode == DialogueTriggerMode.Once && !AlreadyTriggered)
            {
                var saveKey = $"fml_npc_trigger_{NpcId.Domain}_{NpcId.Path}";
                if (SavesSystem.KeyExisits(saveKey) && SavesSystem.Load<bool>(saveKey))
                {
                    AlreadyTriggered = true;
                }
            }
        }

        private void Update()
        {
            if (AlreadyTriggered) return;
            if (Lines == null || Lines.Length == 0) return;
            if (Time.time < _nextCheckTime) return;
            _nextCheckTime = Time.time + CheckInterval;

            if (_playerTransform == null)
            {
                FindPlayer();
                return;
            }

            if (Vector3.Distance(transform.position, _playerTransform.position) <= Distance)
            {
                AlreadyTriggered = true;
                // ES3 跨会话持久化：触发后写入存档
                if (Mode == DialogueTriggerMode.Once)
                {
                    var saveKey = $"fml_npc_trigger_{NpcId.Domain}_{NpcId.Path}";
                    SavesSystem.Save(saveKey, true);
                }
                PlayDialogue().Forget();
            }
        }

        private void FindPlayer()
        {
            var mainChar = LevelManager.Instance?.MainCharacter;
            if (mainChar != null)
                _playerTransform = mainChar.transform;
        }

        private async UniTask PlayDialogue()
        {
            // 优先组件上显式指定的 ActorId；否则查询 NPC 配置中的 ActorId；最后回退 NpcId.Path
            var actorId = !string.IsNullOrEmpty(ActorId) ? ActorId
                : FriendlyNpcUtils.TryGetNpcActorId(NpcId, out var configActorId) ? configActorId
                : NpcId.Path;
            await DialogueManager.PlayDialogue(actorId, Lines);
        }
    }
}
