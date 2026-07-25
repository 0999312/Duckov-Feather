using Duckov;
using Duckov.Quests;
using FeatherMod.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherMod.Interaction.Components
{
    /// <summary>
    /// Feathered 任务交互封装。内部使用 QuestGiver，
    /// 通过 Identifier 管理生命周期，Mod 卸载时自动清理。
    /// 可在建筑、自定义物件等非 NPC 场景使用。
    /// </summary>
    public class FeatherQuestGiverInteract : InteractableBase
    {
        /// <summary>交互点唯一标识，注册到 InteractionRegistry 用于生命周期追踪。</summary>
        public Identifier InteractId;

        [SerializeField] private string? _questGiverId;
        [SerializeField] private string? _interactNameKey;

        /// <summary>
        /// 任务发布者 ID。可为 QuestGiverID 枚举名（如 "Ming"、"Albert"）
        /// 或 QuestGiverUtils.RegisterQuestGiver 分配的数字 ID 字符串（≥50）。
        /// </summary>
        public string? QuestGiverId
        {
            get => _questGiverId;
            set => _questGiverId = value;
        }

        public string? InteractNameKey
        {
            get => _interactNameKey;
            set => _interactNameKey = value;
        }

        private QuestGiver? _native;

        /// <summary>
        /// 将 FeatherQuestGiverInteract 挂载到目标 GameObject 并注册到 InteractionRegistry。
        /// 自动处理 Awake 时序：先禁用 GO → 挂组件 + 设字段 → 恢复原激活状态，确保 Awake 时字段已就绪。
        /// </summary>
        /// <param name="id">交互点唯一标识。</param>
        /// <param name="target">目标 GameObject。</param>
        /// <param name="questGiverId">
        /// 任务发布者 ID：QuestGiverID 枚举名（如 "Ming"）或
        /// QuestGiverUtils.RegisterQuestGiver 分配的数字 ID 字符串（≥50）。
        /// </param>
        /// <param name="interactNameKey">可选交互提示本地化键，默认 "UI_Interact_Quest"。</param>
        /// <returns>创建的 FeatherQuestGiverInteract 实例。</returns>
        public static FeatherQuestGiverInteract Attach(
            Identifier id, GameObject target, string questGiverId, string? interactNameKey = null)
        {
            bool wasActive = target.activeSelf;
            target.SetActive(false);
            FeatherQuestGiverInteract comp;
            try
            {
                comp = target.AddComponent<FeatherQuestGiverInteract>();
                comp.InteractId = id;
                comp._questGiverId = questGiverId;
                comp._interactNameKey = interactNameKey;
            }
            finally
            {
                target.SetActive(wasActive);
            }

            InteractionUtils.Registry.Set(id, new InteractionEntry
            {
                Target = target,
                Modid = id.Domain
            }, id.Domain);

            return comp;
        }

        protected override void Awake()
        {
            // InteractableBase.Awake 会 foreach otherInterablesInGroup，
            // AddComponent 创建的实例该字段为 null，必须先初始化为空列表。
            otherInterablesInGroup = new List<InteractableBase>();

            base.Awake();

            overrideInteractName = true;
            _overrideInteractNameKey = !string.IsNullOrEmpty(_interactNameKey)
                ? _interactNameKey : "UI_Interact_Quest";

            // 添加游戏原生组件前先禁用 GO，初始化 otherInterablesInGroup 后恢复，
            // 确保原生 InteractableBase 子类的 Awake 被 Harmony patch 拦截时不 NRE。
            bool wasActive = gameObject.activeSelf;
            gameObject.SetActive(false);

            _native = gameObject.AddComponent<QuestGiver>();
            _native.otherInterablesInGroup = new List<InteractableBase>();
            _native.spawnPOI = false;
            _native.interactMarkerOffset = new Vector3(0, 1.5f, 0);
            _native.overrideInteractName = true;
            _native._overrideInteractNameKey = _overrideInteractNameKey;
            _native.interactTime = 0f;
            _native.finishWhenTimeOut = false;

            gameObject.SetActive(wasActive);

            // 绑定任务发布者
            if (!string.IsNullOrEmpty(_questGiverId))
            {
                SetQuestGiverId(_native, _questGiverId);
            }
        }

        protected override void OnInteractFinished()
        {
            ViewDispatcher.Open(GameViews.Quest);
        }

        /// <summary>
        /// 将字符串形式的 questGiverId 设置到 QuestGiver 组件的 questGiverID 字段。
        /// 支持数字 ID（≥50，QuestGiverUtils.RegisterQuestGiver 分配）和枚举名两种格式。
        /// </summary>
        private static void SetQuestGiverId(QuestGiver qg, string questGiverId)
        {
            if (string.IsNullOrEmpty(questGiverId)) return;

            // 数字 ID（自定义 questGiverID ≥50）
            if (int.TryParse(questGiverId, out int customId) && customId >= 50)
            {
                qg.questGiverID = (QuestGiverID)customId;
                return;
            }

            // 枚举名（如 "Ming"、"Albert"）
            try
            {
                qg.questGiverID = (QuestGiverID)System.Enum.Parse(typeof(QuestGiverID), questGiverId);
            }
            catch (System.ArgumentException)
            {
                Debug.LogWarning($"[FML] QuestGiverId '{questGiverId}' is not a valid enum name or custom int ID. " +
                    "Use QuestGiverUtils.RegisterQuestGiver() to register custom IDs first, or pass a valid QuestGiverID enum name.");
            }
        }
    }
}