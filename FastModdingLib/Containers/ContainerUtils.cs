using Duckov.Buildings;
using Duckov.Utilities;
using FeatherMod.Interaction;
using FeatherMod.Interaction.Components;
using FeatherMod.Register;
using FeatherMod.Utils;
using ItemStatsSystem;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// 轻量物品容器管理工具。不实现完整的 Inventory 系统，
    /// 仅包装游戏原生 API，提供声明式的容器 CRUD 和物品转移。
    /// </summary>
    public static class ContainerUtils
    {
        private static readonly Dictionary<Identifier, ItemContainerConfig> _containers =
            new Dictionary<Identifier, ItemContainerConfig>();
        private static bool _initialized;

        private static void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;
        }

        // ===== 容器 CRUD =====

        /// <summary>
        /// 创建物品容器。需要在游戏运行时调用。
        /// </summary>
        /// <param name="id">容器唯一标识。</param>
        /// <param name="slotCount">槽位数量。</param>
        /// <param name="modid">所属 mod 标识。</param>
        /// <returns>创建的容器配置。</returns>
        public static ItemContainerConfig CreateContainer(Identifier id, int slotCount, string modid)
        {
            EnsureInit();

            if (_containers.ContainsKey(id))
                throw new InvalidOperationException($"Container '{id}' already exists.");

            var config = new ItemContainerConfig
            {
                Id = id,
                SlotCount = slotCount,
                Modid = modid,
                Items = new ItemContainerEntry[slotCount]
            };

            _containers[id] = config;
            return config;
        }

        /// <summary>按 Identifier 获取容器配置。不存在返回 null。</summary>
        public static ItemContainerConfig? GetContainer(Identifier id)
        {
            return _containers.TryGetValue(id, out var config) ? config : null;
        }

        /// <summary>
        /// 销毁容器。注意：此操作不会移除容器中的物品——
        /// 调用方需在销毁前自行处理物品转移。
        /// </summary>
        /// <returns>是否成功销毁。</returns>
        public static bool DestroyContainer(Identifier id)
        {
            return _containers.Remove(id);
        }

        // ===== 物品转移 =====

        /// <summary>向容器指定槽位放入物品。</summary>
        public static bool PutItem(Identifier containerId, int slot, ItemEntry item)
        {
            if (!_containers.TryGetValue(containerId, out var config)) return false;
            if (slot < 0 || slot >= config.SlotCount) return false;

            config.Items[slot] = new ItemContainerEntry
            {
                Item = item,
                Count = item.Amount
            };
            return true;
        }

        /// <summary>从容器指定槽位取出指定数量的物品。取出物品直接发送到玩家库存。</summary>
        /// <returns>取出的物品引用（已发送给玩家），槽位无可取物品返回 null。</returns>
        public static ItemEntry? TakeItem(Identifier containerId, int slot, int amount)
        {
            if (!_containers.TryGetValue(containerId, out var config)) return null;
            if (slot < 0 || slot >= config.SlotCount) return null;

            var entry = config.Items[slot];
            if (entry.Item.Equals(default(ItemEntry)) || entry.Count <= 0) return null;

            int takeAmount = Math.Min(amount, entry.Count);
            entry.Count -= takeAmount;

            if (entry.Count <= 0)
                config.Items[slot] = default;

            // 通过游戏 API 将物品发送给玩家
            try
            {
                var resolvedId = ItemUtils.ResolveItemRef(entry.Item.ItemId, 0);
                var itemPrefab = ItemAssetsCollection.GetPrefab(resolvedId);
                if (itemPrefab != null)
                {
                    // ItemUtilities.SendToPlayerCharacterInventory 接收 Item，不是 GameObject
                    var itemInstance = ItemAssetsCollection.GetPrefab(resolvedId);
                    if (itemInstance != null)
                    {
                        ItemUtilities.SendToPlayerCharacterInventory(itemInstance);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ContainerUtils.TakeItem] Failed to transfer item to player: {e.Message}");
            }

            return ItemEntry.Of(entry.Item.ItemId ?? default, takeAmount);
        }

        // ===== 绑定到建筑 =====

        /// <summary>
        /// 将容器绑定到建筑。当指定建筑建造完成时，
        /// 自动在建筑上挂载 ViewInteractHandler 以打开对应 View。
        /// </summary>
        /// <param name="buildingId">已注册的建筑 Identifier。</param>
        /// <param name="containerId">容器 Identifier。</param>
        /// <param name="viewType">设备 View 类型。</param>
        public static void BindDeviceToBuilding(
            Identifier buildingId, Identifier containerId, Identifier viewType)
        {
            EnsureInit();

            BuildingUtils.OnBuildingBuilt(buildingId, building =>
            {
                var funcContainer = GetBuildingFunctionContainer(building);
                if (funcContainer == null)
                {
                    Debug.LogWarning($"[ContainerUtils] Building '{buildingId}' has no functionContainer.");
                    return;
                }

                // 构建设备交互 Identifier
                var deviceInteractId = new Identifier(containerId.Domain, $"device_{containerId.Path}");

                InteractionUtils.AttachViewInteract(
                    deviceInteractId, funcContainer, viewType,
                    viewParam: containerId.Path);
            });
        }

        // ===== 生命周期 =====

        /// <summary>批量移除指定 mod 注册的全部容器。</summary>
        public static int RemoveAllContainers(string modid)
        {
            var keys = new List<Identifier>();
            foreach (var kvp in _containers)
            {
                if (kvp.Value.Modid == modid)
                    keys.Add(kvp.Key);
            }

            foreach (var key in keys)
                _containers.Remove(key);

            return keys.Count;
        }

        // ===== 内部辅助 =====

        /// <summary>通过反射获取 Building 的 functionContainer GameObject。</summary>
        private static GameObject? GetBuildingFunctionContainer(Building building)
        {
            var field = typeof(Building).GetField("functionContainer",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            return field?.GetValue(building) as GameObject;
        }
    }

    /// <summary>
    /// 物品容器配置。跟踪容器 Id、槽位、所属 mod 和物品数据。
    /// </summary>
    public class ItemContainerConfig
    {
        /// <summary>容器唯一标识。</summary>
        public Identifier Id;

        /// <summary>槽位数量。</summary>
        public int SlotCount;

        /// <summary>所属 mod 标识。</summary>
        public string Modid = string.Empty;

        /// <summary>物品槽位数据（按索引）。</summary>
        public ItemContainerEntry[] Items = Array.Empty<ItemContainerEntry>();
    }

    /// <summary>
    /// 容器物品条目。记录物品引用和持有数量。
    /// </summary>
    public struct ItemContainerEntry
    {
        public ItemEntry Item;
        public int Count;
    }
}
