using Duckov.Utilities;
using FeatherMod.Register;
using FeatherMod.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// NPC 装备槽位。
    /// </summary>
    public enum EquipmentSlot
    {
        /// <summary>头部（头盔/帽子）。</summary>
        Head,
        /// <summary>身体（护甲/衣服）。</summary>
        Body,
        /// <summary>背包。</summary>
        Backpack
    }

    /// <summary>
    /// NPC 装备管理 API。支持在 NPC 生成前后配置身体/头部/背包装备。
    /// 内部通过操作 <see cref="CharacterRandomPreset.itemsToGenerate"/> 实现，
    /// 或通过 <see cref="global::CharacterModel"/> 的运行时方法设置。
    /// </summary>
    /// <example>
    /// <code>
    /// // 方式 1：通过 FriendlyNpcConfig 配置（生成时自动注入）
    /// var config = new FriendlyNpcConfig
    /// {
    ///     HeadEquipment = ItemEntry.Of("duckov:CowboyHat", 1),
    ///     BodyEquipment = ItemEntry.Of("duckov:Vest_A", 1)
    /// };
    /// FriendlyNpcUtils.RegisterFriendlyNpc(new Identifier("mymod", "merchant"), config);
    ///
    /// // 方式 2：通过 EquipmentUtils 运行时修改已生成的 NPC
    /// EquipmentUtils.SetNpcEquipment(new Identifier("mymod", "merchant"), EquipmentSlot.Head,
    ///     ItemEntry.Of("duckov:Fedora", 1));
    /// </code>
    /// </example>
    public static class EquipmentUtils
    {
        private static bool _initialized;

        // 缓存装备配置（用于生成后延迟应用）
        private static readonly Dictionary<Identifier, Dictionary<EquipmentSlot, ItemEntry>> _pendingEquipment
            = new Dictionary<Identifier, Dictionary<EquipmentSlot, ItemEntry>>();

        /// <summary>初始化（幂等）。</summary>
        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;
        }

        // ═══════════════════════════════════════════════════
        //  配置 API（在 RegisterFriendlyNpc 之前或之后调用）
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 为 NPC 配置装备。在 <see cref="FriendlyNpcUtils.RegisterFriendlyNpc"/> 之前调用时，
        /// 装备会在生成时自动注入到 <c>itemsToGenerate</c>；之后调用则添加到待处理队列，
        /// 在下次 <see cref="FriendlyNpcUtils.SpawnFriendlyNpcAsync"/> 时应用。
        /// </summary>
        /// <param name="npcId">NPC 标识符。</param>
        /// <param name="slot">装备槽位。</param>
        /// <param name="item">装备物品（含数量）。</param>
        public static void ConfigureNpcEquipment(Identifier npcId, EquipmentSlot slot, ItemEntry item)
        {
            if (!_pendingEquipment.TryGetValue(npcId, out var slots))
            {
                slots = new Dictionary<EquipmentSlot, ItemEntry>();
                _pendingEquipment[npcId] = slots;
            }
            slots[slot] = item;
        }

        /// <summary>获取 NPC 的已配置装备。</summary>
        public static bool TryGetConfiguredEquipment(Identifier npcId, EquipmentSlot slot, out ItemEntry item)
        {
            item = default;
            return _pendingEquipment.TryGetValue(npcId, out var slots)
                && slots.TryGetValue(slot, out item);
        }

        /// <summary>清除 NPC 指定槽位的装备配置。</summary>
        public static bool ClearConfiguredEquipment(Identifier npcId, EquipmentSlot slot)
        {
            return _pendingEquipment.TryGetValue(npcId, out var slots)
                && slots.Remove(slot);
        }

        /// <summary>清除 NPC 的全部装备配置。</summary>
        public static void ClearAllEquipment(Identifier npcId)
        {
            _pendingEquipment.Remove(npcId);
        }

        // ═══════════════════════════════════════════════════
        //  运行时 API（对已生成的 NPC）
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 为已生成的 NPC 设置装备。通过 <see cref="global::CharacterModel"/> 的
        /// 反射访问实现。如果 CharacterModel 上存在 <c>SetEquipment</c> 或类似方法，
        /// 则调用之；否则输出警告并记录到待处理队列（下次生成时生效）。
        /// </summary>
        /// <param name="npcId">已注册的 NPC 标识符。</param>
        /// <param name="slot">装备槽位。</param>
        /// <param name="item">装备物品。</param>
        /// <returns>是否成功应用装备。</returns>
        public static bool SetNpcEquipment(Identifier npcId, EquipmentSlot slot, ItemEntry item)
        {
            // 尝试通过 FriendlyNpcUtils.Registry 查找已生成的 NPC
            var registry = FriendlyNpcUtils.Registry;
            if (registry != null && registry.TryGet(npcId, out var go) && go != null)
            {
                var model = go.GetComponent<global::CharacterModel>();
                if (model != null)
                {
                    // 尝试运行时设置装备
                    if (TrySetEquipmentOnModel(model, slot, item))
                    {
                        Debug.Log($"[FML Equipment] Set {slot}={item} on NPC '{npcId}' (runtime).");
                        return true;
                    }
                }
            }

            // 回退：记录到待处理队列
            ConfigureNpcEquipment(npcId, slot, item);
            Debug.Log($"[FML Equipment] Queued {slot}={item} for NPC '{npcId}' (apply on next spawn).");
            return false;
        }

        /// <summary>获取 NPC 当前装备（从已生成角色读取）。</summary>
        public static ItemEntry? GetNpcEquipment(Identifier npcId, EquipmentSlot slot)
        {
            var registry = FriendlyNpcUtils.Registry;
            if (registry == null) return null;
            if (!registry.TryGet(npcId, out var go) || go == null) return null;

            var model = go.GetComponent<global::CharacterModel>();
            if (model == null) return null;

            return TryGetEquipmentFromModel(model, slot);
        }

        /// <summary>
        /// 清除已生成 NPC 的指定槽位装备。删除对应装备物品。
        /// </summary>
        public static bool ClearNpcEquipment(Identifier npcId, EquipmentSlot slot)
        {
            ClearConfiguredEquipment(npcId, slot);

            var registry = FriendlyNpcUtils.Registry;
            if (registry == null) return false;
            if (!registry.TryGet(npcId, out var go) || go == null) return false;

            var model = go.GetComponent<global::CharacterModel>();
            if (model == null) return false;

            return TryClearEquipmentOnModel(model, slot);
        }

        // ═══════════════════════════════════════════════════
        //  内部：注入到 CharacterRandomPreset.itemsToGenerate
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 将装备配置注入到 CharacterRandomPreset.itemsToGenerate。
        /// 由 FriendlyNpcUtils 在生成前调用。
        /// </summary>
        internal static void InjectEquipmentToPreset(CharacterRandomPreset preset, Identifier npcId)
        {
            if (!_pendingEquipment.TryGetValue(npcId, out var slots)) return;

            // itemsToGenerate 经 Publicizer 已公开，直接访问 List<RandomItemGenerateDescription>
            if (preset.itemsToGenerate == null)
                preset.itemsToGenerate = new System.Collections.Generic.List<RandomItemGenerateDescription>();

            foreach (var kvp in slots)
            {
                InjectItemToPreset(preset, kvp.Value);
            }
        }

        private static void InjectItemToPreset(CharacterRandomPreset preset, ItemEntry item)
        {
            int typeId = item.ResolveTypeId();
            if (typeId <= 0) return;

            // RandomItemGenerateDescription 是 struct，大部分字段是 public
            // Entry.itemTypeID 也是 public [SerializeField]
            // 使用 RandomContainer.AddEntry() 简化构造
            var desc = new RandomItemGenerateDescription
            {
                chance = 1f,
                randomCount = new Vector2Int(1, 1),
                controlDurability = false,
                randomFromPool = true,
            };
            desc.itemPool = new RandomContainer<RandomItemGenerateDescription.Entry>();
            desc.itemPool.AddEntry(
                new RandomItemGenerateDescription.Entry { itemTypeID = typeId }, 1f);

            preset.itemsToGenerate.Add(desc);
        }

        // ═══════════════════════════════════════════════════
        //  运行时装备操作（待后续基于 CharacterItemControl 物品系统实现）
        //  CharacterModel 无反编译源码中确认仅有 SetFaceFromPreset/SetFaceFromData，
        //  无 SetEquipment/GetEquipment。装备通过物品槽位管理：
        //  CharacterMainControl.PrimWeaponSlot/MeleeWeaponSlot/ArmorSlot/HelmatSlot/BackpackSlot
        // ═══════════════════════════════════════════════════

        private static bool TrySetEquipmentOnModel(global::CharacterModel model, EquipmentSlot slot, ItemEntry item)
        {
            Debug.LogWarning($"[FML Equipment] Runtime equipment modification not yet implemented. " +
                "Equipment must be configured before NPC spawn via FriendlyNpcConfig or EquipmentUtils.ConfigureNpcEquipment.");
            return false;
        }

        private static ItemEntry? TryGetEquipmentFromModel(global::CharacterModel model, EquipmentSlot slot)
        {
            return null;
        }

        private static bool TryClearEquipmentOnModel(global::CharacterModel model, EquipmentSlot slot)
        {
            Debug.LogWarning($"[FML Equipment] Runtime equipment modification not yet implemented.");
            return false;
        }
    }
}