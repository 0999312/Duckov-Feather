using FeatherMod.Utils;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// QuestGiver 配置 DTO。modder 用纯 C# 创建此对象，传入
    /// <see cref="QuestGiverUtils.RegisterQuestGiver"/> 注册自定义任务发放者。
    /// FML 内部负责创建 Unity GameObject、配置 QuestGiver 组件、
    /// 管理自定义 QuestGiverID 映射。
    /// </summary>
    public class QuestGiverConfig
    {
        /// <summary>NPC 显示名称的本地化键（如 "npc_ming"）。</summary>
        public string DisplayNameKey = "";

        /// <summary>
        /// DuckovDialogueActor.id——引用已有对话角色（用于 NodeCanvas DialogueTree）。
        /// 为空时不添加对话组件。
        /// </summary>
        public string ActorId = "";

        /// <summary>捏脸配置。为 None 时使用默认外观。</summary>
        public FaceRef Face = FaceRef.None;

        /// <summary>世界空间生成位置（SpawnQuestGiver 时使用）。</summary>
        public Vector3 SpawnPosition = Vector3.zero;

        /// <summary>Quaternion 旋转（SpawnQuestGiver 时使用）。</summary>
        public Quaternion SpawnRotation = Quaternion.identity;

        /// <summary>
        /// 绑定的任务 Identifier 列表（可选）。
        /// 设置后 RegisterQuestGiver 会自动将这些任务关联到此任务发放者。
        /// Identifier 需与 QuestUtils.RegisterQuest 注册时使用的 id 一致。
        /// </summary>
        public Identifier[]? BoundQuests;

        /// <summary>
        /// 是否在生成时自动创建 POI 标记（spawnPOI）。
        /// 默认 true，与游戏原生 QuestGiver 行为一致。
        /// </summary>
        public bool SpawnPOI = true;
    }
}
