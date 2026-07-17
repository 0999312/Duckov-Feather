using Duckov.Utilities;
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

        /// <summary>商店绑定 ID（Role=Merchant 时使用）。调用 ShopUtils 注册。</summary>
        public string? ShopId;

        /// <summary>任务发放者 ID（Role=QuestGiver 时使用）。</summary>
        public string? QuestGiverId;

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
    }
}
