using FeatherMod.Utils;
using System;
using System.Collections.Generic;

namespace FeatherMod.Interaction
{
    /// <summary>
    /// View 调度器。维护 Identifier → 打开方法的映射，
    /// 供 ViewInteractHandler 在交互完成时触发。
    /// </summary>
    public static class ViewDispatcher
    {
        private static readonly Dictionary<Identifier, Action<string?>> _handlers =
            new Dictionary<Identifier, Action<string?>>();

        /// <summary>modid → 已注册 View 类型列表（用于按 mod 卸载）。</summary>
        private static readonly Dictionary<string, List<Identifier>> _ownerIndex =
            new Dictionary<string, List<Identifier>>();

        /// <summary>注册 View 打开方法。同一 viewType 重复注册会覆盖。</summary>
        public static void Register(Identifier viewType, Action<string?> openAction, string modid)
        {
            _handlers[viewType] = openAction;

            // 追踪 owner 以便按 mod 卸载
            if (!_ownerIndex.TryGetValue(modid, out var list))
            {
                list = new List<Identifier>();
                _ownerIndex[modid] = list;
            }
            if (!list.Contains(viewType))
                list.Add(viewType);
        }

        /// <summary>打开指定 View，传递可选参数。</summary>
        public static void Open(Identifier viewType, string? viewParam = null)
        {
            if (_handlers.TryGetValue(viewType, out var handler))
            {
                try
                {
                    handler(viewParam);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"[ViewDispatcher] Error opening view '{viewType}': {e}");
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[ViewDispatcher] No handler registered for view type: {viewType}");
            }
        }

        /// <summary>检查指定 View 类型是否已注册。</summary>
        public static bool IsRegistered(Identifier viewType)
            => _handlers.ContainsKey(viewType);

        /// <summary>注销指定 View 类型。</summary>
        public static bool Unregister(Identifier viewType)
            => _handlers.Remove(viewType, out _);

        /// <summary>按 modid 批量注销该 mod 注册的全部 View handler。</summary>
        public static int UnregisterAll(string modid)
        {
            if (!_ownerIndex.TryGetValue(modid, out var types))
                return 0;

            int count = 0;
            foreach (var viewType in types)
            {
                if (_handlers.Remove(viewType))
                    count++;
            }
            _ownerIndex.Remove(modid);
            return count;
        }
    }

    /// <summary>
    /// 游戏内置 View 类型的 Identifier 常量。
    /// 供 InteractionUtils.Init() 自动注册对应的打开方法。
    /// </summary>
    public static class GameViews
    {
        public static readonly Identifier PerkTree  = new Identifier("fml", "perktree");
        public static readonly Identifier Building  = new Identifier("fml", "building");
        public static readonly Identifier Endowment = new Identifier("fml", "endowment");
        public static readonly Identifier Shop      = new Identifier("fml", "shop");
        public static readonly Identifier Crafting  = new Identifier("fml", "crafting");
        public static readonly Identifier Quest     = new Identifier("fml", "quest");
        public static readonly Identifier Formulas         = new Identifier("fml", "formulas");
        public static readonly Identifier FormulasRegister = new Identifier("fml", "formulas_register");
        public static readonly Identifier Decompose        = new Identifier("fml", "decompose");
        public static readonly Identifier Machine          = new Identifier("fml", "machine");
    }
}
