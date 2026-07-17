using System;
using System.Collections.Generic;
using SodaCraft.Localizations;

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
    //  核心数据模型
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 单行对话。指定发言者和文本内容，用于构建对话序列。
    /// </summary>
    public class DialogueLine
    {
        /// <summary>发言者 ID（与 DuckovDialogueActor.id 对应）。为空时使用序列默认 Actor。</summary>
        public string ActorId;

        /// <summary>直接显示的文本。非空时优先于 TextKey。</summary>
        public string? Text;

        /// <summary>本地化键。Text 为空时通过 I18n 解析此键。</summary>
        public string? TextKey;

        public DialogueLine()
        {
            ActorId = "";
        }

        public DialogueLine(string text)
        {
            ActorId = "";
            Text = text;
        }

        public DialogueLine(string actorId, string text)
        {
            ActorId = actorId;
            Text = text;
        }

        /// <summary>获取实际显示文本。Text 优先，否则解析 TextKey。</summary>
        public string GetText()
        {
            if (!string.IsNullOrEmpty(Text)) return Text;
            if (!string.IsNullOrEmpty(TextKey)) return TextKey.ToPlainText();
            return "";
        }

        /// <summary>解析有效的 ActorId：优先行级，否则使用给定的默认值。</summary>
        public string ResolveActorId(string defaultActorId)
        {
            return !string.IsNullOrEmpty(ActorId) ? ActorId : defaultActorId;
        }

        // ── 隐式转换：方便 modder 直接传字符串 ──

        public static implicit operator DialogueLine(string text) => new(text);
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

        /// <summary>单行文本序列。</summary>
        public DialogueSequence(string text) : this("", text) { }

        /// <summary>单行序列（指定 Actor）。</summary>
        public DialogueSequence(string actorId, string text)
        {
            DefaultActorId = actorId;
            Lines = new[] { new DialogueLine(actorId, text) };
        }

        /// <summary>多行序列。</summary>
        public DialogueSequence(string actorId, params DialogueLine[] lines)
        {
            DefaultActorId = actorId;
            Lines = lines;
        }

        /// <summary>是否包含有效内容。</summary>
        public bool HasContent => Lines.Length > 0;

        /// <summary>Builder 入口。</summary>
        public static SequenceBuilder Build(string defaultActorId = "") => new(defaultActorId);

        // ── 隐式转换 ──

        public static implicit operator DialogueSequence(string text) => new(text);
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

        internal SequenceBuilder(string defaultActorId)
        {
            _defaultActorId = defaultActorId;
        }

        public SequenceBuilder Then(string text)
        {
            _lines.Add(new DialogueLine(_defaultActorId, text));
            return this;
        }

        public SequenceBuilder Then(string actorId, string text)
        {
            _lines.Add(new DialogueLine(actorId, text));
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
        public string? Text;
        public string? TextKey;

        public DialogueLine ToLine() => new() { ActorId = ActorId, Text = Text, TextKey = TextKey };

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
