using FeatherMod.Events;
using System;
using System.Reflection;
using UnityEngine;

namespace FeatherMod
{
    internal static class WeatherEventPatch
    {
        private static bool _patched;
        internal static void EnsurePatched()
        {
            if (_patched) return; _patched = true;
            try
            {
                // 通过 BindingFlags.Static 消除 Publicizer 二义性
                var started = typeof(TimeOfDayController).GetEvent("OnStormStarted",
                    BindingFlags.Public | BindingFlags.Static);
                var ended = typeof(TimeOfDayController).GetEvent("OnStormEnded",
                    BindingFlags.Public | BindingFlags.Static);
                started?.AddEventHandler(null, (Action)OnStarted);
                ended?.AddEventHandler(null, (Action)OnEnded);
            }
            catch (Exception e) { Debug.LogError($"[FML Weather] {e}"); _patched = false; }
        }
        static void OnStarted() { try { EventBusManager.Instance.Sync.Post(new StormStartedEvent()); } catch { } }
        static void OnEnded() { try { EventBusManager.Instance.Sync.Post(new StormEndedEvent()); } catch { } }
    }
}
