using Cysharp.Threading.Tasks;
using FeatherMod.Utils;
using Saves;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// NPC 接近触发对话组件。挂载到 NPC GameObject 上，
    /// 在 Update 中检测玩家距离，满足条件时通过 <see cref="DialogueManager.PlayDialogue"/>
    /// 播放对话（优先全屏面板，失败时降级为气泡）。
    /// 由 <see cref="FriendlyNpcUtils.AttachInteractionComponents"/> 根据
    /// <see cref="FriendlyNpcConfig.ProximityDialogue"/> 自动挂载。
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

        /// <summary>
        /// Once 模式已触发标志（实例字段，跟随 GameObject 生命周期）。
        /// 跨会话持久化由 SavesSystem（per-save ES3）负责：Start 时从存档恢复，
        /// 触发时写入存档。新游戏开档自动重置。
        /// </summary>
        private bool _triggered;

        private Transform? _playerTransform;
        private float _nextCheckTime;
        private const float CheckInterval = 0.5f;

        private bool AlreadyTriggered
        {
            get => Mode == DialogueTriggerMode.Once && _triggered;
            set { if (Mode == DialogueTriggerMode.Once) _triggered = value; }
        }

        private void Start()
        {
            FindPlayer();

            // SavesSystem 跨会话持久化：Once 模式下，从游戏存档恢复已触发状态
            if (Mode == DialogueTriggerMode.Once && !_triggered)
            {
                var saveKey = SaveKey();
                if (SavesSystem.KeyExisits(saveKey) && SavesSystem.Load<bool>(saveKey))
                {
                    _triggered = true;
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
                // SavesSystem 跨会话持久化：触发后写入游戏存档
                if (Mode == DialogueTriggerMode.Once)
                {
                    SavesSystem.Save(SaveKey(), true);
                }
                PlayDialogue().Forget();
            }
        }

        private string SaveKey() => $"fml_npc_trigger_{NpcId.Domain}_{NpcId.Path}";

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
