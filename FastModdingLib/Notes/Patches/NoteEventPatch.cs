using Duckov.NoteIndexs;
using FeatherMod.Events;
using FeatherMod.Utils;
using HarmonyLib;
using System;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// Harmony 补丁：将 <see cref="NoteIndex"/> 的原生 onNoteStatusChanged 事件
    /// 桥接到 FML EventBus，提供带 Identifier 的事件。
    /// </summary>
    internal static class NoteEventPatch
    {
        private static bool _patched;

        /// <summary>应用补丁（幂等）。</summary>
        internal static void EnsurePatched()
        {
            if (_patched) return;
            _patched = true;

            try
            {
                var harmony = new Harmony("Feather.NoteEvents");
                FeatherMod.ModBehaviour.ExtraHarmonies.Add(harmony);
                harmony.Patch(
                    original: typeof(NoteIndex).GetMethod(nameof(NoteIndex.SetNoteUnlocked)),
                    postfix: new HarmonyMethod(typeof(NoteEventPatch), nameof(OnNoteUnlocked)));
                harmony.Patch(
                    original: typeof(NoteIndex).GetMethod(nameof(NoteIndex.SetNoteRead)),
                    postfix: new HarmonyMethod(typeof(NoteEventPatch), nameof(OnNoteRead)));
            }
            catch (Exception e)
            {
                Debug.LogError($"[FML NoteEventPatch] Failed to apply patches: {e}");
                _patched = false;
            }
        }

        private static void OnNoteUnlocked(string noteKey)
        {
            try
            {
                if (string.IsNullOrEmpty(noteKey)) return;

                // 从注册表反查 Identifier
                if (TryResolveIdentifier(noteKey, out var id))
                {
                    EventBusManager.Instance.Sync.Post(new NoteUnlockedEvent(id));
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FML NoteEventPatch] OnNoteUnlocked failed: {e}");
            }
        }

        private static void OnNoteRead(string noteKey)
        {
            try
            {
                if (string.IsNullOrEmpty(noteKey)) return;

                if (TryResolveIdentifier(noteKey, out var id))
                {
                    EventBusManager.Instance.Sync.Post(new NoteReadEvent(id));
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FML NoteEventPatch] OnNoteRead failed: {e}");
            }
        }

        private static bool TryResolveIdentifier(string key, out Identifier id)
        {
            // 遍历注册表中的所有 key 映射
            foreach (var kvp in NoteUtils.Registry.GetAllKeys())
            {
                if (kvp.Value == key)
                {
                    id = kvp.Key;
                    return true;
                }
            }
            id = null!;
            return false;
        }
    }
}
