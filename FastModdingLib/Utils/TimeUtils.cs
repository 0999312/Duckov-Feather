using Duckov.Utilities;
using System;
using UnityEngine;

namespace FeatherMod.Utils
{
    /// <summary>
    /// 游戏内时间工具。提供 GameClock 访问、TimeSpan 序列化和时间差计算。
    /// 用于建筑设备的离线进度计算和时间戳持久化。
    /// </summary>
    public static class TimeUtils
    {
        /// <summary>获取当前游戏内时间。</summary>
        public static TimeSpan Now => GameClock.Now;

        /// <summary>将 TimeSpan 序列化为可持久化的字符串（格式: "d.hh:mm:ss"）。</summary>
        public static string TimeSpanToString(TimeSpan time)
            => time.ToString(@"d\.hh\:mm\:ss");

        /// <summary>从字符串反序列化 TimeSpan。失败返回 false。</summary>
        public static bool TryStringToTimeSpan(string timeStr, out TimeSpan result)
        {
            result = TimeSpan.Zero;
            if (string.IsNullOrEmpty(timeStr)) return false;
            if (TimeSpan.TryParseExact(timeStr, @"d\.hh\:mm\:ss", null, out result))
                return true;
            return TimeSpan.TryParse(timeStr, out result);
        }

        /// <summary>
        /// 计算自 pastTime 以来经过的游戏内小时数（正数）。
        /// 若 pastTime 在未来（不应出现），返回 0。
        /// </summary>
        public static float GetPositiveHoursSince(TimeSpan pastTime)
        {
            var now = GameClock.Now;
            var elapsed = now - pastTime;
            if (elapsed.TotalHours < 0) return 0f;
            return (float)elapsed.TotalHours;
        }

        /// <summary>
        /// 计算自 pastTime 以来经过的游戏内秒数（正数）。
        /// </summary>
        public static float GetPositiveSecondsSince(TimeSpan pastTime)
        {
            var now = GameClock.Now;
            var elapsed = now - pastTime;
            if (elapsed.TotalSeconds < 0) return 0f;
            return (float)elapsed.TotalSeconds;
        }

        /// <summary>
        /// 将当前时间序列化为字符串（用于 Item.SetString 持久化时间戳）。
        /// </summary>
        public static string NowAsString() => TimeSpanToString(GameClock.Now);

        /// <summary>
        /// 计算两个时间之间的正小时差。
        /// </summary>
        public static float GetPositiveHoursBetween(TimeSpan start, TimeSpan end)
        {
            var elapsed = end - start;
            return elapsed.TotalHours > 0 ? (float)elapsed.TotalHours : 0f;
        }
    }
}
