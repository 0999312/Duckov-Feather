using Duckov.Buildings.UI;
using Duckov.PerkTrees;
using Duckov.UI;
using FeatherMod.Interaction;
using UnityEngine;

namespace FeatherMod.UI
{
    /// <summary>
    /// Building 系统交互入口模板。挂载到 Building Prefab 的 functionContainer 上。
    /// modder 可继承此类并重写 <see cref="OnInteractFinished"/> 以自定义交互行为。
    /// </summary>
    public class BuildingInteractTemplate : InteractableBase
    {
        /// <summary>建筑 Identifier（由 BuildingUtils.RegisterBuilding 注册时的 id）。</summary>
        [SerializeField]
        private string? buildingIdentifier;

        protected override void OnInteractFinished()
        {
            // 默认行为：通过 ViewDispatcher 打开 Building View
            if (!string.IsNullOrEmpty(buildingIdentifier))
            {
                ViewDispatcher.Open(GameViews.Building, buildingIdentifier);
            }
            else
            {
                BuilderView.Show(null); // 兜底：直接打开 BuilderView
            }
        }
    }

    /// <summary>
    /// PerkTree 系统交互入口模板。挂载到场景物件上，
    /// 交互时打开指定 PerkTree 的 PerkTreeView。
    /// </summary>
    public class PerkTreeInteractTemplate : InteractableBase
    {
        [SerializeField]
        [Tooltip("对应 Identifier.Path（由 PerkTreeUtils.RegisterPerkTree 注册时的 Path）。")]
        public string? PerkTreeID;

        protected override void OnInteractFinished()
        {
            if (string.IsNullOrEmpty(PerkTreeID)) return;
            ViewDispatcher.Open(GameViews.PerkTree, PerkTreeID);
        }
    }

    /// <summary>
    /// Endowment 系统交互入口模板。挂载到基地物件上，
    /// 交互时打开 EndowmentSelectionPanel。
    /// </summary>
    public class EndowmentInteractTemplate : InteractableBase
    {
        protected override void OnInteractFinished()
        {
            ViewDispatcher.Open(GameViews.Endowment);
        }
    }
}
