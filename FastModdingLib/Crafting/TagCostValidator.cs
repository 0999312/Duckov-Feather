using System;
using System.Collections.Generic;
using ItemStatsSystem;
using UnityEngine;

namespace FeatherMod.Crafting
{
    /// <summary>标签成本验证器：搜索、验证、扣除标签匹配物品。</summary>
    internal static class TagCostValidator
    {
        public static bool Validate(TagItemCost[] costs)
        {
            var items = EnumeratePlayerItems();
            if (items == null) return false;

            foreach (var cost in costs)
            {
                if (CountAvailable(items, cost) < cost.Amount)
                    return false;
            }
            return true;
        }

        public static void Consume(TagItemCost[] costs)
        {
            var items = EnumeratePlayerItems();
            if (items == null) return;

            foreach (var cost in costs)
                ConsumeFromItems(items, cost);
        }

        private static float CountAvailable(List<Item> items, TagItemCost cost)
        {
            float total = 0f;
            foreach (var item in items)
            {
                if (item == null) continue;
                if (!string.IsNullOrEmpty(cost.Tag) && !ItemUtils.HasTag(item, cost.Tag)) continue;
                if (cost.MinQuality.HasValue && item.Quality < cost.MinQuality.Value) continue;
                total += GetEffectiveAmount(item, cost.DurabilityCost);
            }
            return total;
        }

        private static void ConsumeFromItems(List<Item> items, TagItemCost cost)
        {
            var candidates = new List<(Item item, float effective)>();
            foreach (var item in items)
            {
                if (item == null) continue;
                if (!string.IsNullOrEmpty(cost.Tag) && !ItemUtils.HasTag(item, cost.Tag)) continue;
                if (cost.MinQuality.HasValue && item.Quality < cost.MinQuality.Value) continue;
                candidates.Add((item, GetEffectiveAmount(item, cost.DurabilityCost)));
            }
            candidates.Sort((a, b) => a.effective.CompareTo(b.effective));

            float remaining = cost.Amount;
            foreach (var (item, _) in candidates)
            {
                if (remaining <= 0) break;
                int toRemove = Mathf.CeilToInt(Mathf.Min(remaining, (float)item.StackCount));
                item.StackCount -= toRemove;
                if (item.StackCount <= 0) item.DestroyTree();
                remaining -= toRemove;
            }
        }

        private static float GetEffectiveAmount(Item item, bool durabilityCost)
        {
            float stack = item.StackCount;
            if (!durabilityCost) return stack;

            try
            {
                // Item.GetStat 与 Stat.BaseValue/Value 均为 public（Publicizer 已公开），直接访问零反射
                var stat = item.GetStat("Durability".GetHashCode());
                if (stat != null)
                {
                    float baseVal = stat.BaseValue;
                    float curVal = stat.Value;
                    if (baseVal > 0) return stack * (curVal / baseVal);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TagCostValidator.GetEffectiveAmount] Failed: {e.Message}");
            }
            return stack;
        }

        private static List<Item>? EnumeratePlayerItems()
        {
            var inv = CharacterMainControl.Main?.CharacterItem?.Inventory;
            if (inv == null) return null;

            // 游戏原生 Inventory 没有 AllSlots；物品列表是 public List<Item> Content，直接访问零反射
            return inv.Content;
        }
    }
}
