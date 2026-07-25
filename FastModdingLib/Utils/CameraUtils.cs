using Cinemachine;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FeatherMod.Utils
{
    /// <summary>
    /// 对话镜头平滑过渡工具。
    /// 参考原游戏 <c>GamingConsole.AnimateCameraIn</c> 模式。
    /// </summary>
    public static class CameraUtils
    {
        /// <summary>默认过渡曲线（ease-in-out）。</summary>
        private static readonly AnimationCurve s_defaultCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        /// <summary>
        /// 平滑过渡游戏主镜头到目标位置 + 注视点。
        /// 安全：GameCamera.Instance 为 null 时静默跳过。
        /// </summary>
        /// <param name="position">目标机位（世界坐标）。</param>
        /// <param name="lookAt">注视点（世界坐标）。</param>
        /// <param name="duration">过渡时长（秒）。</param>
        /// <param name="curve">过渡曲线。null 使用默认 ease-in-out。</param>
        public static async UniTask AnimateCameraTo(
            Vector3 position,
            Vector3 lookAt,
            float duration,
            AnimationCurve? curve = null)
        {
            var gameCam = GameCamera.Instance;
            if (gameCam == null || gameCam.mainVCam == null) return;

            var vcam = gameCam.mainVCam;
            var vcamTransform = vcam.transform;
            var startPos = vcamTransform.position;
            var startRot = vcamTransform.rotation;
            var targetRot = Quaternion.LookRotation(lookAt - position);

            curve ??= s_defaultCurve;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
                vcamTransform.position = Vector3.Lerp(startPos, position, t);
                vcamTransform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                await UniTask.NextFrame();
            }

            // 确保精确到位
            vcamTransform.position = position;
            vcamTransform.rotation = targetRot;
        }

        /// <summary>
        /// 恢复游戏默认镜头（玩家跟随视角）。
        /// 通过禁用当前 VCam 让 CinemachineBrain 自动切回默认 VCam。
        /// </summary>
        /// <param name="duration">过渡时长（秒）。由 Cinemachine 的 Blend 设置控制。</param>
        public static async UniTask RestoreGameplayCamera(float duration = 1f)
        {
            var gameCam = GameCamera.Instance;
            if (gameCam == null) return;

            // 如果当前有临时 VCam 在激活状态，禁用它
            // CinemachineBrain 会自动 blend 回最高优先级的活跃 VCam（即默认游戏 VCam）
            var brain = gameCam.brain;
            if (brain != null && brain.ActiveVirtualCamera != null)
            {
                var activeVCam = brain.ActiveVirtualCamera.VirtualCameraGameObject;
                if (activeVCam != null && activeVCam != gameCam.mainVCam.gameObject)
                {
                    // 禁用非默认 VCam，Cinemachine 自动 blend 回 mainVCam
                    activeVCam.SetActive(false);
                }
            }

            // 等待 blend 完成
            if (duration > 0f)
                await UniTask.Delay((int)(duration * 1000), ignoreTimeScale: true);
        }

        /// <summary>
        /// 应用 <see cref="DialogueCameraShot"/> 到游戏镜头。
        /// 优先使用 VcamName 引用场景 VCam；否则使用内联坐标。
        /// 特殊值 "__RESUME__" 恢复游戏镜头。
        /// </summary>
        public static async UniTask ApplyCameraShot(DialogueCameraShot shot)
        {
            if (shot.IsResumeMarker)
            {
                await RestoreGameplayCamera(shot.BlendTime);
                return;
            }

            // ── 方式 1：按名称引用场景 VCam ──
            if (!string.IsNullOrEmpty(shot.VcamName))
            {
                var vcamGo = GameObject.Find(shot.VcamName);
                if (vcamGo != null)
                {
                    var vcam = vcamGo.GetComponent<CinemachineVirtualCamera>();
                    if (vcam != null)
                    {
                        // 激活目标 VCam，CinemachineBrain 自动 blend
                        // 先禁用当前非默认 VCam
                        var gameCam = GameCamera.Instance;
                        if (gameCam?.brain?.ActiveVirtualCamera != null)
                        {
                            var activeGo = gameCam.brain.ActiveVirtualCamera.VirtualCameraGameObject;
                            if (activeGo != null && activeGo != gameCam.mainVCam.gameObject)
                                activeGo.SetActive(false);
                        }
                        vcam.gameObject.SetActive(true);
                        if (shot.BlendTime > 0f)
                            await UniTask.Delay((int)(shot.BlendTime * 1000), ignoreTimeScale: true);
                        return;
                    }
                }
                Debug.LogWarning($"[FML Camera] VCam '{shot.VcamName}' not found, falling back to inline coordinates.");
            }

            // ── 方式 2：内联坐标（Position + 动态/静态 LookAt）──
            if (shot.Position.HasValue)
            {
                var lookAt = shot.ResolveLookAt();
                if (lookAt.HasValue)
                {
                    await AnimateCameraTo(shot.Position.Value, lookAt.Value, shot.BlendTime);
                    return;
                }
            }

            // ── 方式 3：仅动态注视点（无 Position，不改机位只转头）──
            if (shot.LookAtNpc != null || !string.IsNullOrEmpty(shot.LookAtActorId))
            {
                var lookAt = shot.ResolveLookAt();
                var gameCam = GameCamera.Instance;
                if (lookAt.HasValue && gameCam?.mainVCam != null)
                {
                    var camPos = gameCam.mainVCam.transform.position;
                    await AnimateCameraTo(camPos, lookAt.Value, shot.BlendTime);
                    return;
                }
            }

            Debug.LogWarning("[FML Camera] CameraShot has no valid position or lookAt target. Ignored.");
        }
    }
}
