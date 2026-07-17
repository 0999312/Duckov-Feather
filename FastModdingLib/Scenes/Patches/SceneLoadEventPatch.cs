using Duckov.Scenes;
using FeatherMod.Events;
using FeatherMod.Utils;
using System;
using System.Reflection;
using UnityEngine;

namespace FeatherMod
{
    internal static class SceneLoadEventPatch
    {
        private static bool _patched;
        internal static void EnsurePatched()
        {
            if (_patched) return; _patched = true;
            try
            {
                var loaded = typeof(MultiSceneCore).GetEvent("OnSubSceneLoaded",
                    BindingFlags.Public | BindingFlags.Static);
                var unloading = typeof(MultiSceneCore).GetEvent("OnSubSceneWillBeUnloaded",
                    BindingFlags.Public | BindingFlags.Static);
                loaded?.AddEventHandler(null, (Action)OnLoaded);
                unloading?.AddEventHandler(null, (Action)OnUnloading);
            }
            catch (Exception e) { Debug.LogError($"[FML Scenes] {e}"); _patched = false; }
        }
        static void OnLoaded()
        {
            try
            {
                var n = MultiSceneCore.ActiveSubSceneID; if (string.IsNullOrEmpty(n)) return;
                var id = MultiSceneUtils.Registry.TryGetIdentifier(n, out var i) ? i : new Identifier("duckov", n);
                EventBusManager.Instance.Sync.Post(new SceneLoadFinishedEvent(id));
            }
            catch { }
        }
        static void OnUnloading()
        {
            try
            {
                var n = MultiSceneCore.ActiveSubSceneID; if (string.IsNullOrEmpty(n)) return;
                var old = MultiSceneUtils.Registry.TryGetIdentifier(n, out var i) ? i : new Identifier("duckov", n);
                EventBusManager.Instance.Sync.Post(new SubSceneChangedEvent(old, new Identifier("duckov", "unknown")));
            }
            catch { }
        }
    }
}
