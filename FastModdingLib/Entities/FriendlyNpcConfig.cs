using Duckov.Utilities;

using FeatherMod.Utils;

using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// 友善 NPC 配置 DTO。modder 用纯 C# 创建此对象，传入 <see cref="FriendlyNpcUtils.CreateFriendlyNpcAsync"/>。
    /// FML 内部通过 <see cref="CharacterRandomPreset.CreateCharacterAsync"/> 生成完整的可见角色，
    /// 自动附带 <see cref="global::CharacterModel"/>、<see cref="global::CustomFaceInstance"/>、
    /// Animator、Collider 等组件。
    /// </summary>
    /// <example>
    /// <code>
    /// var npc = await FriendlyNpcUtils.CreateFriendlyNpcAsync(
    ///     new Identifier("mymod", "merchant"),
    ///     new FriendlyNpcConfig
    ///     {
    ///         DisplayNameKey = "NPC_Merchant_Name",
    ///         ActorId = "Merchant_Duck",
    ///         Role = NpcRole.Merchant,
    ///         Face = FaceRef.Preset("Duck_Default"),
    ///         Model = ModelRef.GamePrefab("CharacterModel_Duck_Jeff"),
    ///         SpawnPosition = new Vector3(10, 0, 5),
    ///         HeadEquipment = ItemEntry.Of("duckov:CowboyHat", 1),
    ///         BodyEquipment = ItemEntry.Of("duckov:Vest_A", 1)
    ///     });
    /// </code>
    /// </example>
    public class FriendlyNpcConfig
    {
        /// <summary>NPC 显示名称的本地化键。</summary>
        public string DisplayNameKey = "";

        /// <summary>DuckovDialogueActor.id——引用已有对话角色（用于 NodeCanvas DialogueTree）。</summary>
        public string ActorId = "";

        /// <summary>NPC 角色类型（决定交互行为）。</summary>
        public NpcRole Role = NpcRole.None;

        /// <summary>捏脸配置。为 None 时使用默认外观。</summary>
        public FaceRef Face = FaceRef.None;

        /// <summary>世界空间生成位置。</summary>
        public Vector3 SpawnPosition = Vector3.zero;

        /// <summary>Quaternion 旋转。</summary>
        public Quaternion SpawnRotation = Quaternion.identity;

        /// <summary>目标场景 ID（null = 当前活动场景）。</summary>
        public string? SceneId;

        /// <summary>商店绑定 ID（Role=Merchant 时使用）。调用 ShopUtils.CreateMerchantProfile 注册。</summary>
        public string? ShopId;

        /// <summary>
        /// 商店是否支持账户余额支付（false = 仅现金，商店 UI 显示"只收现金"）。
        /// 默认 true，与原版基地商人小明（accountAvaliable=1）一致。
        /// </summary>
        public bool ShopAccountAvaliable = true;

        /// <summary>
        /// 玩家出售物品时是否直接返还现金物品（true = 给现金物品，false = 计入账户余额）。
        /// 默认 false，与原版小明一致（IslandShop 等野外商店为 true）。
        /// </summary>
        public bool ShopReturnCash;

        /// <summary>商店回收价格倍率。默认 0.5，与原版一致。</summary>
        public float ShopSellFactor = 0.5f;

        /// <summary>
        /// 绑定的 QuestGiver Identifier。需先通过 QuestGiverUtils.RegisterQuestGiver() 注册。
        /// 设置后 SpawnFriendlyNpcAsync 自动绑定 questGiverID。
        /// </summary>
        public Identifier? QuestGiverId;

        /// <summary>
        /// 绑定的 PerkTree Identifier（Path = 游戏 perkTreeID 字符串）。
        /// 设置后在 NPC 上添加 PerkTreeUIInvoker 交互点（原版 Interact_Skill 子对象模式），
        /// 无需额外 NpcRole 标志。引用原版技能树用 Identifier("duckov", "PerkTree_Hacker")；
        /// 引用自定义树需先通过 PerkTreeUtils.RegisterPerkTree() 注册，Path 与注册时的 id.Path 一致。
        /// </summary>
        public Identifier? PerkTreeId;

        // ── 模型与外观（🆕 Phase 6 — 重构后新增） ──

        /// <summary>
        /// 角色模型引用。指定使用的 CharacterModel prefab。
        /// 默认使用游戏 DefaultCharacterModel（GameplayDataSettings.Prefabs.DefaultCharacterModel），
        /// 即玩家捏脸系统的基础模型。
        /// </summary>
        public ModelRef Model = ModelRef.Default;

        /// <summary>
        /// 阵营。友善 NPC 默认为 middle（中立/友善阵营），此阵营不会被玩家攻击也不会主动攻击。
        /// </summary>
        public Teams Team = Teams.middle;

        // ── 装备（🆕 Phase 6） ──

        /// <summary>
        /// 头部装备（头盔/帽子）。null 表示无头部装备。
        /// 生成时自动注入到 CharacterRandomPreset.itemsToGenerate。
        /// </summary>
        public ItemEntry? HeadEquipment;

        /// <summary>
        /// 身体装备（护甲/衣服）。null 表示无身体装备。
        /// 生成时自动注入到 CharacterRandomPreset.itemsToGenerate。
        /// </summary>
        public ItemEntry? BodyEquipment;

        /// <summary>
        /// 接近触发对话配置。null 表示无接近触发。
        /// 设置后，SpawnFriendlyNpcAsync 会自动在 NPC 上挂载 <see cref="NpcProximityTrigger"/> 组件。
        /// </summary>
        public DialogueSequence? ProximityDialogue;

        /// <summary>
        /// 是否自动面向玩家。默认 true（与原版友善 NPC 小明一致——其 AI 行为树含 AimToPlayer）。
        /// 设为 true 时，NPC 在运行时通过游戏原生瞄准管线持续转向玩家（仅水平旋转）。
        /// 可被 <see cref="FriendlyNpcUtils.SetNpcFaceDirection"/> 的固定朝向覆盖。
        /// </summary>
        public bool AutoFacePlayer = true;

        /// <summary>
        /// 自动面向玩家的最大距离（米）。玩家超出该距离后 NPC 保持当前朝向。默认 10f。
        /// </summary>
        public float FacePlayerRange = 10f;

        /// <summary>
        /// 是否无敌（不可被攻击）。默认 true。
        /// 设为 true 时，NPC 无视所有伤害（通过 <c>Health.invincible</c> 实现）。
        /// </summary>
        public bool Invincible = true;

        /// <summary>
        /// AI 视野距离（米）。决定 NPC 能否"看到"玩家并自然面向。
        /// 设为 0 会完全禁用 AI 朝向追踪（NPC 不会面向玩家）。
        /// 友善 NPC（Teams.middle）默认建议 8f，不会攻击玩家。
        /// </summary>
        public float SightDistance = 8f;
    }
}
