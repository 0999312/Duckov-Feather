using Cysharp.Threading.Tasks;
using Duckov.Utilities;
using FeatherMod.Entities;
using FeatherMod.Register;
using FeatherMod.Utils;
using System;
using System.Linq;
using UnityEngine;

namespace FeatherMod
{
    public static class EnemyUtils
    {
        private static readonly EnemyRegistry _enemyRegistry = new EnemyRegistry();
        private static bool _initialized;

        /// <summary>暴露给 <c>RegisterBootstrap</c> 用于注册到元表。</summary>
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

        /// <summary>
        /// 开启/关闭该敌人的 AutoSpawn：开启后每次关卡初始化（InitLevel）会在玩家出生点附近自动生成。
        /// 默认关闭——仅注册 preset 供 <see cref="CharacterSpawnerGroup"/> 等原生系统使用时不会意外刷怪。
        /// </summary>
        public static void SetAutoSpawn(Identifier id, bool auto = true)
        {
            Init();
            _enemyRegistry.SetAutoSpawn(id, auto);
        }

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
        public static object? CompileStateMachine(IStateConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            return StateMachineToBT.Compile(config);
        }

        // ===== 生成 =====

        /// <summary>
        /// 在指定位置异步生成已注册的敌人。
        /// 实际创建由 <c>CharacterRandomPreset.CreateCharacterAsync</c> 完成（public 方法，零反射）。
        /// </summary>
        /// <param name="position">生成位置。</param>
        /// <param name="onSpawned">生成完成回调（await 完成后调用）。</param>
        /// <returns>异步生成立即返回 null；实际角色通过 <paramref name="onSpawned"/> 回调获取。</returns>
        public static CharacterMainControl? SpawnEnemy(Identifier id, Vector3 position, Action<CharacterMainControl>? onSpawned = null)
        {
            if (!TryGetEnemy(id, out var preset)) return null;
            _ = SpawnInternalAsync(preset, position, null, onSpawned);
            return null;
        }

        /// <summary>
        /// 将已注册的敌人添加到指定 <see cref="CharacterSpawnerGroup"/> 异步生成。
        /// </summary>
        public static CharacterMainControl? SpawnEnemy(Identifier id, CharacterSpawnerGroup group, Action<CharacterMainControl>? onSpawned = null)
        {
            if (!TryGetEnemy(id, out var preset)) return null;
            var pos = group != null ? group.transform.position : Vector3.zero;
            _ = SpawnInternalAsync(preset, pos, group, onSpawned);
            return null;
        }

        /// <summary>内部生成实现（public 方法直接调用，零反射）。</summary>
        private static async UniTask SpawnInternalAsync(
            CharacterRandomPreset preset, Vector3 position, CharacterSpawnerGroup? group,
            Action<CharacterMainControl>? onSpawned)
        {
            try
            {
                Vector3 dir = Vector3.forward;
                int sceneBuildIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;

                var character = await preset.CreateCharacterAsync(position, dir, sceneBuildIndex, group, false);
                if (character != null)
                {
                    onSpawned?.Invoke(character);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FML EnemyUtils.SpawnEnemy] Failed: {e}");
            }
        }
    }
}
