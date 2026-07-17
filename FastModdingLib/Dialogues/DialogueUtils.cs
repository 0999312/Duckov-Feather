using Cysharp.Threading.Tasks;
using Dialogues;
using Duckov.UI.DialogueBubbles;
using FeatherMod.Utils;
using NodeCanvas.DialogueTrees;
using SodaCraft.Localizations;
using System;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherMod
{
    /// <summary>
    /// 对话系统公共 API。世界空间气泡 + 全屏字幕（NodeCanvas DialogueTree 驱动）。
    /// 字幕显示由 <see cref="Dialogues.DialogueUI"/> 处理（订阅 <c>DialogueTree.OnSubtitlesRequest</c>），
    /// 主面板和镜头由 <c>DialogueTree.OnDialogueStarted</c> 事件触发。
    /// </summary>
    public static class DialogueUtils
    {
        private static bool _initialized;

        // OnDialogueStarted/OnDialogueFinished 是 DialogueTree 的 static event。
        // C# event 编译为 private static backing field——必须用 NonPublic 才能访问。
        private static MulticastDelegate? _onStartedDel, _onFinishedDel;

        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            // event backing field 是 private static：必须 BindingFlags.NonPublic
            var bfs = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            _onStartedDel = typeof(DialogueTree).GetField("OnDialogueStarted", bfs)?.GetValue(null) as MulticastDelegate;
            _onFinishedDel = typeof(DialogueTree).GetField("OnDialogueFinished", bfs)?.GetValue(null) as MulticastDelegate;

            if (_onStartedDel == null)
                Debug.LogWarning("[FML Dialogue] OnDialogueStarted event backing field not found. " +
                    "Dialogue UI and camera may not open.");
        }

        // ===== 世界空间气泡 =====

        public static void ShowBubble(Identifier npcId, string text, float duration = 2f)
            => FriendlyNpcUtils.ShowBubble(npcId, text, duration);

        public static void ShowBubbleAt(Vector3 pos, string text, float duration = 2f)
        {
            var go = new GameObject("FML_Bubble_Temp"); go.transform.position = pos;
            DialogueBubblesManager.Show(text, go.transform, 1.5f, false, false, -1f, duration).Forget();
            Object.Destroy(go, duration + 0.5f);
        }

        // ===== 全屏字幕（NodeCanvas DialogueTree 驱动） =====

        /// <summary>播放单行字幕。actorId 为 DuckovDialogueActor.id。</summary>
        public static void PlaySubtitle(string actorId, string text)
        {
            var actor = DuckovDialogueActor.Get(actorId);
            if (actor == null)
            {
                Debug.LogWarning($"[FML Dialogue] DuckovDialogueActor '{actorId}' not found.");
                return;
            }
            try
            {
                // OnDialogueStarted → DialogueUI 打开主面板 + 禁用输入
                _onStartedDel?.DynamicInvoke(null);
                var stmt = new LocalizedStatement(text);
                // OnSubtitlesRequest → DialogueUI.DoSubtitle（打字机效果 + 音频）
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
                // OnDialogueStarted → DialogueUI 显示主面板
                _onStartedDel?.DynamicInvoke(null);
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var actorId = string.IsNullOrEmpty(line.ActorId) ? defaultActorId : line.ActorId;
                    var actor = DuckovDialogueActor.Get(actorId);
                    if (actor == null)
                    {
                        Debug.LogWarning($"[FML Dialogue] Actor '{actorId}' not found, skipping line {i}.");
                        continue;
                    }

                    bool done = false;
                    var stmt = MakeStatement(line);
                    // OnSubtitlesRequest → DialogueUI.DoSubtitle
                    DialogueTree.RequestSubtitles(new SubtitlesRequestInfo(actor, stmt, (Action)(() => done = true)));
                    await UniTask.WaitUntil(() => done);
                    await UniTask.Delay(TimeSpan.FromSeconds(0.3f));
                }
            }
            catch (Exception e) { Debug.LogError($"[FML Dialogue] {e}"); }
            finally
            {
                // OnDialogueFinished → DialogueUI 隐藏主面板 + 恢复输入
                _onFinishedDel?.DynamicInvoke(null);
            }
        }

        // ===== 内部 =====

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