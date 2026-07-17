using Cysharp.Threading.Tasks;
using Duckov.UI.DialogueBubbles;
using FeatherMod.Utils;
using NodeCanvas.DialogueTrees;
using NodeCanvas.Framework;
using SodaCraft.Localizations;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// 对话系统公共 API。提供全屏面板（DialogueUI）和世界空间气泡两种播放模式。
    ///
    /// 全屏模式通过动态创建 <see cref="DialogueTreeController"/> 驱动，
    /// 利用 NodeCanvas 的 FullSerializer JSON 图序列化机制。
    /// 如果全屏模式不可用（如 NodeCanvas 初始化失败），自动降级为气泡模式。
    /// </summary>
    public static class DialogueManager
    {
        private static bool _initialized;

        // ── 反射缓存 ──

        private static PropertyInfo? s_blackboardProp;

        /// <summary>初始化（幂等）。</summary>
        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;
        }

        // ═══════════════════════════════════════════════════════
        //  Public API
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 播放一段对话序列。优先使用全屏 DialogueUI 面板，
        /// 失败时自动降级为世界空间气泡。
        /// </summary>
        /// <param name="actorId">主发言者（DuckovDialogueActor.id）。</param>
        /// <param name="lines">对话内容。</param>
        public static async UniTask PlayDialogue(string actorId, DialogueLine[] lines)
        {
            if (string.IsNullOrEmpty(actorId) || lines == null || lines.Length == 0) return;

            var actor = DuckovDialogueActor.Get(actorId);
            if (actor == null)
            {
                Debug.LogWarning($"[FML Dialogue] Actor '{actorId}' not registered. Falling back to bubble.");
                await PlayBubbleDialogue(actorId, lines);
                return;
            }

            var success = await TryPlayPanelDialogue(actorId, actor, lines);
            if (!success)
            {
                Debug.LogWarning($"[FML Dialogue] Panel dialogue failed for '{actorId}', falling back to bubble.");
                await PlayBubbleDialogue(actorId, lines);
            }
        }

        /// <summary>
        /// 播放对话序列（全屏面板模式，无降级）。
        /// </summary>
        /// <param name="actorId">主发言者 Actor ID。</param>
        /// <param name="sequence">对话序列。</param>
        public static async UniTask PlayDialogue(string actorId, DialogueSequence sequence)
        {
            if (!sequence.HasContent) return;
            await PlayDialogue(actorId, sequence.Lines);
        }

        /// <summary>
        /// 使用世界空间气泡播放对话序列。气泡在每个对话行间逐行显示。
        /// 可靠方案——不依赖 NodeCanvas 图反序列化。
        /// </summary>
        /// <param name="actorId">目标 NPC 的 Identifier（用于世界空间定位）。</param>
        /// <param name="lines">对话内容。</param>
        public static async UniTask PlayBubbleDialogue(string actorId, DialogueLine[] lines)
        {
            if (lines == null || lines.Length == 0) return;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var text = line.GetText();
                if (string.IsNullOrEmpty(text)) continue;

                // 通过 DuckovDialogueActor 找到对应的 GO → 挂在上面
                var actor = DuckovDialogueActor.Get(actorId);
                if (actor != null)
                {
                    DialogueBubblesManager.Show(text, actor.transform, 1.5f, false, false, -1f, 3f).Forget();
                }
                else
                {
                    // 没有 actor，创建一个临时 GO
                    ShowBubbleAt(Vector3.zero, text);
                }

                if (i < lines.Length - 1)
                    await UniTask.Delay(3500); // 等待气泡显示 + 阅读时间
            }
        }

        /// <summary>
        /// 用气泡序列播放对话（可靠方案）。
        /// </summary>
        public static async UniTask PlayBubbleDialogue(string actorId, DialogueSequence sequence)
        {
            if (!sequence.HasContent) return;
            await PlayBubbleDialogue(actorId, sequence.Lines);
        }

        /// <summary>在任意世界空间位置显示气泡。</summary>
        public static void ShowBubbleAt(Vector3 pos, string text, float duration = 2f)
        {
            var go = new GameObject("FML_Bubble_Temp");
            go.transform.position = pos;
            DialogueBubblesManager.Show(text, go.transform, 1.5f, false, false, -1f, duration).Forget();
            UnityEngine.Object.Destroy(go, duration + 0.5f);
        }

        /// <summary>在 NPC 上显示气泡（从 World）。</summary>
        public static void ShowNpcBubble(Identifier npcId, string text, float duration = 2f)
        {
            FriendlyNpcUtils.ShowBubble(npcId, text, duration);
        }

        // ═══════════════════════════════════════════════════════
        //  全屏面板（DialogueTreeController 驱动）
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 尝试通过 DialogueTreeController 播放全屏对话。
        /// 使用 NodeCanvas 官方推荐的运行时图注入方式：
        /// ScriptableObject.CreateInstance → Deserialize → StartBehaviour(Graph)。
        /// 返回 true 表示成功，false 表示需要降级。
        /// </summary>
        private static async UniTask<bool> TryPlayPanelDialogue(
            string actorId, DuckovDialogueActor actor, DialogueLine[] lines)
        {
            GameObject? controllerGo = null;

            try
            {
                // ── Step 1: 构建 JSON 并反序列化为 DialogueTree 实例 ──
                var json = BuildDialogueJson(actorId, lines);
                var dt = ScriptableObject.CreateInstance<DialogueTree>();
                var result = dt.Deserialize(json, new List<UnityEngine.Object>(), true);
                if (result == null)
                {
                    Debug.LogWarning("[FML Dialogue] Graph deserialization returned null.");
                    return false;
                }

                // ── Step 2: 创建 GameObject + DialogueTreeController ──
                controllerGo = new GameObject("FML_Dialogue_Runtime");
                controllerGo.hideFlags = HideFlags.HideAndDontSave;
                var blackboard = controllerGo.AddComponent<Blackboard>();
                var controller = controllerGo.AddComponent<DialogueTreeController>();
                SetBlackboard(controller, blackboard);

                // ── Step 3: 通过 StartBehaviour(Graph) 注入图实例（官方推荐方式）──
                controller.StartBehaviour(dt);

                // ── Step 4: 绑定 actor ──
                controller.SetActorReference(actorId, actor);

                // ── Step 5: 启动对话 ──
                controller.StartDialogue();

                // ── Step 6: 等待对话结束 ──
                await UniTask.WaitWhile(() => controller != null && controller.isRunning);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[FML Dialogue] Panel dialogue failed: {e.Message}\n{e.StackTrace}");
                return false;
            }
            finally
            {
                if (controllerGo != null)
                    UnityEngine.Object.Destroy(controllerGo);
            }
        }

        // ═══════════════════════════════════════════════════════
        //  内部：属性注入
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 设置 _blackboard。通过反射注入（_blackboard 可能是字段或属性，
        /// 取决于 NodeCanvas 版本和 Publicizer 配置）。
        /// </summary>
        private static void SetBlackboard(DialogueTreeController controller, Blackboard bb)
        {
            if (s_blackboardProp == null)
            {
                // 先尝试字段（Publicizer 可能已公开）
                var field = typeof(DialogueTreeController).GetField("_blackboard",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(controller, bb);
                    return;
                }

                // 回退到属性
                s_blackboardProp = typeof(DialogueTreeController).GetProperty("_blackboard",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            }

            if (s_blackboardProp != null)
                s_blackboardProp.SetValue(controller, bb);
            else
                Debug.LogWarning("[FML Dialogue] _blackboard not accessible via reflection.");
        }

        // ═══════════════════════════════════════════════════════
        //  JSON 构建
        // ═══════════════════════════════════════════════════════

        private static int s_jsonSeq;

        /// <summary>
        /// 解析对话行在 JSON 中使用的本地化键。
        /// TextKey 优先；否则为纯文本生成唯一 key 并通过 I18n 注册。
        /// 这样游戏端的 ToPlainText() 总是能找到翻译，避免 *text* 包裹。
        /// </summary>
        private static string ResolveDialogueKey(DialogueLine line)
        {
            // 有 TextKey → 直接用作本地化键
            if (!string.IsNullOrEmpty(line.TextKey))
                return line.TextKey;

            // 只有纯文本 → 生成唯一 key 并注册到游戏本地化系统
            if (!string.IsNullOrEmpty(line.Text))
            {
                var key = $"FML_DialogueLine_{s_jsonSeq}_{Guid.NewGuid():N}";
                LocalizationManager.SetOverrideText(key, line.Text);
                return key;
            }

            return "";
        }

        /// <summary>
        /// 构建 FullSerializer 格式的 DialogueTree JSON。
        /// 格式参考：DecompiledDLL 中 Quest 36_Sub.prefab 的 _boundGraphSerialization。
        /// </summary>
        private static string BuildDialogueJson(string defaultActorId, DialogueLine[] lines)
        {
            var actorUid = Guid.NewGuid().ToString();
            var nodeOffset = s_jsonSeq;
            var nodes = new StringBuilder();
            var connections = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                // 确定 JSON 中的 key._value：
                //   TextKey 非空 → 直接用作本地化键（游戏端 ToPlainText 解析）
                //   Text 非空且 TextKey 为空 → 注册为临时本地化键（避免 ToPlainText 返回 *key*）
                var key = ResolveDialogueKey(line);
                var lineActorName = line.ResolveActorId(defaultActorId);
                var nodeId = (nodeOffset + i).ToString();

                if (i > 0) nodes.Append(',');
                nodes.Append("{\"key\":{\"_value\":\"");
                nodes.Append(EscapeJson(key));
                nodes.Append("\"},\"_actorName\":\"");
                nodes.Append(EscapeJson(lineActorName));
                nodes.Append("\",\"_actorParameterID\":\"");
                nodes.Append(actorUid);
                nodes.Append("\",\"_tag\":\"FML_Dialogue_");
                nodes.Append(nodeId);
                nodes.Append("\",\"_position\":{\"x\":0.0,\"y\":");
                nodes.Append(i * 120.0);
                nodes.Append("},\"$type\":\"Dialogues.LocalizedStatementNode\",\"$id\":\"");
                nodes.Append(nodeId);
                nodes.Append("\"}");

                if (i > 0)
                {
                    if (i > 1) connections.Append(',');
                    connections.Append("{\"_sourceNode\":{\"$ref\":\"");
                    connections.Append(nodeOffset + i - 1);
                    connections.Append("\"},\"_targetNode\":{\"$ref\":\"");
                    connections.Append(nodeOffset + i);
                    connections.Append("\"},\"$type\":\"NodeCanvas.DialogueTrees.DTConnection\"}");
                }
            }

            s_jsonSeq += lines.Length + 10;

            return "{\"type\":\"NodeCanvas.DialogueTrees.DialogueTree\"," +
                   "\"nodes\":[" + nodes + "]," +
                   "\"connections\":[" + connections + "]," +
                   "\"canvasGroups\":[]," +
                   "\"localBlackboard\":{\"_variables\":{}}," +
                   "\"derivedData\":{" +
                     "\"actorParameters\":[{\"_keyName\":\"" + EscapeJson(defaultActorId) + "\",\"_id\":\"" + actorUid + "\"}]," +
                     "\"$type\":\"NodeCanvas.DialogueTrees.DialogueTree+DerivedSerializationData\"" +
                   "}}";
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
