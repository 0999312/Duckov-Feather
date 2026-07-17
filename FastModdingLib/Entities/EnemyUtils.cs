using Duckov.Utilities;
using FeatherMod.Entities;
using FeatherMod.Register;
using FeatherMod.Utils;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FeatherMod
{
    public static class EnemyUtils
    {
        private static readonly EnemyRegistry _enemyRegistry = new EnemyRegistry();
        private static bool _initialized;

        /// <summary>暴露给 <c>RegisterBootstrap</c> 用于注册到元表。</summary>
        /// <summary>暴露给 RegisterBootstrap 用于注册到元表和查询。</summary>
        public static EnemyRegistry Registry => _enemyRegistry;

        /// <summary>
        /// 初始化：将 EnemyRegistry 注册到 <see cref="RegistryManager.Registry"/> 元表。
        /// 由 <c>RegisterBootstrap.Init()</c> 调用（幂等）。
        /// </summary>
        internal static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            var meta = RegistryManager.Instance.Registry;
            var id = new Identifier(FMLConstants.Domain, "enemy");
            if (meta is NonAlterableSimpleRegistry<ERegistry> nonAlt)
                nonAlt.SetIfAbsent(id, _enemyRegistry, RegistryManager.CurrentModid);
            else
                meta.Set(id, _enemyRegistry, RegistryManager.CurrentModid);
        }

        // ===== 注册 / 卸载 =====

        /// <summary>
        /// 注册自定义敌人。将 <see cref="CharacterRandomPreset"/> 注入游戏全局列表，
        /// 同时登入 FML Registry 以便按 modid 卸载。
        /// modid 从 <see cref="Identifier.Domain"/> 推导。
        /// </summary>
        /// <param name="aiConfig">modder 实现的状态机逻辑。</param>
        public static void RegisterEnemy(Identifier id, IStateConfig aiConfig, CharacterRandomPreset preset)
        {
            Init();
            _enemyRegistry.RegisterPreset(id, preset, aiConfig);
            Debug.Log($"[FML] Registered enemy: {id} (AI: {aiConfig.GetType().Name}) from mod: {id.Domain}");
        }

        /// <summary>按 Identifier 移除已注册的敌人。</summary>
        public static bool UnregisterEnemy(Identifier id) => _enemyRegistry.Remove(id);

        /// <summary>批量卸载指定 mod 注册的全部敌人。</summary>
        public static int UnregisterAllEnemies(string modid) => _enemyRegistry.RemoveAllByOwner(modid);

        // ===== 查询 =====

        /// <summary>按 nameKey 查找 CharacterRandomPreset（升级版，null-safe）。</summary>
        public static CharacterRandomPreset GetPreset(string name)
        {
            var presets = GameplayDataSettings.CharacterRandomPresetData.presets;
            if (presets == null) throw new InvalidOperationException("CharacterRandomPresetData.presets is null.");
            var result = presets.FirstOrDefault(p => p != null && p.nameKey == name);
            if (result == null)
                throw new ArgumentException($"Preset '{name}' not found.", nameof(name));
            return result;
        }

        /// <summary>按 Identifier 查询已注册的 preset。</summary>
        public static bool TryGetEnemy(Identifier id, out CharacterRandomPreset preset)
        {
            return _enemyRegistry.TryGet(id, out preset);
        }

        // ===== 编译状态机 =====

        /// <summary>
        /// 将 C# <see cref="IStateConfig"/> 编译为 NodeCanvas BehaviourTree。
        /// 编译后的 BT（<c>ScriptableObject</c>）可注入到 <see cref="AICharacterController"/> 的 combatTree 插槽。
        /// 返回 <c>object</c> 而非 <c>BehaviourTree</c> 以避免编译期对 ParadoxNotion.dll 的硬引用；
        /// 调用方可安全地 cast 为 <c>ScriptableObject</c>。
        /// </summary>
        public static object CompileStateMachine(IStateConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            return StateMachineToBT.Compile(config);
        }

        // ===== 反射缓存 =====

        private static MethodInfo? _cachedCreateAsync;
        private static readonly object _cacheLock = new object();

        /// <summary>
        /// 获取 <c>CharacterRandomPreset.CreateCharacterAsync</c> 的反射 MethodInfo。
        /// 缓存以避免每帧重复反射查找。
        /// </summary>
        private static MethodInfo? GetCreateCharacterAsyncMethod()
        {
            if (_cachedCreateAsync != null) return _cachedCreateAsync;
            lock (_cacheLock)
            {
                if (_cachedCreateAsync != null) return _cachedCreateAsync;
                _cachedCreateAsync = typeof(CharacterRandomPreset).GetMethod("CreateCharacterAsync",
                    new Type[] { typeof(Vector3), typeof(Vector3), typeof(int), typeof(CharacterSpawnerGroup), typeof(bool) });
                if (_cachedCreateAsync == null)
                {
                    Debug.LogError("[FML EnemyUtils] CreateCharacterAsync method not found. Game API may have changed.");
                }
                return _cachedCreateAsync;
            }
        }

        // ===== 生成 =====

        /// <summary>
        /// 在指定位置异步生成已注册的敌人。
        /// 实际创建由 <c>CharacterRandomPreset.CreateCharacterAsync</c> 完成（返回 UniTask），
        /// FML 在此触发后通过 <c>GameEventAdapters</c> 的 OnLevelInitialized 等 EventBus 事件
        /// 通知 modder 生成完成。
        /// </summary>
        /// <param name="position">生成位置。</param>
        /// <returns>始终返回 null；实际角色通过 EventBus 回调获取——参见 <see cref="Events.GameEvents.LevelInitializedEvent"/> 或传入 <paramref name="onSpawned"/> 回调。</returns>
        public static CharacterMainControl SpawnEnemy(Identifier id, Vector3 position, Action<CharacterMainControl>? onSpawned = null)
        {
            if (!TryGetEnemy(id, out var preset)) return null;
            return SpawnInternal(preset, position, null, onSpawned);
        }

        /// <summary>
        /// 将已注册的敌人添加到指定 <see cref="CharacterSpawnerGroup"/> 异步生成。
        /// </summary>
        public static CharacterMainControl SpawnEnemy(Identifier id, CharacterSpawnerGroup group, Action<CharacterMainControl>? onSpawned = null)
        {
            if (!TryGetEnemy(id, out var preset)) return null;
            var pos = group != null ? group.transform.position : Vector3.zero;
            return SpawnInternal(preset, pos, group, onSpawned);
        }

        /// <summary>内部生成实现。</summary>
        private static CharacterMainControl SpawnInternal(
            CharacterRandomPreset preset, Vector3 position, CharacterSpawnerGroup? group,
            Action<CharacterMainControl>? onSpawned)
        {
            try
            {
                var method = GetCreateCharacterAsyncMethod();
                if (method == null) return null;

                // 游戏实际重载：CreateCharacterAsync(Vector3 pos, Vector3 dir, int relatedScene, CharacterSpawnerGroup group, bool isLeader)
                Vector3 dir = Vector3.forward;
                int sceneBuildIndex = 0; // Enemy spawn in current scene context

                if (onSpawned != null)
                {
                    Debug.LogWarning("[FML EnemyUtils] onSpawned callback is not supported via reflection; " +
                                     "subscribe to EventBus HurtEvent/LevelInitializedEvent instead.");
                }

                var args = new object[] { position, dir, sceneBuildIndex, group, false };
                method.Invoke(preset, args);
            }
            catch (Exception e)
            {
                Debug.LogError($"[FML EnemyUtils.SpawnEnemy] Failed: {e}");
            }
            return null;
        }
    }
}
