using System;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// 捏脸系统公共 API。支持从官方捏脸数据串（JSON）导入/导出捏脸数据，
    /// 让 Mod 可以动态修改玩家或任意角色的外观。
    /// </summary>
    /// <remarks>
    /// 游戏原生提供了 <see cref="global::CustomFaceSettingData"/> 结构体及其
    /// <c>DataToJson()</c> / <c>JsonToData()</c> 方法。本工具类在此之上封装了
    /// FML 风格的便捷 API，直接接受 JSON 字符串操作。
    /// </remarks>
    /// <example>
    /// <code>
    /// // 从游戏导出的官方捏脸数据串设置玩家脸部
    /// string faceJson = "{\"savedSetting\":false,\"headSetting\":...}";
    /// CustomFaceUtils.SetPlayerFaceFromJson(faceJson);
    ///
    /// // 导出玩家当前捏脸数据为 JSON
    /// string currentFace = CustomFaceUtils.GetPlayerFaceJson();
    /// </code>
    /// </example>
    public static class CustomFaceUtils
    {
        private static bool _initialized;

        /// <summary>初始化（幂等）。</summary>
        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;
        }

        // ═══════════════════════════════════════════════════════════════
        //  玩家主角捏脸
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 将官方捏脸数据串（JSON）应用到玩家主角。
        /// </summary>
        /// <param name="jsonString">游戏官方格式的捏脸 JSON 字符串。
        /// 可通过 <see cref="global::CustomFaceSettingData.DataToJson"/> 生成，
        /// 或从游戏存档中导出。</param>
        /// <returns>是否成功应用。</returns>
        public static bool SetPlayerFaceFromJson(string jsonString)
        {
            Init();
            var face = GetPlayerFaceInstance();
            if (face == null)
            {
                Debug.LogWarning("[FML CustomFaceUtils] MainCharacterFace not found in scene.");
                return false;
            }
            return SetFaceFromJson(face, jsonString);
        }

        /// <summary>
        /// 获取玩家主角当前的捏脸数据（JSON 字符串）。
        /// </summary>
        /// <returns>官方格式的捏脸 JSON 字符串，若主角不存在则返回空字符串。</returns>
        public static string GetPlayerFaceJson()
        {
            Init();
            var face = GetPlayerFaceInstance();
            if (face == null)
            {
                Debug.LogWarning("[FML CustomFaceUtils] MainCharacterFace not found in scene.");
                return string.Empty;
            }
            return GetFaceJson(face);
        }

        /// <summary>
        /// 使用原生 <see cref="global::CustomFaceSettingData"/> 设置玩家主角的捏脸。
        /// </summary>
        /// <param name="data">原生捏脸数据结构。</param>
        public static void SetPlayerFaceFromData(global::CustomFaceSettingData data)
        {
            Init();
            var face = GetPlayerFaceInstance();
            if (face == null)
            {
                Debug.LogWarning("[FML CustomFaceUtils] MainCharacterFace not found in scene.");
                return;
            }
            LoadFaceFromData(face, data);
        }

        /// <summary>
        /// 获取玩家主角当前的捏脸数据结构。
        /// </summary>
        /// <returns>原生 <see cref="global::CustomFaceSettingData"/> 结构。</returns>
        public static global::CustomFaceSettingData GetPlayerFaceAsData()
        {
            Init();
            var face = GetPlayerFaceInstance();
            if (face == null)
            {
                Debug.LogWarning("[FML CustomFaceUtils] MainCharacterFace not found in scene.");
                return default;
            }
            return GetFaceAsData(face);
        }

        // ═══════════════════════════════════════════════════════════════
        //  任意角色捏脸（通过 CustomFaceInstance）
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 将官方捏脸数据串（JSON）应用到任意 <see cref="global::CustomFaceInstance"/>。
        /// </summary>
        /// <param name="instance">目标角色的 CustomFaceInstance 组件。</param>
        /// <param name="jsonString">官方格式的捏脸 JSON 字符串。</param>
        /// <returns>是否成功应用。</returns>
        public static bool SetFaceFromJson(global::CustomFaceInstance instance, string jsonString)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            if (string.IsNullOrEmpty(jsonString))
            {
                Debug.LogWarning("[FML CustomFaceUtils] JSON string is null or empty.");
                return false;
            }

            if (global::CustomFaceSettingData.JsonToData(jsonString, out var data))
            {
                instance.LoadFromData(data);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取任意角色的捏脸数据（JSON 字符串）。
        /// </summary>
        /// <param name="instance">目标角色的 CustomFaceInstance 组件。</param>
        /// <returns>官方格式的捏脸 JSON 字符串。</returns>
        public static string GetFaceJson(global::CustomFaceInstance instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var data = instance.ConvertToSaveData();
            return data.DataToJson();
        }

        /// <summary>
        /// 使用原生数据结构设置任意角色的捏脸。
        /// </summary>
        /// <param name="instance">目标角色的 CustomFaceInstance 组件。</param>
        /// <param name="data">原生捏脸数据结构。</param>
        public static void LoadFaceFromData(global::CustomFaceInstance instance, global::CustomFaceSettingData data)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            instance.LoadFromData(data);
        }

        /// <summary>
        /// 获取任意角色的捏脸数据结构。
        /// </summary>
        /// <param name="instance">目标角色的 CustomFaceInstance 组件。</param>
        /// <returns>原生 <see cref="global::CustomFaceSettingData"/> 结构。</returns>
        public static global::CustomFaceSettingData GetFaceAsData(global::CustomFaceInstance instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            return instance.ConvertToSaveData();
        }

        // ═══════════════════════════════════════════════════════════════
        //  内部辅助
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 在场景中查找玩家主角的 <see cref="global::CustomFaceInstance"/>。
        /// 注：CustomFaceManager 不暴露 CustomFaceInstance（仅 SaveSetting/LoadSetting 数据方法），
        /// 且 LoadFromData 需要绑定 MainCharacterFace 的渲染器才生效——故不做无绑定实例兜底。
        /// </summary>
        public static global::CustomFaceInstance? GetPlayerFaceInstance()
        {
            var mainFace = UnityEngine.Object.FindObjectOfType<global::MainCharacterFace>();
            if (mainFace == null || mainFace.customFace == null)
                return null;
            return mainFace.customFace;
        }

        /// <summary>检查捏脸数据串是否合法。</summary>
        public static bool ValidateJson(string jsonString)
        {
            if (string.IsNullOrEmpty(jsonString)) return false;
            return global::CustomFaceSettingData.JsonToData(jsonString, out _);
        }
    }
}
