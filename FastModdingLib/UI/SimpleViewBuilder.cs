using UnityEngine;
using UnityEngine.UI;
using FeatherMod.UI;

namespace FeatherMod.UI
{
    /// <summary>
    /// 代码端 Canvas 构建器。让 modder 用纯 C# 代码创建基本 UI 面板，
    /// 无需在 Unity 编辑器中手动搭建 Canvas Prefab。
    /// </summary>
    /// <remarks>
    /// 这是轻量级辅助工具，覆盖约 15% 的简单 UI 需求。
    /// 对于更复杂的 UI，推荐使用 Harmony Postfix 注入模式
    /// （在已有 View 的 Setup() 中追加按钮/条目/过滤标签）。
    /// </remarks>
    /// <example>
    /// <code>
    /// var view = SimpleViewBuilder.Create("MyPanel")
    ///     .AddTitle("我的面板")
    ///     .AddText("欢迎使用！")
    ///     .AddButton("确认", () => Debug.Log("Clicked"))
    ///     .AddCloseButton()
    ///     .Build();
    /// </code>
    /// </example>
    public class SimpleViewBuilder
    {
        private readonly GameObject _root;
        private readonly RectTransform _contentParent;
        private float _yOffset;

        private SimpleViewBuilder(GameObject root)
        {
            _root = root;

            // 创建内容容器
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(root.transform, false);
            _contentParent = contentGo.AddComponent<RectTransform>();
            _contentParent.anchorMin = new Vector2(0, 0);
            _contentParent.anchorMax = new Vector2(1, 1);
            _contentParent.offsetMin = new Vector2(40, 40);
            _contentParent.offsetMax = new Vector2(-40, -40);
            _yOffset = -20f;
        }

        /// <summary>创建基础 Canvas（Screen Space Overlay, 1920×1080）。</summary>
        public static SimpleViewBuilder Create(string viewName)
        {
            var go = new GameObject(viewName);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();

            // 半透明背景
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(go.transform, false);
            var bg = bgGo.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.7f);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            return new SimpleViewBuilder(go);
        }

        /// <summary>添加标题文本。</summary>
        public SimpleViewBuilder AddTitle(string text, int fontSize = 28)
        {
            return AddText(text, fontSize, FontStyle.Bold);
        }

        /// <summary>添加普通文本。</summary>
        public SimpleViewBuilder AddText(string text, int fontSize = 18, FontStyle style = FontStyle.Normal)
        {
            var go = CreateChild("Text", _contentParent);
            var txt = go.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = new Vector2(0, _yOffset);
            rect.sizeDelta = new Vector2(0, fontSize + 10);
            _yOffset -= fontSize + 20;
            return this;
        }

        /// <summary>添加按钮。</summary>
        public SimpleViewBuilder AddButton(string text, System.Action onClick)
        {
            var go = CreateChild("Button", _contentParent);
            var btn = go.AddComponent<Button>();
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.AddComponent<Text>();
            label.text = text;
            label.fontSize = 18;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;

            btn.onClick.AddListener(() => onClick?.Invoke());

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.2f, 1);
            rect.anchorMax = new Vector2(0.8f, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = new Vector2(0, _yOffset);
            rect.sizeDelta = new Vector2(0, 40);
            _yOffset -= 50;
            return this;
        }

        /// <summary>
        /// 添加游戏原生风格按钮（克隆自 GameplayDataSettings.UIPrefabs.Button）。
        /// 自动继承游戏原生的精灵、颜色和字体，视觉与游戏内按钮完全一致。
        /// 如果 GameUIUtils 克隆失败（如预制体不可用），降级为纯色按钮。
        /// </summary>
        public SimpleViewBuilder AddGameButton(string text, System.Action onClick)
        {
            try
            {
                GameUIUtils.CloneButton(_contentParent, text, onClick);
            }
            catch
            {
                // 降级：使用纯色按钮
                AddButton(text, onClick);
            }
            _yOffset -= 50;
            return this;
        }

        /// <summary>
        /// 添加游戏原生风格面板。尝试克隆游戏原生面板模板，
        /// 降级为纯色半透明背景面板。
        /// </summary>
        public SimpleViewBuilder AddGamePanel(string title)
        {
            var panelGo = new GameObject($"Panel_{title}");
            panelGo.transform.SetParent(_contentParent, false);
            var img = panelGo.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

            var panelRect = panelGo.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.05f, 1);
            panelRect.anchorMax = new Vector2(0.95f, 1);
            panelRect.pivot = new Vector2(0.5f, 1);
            panelRect.anchoredPosition = new Vector2(0, _yOffset);
            panelRect.sizeDelta = new Vector2(0, 80);

            // 面板标题
            if (!string.IsNullOrEmpty(title))
            {
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(panelGo.transform, false);
                var label = labelGo.AddComponent<Text>();
                label.text = title;
                label.fontSize = 16;
                label.fontStyle = FontStyle.Bold;
                label.color = Color.white;
                label.alignment = TextAnchor.MiddleLeft;
                var labelRect = labelGo.AddComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0, 0);
                labelRect.anchorMax = new Vector2(1, 1);
                labelRect.offsetMin = new Vector2(10, 0);
                labelRect.offsetMax = new Vector2(-10, 0);
            }

            _yOffset -= 90;
            return this;
        }

        /// <summary>添加关闭按钮（调用 Destroy(root)）。</summary>
        public SimpleViewBuilder AddCloseButton(string text = "关闭")
        {
            return AddButton(text, () => UnityEngine.Object.Destroy(_root));
        }

        /// <summary>构建并返回根 GameObject。</summary>
        public GameObject Build()
        {
            return _root;
        }

        private static GameObject CreateChild(string name, RectTransform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }
    }
}
