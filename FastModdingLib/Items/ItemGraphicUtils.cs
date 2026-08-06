using Cysharp.Threading.Tasks;
using FeatherMod.Register;
using FeatherMod.Utils;

using ItemStatsSystem;

using System;
using System.Collections.Generic;

using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// ItemGraphic 封装：快速构建简单物品的 ItemGraphic GameObject（同时包含
    /// <c>ItemGraphicInfo</c> + <c>CharacterSubVisuals</c>，renderers 仅 1 个元素 = 主模型 Mesh Renderer）。
    /// 模型来自 <see cref="ModelUtils"/>（OBJ 简化路径）或复用原版物品模型。
    /// 绑定后原版 <c>ItemGraphicInfo.CreateAGraphic</c> 掉落/装备/手持链路自动生效。
    /// </summary>
    public static class ItemGraphicUtils
    {
        // ===== GO 模板缓存（(meshId, textureKey) → inactive 模板） =====

        private static readonly Dictionary<ItemGraphicKey, GameObject> _templateCache = new Dictionary<ItemGraphicKey, GameObject>();
        private static readonly object _templateLock = new object();
        private static GameObject? _holder;

        /// <summary>
        /// 模板的 inactive 容器。代码创建的模板必须跨场景存活（DontDestroyOnLoad），
        /// 但 active 的模板会被加载界面 Curtain 相机（CullingMask=Everything，DepthOnly）渲染。
        /// 挂到 inactive 容器下：模板 activeSelf 保持 true（Instantiate 的副本正常激活），
        /// 但 activeInHierarchy=false（模板本体不渲染，与原版 asset prefab 语义一致）。
        /// 参照 BuildingUtils.PrefabHolder。
        /// </summary>
        private static GameObject Holder
        {
            get
            {
                if (_holder == null)
                {
                    _holder = new GameObject("FML_ItemGraphicTemplates");
                    _holder.SetActive(false);
                    UnityEngine.Object.DontDestroyOnLoad(_holder);
                }
                return _holder;
            }
        }

        private readonly struct ItemGraphicKey : IEquatable<ItemGraphicKey>
        {
            public readonly Identifier MeshId;
            public readonly string TextureKey;
            public ItemGraphicKey(Identifier meshId, string textureKey)
            {
                MeshId = meshId;
                TextureKey = textureKey;
            }
            public bool Equals(ItemGraphicKey other)
            {
                return MeshId.Equals(other.MeshId) && TextureKey == other.TextureKey;
            }
            public override bool Equals(object? obj)
            {
                return obj is ItemGraphicKey other && Equals(other);
            }
            public override int GetHashCode()
            {
                return HashCode.Combine(MeshId.GetHashCode(), TextureKey);
            }
        }

        // ===== 构建 ItemGraphic GameObject =====

        /// <summary>
        /// 构建简单物品的 ItemGraphic GameObject（含 ItemGraphicInfo + CharacterSubVisuals，
        /// renderers 仅 1 个元素 = 主模型 Mesh Renderer，含 GroundPoint）。
        /// 返回场景中的活动副本（可自由修改 transform/材质），模型模板按 (meshId, textureId) 缓存复用；
        /// 同 meshId 不同 textureId → 独立材质/模板（模型复用、材质隔离）。
        /// </summary>
        public static GameObject? CreateItemGraphic(Identifier meshId, Identifier? textureId = null)
        {
            GameObject? template = TryGetOrBuildTemplate(meshId, textureId);
            if (template == null)
                return null;
            return UnityEngine.Object.Instantiate(template, null);
        }

        /// <summary>
        /// 【推荐】异步构建 ItemGraphic GameObject（模型 IO + 解析在线程池）。
        /// 语义同 <see cref="CreateItemGraphic"/>。
        /// </summary>
        public static async UniTask<GameObject?> CreateItemGraphicAsync(Identifier meshId, Identifier? textureId = null)
        {
            GameObject? template = await GetOrBuildTemplateAsync(meshId, textureId);
            if (template == null)
                return null;
            return UnityEngine.Object.Instantiate(template, null);
        }

        // ===== 构建 + 绑定到 Item =====

        /// <summary>
        /// 构建 ItemGraphic 并绑定到物品（item.itemGraphic 走 Publicizer 公开字段直接赋值）。
        /// 绑定后原版 CreateAGraphic 掉落/装备/手持链路自动显示 3D 模型，不再走 Sprite 兜底。
        /// </summary>
        public static void SetItemGraphic(Item item, Identifier meshId, Identifier? textureId = null)
        {
            if (item == null)
            {
                Debug.LogError("[ItemGraphicUtils] SetItemGraphic: item is null.");
                return;
            }
            GameObject? template = TryGetOrBuildTemplate(meshId, textureId);
            if (template == null)
                return;
            item.itemGraphic = template.GetComponent<ItemGraphicInfo>();
        }

        /// <summary>
        /// 【推荐】异步构建 ItemGraphic 并绑定到物品。语义同 <see cref="SetItemGraphic"/>。
        /// </summary>
        public static async UniTask SetItemGraphicAsync(Item item, Identifier meshId, Identifier? textureId = null)
        {
            if (item == null)
            {
                Debug.LogError("[ItemGraphicUtils] SetItemGraphicAsync: item is null.");
                return;
            }
            GameObject? template = await GetOrBuildTemplateAsync(meshId, textureId);
            if (template == null)
                return;
            item.itemGraphic = template.GetComponent<ItemGraphicInfo>();
        }

        // ===== 复用原版物品模型 =====

        /// <summary>
        /// 复用指定原版物品的 ItemGraphic（共享引用，纯赋值无 IO，仅同步版本）。
        /// originalItemId 为原版物品 Identifier（如 <c>Identifier("duckov", "AK-47")</c>，
        /// 可用 <c>GameItemLookup.TryGetIdentifier(displayName, out id)</c> 发现），
        /// 也兼容已注册的其它 mod 物品。复用后原版 sockets / ShowIf / HideIf / 材质全量生效。
        /// </summary>
        public static void SetItemGraphicFromOriginal(Item item, Identifier originalItemId)
        {
            if (item == null)
            {
                Debug.LogError("[ItemGraphicUtils] SetItemGraphicFromOriginal: item is null.");
                return;
            }
            if (!ItemUtils.TryResolveTypeId(originalItemId, out int typeId))
            {
                Debug.LogWarning($"[ItemGraphicUtils] Original item not found: {originalItemId}");
                return;
            }
            Item prefab = ItemAssetsCollection.GetPrefab(typeId);
            if (prefab == null || prefab.ItemGraphic == null)
            {
                Debug.LogWarning($"[ItemGraphicUtils] Original item '{originalItemId}' has no 3D item graphic (sprite-only item).");
                return;
            }
            item.itemGraphic = prefab.ItemGraphic;
        }

        // ===== 缓存与卸载 =====

        /// <summary>释放指定 (meshId, textureId) 的 GO 模板。建议先释放模板再释放 Mesh（见 ModelUtils.ReleaseModel）。</summary>
        public static void ReleaseItemGraphic(Identifier meshId, Identifier? textureId = null)
        {
            var key = new ItemGraphicKey(meshId, MakeTextureKey(textureId));
            lock (_templateLock)
            {
                if (_templateCache.Remove(key, out GameObject? template) && template != null)
                    UnityEngine.Object.Destroy(template);
            }
        }

        /// <summary>
        /// 批量释放指定 mod 的全部 GO 模板（按 meshId.Domain 过滤）。
        /// modid 未指定时走 <see cref="RegistryManager.CurrentModid"/>。
        /// </summary>
        public static void ReleaseAllItemGraphics(string? modid = null)
        {
            string domain = modid ?? RegistryManager.CurrentModid;
            lock (_templateLock)
            {
                var keys = new List<ItemGraphicKey>();
                foreach (var kvp in _templateCache)
                {
                    if (kvp.Key.MeshId.Domain == domain)
                        keys.Add(kvp.Key);
                }
                foreach (ItemGraphicKey key in keys)
                {
                    if (_templateCache.Remove(key, out GameObject? template) && template != null)
                        UnityEngine.Object.Destroy(template);
                }
            }
        }

        // ===== 内部：模板构建 =====

        private static ItemGraphicKey MakeKey(Identifier meshId, Identifier? textureId)
        {
            return new ItemGraphicKey(meshId, MakeTextureKey(textureId));
        }

        private static string MakeTextureKey(Identifier? textureId)
        {
            return textureId?.ToString() ?? string.Empty;
        }

        private static GameObject? TryGetOrBuildTemplate(Identifier meshId, Identifier? textureId)
        {
            ItemGraphicKey key = MakeKey(meshId, textureId);
            lock (_templateLock)
            {
                if (_templateCache.TryGetValue(key, out GameObject? cached) && cached != null)
                    return cached;
            }
            Mesh? mesh = ModelUtils.LoadMesh(meshId);
            if (mesh == null)
                return null;
            Material? material = ModelUtils.GetModelMaterial(textureId);
            GameObject template = BuildTemplate(meshId, mesh, material);
            lock (_templateLock) _templateCache[key] = template;
            return template;
        }

        private static async UniTask<GameObject?> GetOrBuildTemplateAsync(Identifier meshId, Identifier? textureId)
        {
            ItemGraphicKey key = MakeKey(meshId, textureId);
            lock (_templateLock)
            {
                if (_templateCache.TryGetValue(key, out GameObject? cached) && cached != null)
                    return cached;
            }
            Mesh? mesh = await ModelUtils.LoadMeshAsync(meshId);
            if (mesh == null)
                return null;
            Material? material = await ModelUtils.GetModelMaterialAsync(textureId);
            GameObject template = BuildTemplate(meshId, mesh, material);
            lock (_templateLock) _templateCache[key] = template;
            return template;
        }

        private static GameObject BuildTemplate(Identifier meshId, Mesh mesh, Material? material)
        {
            GameObject root = new GameObject("ItemGraphic_" + meshId.Path);
            ItemGraphicInfo graphic = root.AddComponent<ItemGraphicInfo>();
            CharacterSubVisuals subVisuals = root.AddComponent<CharacterSubVisuals>();

            // 主模型：MeshFilter + MeshRenderer 成对（纯视觉，无碰撞体）
            GameObject model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            MeshFilter filter = model.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = model.AddComponent<MeshRenderer>();
            if (material != null)
                renderer.sharedMaterial = material;

            // 地面锚点：SnapGroundPointToParent 对齐用（CreateAGraphic(snapGround:true) 依赖）
            GameObject groundPoint = new GameObject("GroundPoint");
            groundPoint.transform.SetParent(root.transform, false);

            // 收集渲染器：GetComponentsInChildren 恰好收集到唯一的主模型 MeshRenderer →
            // renderers.Count == 1（不用 AddRenderer——其内部依赖角色注册）
            subVisuals.SetRenderers();

            // sockets 防御：RefreshSubGraphics 遍历 sockets 列表，null 会 NRE
            if (graphic.sockets == null)
                graphic.sockets = new List<ItemGraphicInfo.ItemGraphicSocket>();

            // 挂到 inactive 容器（模板不渲染，Instantiate 副本正常激活）
            root.transform.SetParent(Holder.transform, false);
            return root;
        }
    }
}
