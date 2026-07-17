using System;

namespace FeatherMod
{
    // ═══════════════════════════════════════════════════════════════
    //  FaceRef — 捏脸引用
    // ═══════════════════════════════════════════════════════════════

    /// <summary>捏脸引用模式。</summary>
    public enum FaceRefMode
    {
        /// <summary>不设置脸部（使用 CharacterModel 默认）。</summary>
        None,
        /// <summary>引用已有 CustomFacePreset 预设。</summary>
        Preset,
        /// <summary>使用玩家当前捏脸数据。</summary>
        PlayerFace,
        /// <summary>自定义面部部件组合。</summary>
        Custom,
        /// <summary>从 CustomFaceSettingData JSON 字符串创建捏脸。</summary>
        FromJson
    }

    /// <summary>
    /// 捏脸引用。支持引用已有预设、玩家捏脸或自定义部件组合。
    /// modder 无需在 Unity 编辑器中创建 CustomFacePreset。
    /// </summary>
    /// <example>
    /// <code>
    /// // 引用已有预设
    /// var face = FaceRef.Preset("Boss_Red");
    ///
    /// // 使用玩家捏脸
    /// var face = FaceRef.PlayerFace();
    ///
    /// // 自定义部件
    /// var face = FaceRef.Custom(new FacePartIds { HairId = "Hair_Long_01" });
    ///
    /// // 从 JSON 数据创建（如从存档导出的捏脸数据）
    /// var face = FaceRef.FromJson(jsonString);
    /// </code>
    /// </example>
    public struct FaceRef
    {
        /// <summary>引用模式。</summary>
        public FaceRefMode Mode;

        /// <summary>预设名称（Mode=Preset 时使用）。</summary>
        public string? PresetName;

        /// <summary>自定义部件 ID（Mode=Custom 时使用）。</summary>
        public FacePartIds CustomParts;

        /// <summary>捏脸 JSON 字符串（Mode=FromJson 时使用）。
        /// 格式为 CustomFaceSettingData.DataToJson() 的输出，或从游戏存档中导出。</summary>
        public string? FaceJson;

        /// <summary>引用游戏已有预设。</summary>
        /// <param name="name">预设名称，如 "Boss_Red"、"Default"。</param>
        public static FaceRef Preset(string name)
            => new FaceRef { Mode = FaceRefMode.Preset, PresetName = name };

        /// <summary>使用玩家当前捏脸数据。</summary>
        public static FaceRef PlayerFace()
            => new FaceRef { Mode = FaceRefMode.PlayerFace };

        /// <summary>自定义部件组合。</summary>
        public static FaceRef Custom(FacePartIds parts)
            => new FaceRef { Mode = FaceRefMode.Custom, CustomParts = parts };

        /// <summary>从 CustomFaceSettingData JSON 字符串创建捏脸。
        /// JSON 可通过 CustomFaceSettingData.DataToJson() 或 CustomFaceInstance.ConvertToSaveData() 获取。</summary>
        /// <param name="json">游戏原生格式的捏脸 JSON 字符串。</param>
        public static FaceRef FromJson(string json)
            => new FaceRef { Mode = FaceRefMode.FromJson, FaceJson = json };

        /// <summary>不设置脸部。</summary>
        public static FaceRef None => new FaceRef { Mode = FaceRefMode.None };
    }

    // ═══════════════════════════════════════════════════════════════
    //  FacePartIds — 自定义面部部件 ID
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 自定义面部部件 ID 组合。
    /// 对应游戏 CustomFacePartTypes 的 8 个分类。
    /// 未设置的部件使用默认值。
    /// </summary>
    public struct FacePartIds
    {
        /// <summary>头发部件 ID。</summary>
        public string? HairId;
        /// <summary>眼睛部件 ID。</summary>
        public string? EyeId;
        /// <summary>嘴巴部件 ID。</summary>
        public string? MouthId;
        /// <summary>眉毛部件 ID。</summary>
        public string? EyebrowId;
        /// <summary>装饰部件 ID。</summary>
        public string? DecorationId;
        /// <summary>尾巴部件 ID。</summary>
        public string? TailId;
        /// <summary>脚部部件 ID。</summary>
        public string? FootId;
        /// <summary>翅膀部件 ID。</summary>
        public string? WingId;
    }
}
