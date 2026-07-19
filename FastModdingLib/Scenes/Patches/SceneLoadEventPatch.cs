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
                // 原生签名：Action<MultiSceneCore, Scene>
                loaded?.AddEventHandler(null, (Action<MultiSceneCore, Scene>)OnLoaded);
                unloading?.AddEventHandler(null, (Action<MultiSceneCore, Scene>)OnUnloading);
            }
            catch (Exception e) { Debug.LogError($"[FML Scenes] {e}"); _patched = false; }
        }
        static void OnLoaded(MultiSceneCore core, Scene scene)
        {
            try
            {
                var n = MultiSceneCore.ActiveSubSceneID; if (string.IsNullOrEmpty(n)) return;
                var id = MultiSceneUtils.Registry.TryGetIdentifier(n, out var i) ? i : new Identifier("duckov", n);
                EventBusManager.Instance.Sync.Post(new SceneLoadFinishedEvent(id));
            }
            catch { }
        }
        static void OnUnloading(MultiSceneCore core, Scene scene)
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
