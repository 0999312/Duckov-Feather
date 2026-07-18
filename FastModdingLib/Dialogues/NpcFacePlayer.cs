using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// NPC 面向组件。通过游戏原生 <c>CharacterMainControl.SetAimPoint</c> 设置瞄准点，
    /// 由 <c>Movement.UpdateAiming → UpdateRotation</c> 原生管线平滑转向——
    /// 与原版友善 NPC（小明行为树 AimToPlayer 任务）走的是完全相同的机制。
    ///
    /// 注意：不可直接写 <c>Movement.targetAimDirection</c>——闲置友善 NPC 的
    /// <c>CharacterMainControl.IsAiming()</c> 返回 true，<c>UpdateAiming</c> 每帧会用
    /// 瞄准点重算并覆盖 targetAimDirection，直接写入会被冲掉。
    ///
    /// 两种模式：<see cref="FixedDirection"/> 非 null 时面向固定方向；
    /// 否则在 <see cref="FollowRange"/> 内跟随玩家。由
    /// <see cref="FriendlyNpcUtils.AttachInteractionComponents"/> 根据
    /// <see cref="FriendlyNpcConfig.AutoFacePlayer"/> 自动挂载。
    /// </summary>
    public class NpcFacePlayer : MonoBehaviour
    {
        /// <summary>固定朝向（世界方向，水平）。非 null 时面向该方向，不再跟随玩家。</summary>
        public Vector3? FixedDirection;

        /// <summary>跟随玩家的最大距离（米）。超出后 NPC 保持当前朝向。</summary>
        public float FollowRange = 10f;

        private CharacterMainControl? _cc;
        private Transform? _playerTransform;

        private void Start()
        {
            FindPlayer();
        }

        private void Update()
        {
            if (_cc == null)
            {
                _cc = GetComponent<CharacterMainControl>();
                if (_cc == null) return;
            }

            // 固定朝向模式：瞄准点设为朝向前方 5m，原生管线转向该方向
            if (FixedDirection.HasValue)
            {
                var dir = FixedDirection.Value;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                    _cc.SetAimPoint(transform.position + dir.normalized * 5f);
                return;
            }

            if (_playerTransform == null)
            {
                FindPlayer();
                return;
            }

            var toPlayer = _playerTransform.position - transform.position;
            toPlayer.y = 0f;

            if (toPlayer.magnitude <= FollowRange && toPlayer.sqrMagnitude > 0.25f)
            {
                // 跟随模式：瞄准点设为玩家位置（原版 AimToPlayer 行为）
                _cc.SetAimPoint(_playerTransform.position);
            }
            else
            {
                // 超出范围：瞄准自身位置，UpdateAiming 不再改写朝向，保持当前面向
                _cc.SetAimPoint(transform.position);
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
