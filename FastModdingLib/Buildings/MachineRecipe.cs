using FeatherMod.Utils;
using ItemStatsSystem;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace FeatherMod
{
    /// <summary>
    /// 建筑设备配方抽象基类。modder 继承此类实现自定义机器逻辑。
    /// BuildingSlotsWatcher 在槽位变化时调用 CanExecute → 通过后调用 Execute。
    ///
    /// 生命周期：一个 MachineRecipe 实例绑定到一个 Machine（一个建筑实例上）。
    /// 通过 SetState/GetState 存取的状态自动参与存档序列化，modder 无需手动实现 SL。
    /// </summary>
    public abstract class MachineRecipe
    {
        /// <summary>配方唯一标识（合成表 ID，用于存档识别和 Registry 索引）。</summary>
        public Identifier Id { get; set; }

        /// <summary>绑定的建筑实例主 Inventory。Execute 中可读写。</summary>
        protected internal Inventory? MainInventory { get; internal set; }

        /// <summary>绑定的子库存字典（SubKey → Inventory）。Execute 中可读写。</summary>
        protected internal IReadOnlyDictionary<string, Inventory>? SubInventories { get; internal set; }

        // ═══════════════════════════════════════════════════════
        //  自动存档状态存储
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 运行时状态字典。所有通过 SetState/GetState 存取的值
        /// 在存档时自动 JSON 序列化，读档时自动恢复。
        /// </summary>
        [JsonIgnore]
        private Dictionary<string, object?> _autoState = new();

        /// <summary>设置运行时状态（自动参与存档）。</summary>
        protected void SetState<T>(string key, T value)
        {
            _autoState[key] = value;
        }

        /// <summary>获取运行时状态。</summary>
        protected T GetState<T>(string key, T defaultValue = default!)
        {
            if (_autoState.TryGetValue(key, out var v) && v is T tv)
                return tv;
            return defaultValue;
        }

        /// <summary>框架内部调用：序列化当前状态为 JSON。</summary>
        internal string SerializeState()
        {
            if (_autoState.Count == 0) return "";
            return JsonConvert.SerializeObject(_autoState);
        }

        /// <summary>框架内部调用：从 JSON 恢复状态。</summary>
        internal void DeserializeState(string? json)
        {
            _autoState.Clear();
            if (string.IsNullOrEmpty(json)) return;
            var restored = JsonConvert.DeserializeObject<Dictionary<string, object?>>(json);
            if (restored != null)
            {
                foreach (var kvp in restored)
                    _autoState[kvp.Key] = kvp.Value;
            }
        }

        // ═══════════════════════════════════════════════════════
        //  modder 覆写方法
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 检查当前槽位状态是否满足配方条件。
        /// 子库存内容变化时 BuildingSlotsWatcher 自动调用。
        /// </summary>
        /// <returns>true = 可以开始生产。</returns>
        public abstract bool CanExecute();

        /// <summary>
        /// 执行配方逻辑。由 BuildingSlotsWatcher 在 CanExecute 返回 true 后调用。
        /// 实现者负责：消耗输入物品、创建/销毁物品、更新产物槽。
        /// 如需要异步处理时间，内部使用 UniTask + GameClock。
        /// </summary>
        public abstract void Execute();

        /// <summary>
        /// 获取当前生产进度（0~1）。用于 UI 进度条绑定。
        /// 默认返回 0（即时配方或无进度概念）。
        /// </summary>
        public virtual float GetProgress() => 0f;

        /// <summary>
        /// 是否正在生产中。BuildingSlotsWatcher 在 Execute 期间忽略新的槽位变化。
        /// 默认返回 false（同步配方，瞬间完成）。
        /// </summary>
        public virtual bool IsRunning => false;

        /// <summary>
        /// 存档标识键（框架自动分配：buildingId/machineKey）。
        /// </summary>
        internal string SaveKey = "";
    }
}
