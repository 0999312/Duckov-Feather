using Cysharp.Threading.Tasks;
using Duckov.Utilities;
using FeatherMod.Utils;
using System;
using System.Threading;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// 异步生产计时器。使用 GameClock 驱动（不受真实时间影响），
    /// 支持离线追赶和多周期批量结算。
    /// 供 SimpleMachineRecipe 内部使用，自定义 MachineRecipe 子类也可直接调用。
    /// </summary>
    internal class ProductionTimer
    {
        private float _progress;
        private TimeSpan _lastTickTime;
        private CancellationTokenSource? _cts;

        /// <summary>当前累计进度（0~1，≥1 时触发 onCycleComplete 并重置）。</summary>
        public float Progress
        {
            get => _progress;
            set => _progress = Mathf.Clamp01(value);
        }

        /// <summary>是否正在运行。</summary>
        public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

        /// <summary>
        /// 启动计时循环。每秒 tick 一次，计算游戏内时间差。
        /// 累计进度 ≥ 1.0 时触发 onCycleComplete 回调并重置。
        /// durationSeconds = null 时立即执行一次后退出。
        /// </summary>
        /// <param name="durationSeconds">每周期所需游戏内秒数。null = 即时。</param>
        /// <param name="onCycleComplete">每周期完成时的回调。</param>
        /// <param name="initialProgress">初始进度（用于读档恢复）。</param>
        public async UniTask Run(float? durationSeconds, Action onCycleComplete, float initialProgress = 0f)
        {
            if (durationSeconds == null || durationSeconds <= 0f)
            {
                onCycleComplete();
                return;
            }

            _cts = new CancellationTokenSource();
            _progress = initialProgress;
            _lastTickTime = GameClock.Now;

            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    await UniTask.Delay(1000, cancellationToken: _cts.Token);

                    var now = GameClock.Now;
                    var elapsed = (float)(now - _lastTickTime).TotalSeconds;
                    _lastTickTime = now;

                    // 离线追赶：如果游戏时间跳了很多，累积多倍进度
                    _progress += elapsed / durationSeconds.Value;

                    while (_progress >= 1f)
                    {
                        _progress -= 1f;
                        try { onCycleComplete(); }
                        catch (Exception e) { Debug.LogError($"[ProductionTimer] onCycleComplete threw: {e}"); }
                    }
                }
            }
            catch (OperationCanceledException) { /* 正常取消 */ }
            finally
            {
                _cts?.Dispose();
                _cts = null;
            }
        }

        /// <summary>停止计时器。</summary>
        public void Stop()
        {
            _cts?.Cancel();
        }

        /// <summary>
        /// 序列化当前进度状态（用于存档）。
        /// 格式: "progress|lastTickTime"
        /// </summary>
        public string SerializeState()
        {
            return $"{_progress}|{TimeUtils.TimeSpanToString(_lastTickTime)}";
        }

        /// <summary>从存档恢复进度状态。</summary>
        public void DeserializeState(string? json)
        {
            if (string.IsNullOrEmpty(json)) return;
            var parts = json.Split('|');
            if (parts.Length >= 1) float.TryParse(parts[0], out _progress);
            if (parts.Length >= 2) TimeUtils.TryStringToTimeSpan(parts[1], out _lastTickTime);
        }
    }
}
