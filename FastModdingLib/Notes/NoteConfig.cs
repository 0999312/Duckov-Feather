using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// 笔记配置 DTO。modder 用纯 C# 创建此对象，传入 <see cref="NoteUtils.RegisterNote(Identifier, NoteConfig, string?)"/>。
    /// FML 内部负责将此 DTO 转换为游戏原生的 <see cref="Duckov.NoteIndexs.Note"/>。
    /// </summary>
    public class NoteConfig
    {
        /// <summary>笔记标题的本地化键。默认自动生成为 "Note_{key}_Title"。</summary>
        public string TitleKey { get; set; } = "";

        /// <summary>笔记正文的本地化键。默认自动生成为 "Note_{key}_Content"。</summary>
        public string ContentKey { get; set; } = "";

        /// <summary>可选插图 Sprite。为 null 时不显示图片。</summary>
        public Sprite? Image { get; set; }

        /// <summary>是否隐藏（不计入总数统计）。默认 false。</summary>
        public bool Hidden { get; set; }
    }
}
