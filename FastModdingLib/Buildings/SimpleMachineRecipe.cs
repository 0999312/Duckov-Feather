using FeatherMod.Utils;
using ItemStatsSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// 内置简单配方：声明式 "输入→时间→输出" 模式。
    /// 覆盖大多数建筑设备场景，modder 无需写代码。
    ///
    /// 内部使用 ProductionTimer 驱动异步生产，状态通过 SetState/GetState 自动存档。
    /// </summary>
    public class SimpleMachineRecipe : MachineRecipe
    {
        /// <summary>输入要求：从哪些子库存获取物品。</summary>
        public MachineInput[] Inputs = Array.Empty<MachineInput>();

        /// <summary>产物。</summary>
        public MachineOutput[] Outputs = Array.Empty<MachineOutput>();

        /// <summary>副产品（按概率生成）。</summary>
        public MachineOutput[]? Byproducts;

        /// <summary>处理时间（游戏内秒数）。null = 即时。</summary>
        public float? DurationSeconds;

        /// <summary>每周期耐久消耗。</summary>
        public DurabilityCost[]? DurabilityCosts;

        // ── 内部计时器 ──
        private ProductionTimer? _timer;

        public override bool CanExecute()
        {
            if (SubInventories == null) return false;

            foreach (var input in Inputs)
            {
                if (!SubInventories.TryGetValue(input.FromSubKey, out var inv) || inv == null)
                    return false;

                var typeId = ItemUtils.ResolveItemRef(input.ItemId, 0);
                var available = inv.Content.Count(item => item != null && item.TypeID == typeId);
                if (available < input.Amount)
                    return false;
            }
            return true;
        }

        public override void Execute()
        {
            // 1. 消耗输入物品
            foreach (var input in Inputs)
            {
                if (!input.Consume) continue;
                if (SubInventories == null) continue;
                if (!SubInventories.TryGetValue(input.FromSubKey, out var inv) || inv == null) continue;

                var typeId = ItemUtils.ResolveItemRef(input.ItemId, 0);
                int remaining = input.Amount;
                foreach (var item in inv.Content)
                {
                    if (item == null || item.TypeID != typeId) continue;
                    int take = Math.Min(remaining, item.StackCount);
                    item.StackCount -= take;
                    remaining -= take;
                    if (remaining <= 0) break;
                }
            }

            // 2. 扣除耐久
            if (DurabilityCosts != null && SubInventories != null)
            {
                foreach (var dc in DurabilityCosts)
                {
                    if (!SubInventories.TryGetValue(dc.SubKey, out var inv) || inv == null) continue;
                    foreach (var item in inv.Content)
                    {
                        if (item != null)
                            item.Durability = Mathf.Max(0f, item.Durability - dc.DurabilityPerCycle);
                    }
                }
            }

            // 3. 启动异步生产计时器或立即产出
            if (DurationSeconds != null && DurationSeconds > 0f)
            {
                float initialProgress = GetState<float>("timer_progress", 0f);
                _timer = new ProductionTimer();
                _ = _timer.Run(DurationSeconds, ProduceOutputs, initialProgress);

                // 保存进度到内部状态（每帧由 BuildingSlotsWatcher 更新 UI）
                SetState("timer_running", true);
            }
            else
            {
                // 即时配方：直接产出
                ProduceOutputs();
            }
        }

        private void ProduceOutputs()
        {
            if (SubInventories == null) return;

            // 主产物
            foreach (var output in Outputs)
            {
                SpawnOutput(output);
            }

            // 副产品
            if (Byproducts != null)
            {
                foreach (var bp in Byproducts)
                {
                    if (UnityEngine.Random.value <= bp.Chance)
                        SpawnOutput(bp);
                }
            }

            SetState("timer_running", false);
            SetState("timer_progress", 0f);
        }

        private void SpawnOutput(MachineOutput output)
        {
            if (SubInventories == null) return;

            Inventory targetInv;
            if (output.ToSubKey != null && SubInventories.TryGetValue(output.ToSubKey, out var si) && si != null)
                targetInv = si;
            else if (MainInventory != null)
                targetInv = MainInventory;
            else
                return;

            var typeId = ItemUtils.ResolveItemRef(output.ItemId, 0);
            var resultItem = ItemAssetsCollection.InstantiateSync(typeId);
            if (resultItem != null)
            {
                resultItem.StackCount = output.Amount;
                if (resultItem.Stackable)
                    targetInv.AddAndMerge(resultItem);
                else
                    targetInv.AddItem(resultItem);
            }
        }

        public override float GetProgress()
        {
            if (_timer != null && _timer.IsRunning)
                return _timer.Progress;
            return GetState<float>("timer_progress", 0f);
        }

        public override bool IsRunning
            => GetState<bool>("timer_running", false);

        // ═══════════════════════════════════════════════════════
        //  DTO 定义
        // ═══════════════════════════════════════════════════════
    }

    /// <summary>配方输入定义（SimpleMachineRecipe 使用）。</summary>
    public class MachineInput
    {
        /// <summary>来源子库存的 SubKey。</summary>
        public string FromSubKey = "";

        /// <summary>需要的物品 Identifier。</summary>
        public Identifier ItemId = null!;

        /// <summary>需要数量。</summary>
        public int Amount = 1;

        /// <summary>是否消耗（false = 仅检测不消耗，如发电机槽只需"有电"）。</summary>
        public bool Consume = true;
    }

    /// <summary>配方输出定义。</summary>
    public class MachineOutput
    {
        /// <summary>目标子库存的 SubKey。null = 主 Inventory。</summary>
        public string? ToSubKey;

        /// <summary>产物物品 Identifier。</summary>
        public Identifier ItemId = null!;

        /// <summary>产出数量。</summary>
        public int Amount = 1;

        /// <summary>产出概率（0~1）。默认 1.0。</summary>
        public float Chance = 1.0f;
    }

    /// <summary>耐久消耗定义。</summary>
    public class DurabilityCost
    {
        /// <summary>哪个子库存的物品损耗耐久。</summary>
        public string SubKey = "";

        /// <summary>每周期消耗的耐久值。</summary>
        public float DurabilityPerCycle = 0.01f;
    }
}
