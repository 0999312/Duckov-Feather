using Cysharp.Threading.Tasks;
using FeatherMod.Utils;
using System;
using System.Linq;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// [Obsolete] 对话系统工具类。保留用于向后兼容，新代码请使用 <see cref="DialogueManager"/>。
    /// </summary>
    [Obsolete("Use DialogueManager instead.")]
    public static class DialogueUtils
    {
        /// <summary>初始化。委托到 <see cref="DialogueManager.Init"/>。</summary>
        public static void Init()
        {
            DialogueManager.Init();
        }

        /// <summary>在指定 NPC 上显示世界空间对话气泡。委托到 <see cref="FriendlyNpcUtils.ShowBubble"/>。</summary>
        public static void ShowBubble(Identifier npcId, string text, float duration = 2f)
            => FriendlyNpcUtils.ShowBubble(npcId, text, duration);

        /// <summary>在任意世界空间位置显示气泡。</summary>
        public static void ShowBubbleAt(Vector3 pos, string text, float duration = 2f)
            => DialogueManager.ShowBubbleAt(pos, text, duration);

        /// <summary>播放对话（自动降级）。旧版 SubtitleLine 数组自动转换为 DialogueLine。</summary>
        public static async UniTask PlayDialogue(string actorId, SubtitleLine[] lines)
        {
            if (lines == null || lines.Length == 0) return;

            // SubtitleLine → DialogueLine 转换
            var converted = Array.ConvertAll(lines, sl => (DialogueLine)sl);
            await DialogueManager.PlayDialogue(actorId, converted);
        }
    }
}
