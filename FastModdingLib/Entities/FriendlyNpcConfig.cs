using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// 友善 NPC 配置 DTO。modder 用纯 C# 创建此对象，传入 <see cref="FriendlyNpcUtils.CreateFriendlyNpc"/>。
    /// FML 内部负责创建 Unity GameObject 并配置 InteractableBase / Dialogue / Shop / Quest 绑定。
    /// </summary>
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
    }
}
