using Duckov.Quests;
using ItemStatsSystem;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherMod.Quests
{
    public class FMLTask_SubmitItemByTag : Task
    {
        [SerializeField] private string itemTag = "";
        [SerializeField] private int requireAmount = 1;
        [SerializeField] private int? minQuality;
        [SerializeField] private bool durabilityCost;
        [SerializeField] private int submitted;

        public string ItemTag { get => itemTag; internal set => itemTag = value ?? ""; }
        public int RequireAmount { get => requireAmount; internal set => requireAmount = value; }
        public int? MinQuality { get => minQuality; internal set => minQuality = value; }
        public bool DurabilityCost { get => durabilityCost; internal set => durabilityCost = value; }
        public int Submitted => submitted;

        protected override bool CheckFinished() => submitted >= requireAmount;
        public override object GenerateSaveData() => submitted;
        public override void SetupSaveData(object data) { if (data is int n) submitted = n; }

        public bool TrySubmitFromInventory()
        {
            var items = EnumeratePlayerItems();
            if (items == null) return false;

            int needed = requireAmount - submitted;
            if (needed <= 0) return false;

            var candidates = new List<(Item item, float effective)>();
            foreach (var item in items)
            {
                if (item == null) continue;
                if (!string.IsNullOrEmpty(itemTag) && !ItemUtils.HasTag(item, itemTag)) continue;
                if (minQuality.HasValue && item.Quality < minQuality.Value) continue;
                candidates.Add((item, GetEffective(item, durabilityCost)));
            }
            candidates.Sort((a, b) => a.effective.CompareTo(b.effective));

            float acc = 0f;
            var consume = new List<Item>();
            foreach (var (item, eff) in candidates)
            {
                if (acc >= needed) break;
                acc += eff;
                consume.Add(item);
            }
            if (acc < needed) return false;

            foreach (var item in consume)
            {
                item.StackCount--;
                if (item.StackCount <= 0) item.DestroyTree();
            }
            submitted += needed;
            ReportStatusChanged();
            return true;
        }

        private static float GetEffective(Item item, bool useDurability)
        {
            if (!useDurability) return item.StackCount;
            try
            {
                // Item.GetStat 与 Stat.BaseValue/Value 均为 public（Publicizer 已公开），直接访问零反射
                var stat = item.GetStat("Durability".GetHashCode());
                if (stat != null)
                {
                    float baseVal = stat.BaseValue;
                    float curVal = stat.Value;
                    if (baseVal > 0) return item.StackCount * (curVal / baseVal);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FMLTask_SubmitItemByTag.GetEffective] Failed: {e.Message}");
            }
            return item.StackCount;
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
