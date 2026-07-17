using Cysharp.Threading.Tasks;
using Dialogues;
using Duckov.UI.DialogueBubbles;
using FeatherMod.Events;
using FeatherMod.Utils;
using NodeCanvas.DialogueTrees;
using SodaCraft.Localizations;
using System;
using System.Reflection;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// 对话系统公共 API。世界空间气泡 + 全屏字幕（NodeCanvas DialogueTree 驱动）。
    /// 本地化：通过 <c>LocalizedStatement(textKey)</c> → <c>ToPlainText()</c> 解析，
    /// FeatherMod I18n 预注入的翻译键直接可用；原始文本作为 key 传入时原样返回。
    /// </summary>
    public static class DialogueUtils
    {
        private static bool _initialized;

        // C# event 不允许外部直接调用；通过 backing field 的 MulticastDelegate 绕过
        private static MulticastDelegate? _onStartedDel, _onFinishedDel;

        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            var bfs = BindingFlags.Public | BindingFlags.Static;
            _onStartedDel = typeof(DialogueTree).GetField("OnDialogueStarted", bfs)?.GetValue(null) as MulticastDelegate;
            _onFinishedDel = typeof(DialogueTree).GetField("OnDialogueFinished", bfs)?.GetValue(null) as MulticastDelegate;
        }

        // ===== 世界空间气泡 =====

        public static void ShowBubble(Identifier npcId, string text, float duration = 2f)
            => FriendlyNpcUtils.ShowBubble(npcId, text, duration);

        public static void ShowBubbleAt(Vector3 pos, string text, float duration = 2f)
        {
            var go = new GameObject("FML_Bubble_Temp"); go.transform.position = pos;
            DialogueBubblesManager.Show(text, go.transform, 1.5f, false, false, -1f, duration).Forget();
            GameObject.Destroy(go, duration + 0.5f);
        }

        // ===== 全屏字幕（LocalizedStatement 作为 IStatement） =====

        /// <summary>播放单行字幕。actorId 为 DuckovDialogueActor.id。</summary>
        public static void PlaySubtitle(string actorId, string text)
        {
            var actor = DuckovDialogueActor.Get(actorId);
            if (actor == null) return;
            try
            {
                _onStartedDel?.DynamicInvoke(null);
                var stmt = new LocalizedStatement(text); // text 作为 key → 录入 I18n 后返回翻译；无则原样
                DialogueTree.RequestSubtitles(new SubtitlesRequestInfo(actor, stmt, (Action)(() =>
                    _onFinishedDel?.DynamicInvoke(null))));
            }
            catch (Exception e) { Debug.LogError($"[FML Dialogue] {e}"); }
        }

        /// <summary>播放字幕序列。defaultActorId 用于未指定发言者的行。</summary>
        public static async UniTask PlaySubtitles(string defaultActorId, SubtitleLine[] lines)
        {
            if (lines == null || lines.Length == 0) return;
            try
            {
                _onStartedDel?.DynamicInvoke(null);
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var actorId = string.IsNullOrEmpty(line.ActorId) ? defaultActorId : line.ActorId;
                    var actor = DuckovDialogueActor.Get(actorId);
                    if (actor == null) continue;

                    bool done = false;
                    var stmt = MakeStatement(line);
                    DialogueTree.RequestSubtitles(new SubtitlesRequestInfo(actor, stmt, (Action)(() => done = true)));
                    await UniTask.WaitUntil(() => done);
                    await UniTask.Delay(TimeSpan.FromSeconds(0.3f));
                }
            }
            catch (Exception e) { Debug.LogError($"[FML Dialogue] {e}"); }
            finally { _onFinishedDel?.DynamicInvoke(null); }
        }

        /// <summary>
        /// 构建 LocalizedStatement。若指定 Text 则直接作为 key（ToPlainText 原样返回）；
        /// 若指定 TextKey 则从 FeatherMod I18n 预注入的翻译键解析。
        /// </summary>
        private static LocalizedStatement MakeStatement(SubtitleLine l)
        {
            var key = !string.IsNullOrEmpty(l.Text) ? l.Text : (l.TextKey ?? "");
            return new LocalizedStatement(key);
        }
    }

    /// <summary>单行字幕配置。</summary>
    public class SubtitleLine
    {
        public string ActorId = "";
        public string? Text;
        public string? TextKey;
    }
}
