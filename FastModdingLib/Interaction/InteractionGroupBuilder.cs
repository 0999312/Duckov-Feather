using Duckov;
using FeatherMod.Interaction.Components;
using FeatherMod.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherMod.Interaction
{
    /// <summary>
    /// 声明式多交互组构建器。让 modder 用链式 Add().Add().WithPrimary().BuildOn()
    /// 在一个 GameObject 上组合多个 View 交互入口，并自动编组为
    /// <see cref="InteractableBase.interactableGroup"/>（主交互体唯一可交互，成员碰撞体禁用）。
    /// </summary>
    /// <remarks>
    /// 风格对齐 <c>DialogueSequence.Build</c> 的 <c>SequenceBuilder</c>：内部累积条目，终端方法落地。
    /// 单一交互条目时直接挂到目标对象上（不创建子 GO、不编组）；
    /// 多条目时为每个条目创建子 GameObject 并调用 <see cref="InteractionUtils.SetupInteractionGroup"/>。
    /// </remarks>
    /// <example>
    /// <code>
    /// var primary = new InteractionGroupBuilder()
    ///     .Add(new Identifier("mymod", "craft"), GameViews.Crafting, viewParam: "drink",
    ///          interactNameKey: "UI_Craft_Drinks", markerOffset: new Vector3(0, 1.5f, 0))
    ///     .Add(new Identifier("mymod", "perk"), GameViews.PerkTree, viewParam: "brewmaster",
    ///          interactNameKey: "UI_Perk_Brewmaster")
    ///     .WithPrimary(0)
    ///     .BuildOn(functionContainer);
    /// </code>
    /// </example>
    public class InteractionGroupBuilder
    {
        /// <summary>单条交互声明。</summary>
        private struct Entry
        {
            public Identifier Id;
            public Identifier ViewType;
            public string? ViewParam;
            public string? InteractNameKey;
            public Vector3? MarkerOffset;
        }

        private readonly List<Entry> _entries = new();
        private int _primaryIndex = 0;

        /// <summary>
        /// 追加一个交互条目。
        /// </summary>
        /// <param name="id">交互点唯一标识（同时用作子 GO 名后缀与注册键）。</param>
        /// <param name="viewType">目标 View 类型 Identifier（如 <see cref="GameViews.Crafting"/>）。</param>
        /// <param name="viewParam">可选参数，传递给 View 打开方法。</param>
        /// <param name="interactNameKey">可选交互名本地化 key，覆盖默认交互提示文本。</param>
        /// <param name="markerOffset">可选交互标记相对交互点的世界偏移。</param>
        /// <returns>this（链式调用）。</returns>
        public InteractionGroupBuilder Add(
            Identifier id, Identifier viewType,
            string? viewParam = null, string? interactNameKey = null, Vector3? markerOffset = null)
        {
            _entries.Add(new Entry
            {
                Id = id,
                ViewType = viewType,
                ViewParam = viewParam,
                InteractNameKey = interactNameKey,
                MarkerOffset = markerOffset,
            });
            return this;
        }

        /// <summary>
        /// 指定哪个 Add 序号（0-based）作为组的主交互入口。默认 0。
        /// 仅在多于 1 个条目时影响编组；单条目时忽略。
        /// </summary>
        /// <param name="index">主交互条目的 Add 序号。</param>
        /// <returns>this（链式调用）。</returns>
        public InteractionGroupBuilder WithPrimary(int index)
        {
            _primaryIndex = index;
            return this;
        }

        /// <summary>
        /// 将已声明的条目落地到 <paramref name="target"/> 上，返回主交互处理器。
        /// <list type="bullet">
        /// <item>单条目：直接挂到 target（不创建子 GO、不编组），由
        ///   <see cref="InteractionUtils.AttachViewInteract"/> 完成挂载与注册。</item>
        /// <item>多条目：为每个条目创建子 GameObject（"Interact_{id.Path}"），
        ///   挂 BoxCollider(Trigger) + Interactable 图层 + ViewInteractHandler，
        ///   再以 <see cref="InteractionUtils.SetupInteractionGroup"/> 编组。</item>
        /// </list>
        /// </summary>
        /// <param name="target">承载交互的 GameObject（通常为功能容器/家具/NPC 根对象）。</param>
        /// <returns>主交互条目的 <see cref="ViewInteractHandler"/>。</returns>
        /// <exception cref="System.InvalidOperationException">未声明任何条目，或 <see cref="WithPrimary"/> 指定的序号越界。</exception>
        public ViewInteractHandler BuildOn(GameObject target)
        {
            if (_entries.Count == 0)
                throw new System.InvalidOperationException(
                    "[InteractionGroupBuilder] BuildOn called with no entries. Call Add() at least once first.");

            if (_primaryIndex < 0 || _primaryIndex >= _entries.Count)
                throw new System.InvalidOperationException(
                    $"[InteractionGroupBuilder] Primary index {_primaryIndex} out of range " +
                    $"(have {_entries.Count} entries).");

            // ── 单条目：直接挂到 target，不创建子 GO、不编组 ──
            // 由 AttachViewInteract 完成挂载 + 注册到 InteractionRegistry。
            if (_entries.Count == 1)
            {
                var e = _entries[0];
                InteractionUtils.AttachViewInteract(
                    e.Id, target, e.ViewType,
                    viewParam: e.ViewParam,
                    addColliderIfMissing: true,
                    interactNameKey: e.InteractNameKey,
                    markerOffset: e.MarkerOffset);
                // AttachViewInteract 不返回 handler，从 target 取回。
                // AddComponent 保证返回最后挂载的实例；单条目场景下即本次新增。
                return target.GetComponent<ViewInteractHandler>();
            }

            // ── 多条目：为每个条目创建子 GO + handler，再编组 ──
            var handlers = new List<ViewInteractHandler>(_entries.Count);
            int interactLayer = LayerMask.NameToLayer("Interactable");

            foreach (var e in _entries)
            {
                var child = new GameObject($"Interact_{e.Id.Path}");
                child.transform.SetParent(target.transform);
                child.transform.localPosition = Vector3.zero;
                child.transform.localRotation = Quaternion.identity;
                if (interactLayer != -1) child.layer = interactLayer;

                // 必须有 Collider（Trigger）+ Interactable 图层，否则物理交互扫描发现不到子对象。
                var col = child.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, 0.5f, 0f);
                col.size = new Vector3(2f, 1.3f, 2f);

                var handler = child.AddComponent<ViewInteractHandler>();
                handler.ViewType = e.ViewType;
                handler.ViewParam = e.ViewParam;
                handler.overrideInteractName = e.InteractNameKey != null;
                handler._overrideInteractNameKey = e.InteractNameKey;
                handler.InteractNameKey = e.InteractNameKey;
                handler.MarkerOffset = e.MarkerOffset;
                // CoolTime 默认 0f（无冷却）；条目结构未携带该字段，保持默认。
                handlers.Add(handler);
            }

            var primary = handlers[_primaryIndex];

            // 收集非主成员为 InteractableBase[]（ViewInteractHandler 继承自 InteractableBase）。
            var members = new InteractableBase[handlers.Count - 1];
            int mi = 0;
            for (int i = 0; i < handlers.Count; i++)
            {
                if (i == _primaryIndex) continue;
                members[mi++] = handlers[i];
            }

            InteractionUtils.SetupInteractionGroup(primary, members);
            return primary;
        }
    }
}
