using System;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// FaceRef 运行时解析器。将 <see cref="FaceRef"/> 应用到 <see cref="global::CharacterModel"/>。
    /// </summary>
    internal static class FaceRefResolver
    {
        /// <summary>按名称查找已有 CustomFacePreset。</summary>
        internal static global::CustomFacePreset? FindPresetByName(string name)
        {
            var preset = Resources.Load<global::CustomFacePreset>($"CustomFacePreset_{name}");
            if (preset == null)
                Debug.LogWarning($"[FML FaceRef] CustomFacePreset '{name}' not found.");
            return preset;
        }

        /// <summary>根据 FacePartIds 创建自定义捏脸预设。</summary>
        internal static global::CustomFacePreset CreateCustomFacePreset(FacePartIds parts)
        {
            var preset = ScriptableObject.CreateInstance<global::CustomFacePreset>();
            TrySetSettings(preset, parts);
            return preset;
        }

        /// <summary>将 FaceRef 应用到 CharacterModel。</summary>
        internal static void ApplyToModel(global::CharacterModel model, FaceRef face)
        {
            if (model == null) return;

            switch (face.Mode)
            {
                case FaceRefMode.Preset:
                    if (!string.IsNullOrEmpty(face.PresetName))
                    {
                        var p = FindPresetByName(face.PresetName);
                        if (p != null) model.SetFaceFromPreset(p);
                    }
                    break;

                case FaceRefMode.PlayerFace:
                    model.SetFaceFromPreset(null);
                    break;

                case FaceRefMode.Custom:
                    model.SetFaceFromPreset(CreateCustomFacePreset(face.CustomParts));
                    break;
            }
        }

        private static void TrySetSettings(global::CustomFacePreset preset, FacePartIds parts)
        {
            try
            {
                var field = typeof(global::CustomFacePreset).GetField("settings",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                if (field == null) return;
                var s = field.GetValue(preset);
                if (s == null) return;

                var t = s.GetType();
                TrySet(t, s, "hairId", parts.HairId);
                TrySet(t, s, "eyeId", parts.EyeId);
                TrySet(t, s, "mouthId", parts.MouthId);
                TrySet(t, s, "eyebrowId", parts.EyebrowId);
                TrySet(t, s, "decorationId", parts.DecorationId);
                TrySet(t, s, "tailId", parts.TailId);
                TrySet(t, s, "footId", parts.FootId);
                TrySet(t, s, "wingId", parts.WingId);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FML FaceRef] Settings injection: {e.Message}");
            }
        }

        private static void TrySet(Type t, object obj, string fn, string? v)
        {
            if (string.IsNullOrEmpty(v)) return;
            t.GetField(fn,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                ?.SetValue(obj, v);
        }
    }
}
