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
                // CustomFaceSettingData 是 struct——先取副本、修改、再整体写回，否则修改无效。
                // 字段（hairID/eyeID/...）经 Krafs.Publicizer 已公开，直接访问零反射。
                var s = preset.settings;
                s.savedSetting = true;
                if (!string.IsNullOrEmpty(parts.HairId)) s.hairID = int.TryParse(parts.HairId, out var h) ? h : 0;
                if (!string.IsNullOrEmpty(parts.EyeId)) s.eyeID = int.TryParse(parts.EyeId, out var e) ? e : 0;
                if (!string.IsNullOrEmpty(parts.MouthId)) s.mouthID = int.TryParse(parts.MouthId, out var m) ? m : 0;
                if (!string.IsNullOrEmpty(parts.EyebrowId)) s.eyebrowID = int.TryParse(parts.EyebrowId, out var eb) ? eb : 0;
                if (!string.IsNullOrEmpty(parts.TailId)) s.tailID = int.TryParse(parts.TailId, out var t) ? t : 0;
                if (!string.IsNullOrEmpty(parts.FootId)) s.footID = int.TryParse(parts.FootId, out var f) ? f : 0;
                if (!string.IsNullOrEmpty(parts.WingId)) s.wingID = int.TryParse(parts.WingId, out var w) ? w : 0;
                // 原生无 decorationId 字段（对应部件归类到 eye/其他分类），忽略 DecorationId
                preset.settings = s; // struct 写回
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FML FaceRef] Settings injection: {e.Message}");
            }
        }
    }
}
