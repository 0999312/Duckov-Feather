using Cysharp.Threading.Tasks;
using Duckov.Scenes;
using Duckov.Utilities;
using FeatherMod.Events;
using FeatherMod.Register;
using FeatherMod.Utils;
using FmlEvent = FeatherMod.Events.Event;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherMod
{
    /// <summary>
    /// 多场景系统公共 API。提供子场景加载、传送、数据持久化和场景事件桥接。
    /// </summary>
    public static class MultiSceneUtils
    {
        private static SceneRegistry _registry;
        private static bool _initialized;

        public static SceneRegistry Registry => _registry;

        /// <summary>初始化（幂等）。</summary>
        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            _registry = new SceneRegistry();
            RegistryManager.Instance.Registry.SetIfAbsent(
                new Identifier(FMLConstants.Domain, "scene"),
                _registry,
                RegistryManager.CurrentModid);

            SceneLoadEventPatch.EnsurePatched();
        }

        // ===== 场景注册 =====

        /// <summary>注册自定义场景（将 Identifier 映射到游戏 sceneID）。</summary>
        public static void RegisterScene(Identifier id, string sceneId, string? modid = null)
        {
            if (string.IsNullOrEmpty(sceneId)) throw new ArgumentNullException(nameof(sceneId));
            Init();
            string owner = modid ?? id.Domain;
            _registry.Register(id, sceneId, owner);
        }

        // ===== 场景加载与传送 =====

        /// <summary>加载子场景（关卡内，通过 MultiSceneCore）。</summary>
        public static void LoadSubScene(Identifier sceneId)
        {
            if (!_registry.TryResolve(sceneId, out var nativeSceneId))
            {
                Debug.LogWarning($"[FML MultiScene] Scene '{sceneId}' not registered.");
                return;
            }

            var info = SceneInfoCollection.GetSceneInfo(nativeSceneId);
            var sceneRef = info?.SceneReference;
            if (sceneRef == null)
            {
                Debug.LogWarning($"[FML MultiScene] SceneReference not found for '{nativeSceneId}'.");
                return;
            }

            if (MultiSceneCore.Instance != null)
                MultiSceneCore.Instance.BeginLoadSubScene(sceneRef);
        }

        /// <summary>传送到目标场景的位置（同步加载子场景并传送）。</summary>
        public static void TeleportTo(Identifier sceneId, string locationName)
        {
            if (!_registry.TryResolve(sceneId, out var nativeSceneId)) return;

            var location = new MultiSceneLocation
            {
                SceneID = nativeSceneId,
                LocationName = locationName
            };

            if (MultiSceneCore.Instance != null)
                MultiSceneCore.Instance.LoadAndTeleport(location).Forget();
        }

        /// <summary>传送到目标场景的坐标。</summary>
        public static void TeleportTo(Identifier sceneId, Vector3 position)
        {
            if (!_registry.TryResolve(sceneId, out var nativeSceneId)) return;

            if (MultiSceneCore.Instance != null)
                MultiSceneCore.Instance.LoadAndTeleport(nativeSceneId, position).Forget();
        }

        // ===== 查询 =====

        /// <summary>获取当前活动子场景 Identifier。</summary>
        public static Identifier? GetCurrentSubScene()
        {
            if (MultiSceneCore.Instance == null) return null;
            var id = MultiSceneCore.ActiveSubSceneID;
            if (string.IsNullOrEmpty(id)) return null;

            if (_registry.TryGetIdentifier(id, out var identifier))
                return identifier;
            return new Identifier("duckov", id);
        }

        /// <summary>获取场景显示名称。</summary>
        public static string GetSceneDisplayName(Identifier sceneId)
        {
            if (!_registry.TryResolve(sceneId, out var nativeId)) return sceneId.Path;
            var info = SceneInfoCollection.GetSceneInfo(nativeId);
            return info != null ? info.DisplayName : nativeId;
        }

        /// <summary>获取全部已注册的场景。</summary>
        public static IReadOnlyList<Identifier> GetAllRegisteredScenes(string modid)
        {
            return _registry.GetAllByOwner(modid);
        }

        // ===== 关卡内持久数据 =====

        /// <summary>存储关卡内跨场景持久数据。</summary>
        public static void SetLevelData(string key, object value)
        {
            if (MultiSceneCore.Instance == null) return;
            var hash = key.GetHashCode();
            MultiSceneCore.Instance.inLevelData[hash] = value;
        }

        /// <summary>读取关卡内持久数据。</summary>
        public static T? GetLevelData<T>(string key) where T : class
        {
            if (MultiSceneCore.Instance == null) return null;
            var hash = key.GetHashCode();
            if (MultiSceneCore.Instance.inLevelData.TryGetValue(hash, out var val) && val is T typed)
                return typed;
            return null;
        }

        // ===== 物体场景迁移 =====

        /// <summary>将物体移动到目标子场景。</summary>
        public static void MoveToScene(GameObject obj, Identifier sceneId)
        {
            if (!_registry.TryResolve(sceneId, out var nativeId)) return;
            var scene = SceneManager.GetSceneByName(nativeId);
            if (scene.isLoaded)
                SceneManager.MoveGameObjectToScene(obj, scene);
        }

        /// <summary>将物体移动到主场景。</summary>
        public static void MoveToMainScene(GameObject obj)
        {
            if (MultiSceneCore.Instance != null)
                MultiSceneCore.MoveToMainScene(obj);
        }
    }

    /// <summary>场景加载开始事件。</summary>
    public class SceneLoadStartedEvent : FmlEvent
    {
        public Identifier SceneId { get; }
        public SceneLoadStartedEvent(Identifier sceneId) { SceneId = sceneId; }
    }

    /// <summary>场景加载完成事件。</summary>
    public class SceneLoadFinishedEvent : FmlEvent
    {
        public Identifier SceneId { get; }
        public SceneLoadFinishedEvent(Identifier sceneId) { SceneId = sceneId; }
    }

    /// <summary>子场景切换事件。</summary>
    public class SubSceneChangedEvent : FmlEvent
    {
        public Identifier? FromScene { get; }
        public Identifier ToScene { get; }
        public SubSceneChangedEvent(Identifier? from, Identifier to) { FromScene = from; ToScene = to; }
    }
}
