using Cysharp.Threading.Tasks;
using FeatherMod.Utils;
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

        private bool _triggered;
        private Transform? _playerTransform;
        private float _nextCheckTime;
        private const float CheckInterval = 0.5f;

        private void Start()
        {
            FindPlayer();
        }

        private void Update()
        {
            if (_triggered && Mode == DialogueTriggerMode.Once) return;
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
                _triggered = true;
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
            var actorId = !string.IsNullOrEmpty(ActorId) ? ActorId : NpcId.Path;
            await DialogueManager.PlayDialogue(actorId, Lines);
        }
    }
}
