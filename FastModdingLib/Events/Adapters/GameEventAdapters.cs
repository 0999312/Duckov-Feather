using FeatherMod.Events.GameEvents;
using Saves;
using SodaCraft.Localizations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace FeatherMod.Events.Adapters
{
    /// <summary>
    /// 游戏原生 C# 事件/委托字段 → FML EventBus 桥接层。
    /// 通过运行时反射定位原生静态事件或委托字段（部分原生使用
    /// <c>public static Action&lt;...&gt;</c> 字段而非 <c>event</c> 关键字），
    /// DynamicMethod 动态构造匹配委托并订阅。
    /// </summary>
    public static class GameEventAdapters
    {
        // EventInfo 路径订阅（C# event 关键字声明的）
        private static readonly List<(EventInfo Evt, Delegate Handler)> _wired = new();
        // FieldInfo 路径订阅（public static Action<...> 委托字段）
        private static readonly List<(FieldInfo Field, Delegate Handler)> _wiredFields = new();

        private static Action<SystemLanguage>? _onSetLanguageHandler;

        public static void WireUp()
        {
            // Health.OnHurt → HurtEvent  原生: Action<Health, DamageInfo>
            WireDynamicEvent("Health", "OnHurt", nameof(OnHurtBridge));
            // Health.OnDead → EntityDeathEvent  原生: Action<Health, DamageInfo>
            WireDynamicEvent("Health", "OnDead", nameof(OnDeadBridge));
            // LevelManager.OnLevelInitialized → LevelInitializedEvent  原生: Action（无参）
            WireDynamicEvent("LevelManager", "OnLevelInitialized", nameof(OnLevelInitBridge));
            // EconomyManager.OnMoneyChanged → MoneyChangedEvent  原生: Action<long, long>
            WireDynamicEvent("Duckov.Economy.EconomyManager", "OnMoneyChanged", nameof(OnMoneyChangedBridge));
            // LocalizationManager.OnSetLanguage → LanguageChangedEvent  直接订阅（无二义性）
            _onSetLanguageHandler = OnSetLanguage;
            LocalizationManager.OnSetLanguage += _onSetLanguageHandler;

            // AIMainBrain.OnPlayerHearSound → PlayerHearSoundEvent  原生: Action<AISound>
            WireDynamicEvent("AIMainBrain", "OnPlayerHearSound", nameof(OnPlayerHearSoundBridge));
            // AIMainBrain.OnSoundSpawned → SoundSpawnedEvent  原生: Action<AISound>
            WireDynamicEvent("AIMainBrain", "OnSoundSpawned", nameof(OnSoundSpawnedBridge));
            // LevelManager.OnMainCharacterDead → PlayerDeathEvent  原生: Action<DamageInfo>
            WireDynamicEvent("LevelManager", "OnMainCharacterDead", nameof(OnPlayerDeathBridge));
            // LevelManager.OnControllingCharacterChanged → ControllingCharacterChangedEvent
            //   原生: Action<CharacterMainControl>（委托字段，非 event）—— 反编译确认，仅 1 参。
            WireDynamicEvent("LevelManager", "OnControllingCharacterChanged", nameof(OnControllingCharChangedBridge));
            // EconomyManager.OnItemUnlockStateChanged → ItemUnlockStateChangedEvent
            //   原生: Action<int>（委托字段，非 event）—— 反编译确认，仅 1 参 itemTypeID。
            WireDynamicEvent("Duckov.Economy.EconomyManager", "OnItemUnlockStateChanged", nameof(OnItemUnlockStateChangedBridge));
            // CraftingManager.OnItemCrafted → ItemCraftedEvent
            //   原生: Action<CraftingFormula, Item>（委托字段）
            WireDynamicEvent("CraftingManager", "OnItemCrafted", nameof(OnItemCraftedBridge));
            // CraftingManager.OnFormulaUnlocked → FormulaUnlockedEvent
            //   原生: Action<string>（委托字段）
            WireDynamicEvent("CraftingManager", "OnFormulaUnlocked", nameof(OnFormulaUnlockedBridge));
            // QuestManager.OnTaskFinishedEvent → QuestTaskFinishedEvent
            //   原生: Action<Quest, Task>（委托字段，非 event）—— 反编译确认，2 参。
            WireDynamicEvent("Duckov.Quests.QuestManager", "OnTaskFinishedEvent", nameof(OnQuestTaskFinishedBridge));
            // SavesSystem.OnCollectSaveData → CollectSaveDataEvent
            //   原生: Action（无参 event）
            WireDynamicEvent("Saves.SavesSystem", "OnCollectSaveData", nameof(OnCollectSaveDataBridge));
            // SavesSystem.OnSaveDeleted → SaveDeletedEvent
            //   原生: Action（无参 event），存档槽位删除完成后触发。
            //   各 FML 模块通过本事件清理内存注册表与跨槽位持久化状态。
            WireDynamicEvent("Saves.SavesSystem", "OnSaveDeleted", nameof(OnSaveDeletedBridge));
            // SavesCounter.OnKillCountChanged → KillCountChangedEvent
            //   原生: Action<string, int>（委托字段）—— 反编译确认，2 参。
            WireDynamicEvent("SavesCounter", "OnKillCountChanged", nameof(OnKillCountChangedBridge));
            // SceneLoader.onFinishedLoadingScene → MainSceneLoadedEvent  原生: Action<SceneLoadingContext>
            WireDynamicEvent("SceneLoader", "onFinishedLoadingScene", nameof(OnMainSceneLoadedBridge));
        }

        public static void TearDown()
        {
            foreach (var (evt, handler) in _wired)
            {
                try { evt.RemoveEventHandler(null, handler); }
                catch (Exception e) { Debug.LogWarning($"[FML GameEventAdapters] TearDown RemoveEventHandler failed: {e.Message}"); }
            }
            _wired.Clear();

            foreach (var (field, handler) in _wiredFields)
            {
                try
                {
                    var combined = (Delegate)field.GetValue(null);
                    field.SetValue(null, Delegate.Remove(combined, handler));
                }
                catch (Exception e) { Debug.LogWarning($"[FML GameEventAdapters] TearDown field remove failed: {e.Message}"); }
            }
            _wiredFields.Clear();

            if (_onSetLanguageHandler != null)
            {
                LocalizationManager.OnSetLanguage -= _onSetLanguageHandler;
                _onSetLanguageHandler = null;
            }
        }

        // ---- 反射 + DynamicMethod 订阅辅助 ----

        /// <summary>
        /// 通过运行时反射定位静态事件或委托字段，动态构造匹配委托并订阅。
        /// 优先尝试 <c>GetEvent</c>（C# event），失败时回退 <c>GetField</c>（public static Action&lt;...&gt; 委托字段）。
        /// </summary>
        private static void WireDynamicEvent(string typeName, string memberName, string bridgeMethod)
        {
            Type? type = FindType(typeName);
            if (type == null)
            {
                Debug.LogWarning($"[FML GameEventAdapters] 未找到类型 {typeName}，跳过 {memberName} 桥接。");
                return;
            }

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

            // 优先 C# event
            EventInfo? evt = type.GetEvent(memberName, flags);
            FieldInfo? field = null;
            Type? handlerType;

            if (evt != null)
            {
                handlerType = evt.EventHandlerType;
                if (handlerType == null)
                {
                    Debug.LogWarning($"[FML GameEventAdapters] {typeName}.{memberName} 无 EventHandlerType，跳过桥接。");
                    return;
                }
            }
            else
            {
                // 回退：public static Action<...> 委托字段
                field = type.GetField(memberName, flags);
                if (field == null)
                {
                    Debug.LogWarning($"[FML GameEventAdapters] {typeName}.{memberName} 不存在（非 event 也非 field），跳过桥接。");
                    return;
                }
                handlerType = field.FieldType;
            }

            MethodInfo? bridge = typeof(GameEventAdapters).GetMethod(bridgeMethod, BindingFlags.NonPublic | BindingFlags.Static);
            if (bridge == null)
            {
                Debug.LogWarning($"[FML GameEventAdapters] 桥接方法 {bridgeMethod} 未找到，跳过 {memberName}。");
                return;
            }

            MethodInfo? invoke = handlerType.GetMethod("Invoke");
            if (invoke == null) return;
            Type[] paramTypes = invoke.GetParameters().Select(p => p.ParameterType).ToArray();
            ParameterInfo[] bridgeParams = bridge.GetParameters();

            if (bridgeParams.Length > paramTypes.Length)
            {
                Debug.LogWarning(
                    $"[FML GameEventAdapters] {typeName}.{memberName} 原生参数数（{paramTypes.Length}）" +
                    $"少于桥接方法 {bridgeMethod} 参数数（{bridgeParams.Length}），无法桥接，跳过。");
                return;
            }
            if (bridgeParams.Length != paramTypes.Length)
            {
                Debug.LogWarning(
                    $"[FML GameEventAdapters] {typeName}.{memberName} 原生参数数（{paramTypes.Length}）" +
                    $"与桥接方法 {bridgeMethod} 参数数（{bridgeParams.Length}）不一致，" +
                    $"将丢弃多余的原生参数。建议更新桥接方法签名以匹配原生事件。");
            }

            var dm = new DynamicMethod("fml_" + memberName, null, paramTypes, typeof(GameEventAdapters));
            ILGenerator il = dm.GetILGenerator();
            for (int i = 0; i < bridgeParams.Length; i++)
            {
                if (i == 0) il.Emit(OpCodes.Ldarg_0);
                else if (i == 1) il.Emit(OpCodes.Ldarg_1);
                else if (i == 2) il.Emit(OpCodes.Ldarg_2);
                else if (i == 3) il.Emit(OpCodes.Ldarg_3);
                else il.Emit(OpCodes.Ldarg_S, (byte)i);
                if (paramTypes[i].IsValueType)
                    il.Emit(OpCodes.Box, paramTypes[i]);
            }
            il.Emit(OpCodes.Call, bridge);
            il.Emit(OpCodes.Ret);

            Delegate del = dm.CreateDelegate(handlerType);

            if (evt != null)
            {
                evt.AddEventHandler(null, del);
                _wired.Add((evt, del));
            }
            else
            {
                var existing = (Delegate?)field!.GetValue(null);
                field.SetValue(null, Delegate.Combine(existing, del));
                _wiredFields.Add((field, del));
            }
        }

        private static Type? FindType(string fullTypeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type? t = asm.GetType(fullTypeName, false);
                    if (t != null) return t;
                }
                catch { }
            }
            return null;
        }

        // ═══════════════════════════════════════════════════
        //  桥接方法 — 原生参数 → FML EventBus publish
        //  所有参数以 object 接收（值类型由 DynamicMethod 装箱）。
        //  签名已按反编译代码核实。
        // ═══════════════════════════════════════════════════

        private static void OnHurtBridge(object target, object info)
            => EventBusManager.Instance.Sync.Post(new HurtEvent(target, info));

        private static void OnDeadBridge(object victim, object info)
            => EventBusManager.Instance.Sync.Post(new EntityDeathEvent(victim, info));

        private static void OnLevelInitBridge()
            => EventBusManager.Instance.Sync.Post(new LevelInitializedEvent(null));

        private static void OnMainSceneLoadedBridge(object context)
            => EventBusManager.Instance.Sync.Post(new MainSceneLoadedEvent());

        private static void OnMoneyChangedBridge(object oldMoney, object nowMoney)
            => EventBusManager.Instance.Sync.Post(new MoneyChangedEvent((long)oldMoney, (long)nowMoney));

        private static void OnSetLanguage(SystemLanguage lang)
        {
            string langCode = I18n.GetLangCode(lang);
            EventBusManager.Instance.Sync.Post(new LanguageChangedEvent(langCode));
        }

        private static void OnPlayerHearSoundBridge(object soundInfo)
            => EventBusManager.Instance.Sync.Post(new PlayerHearSoundEvent(soundInfo));

        private static void OnSoundSpawnedBridge(object soundInfo)
            => EventBusManager.Instance.Sync.Post(new SoundSpawnedEvent(soundInfo));

        private static void OnPlayerDeathBridge(object info)
            => EventBusManager.Instance.Sync.Post(new PlayerDeathEvent(info));

        // 原生: Action<CharacterMainControl>（仅 1 参：当前控制角色）
        private static void OnControllingCharChangedBridge(object character)
            => EventBusManager.Instance.Sync.Post(new ControllingCharacterChangedEvent(null, character));

        // 原生: Action<int>（仅 1 参：解锁物品 typeID；无 bool unlocked 参数）
        private static void OnItemUnlockStateChangedBridge(object itemTypeID)
            => EventBusManager.Instance.Sync.Post(new ItemUnlockStateChangedEvent(itemTypeID, true));

        // 原生: Action<CraftingFormula, Item>
        private static void OnItemCraftedBridge(object formula, object item)
            => EventBusManager.Instance.Sync.Post(new ItemCraftedEvent(formula, item));

        // 原生: Action<string>
        private static void OnFormulaUnlockedBridge(object formulaID)
            => EventBusManager.Instance.Sync.Post(new FormulaUnlockedEvent(formulaID));

        // 原生: Action<Quest, Task>（2 参）
        private static void OnQuestTaskFinishedBridge(object quest, object task)
            => EventBusManager.Instance.Sync.Post(new QuestTaskFinishedEvent(quest, task));

        // 原生: Action（无参 event）
        private static void OnCollectSaveDataBridge()
            => EventBusManager.Instance.Sync.Post(new CollectSaveDataEvent(null));

        // 原生: Action（无参 event）——存档槽位删除完成。
        // 桥接时取出 SavesSystem.CurrentSlot 作为快照（删除流程在触发前不会修改 CurrentSlot；
        // 若未来版本行为变化，桥接仍按删除前槽位号语义发布）。
        private static void OnSaveDeletedBridge()
            => EventBusManager.Instance.Sync.Post(new SaveDeletedEvent(SavesSystem.CurrentSlot));

        // 原生: Action<string, int>（2 参：key, count）
        private static void OnKillCountChangedBridge(object key, object count)
            => EventBusManager.Instance.Sync.Post(new KillCountChangedEvent((int)count));
    }
}
