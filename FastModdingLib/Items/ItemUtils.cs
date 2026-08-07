using Cysharp.Threading.Tasks;
using Duckov.ItemBuilders;
using Duckov.Utilities;

using FeatherMod.Items;
using FeatherMod.Register;
using FeatherMod.Utils;
using ItemStatsSystem;
using ItemStatsSystem.Items;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

using Unity.VisualScripting;

using UnityEngine;

namespace FeatherMod
{
    public static class ItemUtils
    {
        public static void RegisterTag(Identifier id, TagBuilder tag)
        {
            if (RegistryManager.Instance.TagRegistry.TryGet(id, out _))
            {
                throw new ArgumentException($"{id} already exists.");
            }

            var tagv = tag.Name(id.ToString()).Instantiate();
            RegistryManager.Instance.TagRegistry.Register(id.ToString(), id, tagv, id.Domain);
            GameplayDataSettings.Tags.allTags.Add(tagv);
        }

        private static void createUsage(Item item, ItemData config)
        {
            if (config.usages == null)
                return;

            item.AddUsageUtilitiesComponent();
            UsageUtilities usageUtilities = item.UsageUtilities;

            usageUtilities.useTime = config.usages.useTime;

            item.usageUtilities = usageUtilities;

            if (config.usages.useSound != string.Empty)
            {
                usageUtilities.hasSound = true;
                usageUtilities.useSound = config.usages.useSound;
            }
            if (config.usages.actionSound != string.Empty)
            {
                usageUtilities.hasSound = true;
                usageUtilities.actionSound = config.usages.actionSound;
            }
            if (config.usages.useDurability && config.maxDurability > 0)
            {
                usageUtilities.useDurability = true;
                usageUtilities.durabilityUsage = config.usages.durabilityUsage;
            }

            foreach (var behavior in config.usages.behaviors)
            {
                createBehavior(item, behavior, usageUtilities);
            }
        }

        public static void createBehavior(Item item, UsageBehaviorData behaviorData, UsageUtilities usageUtilities)
        {
            if (behaviorData == null)
                return;

            usageUtilities.behaviors.Add(behaviorData.GetBehavior(item));
        }

        // ===== Sprite 加载（通用：物品图标 / Perk 图标 / 建筑图标等） =====

        /// <summary>
        /// 从调用方 mod 目录 <c>assets/textures/</c> 加载 Sprite（适用物品图标、Perk 图标等）。
        /// modid 从调用方程序集名自动推导。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Sprite? LoadSprite(string resourceName)
        {
            var callingAssembly = System.Reflection.Assembly.GetCallingAssembly();
            var id = new Identifier(callingAssembly.GetName().Name, resourceName);
            return LoadSprite(id);
        }

        /// <summary>
        /// 从调用方 mod 目录 <c>assets/textures/</c> 加载 Sprite（适用物品图标、Perk 图标等）。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Sprite? LoadSprite(Identifier id)
        {
            var modDir = ModPathResolver.ResolveDirectory(id.Domain);
            return LoadSpriteFromDir(modDir!, id.Path);
        }

        /// <summary>从指定目录加载 Sprite。适用物品图标、Perk 图标等所有 Sprite 场景。</summary>
        public static Sprite? LoadSpriteFromDir(string modDirectory, string resourceName)
        {
            try
            {
                StringBuilder assetLoc = new StringBuilder($"assets/textures/");
                assetLoc.Append(resourceName);
                string fileLoc = Path.Combine(modDirectory, assetLoc.ToString());
                if (File.Exists(fileLoc) == false)
                {
                    Debug.LogError("Sprite is missing: " + fileLoc);
                    return null;
                }

                byte[] ImageArray = File.ReadAllBytes(fileLoc);
                Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
                if (!texture2D.LoadImage(ImageArray))
                {
                    Debug.LogError($"Invaild sprite image, Resource:{resourceName}");
                    UnityEngine.Object.Destroy(texture2D);
                    return null;
                }
                texture2D.filterMode = FilterMode.Bilinear;
                texture2D.Apply();
                Sprite sprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), 100f);
                return sprite;
            }
            catch (Exception arg)
            {
                Debug.LogError($"Except on loading sprite: {arg}");
                return null;
            }
        }

        // ===== 异步 Sprite 加载（推荐：加载阶段减少 IO 阻塞） =====

        /// <summary>
        /// 【推荐】异步加载 Sprite。modid 从调用方程序集名自动推导。
        /// 文件 IO 在线程池执行，Texture2D 创建在主线程。用于加载阶段加速。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async UniTask<Sprite?> LoadSpriteAsync(string resourceName)
        {
            var callingAssembly = System.Reflection.Assembly.GetCallingAssembly();
            var id = new Identifier(callingAssembly.GetName().Name, resourceName);
            return await LoadSpriteAsync(id);
        }

        /// <summary>
        /// 【推荐】异步加载 Sprite。文件 IO 在线程池执行，Texture2D 创建在主线程。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async UniTask<Sprite?> LoadSpriteAsync(Identifier id)
        {
            var modDir = ModPathResolver.ResolveDirectory(id.Domain);
            return await LoadSpriteFromDirAsync(modDir!, id.Path);
        }

        /// <summary>
        /// 【推荐】从指定目录异步加载 Sprite。
        /// 文件 IO 通过 <c>UniTask.RunOnThreadPool</c> 在线程池执行，避免阻塞主线程；
        /// Texture2D.LoadImage / Apply / Sprite.Create 回到主线程完成。
        /// </summary>
        public static async UniTask<Sprite?> LoadSpriteFromDirAsync(string modDirectory, string resourceName)
        {
            try
            {
                StringBuilder assetLoc = new StringBuilder($"assets/textures/");
                assetLoc.Append(resourceName);
                string fileLoc = Path.Combine(modDirectory, assetLoc.ToString());
                if (File.Exists(fileLoc) == false)
                {
                    Debug.LogError("Sprite is missing: " + fileLoc);
                    return null;
                }

                // 文件 IO 在线程池执行 —— 加载阶段多个 Sprite 可并行读取
                byte[] imageData = await UniTask.RunOnThreadPool(() => File.ReadAllBytes(fileLoc));

                // 回到主线程 —— Texture2D / Sprite API 必须在主线程
                Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
                if (!texture2D.LoadImage(imageData))
                {
                    Debug.LogError($"Invalid sprite image, Resource:{resourceName}");
                    UnityEngine.Object.Destroy(texture2D);
                    return null;
                }
                texture2D.filterMode = FilterMode.Bilinear;
                texture2D.Apply();
                Sprite sprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), 100f);
                return sprite;
            }
            catch (Exception arg)
            {
                Debug.LogError($"Except on loading sprite: {arg}");
                return null;
            }
        }

        // ===== 物品构造 =====

        /// <summary>
        /// 将 <see cref="ItemData.slots"/> 应用到 ItemBuilder。
        /// 空表 / null → 无槽位物品。Tag 解析规则：必须已存在（游戏原生或 <see cref="TagUtils.RegisterTag"/> 注册），
        /// 不存在的 Tag 舍弃并告警（槽位本身保留）。
        /// </summary>
        private static void ApplySlots(ItemBuilder itemBuilder, ItemData config)
        {
            if (config.slots == null || config.slots.Count == 0)
                return;

            void Resolve(List<string> names, List<Tag> target, string slotKey)
            {
                foreach (string tagName in names)
                {
                    Tag? tag = TagUtils.GetTag(tagName);
                    if (tag != null)
                    {
                        target.Add(tag);
                    }
                    else
                    {
                        Debug.LogWarning($"[FML] 槽位 '{slotKey}' 引用的 Tag '{tagName}' 不存在，已舍弃（槽位保留）。请先用 TagUtils.RegisterTag 注册。");
                    }
                }
            }

            foreach (SlotData slot in config.slots)
            {
                if (string.IsNullOrWhiteSpace(slot.key))
                {
                    Debug.LogWarning($"[FML] ItemData '{config.localizationKey}' 存在空 key 的槽位配置，已跳过。");
                    continue;
                }

                List<Tag> requireTags = new List<Tag>();
                List<Tag> excludeTags = new List<Tag>();
                Resolve(slot.requireTags, requireTags, slot.key);
                Resolve(slot.excludeTags, excludeTags, slot.key);
                itemBuilder.Slot(slot.key, requireTags, excludeTags);
            }
        }

        /// <summary>
        /// 将 <see cref="SlotData.spritePath"/> 应用到已实例化的槽位（游戏 ItemBuilder.Slot 不接受图标，须在 Instantiate 后赋值）。
        /// 必须在 <see cref="ApplySlots"/> 之后、物品实例化后调用。spritePath 为空的槽位跳过（UI 显示默认槽位图标）。
        /// </summary>
        private static void ApplySlotIcons(Item item, ItemData config, string? modDir)
        {
            if (config.slots == null || config.slots.Count == 0 || item.Slots == null)
                return;

            foreach (SlotData slot in config.slots)
            {
                if (string.IsNullOrWhiteSpace(slot.spritePath))
                    continue;

                Slot? gameSlot = item.Slots.GetSlot(slot.key);
                if (gameSlot == null)
                {
                    Debug.LogWarning($"[FML] 槽位 '{slot.key}' 未创建，无法设置图标（spritePath='{slot.spritePath}'）。");
                    continue;
                }
                gameSlot.SlotIcon = LoadSpriteFromDir(modDir!, slot.spritePath);
            }
        }

        /// <summary><see cref="ApplySlotIcons"/> 的异步版本，Sprite 加载使用异步 IO。</summary>
        private static async UniTask ApplySlotIconsAsync(Item item, ItemData config, string? modDir)
        {
            if (config.slots == null || config.slots.Count == 0 || item.Slots == null)
                return;

            foreach (SlotData slot in config.slots)
            {
                if (string.IsNullOrWhiteSpace(slot.spritePath))
                    continue;

                Slot? gameSlot = item.Slots.GetSlot(slot.key);
                if (gameSlot == null)
                {
                    Debug.LogWarning($"[FML] 槽位 '{slot.key}' 未创建，无法设置图标（spritePath='{slot.spritePath}'）。");
                    continue;
                }
                gameSlot.SlotIcon = await LoadSpriteFromDirAsync(modDir!, slot.spritePath);
            }
        }

        /// <summary>
        /// 创建自定义 Item 实例（不注册到 Registry）。modid 从调用方程序集名自动推导。
        /// 要求调用方已通过 <see cref="ModPathResolver.Register"/> 注册路径。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Item GetCustomItem(ItemData config)
        {
            var callingAssembly = System.Reflection.Assembly.GetCallingAssembly();
            string modid = callingAssembly.GetName().Name;
            return GetCustomItem(new Identifier(modid, config.localizationKey), config);
        }

        /// <summary>
        /// 创建自定义 Item 实例（不注册到 Registry）。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Item GetCustomItem(Identifier id, ItemData config)
        {
            var modDir = ModPathResolver.ResolveDirectory(id.Domain);
            ItemBuilder itemBuilder = ItemBuilder.New()
                .TypeID(config.itemId)
                .EnableStacking(config.maxStackCount, 1)
                .Icon(ItemUtils.LoadSpriteFromDir(modDir!, config.spritePath));

            foreach (var keyValuePair in config.slots)
            {
                List<Tag> exists = new();
                foreach (var targetSpec in keyValuePair.Value.requiredTags)
                {
                    string? v = TagLookup.GetNativeMayNotExist(targetSpec);
                    if (v == null) throw new IndexOutOfRangeException($"Key {targetSpec} has not yet been registered.");
                    var tg = GetTargetTag(v);
                    if (tg == null) throw new IndexOutOfRangeException($"Key {targetSpec} does not exists.");
                    exists.Add(tg);
                }

                List<Tag> excludes = new();
                if (keyValuePair.Value.excludeTags != null)
                {
                    foreach (var targetSpec in keyValuePair.Value.excludeTags)
                    {
                        string? v = TagLookup.GetNativeMayNotExist(targetSpec);
                        if (v == null) throw new IndexOutOfRangeException($"Key {targetSpec} has not yet been registered.");
                        var tg = GetTargetTag(v);
                        if (tg == null) throw new IndexOutOfRangeException($"Key {targetSpec} does not exists.");
                        excludes.Add(tg);
                    }
                }

                itemBuilder.Slot(keyValuePair.Key, exists, excludes);
            }

            foreach (var keyValuePair in config.consts)
            {
                switch (keyValuePair.Value.Item1)
                {
                    case float f: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, f, keyValuePair.Value.Item2); break;
                    case int i: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, i, keyValuePair.Value.Item2); break;
                    case bool b: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, b, keyValuePair.Value.Item2); break;
                    case string s: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, s, keyValuePair.Value.Item2); break;
                    default: throw new NotSupportedException();
                }
            }

            foreach (var keyValuePair in config.variables)
            {
                switch (keyValuePair.Value.Item1)
                {
                    case float f: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, f, keyValuePair.Value.Item2); break;
                    case int i: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, i, keyValuePair.Value.Item2); break;
                    case bool b: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, b, keyValuePair.Value.Item2); break;
                    case string s: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, s, keyValuePair.Value.Item2); break;
                    default: throw new NotSupportedException();
                }
            }

            config.modifiers.ForEach(modifier =>
            {
                itemBuilder.Modifier(modifier.getModifier());
            });
            ApplySlots(itemBuilder, config);

            Item component = itemBuilder
                .Instantiate();
            ApplySlotIcons(component, config, modDir);

            UnityEngine.Object.DontDestroyOnLoad(component);
            SetItemProperties(component, config);

            return component;
        }

        /// <summary>
        /// 【推荐】异步创建自定义 Item 实例（不注册到 Registry）。Sprite 加载使用异步 IO。
        /// modid 从调用方程序集名自动推导。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async UniTask<Item> GetCustomItemAsync(ItemData config)
        {
            var callingAssembly = System.Reflection.Assembly.GetCallingAssembly();
            string modid = callingAssembly.GetName().Name;
            return await GetCustomItemAsync(new Identifier(modid, config.localizationKey), config);
        }

        /// <summary>
        /// 【推荐】异步创建自定义 Item 实例（不注册到 Registry）。Sprite 加载使用异步 IO。
        /// 加载阶段使用可显著减少 IO 阻塞时间。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async UniTask<Item> GetCustomItemAsync(Identifier id, ItemData config)
        {
            var modDir = ModPathResolver.ResolveDirectory(id.Domain);
            ItemBuilder itemBuilder = ItemBuilder.New()
                .TypeID(config.itemId)
                .EnableStacking(config.maxStackCount, 1)
                .Icon(await LoadSpriteFromDirAsync(modDir!, config.spritePath));

            foreach (var keyValuePair in config.slots)
            {
                List<Tag> exists = new();
                foreach (var targetSpec in keyValuePair.Value.requiredTags)
                {
                    string? v = TagLookup.GetNativeMayNotExist(targetSpec);
                    if (v == null) throw new IndexOutOfRangeException($"Key {targetSpec} has not yet been registered.");
                    var tg = GetTargetTag(v);
                    if (tg == null) throw new IndexOutOfRangeException($"Key {targetSpec} does not exists.");
                    exists.Add(tg);
                }

                List<Tag> excludes = new();
                if (keyValuePair.Value.excludeTags != null)
                {
                    foreach (var targetSpec in keyValuePair.Value.excludeTags)
                    {
                        string? v = TagLookup.GetNativeMayNotExist(targetSpec);
                        if (v == null) throw new IndexOutOfRangeException($"Key {targetSpec} has not yet been registered.");
                        var tg = GetTargetTag(v);
                        if (tg == null) throw new IndexOutOfRangeException($"Key {targetSpec} does not exists.");
                        excludes.Add(tg);
                    }
                }

                itemBuilder.Slot(keyValuePair.Key, exists, excludes);
            }

            foreach (var keyValuePair in config.consts)
            {
                switch (keyValuePair.Value.Item1)
                {
                    case float f: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, f, keyValuePair.Value.Item2); break;
                    case int i: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, i, keyValuePair.Value.Item2); break;
                    case bool b: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, b, keyValuePair.Value.Item2); break;
                    case string s: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, s, keyValuePair.Value.Item2); break;
                    default: throw new NotSupportedException();
                }
            }

            foreach (var keyValuePair in config.variables)
            {
                switch (keyValuePair.Value.Item1)
                {
                    case float f: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, f, keyValuePair.Value.Item2); break;
                    case int i: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, i, keyValuePair.Value.Item2); break;
                    case bool b: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, b, keyValuePair.Value.Item2); break;
                    case string s: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, s, keyValuePair.Value.Item2); break;
                    default: throw new NotSupportedException();
                }
            }

            config.modifiers.ForEach(modifier =>
            {
                itemBuilder.Modifier(modifier.getModifier());
            });
            ApplySlots(itemBuilder, config);

            Item component = itemBuilder
                .Instantiate();
            await ApplySlotIconsAsync(component, config, modDir);

            UnityEngine.Object.DontDestroyOnLoad(component);
            SetItemProperties(component, config);

            return component;
        }

        /// <summary>
        /// 【推荐】异步创建并注册自定义物品。Sprite 加载使用异步 IO。
        /// modid 从 <see cref="Identifier.Domain"/> 推导。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async UniTask CreateCustomItemAsync(Identifier id, ItemData config)
        {
            // 在 await 前预定 TypeID，防止被低优先级同步加载抢占。
            // 若首选 ID 冲突则自动分配空闲值。
            int actualTypeId = ReserveTypeId(id, config.itemId);

            try
            {
                var modDir = ModPathResolver.ResolveDirectory(id.Domain);
                ItemBuilder itemBuilder = ItemBuilder.New()
                    .TypeID(actualTypeId)
                    .EnableStacking(config.maxStackCount, 1)
                    .Icon(await LoadSpriteFromDirAsync(modDir!, config.spritePath));

                foreach (var keyValuePair in config.slots)
                {
                    List<Tag> exists = new();
                    foreach (var targetSpec in keyValuePair.Value.requiredTags)
                    {
                        string? v = TagLookup.GetNativeMayNotExist(targetSpec);
                        if (v == null) throw new IndexOutOfRangeException($"Key {targetSpec} has not yet been registered.");
                        var tg = GetTargetTag(v);
                        if (tg == null) throw new IndexOutOfRangeException($"Key {targetSpec} does not exists.");
                        exists.Add(tg);
                    }

                    List<Tag> excludes = new();
                    if (keyValuePair.Value.excludeTags != null)
                    {
                        foreach (var targetSpec in keyValuePair.Value.excludeTags)
                        {
                            string? v = TagLookup.GetNativeMayNotExist(targetSpec);
                            if (v == null) throw new IndexOutOfRangeException($"Key {targetSpec} has not yet been registered.");
                            var tg = GetTargetTag(v);
                            if (tg == null) throw new IndexOutOfRangeException($"Key {targetSpec} does not exists.");
                            excludes.Add(tg);
                        }
                    }

                    itemBuilder.Slot(keyValuePair.Key, exists, excludes);
                }

                foreach (var keyValuePair in config.consts)
                {
                    switch (keyValuePair.Value.Item1)
                    {
                        case float f: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, f, keyValuePair.Value.Item2); break;
                        case int i: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, i, keyValuePair.Value.Item2); break;
                        case bool b: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, b, keyValuePair.Value.Item2); break;
                        case string s: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, s, keyValuePair.Value.Item2); break;
                        default: throw new NotSupportedException();
                    }
                }

                foreach (var keyValuePair in config.variables)
                {
                    switch (keyValuePair.Value.Item1)
                    {
                        case float f: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, f, keyValuePair.Value.Item2); break;
                        case int i: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, i, keyValuePair.Value.Item2); break;
                        case bool b: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, b, keyValuePair.Value.Item2); break;
                        case string s: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, s, keyValuePair.Value.Item2); break;
                        default: throw new NotSupportedException();
                    }
                }

                config.modifiers.ForEach(modifier =>
                {
                    itemBuilder.Modifier(modifier.getModifier());
                });
                ApplySlots(itemBuilder, config);

                Item component = itemBuilder
                    .Instantiate();
                await ApplySlotIconsAsync(component, config, modDir);

                UnityEngine.Object.DontDestroyOnLoad(component);
                SetItemProperties(component, config);

                RegisterItem(id, component);
            }
            finally
            {
                // 无论成功失败，确保预定被清理。RegisterItem 成功时内部已 ConfirmReservation，
                // 此处再清一次幂等无害；失败时（如 Sprite 加载异常）释放预定防止 TypeID 泄漏。
                CancelReservation(id);
            }
        }

        /// <summary>
        /// 创建并注册自定义物品。modid 从 <see cref="Identifier.Domain"/> 推导，
        /// mod 目录从 <see cref="ModPathResolver"/> 自动探测。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void CreateCustomItem(Identifier id, ItemData config)
        {
            var modDir = ModPathResolver.ResolveDirectory(id.Domain);
            ItemBuilder itemBuilder = ItemBuilder.New()
                .TypeID(config.itemId)
                .EnableStacking(config.maxStackCount, 1)
                .Icon(ItemUtils.LoadSpriteFromDir(modDir!, config.spritePath));

            foreach (var keyValuePair in config.slots)
            {
                List<Tag> exists = new();
                foreach (var targetSpec in keyValuePair.Value.requiredTags)
                {
                    string? v = TagLookup.GetNativeMayNotExist(targetSpec);
                    if (v == null) throw new IndexOutOfRangeException($"Key {targetSpec} has not yet been registered.");
                    var tg = GetTargetTag(v);
                    if (tg == null) throw new IndexOutOfRangeException($"Key {targetSpec} does not exists.");
                    exists.Add(tg);
                }

                List<Tag> excludes = new();
                if (keyValuePair.Value.excludeTags != null)
                {
                    foreach (var targetSpec in keyValuePair.Value.excludeTags)
                    {
                        string? v = TagLookup.GetNativeMayNotExist(targetSpec);
                        if (v == null) throw new IndexOutOfRangeException($"Key {targetSpec} has not yet been registered.");
                        var tg = GetTargetTag(v);
                        if (tg == null) throw new IndexOutOfRangeException($"Key {targetSpec} does not exists.");
                        excludes.Add(tg);
                    }
                }

                itemBuilder.Slot(keyValuePair.Key, exists, excludes);
            }

            foreach (var keyValuePair in config.consts)
            {
                switch (keyValuePair.Value.Item1)
                {
                    case float f: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, f, keyValuePair.Value.Item2); break;
                    case int i: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, i, keyValuePair.Value.Item2); break;
                    case bool b: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, b, keyValuePair.Value.Item2); break;
                    case string s: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, s, keyValuePair.Value.Item2); break;
                    default: throw new NotSupportedException();
                }
            }

            foreach (var keyValuePair in config.variables)
            {
                switch (keyValuePair.Value.Item1)
                {
                    case float f: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, f, keyValuePair.Value.Item2); break;
                    case int i: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, i, keyValuePair.Value.Item2); break;
                    case bool b: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, b, keyValuePair.Value.Item2); break;
                    case string s: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, s, keyValuePair.Value.Item2); break;
                    default: throw new NotSupportedException();
                }
            }

            config.modifiers.ForEach(modifier =>
            {
                itemBuilder.Modifier(modifier.getModifier());
            });
            ApplySlots(itemBuilder, config);

            Item component = itemBuilder
                .Instantiate();
            ApplySlotIcons(component, config, modDir);

            UnityEngine.Object.DontDestroyOnLoad(component);
            SetItemProperties(component, config);
            RegisterItem(id, component);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void CreateCustomCartridge(Identifier id, Identifier gameId, ItemData config)
        {
            var modDir = ModPathResolver.ResolveDirectory(id.Domain);
            ItemBuilder itemBuilder = ItemBuilder.New()
                .TypeID(config.itemId)
                .EnableStacking(config.maxStackCount, 1)
                .Icon(ItemUtils.LoadSpriteFromDir(modDir!, config.spritePath))
                .SetConstant("GameID", gameId.ToString());

            foreach (var keyValuePair in config.slots)
            {
                List<Tag> exists = new();
                foreach (var targetSpec in keyValuePair.Value.requiredTags)
                {
                    string? v = TagLookup.GetNativeMayNotExist(targetSpec);
                    if (v == null) throw new IndexOutOfRangeException($"Key {targetSpec} has not yet been registered.");
                    var tg = GetTargetTag(v);
                    if (tg == null) throw new IndexOutOfRangeException($"Key {targetSpec} does not exists.");
                    exists.Add(tg);
                }

                List<Tag> excludes = new();
                if (keyValuePair.Value.excludeTags != null)
                {
                    foreach (var targetSpec in keyValuePair.Value.excludeTags)
                    {
                        string? v = TagLookup.GetNativeMayNotExist(targetSpec);
                        if (v == null) throw new IndexOutOfRangeException($"Key {targetSpec} has not yet been registered.");
                        var tg = GetTargetTag(v);
                        if (tg == null) throw new IndexOutOfRangeException($"Key {targetSpec} does not exists.");
                        excludes.Add(tg);
                    }
                }

                itemBuilder.Slot(keyValuePair.Key, exists, excludes);
            }

            foreach (var keyValuePair in config.consts)
            {
                switch (keyValuePair.Value.Item1)
                {
                    case float f: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, f, keyValuePair.Value.Item2); break;
                    case int i: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, i, keyValuePair.Value.Item2); break;
                    case bool b: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, b, keyValuePair.Value.Item2); break;
                    case string s: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, s, keyValuePair.Value.Item2); break;
                    default: throw new NotSupportedException();
                }
            }

            foreach (var keyValuePair in config.variables)
            {
                switch (keyValuePair.Value.Item1)
                {
                    case float f: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, f, keyValuePair.Value.Item2); break;
                    case int i: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, i, keyValuePair.Value.Item2); break;
                    case bool b: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, b, keyValuePair.Value.Item2); break;
                    case string s: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, s, keyValuePair.Value.Item2); break;
                    default: throw new NotSupportedException();
                }
            }

            config.modifiers.ForEach(modifier =>
            {
                itemBuilder.Modifier(modifier.getModifier());
            });
            ApplySlots(itemBuilder, config);

            Item component = itemBuilder
                .Instantiate();
            ApplySlotIcons(component, config, modDir);

            UnityEngine.Object.DontDestroyOnLoad(component);
            SetItemProperties(component, config);
            RegisterItem(id, component);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async UniTask CreateCustomCartridgeAsync(Identifier id, Identifier gameId, ItemData config)
        {
            // 在 await 前预定 TypeID，防止被低优先级同步加载抢占。
            // 若首选 ID 冲突则自动分配空闲值。
            int actualTypeId = ReserveTypeId(id, config.itemId);

            try
            {
                var modDir = ModPathResolver.ResolveDirectory(id.Domain);
                ItemBuilder itemBuilder = ItemBuilder.New()
                    .TypeID(actualTypeId)
                    .EnableStacking(config.maxStackCount, 1)
                    .Icon(await LoadSpriteFromDirAsync(modDir!, config.spritePath))
                    .SetConstant("GameID", gameId.ToString());

                foreach (var keyValuePair in config.slots)
                {
                    List<Tag> exists = new();
                    foreach (var targetSpec in keyValuePair.Value.requiredTags)
                    {
                        string? v = TagLookup.GetNativeMayNotExist(targetSpec);
                        if (v == null) throw new IndexOutOfRangeException($"Key {targetSpec} has not yet been registered.");
                        var tg = GetTargetTag(v);
                        if (tg == null) throw new IndexOutOfRangeException($"Key {targetSpec} does not exists.");
                        exists.Add(tg);
                    }

                    List<Tag> excludes = new();
                    if (keyValuePair.Value.excludeTags != null)
                    {
                        foreach (var targetSpec in keyValuePair.Value.excludeTags)
                        {
                            string? v = TagLookup.GetNativeMayNotExist(targetSpec);
                            if (v == null) throw new IndexOutOfRangeException($"Key {targetSpec} has not yet been registered.");
                            var tg = GetTargetTag(v);
                            if (tg == null) throw new IndexOutOfRangeException($"Key {targetSpec} does not exists.");
                            excludes.Add(tg);
                        }
                    }

                    itemBuilder.Slot(keyValuePair.Key, exists, excludes);
                }

                foreach (var keyValuePair in config.consts)
                {
                    switch (keyValuePair.Value.Item1)
                    {
                        case float f: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, f, keyValuePair.Value.Item2); break;
                        case int i: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, i, keyValuePair.Value.Item2); break;
                        case bool b: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, b, keyValuePair.Value.Item2); break;
                        case string s: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, s, keyValuePair.Value.Item2); break;
                        default: throw new NotSupportedException();
                    }
                }

                foreach (var keyValuePair in config.variables)
                {
                    switch (keyValuePair.Value.Item1)
                    {
                        case float f: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, f, keyValuePair.Value.Item2); break;
                        case int i: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, i, keyValuePair.Value.Item2); break;
                        case bool b: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, b, keyValuePair.Value.Item2); break;
                        case string s: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, s, keyValuePair.Value.Item2); break;
                        default: throw new NotSupportedException();
                    }
                }

                config.modifiers.ForEach(modifier =>
                {
                    itemBuilder.Modifier(modifier.getModifier());
                });
                ApplySlots(itemBuilder, config);

                Item component = itemBuilder
                    .Instantiate();
                await ApplySlotIconsAsync(component, config, modDir);

                UnityEngine.Object.DontDestroyOnLoad(component);
                SetItemProperties(component, config);

                RegisterItem(id, component);
            }
            finally
            {
                // 无论成功失败，确保预定被清理。RegisterItem 成功时内部已 ConfirmReservation，
                // 此处再清一次幂等无害；失败时（如 Sprite 加载异常）释放预定防止 TypeID 泄漏。
                CancelReservation(id);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Item GetCustomCartridge(Identifier id, Identifier gameId, ItemData config)
        {
            var modDir = ModPathResolver.ResolveDirectory(id.Domain);
            ItemBuilder itemBuilder = ItemBuilder.New()
                .TypeID(config.itemId)
                .EnableStacking(config.maxStackCount, 1)
                .Icon(ItemUtils.LoadSpriteFromDir(modDir!, config.spritePath))
                .SetConstant("GameID", gameId.ToString());

            foreach (var keyValuePair in config.slots)
            {
                List<Tag> exists = new();
                foreach (var targetSpec in keyValuePair.Value.requiredTags)
                {
                    string? v = TagLookup.GetNativeMayNotExist(targetSpec);
                    if (v == null) throw new IndexOutOfRangeException($"Key {targetSpec} has not yet been registered.");
                    var tg = GetTargetTag(v);
                    if (tg == null) throw new IndexOutOfRangeException($"Key {targetSpec} does not exists.");
                    exists.Add(tg);
                }

                List<Tag> excludes = new();
                if (keyValuePair.Value.excludeTags != null)
                {
                    foreach (var targetSpec in keyValuePair.Value.excludeTags)
                    {
                        string? v = TagLookup.GetNativeMayNotExist(targetSpec);
                        if (v == null) throw new IndexOutOfRangeException($"Key {targetSpec} has not yet been registered.");
                        var tg = GetTargetTag(v);
                        if (tg == null) throw new IndexOutOfRangeException($"Key {targetSpec} does not exists.");
                        excludes.Add(tg);
                    }
                }

                itemBuilder.Slot(keyValuePair.Key, exists, excludes);
            }

            foreach (var keyValuePair in config.consts)
            {
                switch (keyValuePair.Value.Item1)
                {
                    case float f: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, f, keyValuePair.Value.Item2); break;
                    case int i: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, i, keyValuePair.Value.Item2); break;
                    case bool b: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, b, keyValuePair.Value.Item2); break;
                    case string s: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, s, keyValuePair.Value.Item2); break;
                    default: throw new NotSupportedException();
                }
            }

            foreach (var keyValuePair in config.variables)
            {
                switch (keyValuePair.Value.Item1)
                {
                    case float f: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, f, keyValuePair.Value.Item2); break;
                    case int i: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, i, keyValuePair.Value.Item2); break;
                    case bool b: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, b, keyValuePair.Value.Item2); break;
                    case string s: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, s, keyValuePair.Value.Item2); break;
                    default: throw new NotSupportedException();
                }
            }

            config.modifiers.ForEach(modifier =>
            {
                itemBuilder.Modifier(modifier.getModifier());
            });
            ApplySlots(itemBuilder, config);

            Item component = itemBuilder
                .Instantiate();
            ApplySlotIcons(component, config, modDir);

            UnityEngine.Object.DontDestroyOnLoad(component);
            SetItemProperties(component, config);
            return component;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async UniTask<Item> GetCustomCartridgeAsync(Identifier id, Identifier gameId, ItemData config)
        {
            // 在 await 前预定 TypeID，防止被低优先级同步加载抢占。
            // 若首选 ID 冲突则自动分配空闲值。
            int actualTypeId = ReserveTypeId(id, config.itemId);

            try
            {
                var modDir = ModPathResolver.ResolveDirectory(id.Domain);
                ItemBuilder itemBuilder = ItemBuilder.New()
                    .TypeID(actualTypeId)
                    .EnableStacking(config.maxStackCount, 1)
                    .Icon(await LoadSpriteFromDirAsync(modDir, config.spritePath))
                    .SetConstant("GameID", gameId.ToString());

                foreach (var keyValuePair in config.slots)
                {
                    List<Tag> exists = new();
                    foreach (var targetSpec in keyValuePair.Value.requiredTags)
                    {
                        string? v = TagLookup.GetNativeMayNotExist(targetSpec);
                        if (v == null) throw new IndexOutOfRangeException($"Key {targetSpec} has not yet been registered.");
                        var tg = GetTargetTag(v);
                        if (tg == null) throw new IndexOutOfRangeException($"Key {targetSpec} does not exists.");
                        exists.Add(tg);
                    }

                    List<Tag> excludes = new();
                    if (keyValuePair.Value.excludeTags != null)
                    {
                        foreach (var targetSpec in keyValuePair.Value.excludeTags)
                        {
                            string? v = TagLookup.GetNativeMayNotExist(targetSpec);
                            if (v == null) throw new IndexOutOfRangeException($"Key {targetSpec} has not yet been registered.");
                            var tg = GetTargetTag(v);
                            if (tg == null) throw new IndexOutOfRangeException($"Key {targetSpec} does not exists.");
                            excludes.Add(tg);
                        }
                    }

                    itemBuilder.Slot(keyValuePair.Key, exists, excludes);
                }

                foreach (var keyValuePair in config.consts)
                {
                    switch (keyValuePair.Value.Item1)
                    {
                        case float f: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, f, keyValuePair.Value.Item2); break;
                        case int i: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, i, keyValuePair.Value.Item2); break;
                        case bool b: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, b, keyValuePair.Value.Item2); break;
                        case string s: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, s, keyValuePair.Value.Item2); break;
                        default: throw new NotSupportedException();
                    }
                }

                foreach (var keyValuePair in config.variables)
                {
                    switch (keyValuePair.Value.Item1)
                    {
                        case float f: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, f, keyValuePair.Value.Item2); break;
                        case int i: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, i, keyValuePair.Value.Item2); break;
                        case bool b: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, b, keyValuePair.Value.Item2); break;
                        case string s: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, s, keyValuePair.Value.Item2); break;
                        default: throw new NotSupportedException();
                    }
                }

                config.modifiers.ForEach(modifier =>
                {
                    itemBuilder.Modifier(modifier.getModifier());
                });
                ApplySlots(itemBuilder, config);

                Item component = itemBuilder
                    .Instantiate();
                await ApplySlotIconsAsync(component, config, modDir);

                UnityEngine.Object.DontDestroyOnLoad(component);
                SetItemProperties(component, config);

                return component;
            }
            finally
            {
                // 无论成功失败，确保预定被清理。RegisterItem 成功时内部已 ConfirmReservation，
                // 此处再清一次幂等无害；失败时（如 Sprite 加载异常）释放预定防止 TypeID 泄漏。
                CancelReservation(id);
            }
        }

        /// <summary>蓝图物品通用标签名。所有 BP 物品均携带此标签以供识别。
        /// 对应游戏原生 Tag ScriptableObject: Formula.asset (GUID c5f8d45583bcc1b48b05d71afc295c0e)，m_Name = "Formula"。</summary>
        private const string DefaultBPTag = "Formula";

        /// <summary>
        /// 创建并注册自定义蓝图。modid 从 <see cref="Identifier.Domain"/> 推导。
        /// </summary>
        public static async UniTask CreateCustomBluePrintAsync(Identifier id, BlueprintData config)
        {
            // 在 await 前预定 TypeID，防止被低优先级同步加载抢占。
            // 若首选 ID 冲突则自动分配空闲值。
            int actualTypeId = ReserveTypeId(id, config.itemId);

            try
            {
                // 确保标签已在游戏中注册（TagUtils.RegisterTag 会先查游戏原生 AllTags，
                // 已存在则复用，不存在则创建 ScriptableObject 实例并注册到游戏原生数据库）。
                TagUtils.RegisterTag(DefaultBPTag);
                TagUtils.RegisterTag(config.FormulaTag, new TagConfig
                {
                    Color = Color.blue,
                    Show = true,
                });

                // 将蓝图通用标签和 formulaTag 注入 tags 列表（避免重复）
                if (!config.tags.Contains(DefaultBPTag))
                    config.tags.Insert(0, DefaultBPTag);
                if (!config.tags.Contains(config.FormulaTag))
                    config.tags.Add(config.FormulaTag);

                var modDir = ModPathResolver.ResolveDirectory(id.Domain);
                Item component = ItemBuilder.New()
                    .TypeID(actualTypeId)
                    .Icon(!string.IsNullOrWhiteSpace(config.spritePath) ? await LoadSpriteFromDirAsync(modDir!, config.spritePath) : ItemAssetsCollection.GetPrefab(285).icon)
                    .Instantiate();
                UnityEngine.Object.DontDestroyOnLoad(component);
                SetItemProperties(component, config);
                ItemSetting_Formula formula = component.AddComponent<ItemSetting_Formula>();
                formula.formulaID = config.formulaID.Path;  // 游戏原生用 Path，非完整 Identifier
                RegisterItem(id, component);
            }
            finally
            {
                // 无论成功失败，确保预定被清理。RegisterItem 成功时内部已 ConfirmReservation，
                // 此处再清一次幂等无害；失败时（如 Sprite 加载异常）释放预定防止 TypeID 泄漏。
                CancelReservation(id);
            }
        }

        /// <summary>
        /// 创建并注册自定义蓝图。modid 从 <see cref="Identifier.Domain"/> 推导。
        /// </summary>
        public static void CreateCustomBluePrint(Identifier id, BlueprintData config)
        {
            // 确保标签已在游戏中注册（TagUtils.RegisterTag 会先查游戏原生 AllTags，
            // 已存在则复用，不存在则创建 ScriptableObject 实例并注册到游戏原生数据库）。
            TagUtils.RegisterTag(DefaultBPTag);
            TagUtils.RegisterTag(config.FormulaTag, new TagConfig
            {
                Color = Color.blue,
                Show = true,
            });

            // 将蓝图通用标签和 formulaTag 注入 tags 列表（避免重复）
            if (!config.tags.Contains(DefaultBPTag))
                config.tags.Insert(0, DefaultBPTag);
            if (!config.tags.Contains(config.FormulaTag))
                config.tags.Add(config.FormulaTag);

            var modDir = ModPathResolver.ResolveDirectory(id.Domain);
            var itemBuilder = ItemBuilder.New()
                .TypeID(config.itemId)
                .Icon(!string.IsNullOrWhiteSpace(config.spritePath)
                    ? LoadSpriteFromDir(modDir!, config.spritePath)
                    : ItemAssetsCollection.GetPrefab(285).icon);

            foreach (var keyValuePair in config.slots)
            {
                List<Tag> exists = new();
                foreach (var targetSpec in keyValuePair.Value.requiredTags)
                {
                    string? v = TagLookup.GetNativeMayNotExist(targetSpec);
                    if (v == null) throw new IndexOutOfRangeException($"Key {targetSpec} has not yet been registered.");
                    var tg = GetTargetTag(v);
                    if (tg == null) throw new IndexOutOfRangeException($"Key {targetSpec} does not exists.");
                    exists.Add(tg);
                }

                List<Tag> excludes = new();
                if (keyValuePair.Value.excludeTags != null)
                {
                    foreach (var targetSpec in keyValuePair.Value.excludeTags)
                    {
                        string? v = TagLookup.GetNativeMayNotExist(targetSpec);
                        if (v == null) throw new IndexOutOfRangeException($"Key {targetSpec} has not yet been registered.");
                        var tg = GetTargetTag(v);
                        if (tg == null) throw new IndexOutOfRangeException($"Key {targetSpec} does not exists.");
                        excludes.Add(tg);
                    }
                }

                itemBuilder.Slot(keyValuePair.Key, exists, excludes);
            }

            foreach (var keyValuePair in config.consts)
            {
                switch (keyValuePair.Value.Item1)
                {
                    case float f: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, f, keyValuePair.Value.Item2); break;
                    case int i: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, i, keyValuePair.Value.Item2); break;
                    case bool b: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, b, keyValuePair.Value.Item2); break;
                    case string s: itemBuilder = itemBuilder.SetConstant(keyValuePair.Key, s, keyValuePair.Value.Item2); break;
                    default: throw new NotSupportedException();
                }
            }

            foreach (var keyValuePair in config.variables)
            {
                switch (keyValuePair.Value.Item1)
                {
                    case float f: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, f, keyValuePair.Value.Item2); break;
                    case int i: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, i, keyValuePair.Value.Item2); break;
                    case bool b: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, b, keyValuePair.Value.Item2); break;
                    case string s: itemBuilder = itemBuilder.SetVariable(keyValuePair.Key, s, keyValuePair.Value.Item2); break;
                    default: throw new NotSupportedException();
                }
            }

            var component = itemBuilder.Instantiate();
            UnityEngine.Object.DontDestroyOnLoad(component);
            SetItemProperties(component, config);
            ItemSetting_Formula formula = component.AddComponent<ItemSetting_Formula>();
            formula.formulaID = config.formulaID.Path;  // 游戏原生用 Path，非完整 Identifier
            RegisterItem(id, component);
        }


        public static void SetItemProperties(Item item, ItemData config)
        {
            item.weight = config.weight;
            item.Order = config.order;
            item.Value = config.value;
            item.Quality = config.quality;

            item.DisplayNameRaw = config.localizationKey;
            item.description = config.localizationDesc;
            item.MaxDurability = config.maxDurability;
            item.Durability = config.maxDurability;
            ItemUtils.createUsage(item, config);
            item.Tags.Clear();
            foreach (string tagName in config.tags)
            {
                item.Tags.Add(GetTargetTag(tagName));
            }
        }

        public static void SetItemGraphic(Item item, AssetBundle assetBundle, string name)
        {
            ShaderReplacer.ApplyToBundle(assetBundle);
            GameObject graphic = assetBundle.LoadAsset<GameObject>(name);
            item.itemGraphic = graphic.GetComponent<ItemGraphicInfo>();
        }

        /// <summary>
        /// 查找指定名称的 Tag。先查游戏原生数据库，再查 <see cref="TagUtils"/> 缓存。
        /// 不自动创建——Tag 需通过 <see cref="TagUtils.RegisterTag"/> 显式注册。
        /// </summary>
        public static Tag GetTargetTag(string tagName)
        {
            var tag = TagUtils.GetTag(tagName);
            if (tag == null)
                Debug.LogWarning($"[FML] Tag '{tagName}' not found. Use TagUtils.RegisterTag(\"{tagName}\") to register it first.");
            return tag!;
        }

        /// <summary>
        /// TypeID 预定表：异步加载在 await 前预占 TypeID，防止被低优先级同步加载抢占。
        /// key=TypeID, value=预定者 Identifier。RegisterItem 确认后清除。
        /// </summary>
        private static readonly Dictionary<int, Identifier> _reservedTypeIds = new Dictionary<int, Identifier>();
        private static readonly object _reservationLock = new object();

        private const int FallbackTypeIdStart = 90000;
        private const int ForwardScanRange = 10000;

        /// <summary>
        /// 二重检测：游戏原生静态表 + FML 已注册动态条目。
        /// 不包含预定表检查（预定表由调用方在锁内检查以避免死锁）。
        /// </summary>
        private static bool IsTypeIdOccupied(int tid)
        {
            if (ItemAssetsCollection.Instance.GetEntry(tid) != null)
                return true;
            if (RegistryManager.Instance.ItemID.TryGetIdentifier(tid, out _))
                return true;
            return false;
        }

        /// <summary>
        /// 检查 TypeID 是否被当前 Identifier 之外的其他人预定。
        /// </summary>
        private static bool IsTypeIdReservedByOther(int tid, Identifier currentId)
        {
            lock (_reservationLock)
            {
                if (_reservedTypeIds.TryGetValue(tid, out var reserver))
                    return !reserver.Equals(currentId);
                return false;
            }
        }

        /// <summary>
        /// 从 preferred 位置向后扫描空闲 TypeID。先扫 preferred+1 ~ preferred+ForwardScanRange，
        /// 无空闲则回退到 FallbackTypeIdStart 起扫描。每次检测均查占用+预定。
        /// </summary>
        private static int FindNextFreeTypeId(int preferred, Identifier id)
        {
            // 前向扫描
            int scanEnd = preferred + ForwardScanRange;
            for (int tid = preferred + 1; tid < scanEnd; tid++)
            {
                if (!IsTypeIdOccupied(tid) && !IsTypeIdReservedByOther(tid, id))
                    return tid;
            }
            // 兜底：从 90000 起扫描
            int fallback = FallbackTypeIdStart;
            while (IsTypeIdOccupied(fallback) || IsTypeIdReservedByOther(fallback, id))
                fallback++;
            return fallback;
        }

        /// <summary>
        /// 预定 TypeID（供异步加载在 await 前调用）。如果首选 TypeID 已被占用或预定，
        /// 通过 FindNextFreeTypeId 从 preferred 位置后移扫描空闲值。返回实际分配的 TypeID。
        /// </summary>
        internal static int ReserveTypeId(Identifier id, int preferredTypeId)
        {
            lock (_reservationLock)
            {
                int tid = preferredTypeId;
                if (IsTypeIdOccupied(tid) || _reservedTypeIds.ContainsKey(tid))
                {
                    tid = FindNextFreeTypeId(preferredTypeId, id);
                }
                _reservedTypeIds[tid] = id;
                return tid;
            }
        }

        /// <summary>
        /// 确认预定：移除该 Identifier 在预定表中的所有记录。
        /// RegisterItem 成功后调用。
        /// </summary>
        private static void ConfirmReservation(Identifier id)
        {
            RemoveReservation(id);
        }

        /// <summary>
        /// 取消预定：异步创建失败（如 Sprite 加载异常）时释放预定，防止 TypeID 永久泄漏。
        /// </summary>
        private static void CancelReservation(Identifier id)
        {
            RemoveReservation(id);
        }

        private static void RemoveReservation(Identifier id)
        {
            lock (_reservationLock)
            {
                var keysToRemove = new List<int>();
                foreach (var kvp in _reservedTypeIds)
                {
                    if (kvp.Value.Equals(id))
                        keysToRemove.Add(kvp.Key);
                }
                foreach (var key in keysToRemove)
                    _reservedTypeIds.Remove(key);
            }
        }

        /// <summary>
        /// 注册自定义物品到游戏系统。owner modid 从 <see cref="Identifier.Domain"/> 推导。
        /// </summary>
        public static void RegisterItem(Identifier id, Item item)
        {
            string owner = id.Domain;

            // 冲突检测：游戏原生表 + FML 已注册 + 被其他 Identifier 预定
            if (IsTypeIdOccupied(item.TypeID) || IsTypeIdReservedByOther(item.TypeID, id))
            {
                item.TypeID = FindNextFreeTypeId(item.TypeID, id);
            }
            if (RegistryManager.Instance.ItemID.TryGet(id, out _))
            {
                throw new ArgumentException($"ItemID already registered: {id.Domain}:{id.Path}");
            }
            ItemAssetsCollection.AddDynamicEntry(item);
            RegistryManager.Instance.ItemID.Register(item.TypeID, id, item.TypeID, owner);
            ConfirmReservation(id);
            Debug.Log($"Registered custom item: {item.TypeID} - {item.DisplayName}");
        }

        /// <summary>
        /// 从 AssetBundle 注册枪支。modid 从 <see cref="Identifier.Domain"/> 推导。
        /// </summary>
        public static void RegisterGun(Identifier id, AssetBundle assetBundle, string name, int originGunID = 654)
        {
            ShaderReplacer.ApplyToBundle(assetBundle);
            var gameobject = assetBundle.LoadAsset<GameObject>(name);
            Item prefab = gameobject.GetComponent<Item>();
            Item rifle = ItemAssetsCollection.GetPrefab(originGunID);

            prefab.Tags.Clear();
            prefab.Tags.AddRange(rifle.Tags);

            foreach (var slot in prefab.Slots)
            {
                if (slot.Key.Equals("Muzzle") || slot.Key.Equals("Stock") || slot.Key.Equals("Mag"))
                    if (rifle.Slots[slot.Key] != null)
                    {
                        prefab.Slots[slot.Key].requireTags = rifle.Slots[slot.Key].requireTags;
                        prefab.Slots[slot.Key].excludeTags = rifle.Slots[slot.Key].excludeTags;
                    }
            }

            ItemSetting_Gun rifleSetting = rifle.GetComponent<ItemSetting_Gun>();
            ItemSetting_Gun setting = prefab.GetComponent<ItemSetting_Gun>();
            setting.adsAimMarker = rifleSetting.adsAimMarker;
            setting.muzzleFxPfb = rifleSetting.muzzleFxPfb;
            setting.bulletPfb = rifleSetting.bulletPfb;

            ItemUtils.RegisterItem(id, prefab);
        }

        /// <summary>
        /// 从 AssetBundle 注册物品。modid 从 <see cref="Identifier.Domain"/> 推导。
        /// </summary>
        public static void RegisterItemFromBundle(Identifier id, AssetBundle assetBundle, string name)
        {
            ShaderReplacer.ApplyToBundle(assetBundle);
            var gameobject = assetBundle.LoadAsset<GameObject>(name);
            Item prefab = gameobject.GetComponent<Item>();
            ItemUtils.RegisterItem(id, prefab);
        }

        public static void UnregisterItem(Item item)
        {
            ItemAssetsCollection.RemoveDynamicEntry(item);
            Debug.Log($"Unregistered custom item: {item.TypeID}");
        }

        /// <summary>
        /// 批量卸载指定 mod 注册的全部自定义物品。
        /// modid 未指定时走 <see cref="RegistryManager.CurrentModid"/>。
        /// </summary>
        public static void UnregisterAllItem(string? modid = null)
        {
            RegistryManager.Instance.ItemID.RemoveAllByOwner(modid ?? RegistryManager.CurrentModid);
        }

        public static void UnregisterAllTags(string? modid = null)
        {
            if (RegistryManager.Instance.TagRegistry.RemoveAllByOwner(modid ?? RegistryManager.CurrentModid, out var name) != 0)
            {
                GameplayDataSettings.Tags.allTags.RemoveAll(t => name.Exists(s => s.Equals(t.name)));
            }
        }

        /// <summary>
        /// 按 TypeID 反查 Item（内部使用，仅供 FML 框架内部调用）。
        /// </summary>
        internal static bool TryGetCustomItem(int typeID, out Item? item)
        {
            item = null;
            if (!RegistryManager.Instance.ItemID.TryGetIdentifier(typeID, out _)
                && !GameItemLookup.TryGetIdentifier(typeID, out _))
            {
                return false;
            }
            item = ItemAssetsCollection.GetPrefab(typeID);
            return item != null;
        }

        /// <summary>
        /// 【推荐】按 Identifier 反查已注册的自定义物品。
        /// 内部查询 FML 注册表 + 原版反查表，解析为原生 TypeID 后获取 Item。
        /// </summary>
        public static bool TryGetCustomItem(Identifier id, out Item? item)
        {
            if (TryResolveTypeId(id, out int typeId))
                return TryGetCustomItem(typeId, out item);
            item = null;
            return false;
        }

        /// <summary>
        /// 将 <see cref="Identifier"/> 解析为物品的 TypeID（内部使用）。
        /// 查询顺序：FML 注册的自定义物品 → 原版物品反查表。
        /// </summary>
        internal static bool TryResolveTypeId(Identifier id, out int typeId)
        {
            if (RegistryManager.Instance.ItemID.TryGet(id, out typeId))
                return true;
            if (GameItemLookup.TryResolve(id, out typeId))
                return true;
            return false;
        }

        /// <summary>
        /// 解析 item 引用：若 <paramref name="identifier"/> 有值则解析为 typeID；
        /// 否则回退到 <paramref name="fallbackTypeId"/>。
        /// </summary>
        internal static int ResolveItemRef(Identifier? identifier, int fallbackTypeId)
        {
            if (identifier != null && TryResolveTypeId(identifier, out int resolved))
                return resolved;
            return fallbackTypeId;
        }

        /// <summary>
        /// 创建并注册自定义子弹。modid 从 <see cref="Identifier.Domain"/> 推导，
        /// mod 目录从 <see cref="ModPathResolver"/> 自动探测。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void CreateCustomBullet(Identifier id, BulletData config)
        {
            var modDir = ModPathResolver.ResolveDirectory(id.Domain);
            Item component = ItemBuilder.New()
                .TypeID(config.itemId)
                .EnableStacking(config.maxStackCount, 1)
                .Icon(ItemUtils.LoadSpriteFromDir(modDir!, config.spritePath))
                .SetConstant("Caliber", config.Caliber, true)
                .SetConstant("SFX_Put", config.SFX_Put, false)
                .SetConstant("CritDamageFactorGain", config.CritDamageFactorGain, config.CritDamageFactorGain != 0F)
                .SetConstant("damageMultiplier", config.damageMultiplier, config.damageMultiplier != 0F)
                .SetConstant("CritRateGain", config.CritRateGain, config.CritRateGain != 0F)
                .SetConstant("ArmorPiercingGain", config.ArmorPiercingGain, config.ArmorPiercingGain != 0F)
                .SetConstant("ArmorBreakGain", config.ArmorBreakGain, config.ArmorBreakGain != 0F)
                .SetConstant("DurabilityCost", config.DurabilityCost, config.DurabilityCost != 0F)
                .SetConstant("ExplosionRange", config.ExplosionRange, config.ExplosionRange != 0F)
                .SetConstant("ExplosionDamage", config.ExplosionDamage, config.ExplosionDamage != 0F)
                .SetConstant("buffChanceMultiplier", config.buffChanceMultiplier, true)
                .SetConstant("bleedChance", config.bleedChance, true)
                .Instantiate();
            UnityEngine.Object.DontDestroyOnLoad(component);
            ItemUtils.SetItemProperties(component, config);
            ItemSetting_Bullet setting = component.AddComponent<ItemSetting_Bullet>();
            ItemUtils.RegisterItem(id, component);
        }

        /// <summary>
        /// 【推荐】创建并注册自定义子弹（异步）。Sprite 加载使用异步 IO。
        /// modid 从 <see cref="Identifier.Domain"/> 推导，mod 目录从 <see cref="ModPathResolver"/> 自动探测。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async UniTask CreateCustomBulletAsync(Identifier id, BulletData config)
        {
            // 在 await 前预定 TypeID，防止被低优先级同步加载抢占。
            // 若首选 ID 冲突则自动分配空闲值。
            int actualTypeId = ReserveTypeId(id, config.itemId);

            try
            {
                var modDir = ModPathResolver.ResolveDirectory(id.Domain);
                Item component = ItemBuilder.New()
                    .TypeID(actualTypeId)
                    .EnableStacking(config.maxStackCount, 1)
                    .Icon(await LoadSpriteFromDirAsync(modDir!, config.spritePath))
                    .SetConstant("Caliber", config.Caliber, true)
                    .SetConstant("SFX_Put", config.SFX_Put, false)
                    .SetConstant("CritDamageFactorGain", config.CritDamageFactorGain, config.CritDamageFactorGain != 0F)
                    .SetConstant("damageMultiplier", config.damageMultiplier, config.damageMultiplier != 0F)
                    .SetConstant("CritRateGain", config.CritRateGain, config.CritRateGain != 0F)
                    .SetConstant("ArmorPiercingGain", config.ArmorPiercingGain, config.ArmorPiercingGain != 0F)
                    .SetConstant("ArmorBreakGain", config.ArmorBreakGain, config.ArmorBreakGain != 0F)
                    .SetConstant("DurabilityCost", config.DurabilityCost, config.DurabilityCost != 0F)
                    .SetConstant("ExplosionRange", config.ExplosionRange, config.ExplosionRange != 0F)
                    .SetConstant("ExplosionDamage", config.ExplosionDamage, config.ExplosionDamage != 0F)
                    .SetConstant("buffChanceMultiplier", config.buffChanceMultiplier, true)
                    .SetConstant("bleedChance", config.bleedChance, true)
                    .Instantiate();
                UnityEngine.Object.DontDestroyOnLoad(component);
                ItemUtils.SetItemProperties(component, config);
                ItemSetting_Bullet setting = component.AddComponent<ItemSetting_Bullet>();
                RegisterItem(id, component);
            }
            finally
            {
                // 无论成功失败，确保预定被清理。RegisterItem 成功时内部已 ConfirmReservation，
                // 此处再清一次幂等无害；失败时（如 Sprite 加载异常）释放预定防止 TypeID 泄漏。
                CancelReservation(id);
            }
        }

        /// <summary>
        /// 检查物品是否有指定标签。
        /// 直接遍历 <see cref="Item.Tags"/>（public TagCollection，Tag.name 为 public 属性）——零反射。
        /// </summary>
        /// <param name="item">待检查的物品。</param>
        /// <param name="tag">标签名称。</param>
        /// <returns>物品包含指定标签时返回 true。</returns>
        public static bool HasTag(Item item, string tag)
        {
            if (item == null || string.IsNullOrEmpty(tag)) return false;
            try
            {
                if (item.Tags == null) return false;
                foreach (var t in item.Tags)
                {
                    if (t != null && t.name == tag) return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ItemUtils.HasTag] Failed: {e.Message}");
            }
            return false;
        }
    }
}
