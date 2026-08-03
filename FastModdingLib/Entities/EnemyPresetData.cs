using Duckov.Utilities;
using FeatherMod.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherMod
{
    // ═══════════════════════════════════════════════════════════════
    //  ModelRef — 模型引用
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 模型引用。支持引用游戏已有 CharacterModel prefab 或从 AssetBundle 加载自定义模型。
    /// </summary>
    public struct ModelRef
    {
        /// <summary>游戏已有 CharacterModel 的 prefab 名称（如 "CharacterModel_Default"）。</summary>
        public string GamePrefabName { get; set; }

        /// <summary>AssetBundle 名称（可选，自定义模型时使用）。</summary>
        public string BundleName { get; set; }

        /// <summary>AssetBundle 内的资源路径（可选）。</summary>
        public string AssetPath { get; set; }

        /// <summary>引用游戏已有 CharacterModel prefab。</summary>
        public static ModelRef GamePrefab(string name)
            => new ModelRef { GamePrefabName = name };

        /// <summary>使用游戏默认 CharacterModel（GameplayDataSettings.Prefabs.DefaultCharacterModel）。
        /// 即玩家捏脸系统的基础模型，适用于 FaceRef.FromJson 等自定义捏脸场景。</summary>
        public static ModelRef Default => new ModelRef { GamePrefabName = null! };

        /// <summary>从 AssetBundle 加载自定义模型。</summary>
        public static ModelRef FromBundle(string bundle, string path)
            => new ModelRef { BundleName = bundle, AssetPath = path };
    }

    // ═══════════════════════════════════════════════════════════════
    //  NpcRole — NPC 角色类型
    // ═══════════════════════════════════════════════════════════════

    /// <summary>NPC 角色类型（Flags 枚举，支持复合角色如 <c>Merchant | QuestGiver</c>）。</summary>
    [System.Flags]
    public enum NpcRole
    {
        /// <summary>无角色。</summary>
        None = 0,
        /// <summary>敌对敌人。</summary>
        Enemy = 1 << 0,
        /// <summary>友善商人（交互打开商店）。</summary>
        Merchant = 1 << 1,
        /// <summary>任务给予者。</summary>
        QuestGiver = 1 << 2,
        /// <summary>中立 NPC（不攻击也不交互）。</summary>
        Neutral = 1 << 3,
        /// <summary>同伴（跟随玩家）。</summary>
        Companion = 1 << 4,
        /// <summary>仅对话（无可交互行为）。</summary>
        DialogueOnly = 1 << 5,
    }

    // ═══════════════════════════════════════════════════════════════
    //  WeaponConfig — 武器配置
    // ═══════════════════════════════════════════════════════════════

    /// <summary>武器生成配置。</summary>
    public class WeaponConfig
    {
        /// <summary>武器物品池（Identifier 或 typeID）。</summary>
        public ItemEntry[] WeaponPool { get; set; } = Array.Empty<ItemEntry>();

        /// <summary>生成概率（0-1）。默认 1.0。</summary>
        public float Chance { get; set; } = 1f;

        /// <summary>品质范围。null 表示不限制。</summary>
        public QualityRange? Qualities { get; set; }

        /// <summary>耐久度范围（0-1 比例）。</summary>
        public Vector2 Durability { get; set; } = new Vector2(0.4f, 0.7f);

        /// <summary>耐久完整性范围（0-1 比例）。</summary>
        public Vector2 DurabilityIntegrity { get; set; } = new Vector2(0.5f, 0.9f);

        /// <summary>是否自动生成匹配口径的子弹。</summary>
        public bool WithMatchingAmmo { get; set; } = true;
    }

    /// <summary>品质范围。</summary>
    public struct QualityRange
    {
        public int Min;
        public int Max;
    }

    // ═══════════════════════════════════════════════════════════════
    //  EquipmentConfig — 装备配置（护甲/头盔/背包）
    // ═══════════════════════════════════════════════════════════════

    /// <summary>装备槽配置。</summary>
    public class EquipmentSlotConfig
    {
        /// <summary>物品池。</summary>
        public ItemEntry[] ItemPool { get; set; } = Array.Empty<ItemEntry>();

        /// <summary>生成概率（0-1）。</summary>
        public float Chance { get; set; } = 0.5f;

        /// <summary>品质范围。</summary>
        public QualityRange? Qualities { get; set; }

        /// <summary>耐久度范围。</summary>
        public Vector2 Durability { get; set; } = new Vector2(0.3f, 0.8f);
    }

    // ═══════════════════════════════════════════════════════════════
    //  LootConfig — 战利品配置
    // ═══════════════════════════════════════════════════════════════

    /// <summary>战利品掉落配置。</summary>
    public class LootConfig
    {
        /// <summary>是否死亡掉落战利品箱。</summary>
        public bool DropBoxOnDead { get; set; } = true;

        /// <summary>携带现金概率（0-1）。</summary>
        public float HasCashChance { get; set; }

        /// <summary>现金数量范围。</summary>
        public Vector2Int CashRange { get; set; }

        /// <summary>额外掉落物品。</summary>
        public ItemEntry[] ExtraLoot { get; set; } = Array.Empty<ItemEntry>();
    }

    // ═══════════════════════════════════════════════════════════════
    //  EnemyPresetData — 敌人/NPC 预设数据 DTO
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 敌人/NPC 预设数据的纯代码 DTO。modder 通过此对象配置角色的全部属性，
    /// FML 内部转换为 <see cref="CharacterRandomPreset"/>。
    /// </summary>
    /// <example>
    /// <code>
    /// var data = new EnemyPresetData
    /// {
    ///     NameKey = "NPC_Merchant_Drink",
    ///     NpcRole = NpcRole.Merchant,
    ///     Team = Teams.middle,
    ///     Health = 300,
    ///     Model = ModelRef.GamePrefab("CharacterModel_Duck_Jeff"),
    ///     Weapon = new WeaponConfig { WeaponPool = new[] { ItemEntry.Of("mymod:pistol", 1) }, Chance = 0.5f }
    /// };
    /// EnemyUtils.RegisterEnemy(new Identifier("mymod", "merchant"), aiConfig, data);
    /// </code>
    /// </example>
    public class EnemyPresetData
    {
        // ===== 必填标识 =====
        /// <summary>本地化 key（兼 kill counter key）。</summary>
        public string NameKey { get; set; } = "";

        // ===== 角色类型 =====
        /// <summary>NPC 角色类型。默认 Enemy（敌对）。</summary>
        public NpcRole NpcRole { get; set; } = NpcRole.Enemy;

        // ===== 基础属性 =====
        /// <summary>阵营。默认 scav。</summary>
        public Teams Team { get; set; } = Teams.scav;

        /// <summary>生命值。</summary>
        public float Health { get; set; } = 100f;

        /// <summary>经验值。</summary>
        public int Exp { get; set; } = 100;

        /// <summary>是否 Boss。</summary>
        public bool IsBoss { get; set; }

        /// <summary>是否显示血条。</summary>
        public bool ShowHealthBar { get; set; } = true;

        /// <summary>是否有灵魂（可被灵魂技能影响）。</summary>
        public bool HasSoul { get; set; } = true;

        /// <summary>是否默认掏武器。</summary>
        public bool DefaultWeaponOut { get; set; } = true;

        /// <summary>是否可以对话。</summary>
        public bool CanTalk { get; set; } = true;

        /// <summary>是否可以死亡（非 Raid 地图中）。</summary>
        public bool CanDieIfNotRaidMap { get; set; }

        // ===== AI 战斗参数 =====
        /// <summary>视野距离。</summary>
        public float SightDistance { get; set; } = 17f;

        /// <summary>视野角度。</summary>
        public float SightAngle { get; set; } = 100f;

        /// <summary>反应时间（秒）。</summary>
        public float ReactionTime { get; set; } = 0.2f;

        /// <summary>听力能力。</summary>
        public float HearingAbility { get; set; } = 1f;

        /// <summary>巡逻范围。</summary>
        public float PatrolRange { get; set; } = 8f;

        /// <summary>战斗移动范围。</summary>
        public float CombatMoveRange { get; set; } = 8f;

        /// <summary>是否可冲刺。</summary>
        public bool CanDash { get; set; }

        /// <summary>伤害倍率。</summary>
        public float DamageMultiplier { get; set; } = 1f;

        /// <summary>移动速度因子。</summary>
        public float MoveSpeedFactor { get; set; } = 1f;

        /// <summary>是否显示名称。</summary>
        public bool ShowName { get; set; }

        /// <summary>遗忘时间（秒）。追踪丢失后多久忘记目标。</summary>
        public float ForgetTime { get; set; } = 8f;

        // ===== 武器 =====
        /// <summary>主武器配置。null 表示不使用武器。</summary>
        public WeaponConfig? Weapon { get; set; }

        // ===== 装备 =====
        /// <summary>护甲配置。null 表示不生成护甲。</summary>
        public EquipmentSlotConfig? Armor { get; set; }

        /// <summary>头盔配置。</summary>
        public EquipmentSlotConfig? Helmet { get; set; }

        /// <summary>背包配置。</summary>
        public EquipmentSlotConfig? Backpack { get; set; }

        // ===== 战利品 =====
        /// <summary>战利品掉落配置。null 使用默认值。</summary>
        public LootConfig? Loot { get; set; }

        // ===== 模型 =====
        /// <summary>角色模型引用。</summary>
        public ModelRef Model { get; set; } = ModelRef.GamePrefab("CharacterModel_Default");

        // ===== 元素抗性 =====
        public float ElementFactor_Physics { get; set; } = 1f;
        public float ElementFactor_Fire { get; set; } = 1f;
        public float ElementFactor_Ice { get; set; } = 1f;
        public float ElementFactor_Poison { get; set; } = 1f;
        public float ElementFactor_Electricity { get; set; } = 1f;
        public float ElementFactor_Space { get; set; } = 1f;
        public float ElementFactor_Ghost { get; set; } = 1f;

        // ===== 捏脸（🆕 Phase 5） =====
        /// <summary>捏脸配置。默认不设置（使用 CharacterModel 默认）。</summary>
        public FaceRef Face { get; set; } = FaceRef.None;

        /// <summary>是否使用玩家捏脸（快捷方式，等价于 Face = FaceRef.PlayerFace()）。</summary>
        public bool UsePlayerFace
        {
            get => Face.Mode == FaceRefMode.PlayerFace;
            set { if (value) Face = FaceRef.PlayerFace(); }
        }

        // ===== 商店（仅 Merchant 角色） =====
        /// <summary>商店 profile ID。仅 NpcRole.Merchant 时使用。</summary>
        public string? ShopProfile { get; set; }

        // ===== 内部方法 =====

        /// <summary>
        /// 将 DTO 转换为游戏原生 <see cref="CharacterRandomPreset"/>。
        /// FML 内部调用，modder 无需关心。
        /// </summary>
        internal CharacterRandomPreset ToNative()
        {
            var preset = ScriptableObject.CreateInstance<CharacterRandomPreset>();

            // 基础标识（[SerializeField] private 经 Publicizer 已公开，直接赋值零反射）
            preset.nameKey = NameKey;
            preset.isBoss = IsBoss;
            preset.health = Health;
            preset.exp = Exp;
            preset.hasSoul = HasSoul;
            preset.showHealthBar = ShowHealthBar;
            preset.showName = ShowName;
            preset.canTalk = CanTalk;
            preset.canDieIfNotRaidMap = CanDieIfNotRaidMap;
            preset.defaultWeaponOut = DefaultWeaponOut;

            // 阵营
            preset.team = Team;

            // 捏脸（🆕 Phase 5）
            switch (Face.Mode)
            {
                case FaceRefMode.PlayerFace:
                    preset.usePlayerPreset = true;
                    break;
                case FaceRefMode.Preset when Face.PresetName != null:
                    preset.facePreset = FindFacePreset(Face.PresetName);
                    break;
                case FaceRefMode.Custom:
                    preset.facePreset = CreateCustomFacePreset(Face.CustomParts);
                    break;
            }

            // AI 战斗
            preset.sightDistance = SightDistance;
            preset.sightAngle = SightAngle;
            preset.reactionTime = ReactionTime;
            preset.hearingAbility = HearingAbility;
            preset.patrolRange = PatrolRange;
            preset.combatMoveRange = CombatMoveRange;
            preset.canDash = CanDash;
            preset.damageMultiplier = DamageMultiplier;
            preset.moveSpeedFactor = MoveSpeedFactor;
            preset.forgetTime = ForgetTime;

            // 元素抗性
            preset.elementFactor_Physics = ElementFactor_Physics;
            preset.elementFactor_Fire = ElementFactor_Fire;
            preset.elementFactor_Ice = ElementFactor_Ice;
            preset.elementFactor_Poison = ElementFactor_Poison;
            preset.elementFactor_Electricity = ElementFactor_Electricity;
            preset.elementFactor_Space = ElementFactor_Space;
            preset.elementFactor_Ghost = ElementFactor_Ghost;

            // 战利品
            if (Loot != null)
            {
                preset.dropBoxOnDead = Loot.DropBoxOnDead;
                preset.hasCashChance = Loot.HasCashChance;
                preset.cashRange = Loot.CashRange;
            }

            // 武器配置
            if (Weapon != null)
            {
                ApplyWeaponConfig(preset, Weapon);
            }

            return preset;
        }

        /// <summary>
        /// 将 WeaponConfig 应用到 preset 的 itemsToGenerate（随机生成武器池）。
        /// itemsToGenerate / RandomItemGenerateDescription 经 Publicizer 已公开，直接构建零反射。
        /// </summary>
        private static void ApplyWeaponConfig(CharacterRandomPreset preset, WeaponConfig weapon)
        {
            if (weapon.WeaponPool.Length == 0) return;

            if (preset.itemsToGenerate == null)
                preset.itemsToGenerate = new List<RandomItemGenerateDescription>();

            foreach (var w in weapon.WeaponPool)
            {
                int weaponTypeId = w.ResolveTypeId();
                if (weaponTypeId <= 0) continue;

                var desc = new RandomItemGenerateDescription
                {
                    randomFromPool = true,
                    chance = Mathf.Clamp01(weapon.Chance),
                    randomCount = new Vector2Int(1, 1),
                };
                desc.itemPool = new RandomContainer<RandomItemGenerateDescription.Entry>();
                desc.itemPool.AddEntry(new RandomItemGenerateDescription.Entry { itemTypeID = weaponTypeId }, 1f);
                desc.itemPool.RefreshPercent();
                preset.itemsToGenerate.Add(desc);
            }
        }

        /// <summary>按名称查找已有 CustomFacePreset。</summary>
        private static CustomFacePreset? FindFacePreset(string name)
        {
            // 遍历 GameplayDataSettings.CustomFaceData 查找预设
            var faceData = GameplayDataSettings.CustomFaceData;
            if (faceData?.DefaultPreset != null && faceData.DefaultPreset.name == name)
                return faceData.DefaultPreset;

            // 回退：按资源名加载（CustomFacePreset_{name}）
            var loaded = Resources.Load<CustomFacePreset>($"CustomFacePreset_{name}");
            if (loaded != null)
                return loaded;

            Debug.LogWarning($"[FML] CustomFacePreset '{name}' not found.");
            return null;
        }

        /// <summary>根据部件 ID 创建自定义捏脸预设。</summary>
        private static CustomFacePreset? CreateCustomFacePreset(FacePartIds parts)
        {
            var preset = ScriptableObject.CreateInstance<CustomFacePreset>();
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
                preset.settings = s; // struct 写回
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FML EnemyPresetData] Face settings injection: {e.Message}");
            }
            return preset;
        }
    }
}
