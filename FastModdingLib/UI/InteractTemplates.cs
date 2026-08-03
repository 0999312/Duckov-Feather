using Duckov.Buildings.UI;
using Duckov.PerkTrees;
using Duckov.UI;
using FeatherMod.Interaction;
using FeatherMod.Utils;
using UnityEngine;

namespace FeatherMod.UI
{
    /// <summary>
    /// Building 系统交互入口模板。挂载到 Building Prefab 的 functionContainer 上。
    /// modder 可继承此类并重写 <see cref="OnInteractFinished"/> 以自定义交互行为。
    /// </summary>
    public class BuildingInteractTemplate : InteractableBase
    {
        /// <summary>建筑 Identifier（由 BuildingUtils.RegisterBuilding 注册时的 id）。
        /// 序列化为字符串，派发前经 <see cref="Identifier.Parse"/> 规范化（缺省 domain 为 duckov）。</summary>
        [SerializeField]
        private string? buildingIdentifier = null;

        [SerializeField]
        [Tooltip("交互提示本地化键。为空时使用默认文本。")]
        public string? InteractNameKey;

        protected override void Awake()
        {
            base.Awake();
            if (!string.IsNullOrEmpty(InteractNameKey))
            {
                overrideInteractName = true;
                _overrideInteractNameKey = InteractNameKey;
            }
        }

        protected override void OnInteractFinished()
        {
            // 默认行为：通过 ViewDispatcher 打开 Building View
            if (!string.IsNullOrEmpty(buildingIdentifier))
            {
                try
                {
                    var raw = buildingIdentifier!.Contains(":")
                        ? buildingIdentifier
                        : $"duckov:{buildingIdentifier}";
                    var id = Identifier.Parse(raw!);
                    ViewDispatcher.Open(GameViews.Building, id.ToString());
                }
                catch (System.ArgumentException e)
                {
                    Debug.LogWarning($"[BuildingInteractTemplate] Invalid buildingIdentifier '{buildingIdentifier}': {e.Message}");
                    BuilderView.Show(null);
                }
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
        [Tooltip("对应 Identifier（由 PerkTreeUtils.RegisterPerkTree 注册时的 id）。缺省 domain 为 duckov。")]
        public string? PerkTreeId;

        [SerializeField]
        [Tooltip("交互提示本地化键。为空时使用默认文本。")]
        public string? InteractNameKey;

        protected override void Awake()
        {
            base.Awake();
            if (!string.IsNullOrEmpty(InteractNameKey))
            {
                overrideInteractName = true;
                _overrideInteractNameKey = InteractNameKey;
            }
        }

        protected override void OnInteractFinished()
        {
            if (string.IsNullOrEmpty(PerkTreeId)) return;
            try
            {
                var raw = PerkTreeId!.Contains(":")
                    ? PerkTreeId
                    : $"duckov:{PerkTreeId}";
                var id = Identifier.Parse(raw!);
                ViewDispatcher.Open(GameViews.PerkTree, id.ToString());
            }
            catch (System.ArgumentException e)
            {
                Debug.LogWarning($"[PerkTreeInteractTemplate] Invalid PerkTreeId '{PerkTreeId}': {e.Message}");
            }
        }
    }

    /// <summary>
    /// Endowment 系统交互入口模板。挂载到基地物件上，
    /// 交互时打开 EndowmentSelectionPanel。
    /// </summary>
    public class EndowmentInteractTemplate : InteractableBase
    {
        [SerializeField]
        [Tooltip("交互提示本地化键。为空时使用默认文本。")]
        public string? InteractNameKey;

        protected override void Awake()
        {
            base.Awake();
            if (!string.IsNullOrEmpty(InteractNameKey))
            {
                overrideInteractName = true;
                _overrideInteractNameKey = InteractNameKey;
            }
        }

        protected override void OnInteractFinished()
        {
            ViewDispatcher.Open(GameViews.Endowment);
        }
    }

    /// <summary>
    /// Crafting 合成系统交互入口模板。挂载到建筑 functionContainer 等场景物件上，
    /// 交互时打开 CraftingView，按 CraftingTag 过滤配方。
    /// </summary>
    public class CraftingInteractTemplate : InteractableBase
    {
        [SerializeField]
        [Tooltip("配方标签过滤（对应 Recipe.Tags）。为空时显示全部配方。")]
        public string? CraftingTag;

        [SerializeField]
        [Tooltip("交互提示本地化键。为空时使用默认文本。")]
        public string? InteractNameKey;

        protected override void Awake()
        {
            base.Awake();
            if (!string.IsNullOrEmpty(InteractNameKey))
            {
                overrideInteractName = true;
                _overrideInteractNameKey = InteractNameKey;
            }
        }

        protected override void OnInteractFinished()
        {
            ViewDispatcher.Open(GameViews.Crafting, CraftingTag);
        }
    }

    /// <summary>
    /// FormulasIndex 配方索引交互入口模板。挂载到建筑 functionContainer 等场景物件上，
    /// 交互时打开 FormulasIndexView，浏览全部配方。
    /// </summary>
    public class FormulasIndexInteractTemplate : InteractableBase
    {
        [SerializeField]
        [Tooltip("交互提示本地化键。为空时使用默认文本。")]
        public string? InteractNameKey;

        protected override void Awake()
        {
            base.Awake();
            if (!string.IsNullOrEmpty(InteractNameKey))
            {
                overrideInteractName = true;
                _overrideInteractNameKey = InteractNameKey;
            }
        }

        protected override void OnInteractFinished()
        {
            ViewDispatcher.Open(GameViews.Formulas);
        }
    }

    /// <summary>
    /// FormulasRegister 配方注册交互入口模板。挂载到建筑 functionContainer 等场景物件上，
    /// 交互时打开 FormulasRegisterView，提交物品学习配方。
    /// </summary>
    public class FormulasRegisterInteractTemplate : InteractableBase
    {
        [SerializeField]
        [Tooltip("配方标签过滤（对应 Recipe.Tags）。为空时显示全部可注册配方。")]
        public string? RegisterTag;

        [SerializeField]
        [Tooltip("交互提示本地化键。为空时使用默认文本。")]
        public string? InteractNameKey;

        protected override void Awake()
        {
            base.Awake();
            if (!string.IsNullOrEmpty(InteractNameKey))
            {
                overrideInteractName = true;
                _overrideInteractNameKey = InteractNameKey;
            }
        }

        protected override void OnInteractFinished()
        {
            ViewDispatcher.Open(GameViews.FormulasRegister, RegisterTag);
        }
    }

    /// <summary>
    /// Decompose 物品分解交互入口模板。挂载到建筑 functionContainer 等场景物件上，
    /// 交互时打开 ItemDecomposeView，将物品拆解为材料。
    /// </summary>
    public class DecomposeInteractTemplate : InteractableBase
    {
        [SerializeField]
        [Tooltip("交互提示本地化键。为空时使用默认文本。")]
        public string? InteractNameKey;

        protected override void Awake()
        {
            base.Awake();
            if (!string.IsNullOrEmpty(InteractNameKey))
            {
                overrideInteractName = true;
                _overrideInteractNameKey = InteractNameKey;
            }
        }

        protected override void OnInteractFinished()
        {
            ViewDispatcher.Open(GameViews.Decompose);
        }
    }
}
