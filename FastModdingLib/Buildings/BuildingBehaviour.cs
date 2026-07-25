using Duckov.Buildings;
using ItemStatsSystem;
using UnityEngine;

namespace FeatherMod
{
    /// <summary>
    /// 建筑行为组件抽象基类。modder 继承此类实现自定义建筑运行时逻辑。
    /// 与 PerkBehaviour 模式一致：声明式配置（BuildingConfig.Behaviours），
    /// FML 在建筑实例化时自动挂载到 Building GameObject。
    ///
    /// 生命周期：Awake（挂载时）→ OnBuildingPlaced（建筑实际放置到场景中）→ OnDestroy（建筑拆除）。
    /// </summary>
    public abstract class BuildingBehaviour : MonoBehaviour
    {
        /// <summary>绑定的建筑组件引用。</summary>
        protected Building? Building { get; private set; }

        /// <summary>绑定的建筑主 Inventory。</summary>
        protected Inventory? MainInventory { get; private set; }

        /// <summary>
        /// 初始化（FML 内部调用）。在 Awake 之前设置引用。
        /// </summary>
        internal void SetBuilding(Building building, Inventory? inventory)
        {
            Building = building;
            MainInventory = inventory;
        }

        /// <summary>
        /// 建筑被放置到场景中时调用。
        /// 默认空实现，子类覆写以添加自定义逻辑。
        /// </summary>
        public virtual void OnBuildingPlaced() { }

        /// <summary>
        /// 建筑被拆除时调用。在此清理资源。
        /// </summary>
        public virtual void OnBuildingDemolished() { }
    }
}
