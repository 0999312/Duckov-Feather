using System;
using System.Collections.Generic;
using FeatherMod.Utils;
using SodaCraft.Localizations;
using UnityEngine;

namespace FeatherMod
{
    // ═══════════════════════════════════════════════════════════
    //  枚举
    // ═══════════════════════════════════════════════════════════

    /// <summary>对话触发模式。</summary>
    public enum DialogueTriggerMode
    {
        /// <summary>仅触发一次。</summary>
        Once,
        /// <summary>每次条件满足都触发。</summary>
        Repeatable,
    }

    // ═══════════════════════════════════════════════════════════
    //  镜头数据模型
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 对话镜头位定义。支持三种注视目标模式（按优先级）：
    /// <list type="number">
    /// <item><b>NPC 标识符</b>：<see cref="LookAtNpc"/> — 应用时实时查询 NPC 当前位置。</item>
    /// <item><b>Actor ID</b>：<see cref="LookAtActorId"/> — 应用时实时查询 DuckovDialogueActor 位置。</item>
    /// <item><b>静态坐标</b>：<see cref="LookAt"/> — 固定的世界坐标注视点。</item>
    /// </list>
    /// 机位可通过内联 <see cref="Position"/> 或 <see cref="VcamName"/> 引用场景 VCam 指定。
    /// </summary>
    public class DialogueCameraShot
    {
        /// <summary>摄像机位（世界坐标）。仅内联模式使用。</summary>
        public Vector3? Position;

        /// <summary>注视点（世界坐标）。仅静态模式使用，优先级最低。</summary>
        public Vector3? LookAt;

        /// <summary>
        /// 注视指定 NPC 的 Identifier（如 <c>Identifier("mymod", "merchant")</c>）。
        /// 非 null 时优先级最高——应用镜头时实时查询 NPC 当前位置。
        /// </summary>
        public Identifier? LookAtNpc;

        /// <summary>
        /// 注视指定 Actor ID（<see cref="DuckovDialogueActor.id"/>）。
        /// 非空时优先级次于 LookAtNpc，高于静态 LookAt。
        /// </summary>
        public string? LookAtActorId;

        /// <summary>
        /// 注视点偏移（相对目标位置）。默认 <c>(0, 1.5, 0)</c>（目标头顶上方）。
        /// 仅当 <see cref="LookAtNpc"/> 或 <see cref="LookAtActorId"/> 生效时使用。
        /// </summary>
        public Vector3? LookAtOffset;

        /// <summary>
        /// 场景中 CinemachineVirtualCamera GameObject 的名称（如 "VCam_Start"）。
        /// 非空时优先使用，忽略 Position/LookAt。
        /// </summary>
        public string? VcamName;

        /// <summary>平滑过渡时长（秒）。0 = 硬切。默认 0.5s。</summary>
        public float BlendTime = 0.5f;

        /// <summary>空镜头，用于标记"恢复游戏默认镜头"。</summary>
        public static readonly DialogueCameraShot ResumeGameplay = new() { BlendTime = 1f, VcamName = "__RESUME__" };

        /// <summary>是否为恢复游戏镜头的标记。</summary>
        public bool IsResumeMarker => VcamName == "__RESUME__";

        /// <summary>
        /// 解析注视目标为世界坐标。
        /// 优先级：LookAtNpc → LookAtActorId → LookAt。
        /// 动态目标实时查询 GameObject 当前位置，适用于对话 NPC（认定为不可移动）。
        /// </summary>
        /// <returns>解析后的注视点坐标，无法解析时返回 null。</returns>
        public Vector3? ResolveLookAt()
        {
            var offset = LookAtOffset ?? new Vector3(0f, 1.5f, 0f);

            // ── 优先级 1：NPC Identifier ──
            if (LookAtNpc != null)
            {
                if (FriendlyNpcUtils.Registry.TryGet(LookAtNpc, out var go) && go != null)
                    return go.transform.position + offset;
                Debug.LogWarning($"[FML Camera] LookAtNpc '{LookAtNpc}' not found.");
                return null;
            }

            // ── 优先级 2：Actor ID ──
            if (!string.IsNullOrEmpty(LookAtActorId))
            {
                var actor = DuckovDialogueActor.Get(LookAtActorId);
                if (actor != null)
                    return actor.transform.position + offset;
                Debug.LogWarning($"[FML Camera] LookAtActor '{LookAtActorId}' not found.");
                return null;
            }

            // ── 优先级 3：静态坐标 ──
            return LookAt;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  核心数据模型
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 单行对话。指定发言者和本地化键，用于构建对话序列。
    /// </summary>
    public class DialogueLine
    {
        /// <summary>发言者 ID（与 DuckovDialogueActor.id 对应）。为空时使用序列默认 Actor。</summary>
        public string ActorId;

        /// <summary>本地化键，通过 I18n 解析为实际显示文本。</summary>
        public string? TextKey;

        /// <summary>
        /// 此对话行播放前应用的镜头切换。null 表示沿用当前镜头。
        /// 仅在面板模式（DialogueTreeController）下生效，气泡模式忽略。
        /// </summary>
        public DialogueCameraShot? CameraBefore;

        public DialogueLine()
        {
            ActorId = "";
        }

        /// <summary>获取实际显示文本（通过本地化键解析）。</summary>
        public string GetText()
        {
            if (!string.IsNullOrEmpty(TextKey)) return TextKey.ToPlainText();
            return "";
        }

        /// <summary>解析有效的 ActorId：优先行级，否则使用给定的默认值。</summary>
        public string ResolveActorId(string defaultActorId)
        {
            return !string.IsNullOrEmpty(ActorId) ? ActorId : defaultActorId;
        }
    }

    /// <summary>
    /// 对话序列。一组按顺序播放的对话行。
    /// 替代旧版 <c>ProximityDialogueConfig</c>。
    /// </summary>
    public class DialogueSequence
    {
        /// <summary>对话行列表。</summary>
        public DialogueLine[] Lines = Array.Empty<DialogueLine>();

        /// <summary>默认发言者 Actor ID。行中 ActorId 为空时使用此值。</summary>
        public string DefaultActorId = "";

        /// <summary>触发模式。</summary>
        public DialogueTriggerMode Mode = DialogueTriggerMode.Once;

        /// <summary>接近触发距离（米）。0 = 不使用接近触发。</summary>
        public float ProximityDistance;

        /// <summary>空序列。</summary>
        public DialogueSequence() { }

        /// <summary>多行序列（指定默认 Actor）。</summary>
        public DialogueSequence(string actorId, params DialogueLine[] lines)
        {
            DefaultActorId = actorId;
            Lines = lines;
        }

        /// <summary>多行序列（无默认 Actor）。</summary>
        public DialogueSequence(params DialogueLine[] lines)
        {
            Lines = lines;
        }

        /// <summary>是否包含有效内容。</summary>
        public bool HasContent => Lines.Length > 0;

        /// <summary>Builder 入口。</summary>
        public static SequenceBuilder Build(string defaultActorId = "") => new(defaultActorId);
    }

    // ═══════════════════════════════════════════════════════════
    //  Builder
    // ═══════════════════════════════════════════════════════════

    /// <summary>对话序列 Builder，支持链式调用。</summary>
    public class SequenceBuilder
    {
        private readonly string _defaultActorId;
        private readonly List<DialogueLine> _lines = new();
        private DialogueTriggerMode _mode = DialogueTriggerMode.Once;
        private float _proximityDistance;
        private DialogueCameraShot? _pendingCameraShot;

        internal SequenceBuilder(string defaultActorId)
        {
            _defaultActorId = defaultActorId;
        }

        /// <summary>设置下一次 <see cref="Then"/> 对话行播放前应用的镜头切换。</summary>
        public SequenceBuilder WithCamera(DialogueCameraShot shot)
        {
            _pendingCameraShot = shot;
            return this;
        }

        /// <summary>快捷方法：内联坐标切镜头（平滑过渡）。</summary>
        public SequenceBuilder CutTo(Vector3 position, Vector3 lookAt, float blendTime = 0.5f)
        {
            _pendingCameraShot = new DialogueCameraShot
            {
                Position = position,
                LookAt = lookAt,
                BlendTime = blendTime,
            };
            return this;
        }

        /// <summary>快捷方法：内联坐标 + 动态注视 NPC。</summary>
        public SequenceBuilder CutTo(Vector3 position, Identifier lookAtNpc, Vector3? lookAtOffset = null, float blendTime = 0.5f)
        {
            _pendingCameraShot = new DialogueCameraShot
            {
                Position = position,
                LookAtNpc = lookAtNpc,
                LookAtOffset = lookAtOffset,
                BlendTime = blendTime,
            };
            return this;
        }

        /// <summary>快捷方法：从当前镜头位置注视指定 NPC（只旋转，不位移）。</summary>
        public SequenceBuilder LookAtNpc(Identifier npcId, Vector3? offset = null, float blendTime = 0.5f)
        {
            _pendingCameraShot = new DialogueCameraShot
            {
                LookAtNpc = npcId,
                LookAtOffset = offset,
                BlendTime = blendTime,
            };
            return this;
        }

        /// <summary>快捷方法：从当前镜头位置注视指定 Actor（只旋转，不位移）。</summary>
        public SequenceBuilder LookAtActor(string actorId, Vector3? offset = null, float blendTime = 0.5f)
        {
            _pendingCameraShot = new DialogueCameraShot
            {
                LookAtActorId = actorId,
                LookAtOffset = offset,
                BlendTime = blendTime,
            };
            return this;
        }

        /// <summary>快捷方法：按名称引用场景中已有的 VCam。</summary>
        public SequenceBuilder CutToVcam(string vcamName, float blendTime = 0.5f)
        {
            _pendingCameraShot = new DialogueCameraShot
            {
                VcamName = vcamName,
                BlendTime = blendTime,
            };
            return this;
        }

        /// <summary>快捷方法：下一次对话行前恢复游戏默认镜头。</summary>
        public SequenceBuilder ResumeCamera(float blendTime = 1f)
        {
            _pendingCameraShot = new DialogueCameraShot
            {
                BlendTime = blendTime,
                VcamName = "__RESUME__",
            };
            return this;
        }

        /// <summary>添加一行对话（默认 Actor，本地化键）。</summary>
        public SequenceBuilder Then(string textKey)
        {
            _lines.Add(new DialogueLine
            {
                ActorId = _defaultActorId,
                TextKey = textKey,
                CameraBefore = _pendingCameraShot,
            });
            _pendingCameraShot = null;
            return this;
        }

        /// <summary>添加一行对话（指定 Actor，本地化键）。</summary>
        public SequenceBuilder Then(string actorId, string textKey)
        {
            _lines.Add(new DialogueLine
            {
                ActorId = actorId,
                TextKey = textKey,
                CameraBefore = _pendingCameraShot,
            });
            _pendingCameraShot = null;
            return this;
        }

        public SequenceBuilder Repeatable()
        {
            _mode = DialogueTriggerMode.Repeatable;
            return this;
        }

        public SequenceBuilder Proximity(float distance)
        {
            _proximityDistance = distance;
            return this;
        }

        public DialogueSequence Build()
        {
            return new DialogueSequence
            {
                DefaultActorId = _defaultActorId,
                Lines = _lines.ToArray(),
                Mode = _mode,
                ProximityDistance = _proximityDistance,
            };
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  向后兼容（旧类型，标记 Obsolete）
    // ═══════════════════════════════════════════════════════════

    /// <summary>[Obsolete] 使用 <see cref="DialogueLine"/> 替代。</summary>
    [Obsolete("Use DialogueLine instead.")]
    public class SubtitleLine
    {
        public string ActorId = "";
        public string? TextKey;

        public DialogueLine ToLine() => new() { ActorId = ActorId, TextKey = TextKey };

        public static implicit operator DialogueLine(SubtitleLine s) => s.ToLine();
    }

    /// <summary>[Obsolete] 使用 <see cref="DialogueSequence"/> 替代。</summary>
    [Obsolete("Use DialogueSequence instead.")]
    public class ProximityDialogueConfig
    {
        public float Distance = 3f;
        public DialogueTriggerMode Mode = DialogueTriggerMode.Once;
        public DialogueLine[] Lines = Array.Empty<DialogueLine>();

        public DialogueSequence ToSequence()
        {
            return new DialogueSequence
            {
                Lines = Lines,
                Mode = Mode,
                ProximityDistance = Distance,
            };
        }
    }
}
