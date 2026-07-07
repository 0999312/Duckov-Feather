using System;
using FeatherMod.Entities;
using FeatherMod.Register;
using FeatherMod.Utils;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// LotteryBox 物品注入 API。
    /// 通过 Harmony Patch 在 LotteryBox.Begin() 时自动注入，无需手动管理时机。
    /// </summary>
    public static class LotteryBoxUtils
    {
        internal static readonly LotteryBoxRegistry Registry = new LotteryBoxRegistry();
        private static bool _initialized;

        /// <summary>
        /// 暴露给 RegisterBootstrap 和外部查询。
        /// </summary>
        public static LotteryBoxRegistry LotteryRegistry => Registry;

        /// <summary>
        /// 初始化：将 LotteryBoxRegistry 注册到 RegistryManager 元表。
        /// 由 RegisterBootstrap.Init() 调用（幂等）。
        /// </summary>
        internal static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            var meta = RegistryManager.Instance.Registry;
            var id = new Identifier(FMLConstants.Domain, "lottery_box");
            if (meta is NonAlterableSimpleRegistry<ERegistry> nonAlt)
                nonAlt.SetIfAbsent(id, Registry, RegistryManager.CurrentModid);
            else
                meta.Set(id, Registry, RegistryManager.CurrentModid);
        }

        // ═══════════════════════════════════════════════════
        //  公开 API — 注册
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 向场景中匹配名称的 LotteryBox 注入物品。
        /// 仅存储规则；物品在 LotteryBox.Begin() 时由 Harmony Patch 自动注入。
        /// 自动识别箱子现有物品的枪/刀类型，仅注入匹配类型（严格隔离）。
        /// </summary>
        /// <param name="sceneNamePattern">目标 LotteryBox GameObject 名称（支持前缀通配 "*"）。</param>
        /// <param name="item">要注入的物品引用（Identifier 或 typeID）。</param>
        /// <param name="weight">注入权重 = 原生条目平均权重 × 此倍数。默认 1.0（与原生条目等权）。</param>
        public static void AddItemToLotteryBox(string sceneNamePattern, ItemEntry item, float weight = 1.0f)
        {
            Init();
            if (string.IsNullOrEmpty(sceneNamePattern))
                throw new ArgumentNullException(nameof(sceneNamePattern));

            var data = new LotteryBoxData
            {
                Item = item,
                Weight = Mathf.Max(0f, weight),
                SceneNamePattern = sceneNamePattern
            };

            var modid = RegistryManager.CurrentModid;
            var id = new Identifier(modid, Guid.NewGuid().ToString());
            Registry.Set(id, data, modid);
            Debug.Log($"[FML] LotteryBox: registered injection '{sceneNamePattern}' <- typeID={item.ResolveTypeId()} weight={weight:F2} mod={modid}");
        }

        // ═══════════════════════════════════════════════════
        //  公开 API — 卸载
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// 移除指定名称模式的 LotteryBox 注入规则。
        /// </summary>
        public static bool RemoveItemFromLotteryBox(string sceneNamePattern, ItemEntry item)
        {
            foreach (var kvp in Registry)
            {
                var data = kvp.Value;
                if (data.SceneNamePattern == sceneNamePattern &&
                    data.Item.ResolveTypeId() == item.ResolveTypeId())
                {
                    return Registry.Remove(kvp.Key);
                }
            }
            return false;
        }

        /// <summary>
        /// 批量卸载指定 mod 注册的全部 LotteryBox 注入。
        /// </summary>
        public static int UnregisterAllLotteryInjections(string modid)
        {
            return Registry.RemoveAllByOwner(modid);
        }
    }
}
