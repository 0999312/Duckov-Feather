using Duckov.UI;
using Duckov.Utilities;
using ItemStatsSystem;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FeatherMod.UI
{
    /// <summary>
    /// 游戏原生 UI 桥接工具。提供控件克隆、样式查询和快捷 View 打开。
    /// 所有克隆操作从 GameplayDataSettings.UIPrefabs 获取原生预制体，
    /// 自动继承精灵/材质/着色器，保证视觉一致性。
    /// </summary>
    public static class GameUIUtils
    {
        // ═══════════════════════════════════════════════════
        //  控件克隆（来源：GameplayDataSettings.UIPrefabs）
        // ═══════════════════════════════════════════════════

        /// <summary>克隆游戏原生物品图标显示。</summary>
        public static ItemDisplay CloneItemDisplay(Transform parent)
        {
            var prefab = GameplayDataSettings.UIPrefabs.ItemDisplay;
            if (prefab == null) throw new InvalidOperationException("UIPrefabs.ItemDisplay is null.");
            return Object.Instantiate(prefab, parent);
        }

        /// <summary>克隆游戏原生物品槽位显示。</summary>
        public static SlotDisplay CloneSlotDisplay(Transform parent)
        {
            var prefab = GameplayDataSettings.UIPrefabs.SlotDisplay;
            if (prefab == null) throw new InvalidOperationException("UIPrefabs.SlotDisplay is null.");
            return Object.Instantiate(prefab, parent);
        }

        /// <summary>克隆游戏原生库存条目显示。</summary>
        public static InventoryEntry CloneInventoryEntry(Transform parent)
        {
            var prefab = GameplayDataSettings.UIPrefabs.InventoryEntry;
            if (prefab == null) throw new InvalidOperationException("UIPrefabs.InventoryEntry is null.");
            return Object.Instantiate(prefab, parent);
        }

        /// <summary>克隆游戏原生按钮（含正确的精灵/颜色/字体）。</summary>
        public static Button CloneButton(Transform parent, string label, Action onClick)
        {
            var prefab = GameplayDataSettings.UIPrefabs.Button;
            if (prefab == null) throw new InvalidOperationException("UIPrefabs.Button is null.");
            var button = Object.Instantiate(prefab, parent);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke());

            // 设置按钮文本
            var labelComp = button.GetComponentInChildren<TextMeshProUGUI>();
            if (labelComp != null)
                labelComp.text = label;

            return button;
        }

        /// <summary>克隆游戏原生滚动区域。</summary>
        public static ScrollRect CloneScrollRect(Transform parent)
        {
            var prefab = GameplayDataSettings.UIPrefabs.ScrollRect;
            if (prefab == null) throw new InvalidOperationException("UIPrefabs.ScrollRect is null.");
            return Object.Instantiate(prefab, parent);
        }

        // ═══════════════════════════════════════════════════
        //  样式查询
        // ═══════════════════════════════════════════════════

        /// <summary>获取游戏主字体（从活跃 View 中的 TextMeshProUGUI 提取）。</summary>
        public static TMP_FontAsset? GetGameFont()
        {
            var views = GameplayUIManager.Instance?.views;
            if (views == null) return null;

            foreach (var view in views)
            {
                if (view == null) continue;
                var tmpTexts = view.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var tmp in tmpTexts)
                {
                    if (tmp.font != null)
                        return tmp.font;
                }
            }
            return null;
        }

        /// <summary>提取游戏 UI 配色方案（从活跃 View 实例的 [SerializeField] Color 字段）。</summary>
        public static GameUIColorPalette GetColorPalette()
        {
            var palette = new GameUIColorPalette();
            var views = GameplayUIManager.Instance?.views;
            if (views == null) return palette;

            foreach (var view in views)
            {
                if (view == null) continue;
                ExtractColorsFromView(view, palette);
            }
            return palette;
        }

        /// <summary>
        /// 遍历指定 View 及其子物体的 [SerializeField] Color 字段，
        /// 提取到 palette 中（仅填充尚未赋值的字段）。
        /// </summary>
        private static void ExtractColorsFromView(Component view, GameUIColorPalette palette)
        {
            var components = view.GetComponentsInChildren<Component>(true);
            foreach (var comp in components)
            {
                if (comp == null) continue;
                var fields = comp.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    if (field.FieldType != typeof(Color)) continue;
                    if (field.GetCustomAttribute<SerializeField>() == null) continue;

                    var color = (Color)field.GetValue(comp);
                    var fieldName = field.Name.ToLowerInvariant();

                    if (IsTextColorField(fieldName) && palette.TextPrimary == default)
                        palette.TextPrimary = color;
                    else if (IsPanelBackgroundField(fieldName) && palette.PanelBackground == default)
                        palette.PanelBackground = color;
                    else if (IsButtonNormalField(fieldName) && palette.ButtonNormal == default)
                        palette.ButtonNormal = color;
                    else if (IsButtonHighlightField(fieldName) && palette.ButtonHighlight == default)
                        palette.ButtonHighlight = color;
                }
            }
        }

        private static bool IsTextColorField(string name)
            => name.Contains("text") || name.Contains("font") || name.Contains("label");

        private static bool IsPanelBackgroundField(string name)
            => name.Contains("panel") || name.Contains("background") || name.Contains("bg");

        private static bool IsButtonNormalField(string name)
            => name.Contains("button") && !name.Contains("highlight") && !name.Contains("hover") && !name.Contains("pressed");

        private static bool IsButtonHighlightField(string name)
            => name.Contains("highlight") || name.Contains("hover") || name.Contains("selected");

        // ═══════════════════════════════════════════════════
        //  快捷 View 打开
        // ═══════════════════════════════════════════════════

        /// <summary>打开过滤式合成界面。tags 为工作台标签数组（如 "Forge"）。</summary>
        public static void OpenCraftingView(string[]? tags = null)
        {
            if (tags == null || tags.Length == 0)
            {
                CraftView.SetupAndOpenView(null);
                return;
            }

            var tagSet = new HashSet<string>(tags);
            CraftView.SetupAndOpenView(formula =>
            {
                if (formula.tags == null) return false;
                foreach (var tag in formula.tags)
                {
                    if (tagSet.Contains(tag))
                        return true;
                }
                return false;
            });
        }

        /// <summary>
        /// 打开库存设备面板。将指定 Inventory 绑定到 InventoryDisplay。
        /// </summary>
        public static void OpenInventoryDevice(Inventory inventory)
        {
            // InventoryDisplay 是 MonoBehaviour（非 View 子类），通过 FindObjectOfType 查找
            var display = Object.FindObjectOfType<InventoryDisplay>();
            if (display != null)
            {
                display.Setup(inventory);
            }
        }

        /// <summary>打开配方索引视图（浏览全部配方）。</summary>
        public static void OpenFormulasIndexView()
        {
            FormulasIndexView.Show();
        }

        /// <summary>打开配方注册视图（提交物品学习配方）。显示全部可注册配方。</summary>
        /// <remarks>Tag 为 ScriptableObject，运行时无法通过字符串查找，故显示全部。</remarks>
        public static void OpenFormulasRegisterView()
        {
            FormulasRegisterView.Show(null);
        }

        /// <summary>打开物品分解视图。</summary>
        public static void OpenDecomposeView()
        {
            ItemDecomposeView.Show();
        }
    }

    /// <summary>
    /// 游戏 UI 配色方案，运行时从活跃 View 实例中提取。
    /// </summary>
    public class GameUIColorPalette
    {
        public Color TextPrimary = Color.white;
        public Color PanelBackground = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        public Color ButtonNormal = new Color(0.3f, 0.3f, 0.3f, 1f);
        public Color ButtonHighlight = new Color(0.5f, 0.5f, 0.5f, 1f);
    }
}
