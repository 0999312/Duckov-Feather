using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// NPC 自动面向玩家组件。通过设置游戏原生 <c>Movement.targetAimDirection</c>
    /// 让 <c>Movement.UpdateRotation</c> 自动平滑旋转 <c>rotationRoot</c>，
    /// 与游戏 AI 的旋转机制完全一致，不会产生抽搐或被覆盖。
    /// 由 <see cref="FriendlyNpcUtils.AttachInteractionComponents"/> 根据
    /// <see cref="FriendlyNpcConfig.AutoFacePlayer"/> 自动挂载。
    /// </summary>
    public class NpcFacePlayer : MonoBehaviour
    {
        private Movement? _movement;
        private Transform? _playerTransform;

        private void Start()
        {
            FindPlayer();
        }

        private void Update()
        {
            if (_movement == null)
            {
                var cc = GetComponent<CharacterMainControl>();
                if (cc != null) _movement = cc.movementControl;
                if (_movement == null) return;
            }

            if (_playerTransform == null)
            {
                FindPlayer();
                return;
            }

            var dir = _playerTransform.position - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.001f)
            {
                // 设置 targetAimDirection，让 Movement.UpdateRotation 自动平滑旋转
                // 不使用 SetAimPoint（会同时设置瞄准目标点，改变 IK）
                _movement.targetAimDirection = dir;
            }
        }

        private void FindPlayer()
        {
            var mainChar = LevelManager.Instance?.MainCharacter;
            if (mainChar != null)
                _playerTransform = mainChar.transform;
        }
    }
}
