using ItemStatsSystem;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// 建筑槽位监听器。每 Machine 一个独立实例，监听自己的子库存内容变化。
    /// 当子库存内容变化时，调用本 Machine Recipe 的 CanExecute → Execute。
    /// 由 ConfigureBuildingUI 或 RegisterMachineRecipe 自动创建和挂载。
    /// </summary>
    internal class BuildingSlotsWatcher : MonoBehaviour
    {
        private MachineRecipe? _recipe;
        private Dictionary<string, Inventory> _subs = new();

        public void Initialize(MachineRecipe recipe, Dictionary<string, Inventory> subInventories)
        {
            _recipe = recipe;
            _subs = subInventories;

            foreach (var sub in _subs.Values)
            {
                if (sub != null)
                    sub.onContentChanged += OnSlotChanged;
            }
        }

        private void OnSlotChanged(Inventory inv, int index)
        {
            if (_recipe == null) return;
            if (_recipe.IsRunning) return;

            _recipe.MainInventory = GetComponent<Inventory>();
            _recipe.SubInventories = _subs;

            if (_recipe.CanExecute())
            {
                _recipe.Execute();
            }
        }

        public float GetProgress() => _recipe?.GetProgress() ?? 0f;
        public bool IsRecipeRunning() => _recipe?.IsRunning ?? false;

        private void OnDestroy()
        {
            if (_subs == null) return;
            foreach (var sub in _subs.Values)
            {
                if (sub != null)
                    sub.onContentChanged -= OnSlotChanged;
            }
        }
    }
}
