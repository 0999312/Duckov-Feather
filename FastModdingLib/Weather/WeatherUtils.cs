using Duckov.Weathers;
using FeatherMod.Events;
using FeatherMod.Register;
using FeatherMod.Utils;
using FmlEvent = FeatherMod.Events.Event;
using System;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// 天气与季节系统公共 API。提供天气查询、天气覆盖、风暴信息和温度/防护查询。
    /// </summary>
    public static class WeatherUtils
    {
        private static bool _initialized;

        /// <summary>初始化（幂等）。</summary>
        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            WeatherEventPatch.EnsurePatched();
        }

        // ===== 天气查询 =====

        /// <summary>获取当前天气（FML WeatherType）。</summary>
        public static WeatherType GetCurrentWeather()
        {
            var native = WeatherManager.GetWeather();
            return ConvertFromNative(native);
        }

        /// <summary>获取指定时间的天气。</summary>
        public static WeatherType GetWeatherAt(TimeSpan time)
        {
            var native = WeatherManager.GetWeather(time);
            return ConvertFromNative(native);
        }

        /// <summary>强制覆盖天气（调试/剧情用）。</summary>
        public static void ForceWeather(WeatherType type, bool force = true)
        {
            WeatherManager.SetForceWeather(force, ConvertToNative(type));
            EventBusManager.Instance.Sync.Post(new WeatherChangedEvent(type));
        }

        /// <summary>取消强制天气覆盖，恢复确定性天气。</summary>
        public static void ResetWeather()
        {
            WeatherManager.SetForceWeather(false);
        }

        /// <summary>是否正在下雨。</summary>
        public static bool IsRaining()
        {
            var w = GetCurrentWeather();
            return w == WeatherType.Rainy || w == WeatherType.Stormy || w == WeatherType.SevereStormy;
        }

        /// <summary>是否正在下雪。</summary>
        public static bool IsSnowing()
        {
            return GetCurrentWeather() == WeatherType.Snow;
        }

        // ===== 季节 =====

        /// <summary>获取当前季节。</summary>
        public static SeasonType GetCurrentSeason()
        {
            var native = WeatherManager.Season;
            return native switch
            {
                Seasons.spring => SeasonType.Spring,
                Seasons.summer => SeasonType.Summer,
                Seasons.autumn => SeasonType.Autumn,
                Seasons.winter => SeasonType.Winter,
                _ => SeasonType.Spring
            };
        }

        // ===== 风暴信息 =====

        /// <summary>获取风暴等级（0=无风暴，1=Stormy_I，2=Stormy_II）。</summary>
        public static int GetStormLevel()
        {
            // 通过当前天气推断风暴等级
            var native = WeatherManager.GetWeather();
            return native switch
            {
                global::Duckov.Weathers.Weather.Stormy_I => 1,
                global::Duckov.Weathers.Weather.Stormy_II => 2,
                _ => 0
            };
        }

        /// <summary>是否正在风暴中。</summary>
        public static bool IsStormActive() => GetStormLevel() > 0;

        // ===== 温度 =====

        /// <summary>获取当前场景的寒冷等级（-10 到 +10）。</summary>
        public static float GetColdLevel()
        {
            return TimeOfDayController.coldLevel;
        }

        /// <summary>获取当前场景的炎热等级（-10 到 +10）。</summary>
        public static float GetHeatLevel()
        {
            return TimeOfDayController.heatLevel;
        }

        // ===== 防护属性查询 =====

        /// <summary>获取角色的风暴防护值。</summary>
        public static float GetStormProtection(CharacterMainControl character)
        {
            return character?.CharacterItem?.GetStatValue("StormProtection".GetHashCode()) ?? 0f;
        }

        /// <summary>获取角色的寒冷防护值。</summary>
        public static float GetColdProtection(CharacterMainControl character)
        {
            return character?.CharacterItem?.GetStatValue("ColdProtection".GetHashCode()) ?? 0f;
        }

        /// <summary>获取角色的炎热防护值。</summary>
        public static float GetHeatProtection(CharacterMainControl character)
        {
            return character?.CharacterItem?.GetStatValue("HeatProtection".GetHashCode()) ?? 0f;
        }

        // ===== 内部转换 =====

        private static WeatherType ConvertFromNative(global::Duckov.Weathers.Weather native)
        {
            return native switch
            {
                global::Duckov.Weathers.Weather.Sunny => WeatherType.Sunny,
                global::Duckov.Weathers.Weather.Cloudy => WeatherType.Cloudy,
                global::Duckov.Weathers.Weather.Rainy => WeatherType.Rainy,
                global::Duckov.Weathers.Weather.Snow => WeatherType.Snow,
                global::Duckov.Weathers.Weather.Stormy_I => WeatherType.Stormy,
                global::Duckov.Weathers.Weather.Stormy_II => WeatherType.SevereStormy,
                _ => WeatherType.Sunny
            };
        }

        private static global::Duckov.Weathers.Weather ConvertToNative(WeatherType type)
        {
            return type switch
            {
                WeatherType.Sunny => global::Duckov.Weathers.Weather.Sunny,
                WeatherType.Cloudy => global::Duckov.Weathers.Weather.Cloudy,
                WeatherType.Rainy => global::Duckov.Weathers.Weather.Rainy,
                WeatherType.Snow => global::Duckov.Weathers.Weather.Snow,
                WeatherType.Stormy => global::Duckov.Weathers.Weather.Stormy_I,
                WeatherType.SevereStormy => global::Duckov.Weathers.Weather.Stormy_II,
                _ => global::Duckov.Weathers.Weather.Sunny
            };
        }
    }

    /// <summary>FML 自有季节类型枚举。</summary>
    public enum SeasonType
    {
        Spring,
        Summer,
        Autumn,
        Winter
    }

    /// <summary>天气变化事件。</summary>
    public class WeatherChangedEvent : FmlEvent
    {
        public WeatherType NewWeather { get; }
        public WeatherChangedEvent(WeatherType newWeather) { NewWeather = newWeather; }
    }

    /// <summary>风暴开始事件。</summary>
    public class StormStartedEvent : FmlEvent { }

    /// <summary>风暴结束事件。</summary>
    public class StormEndedEvent : FmlEvent { }
}
