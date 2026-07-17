using FeatherMod.Events;
using FeatherMod.Utils;

namespace FeatherMod
{
    /// <summary>笔记注册完成事件。在 <see cref="NoteUtils.RegisterNote"/> 成功后触发。</summary>
    public class NoteRegisteredEvent : Event
    {
        public Identifier NoteId { get; }
        public NoteRegisteredEvent(Identifier noteId) { NoteId = noteId; }
    }

    /// <summary>笔记解锁事件。在笔记被解锁时触发（含 RegisterNote 间接解锁和直接 Unlock 调用）。</summary>
    public class NoteUnlockedEvent : Event
    {
        public Identifier NoteId { get; }
        public NoteUnlockedEvent(Identifier noteId) { NoteId = noteId; }
    }

    /// <summary>笔记已读事件。在笔记详情页首次被查看时触发。</summary>
    public class NoteReadEvent : Event
    {
        public Identifier NoteId { get; }
        public NoteReadEvent(Identifier noteId) { NoteId = noteId; }
    }
}
