using System;

using Duckov.Buffs;
using Duckov.ItemUsage;
using Duckov.Utilities;
using FeatherMod.Items;
using FeatherMod.Utils;
using ItemStatsSystem;
using ItemStatsSystem.Stats;
using System.Collections.Generic;

using Unity.VisualScripting;

namespace FeatherMod
{
    public class ItemData
    {
        public int itemId;
        public int order = 0;
        public string localizationKey = string.Empty;
        public string localizationDesc = string.Empty;
        public float weight;
        public int value;
        public int maxStackCount = 1;
        public float maxDurability = 0f;
        public int quality;
        public DisplayQuality displayQuality = DisplayQuality.None;
        public string spritePath = string.Empty;
        public List<string> tags = new List<string>();
        public UsageData? usages;
        public List<ModifierData> modifiers = new List<ModifierData>();

        public Dictionary<string, (object, bool)> consts = new();
        public Dictionary<string, (object, bool)> variables = new();

        public void AddTags(Identifier tag)
        {
            string? v = TagLookup.GetNativeMayNotExist(tag);
            if (v == null) throw new IndexOutOfRangeException($"Key {tag} has not yet been registered.");
            tags.Add(v);
        }

        /// <summary>槽位配置。默认空表 → 无槽位物品。槽位约束完全由 Tag 决定，见 <see cref="SlotData"/>。</summary>
        public List<SlotData> slots = new List<SlotData>();
    }

    /// <summary>
    /// 物品槽位配置（游戏 <c>ItemStatsSystem.Items.Slot</c> 的抽象层）。
    /// 槽位兼容性完全由 Tag 决定——游戏 <c>Slot.CheckAbleToPlug</c> 只校验 requireTags/excludeTags，不查 typeID。
    /// 引用的 Tag 必须已存在（游戏原生 Tag 或 <see cref="TagUtils.RegisterTag"/> 注册）；不存在的 Tag 会被舍弃并告警，槽位本身保留。
    /// </summary>
    public class SlotData
    {
        /// <summary>槽位唯一标识。游戏内建 key 见 <see cref="SlotKeys"/>（如 "Muzzle" / "Scope"）。</summary>
        public string key = string.Empty;

        /// <summary>槽位图标路径（`assets/textures/` 下，与 <see cref="ItemData.spritePath"/> 同一约定）。
        /// 留空 = 不设置图标，UI 显示默认槽位图标。</summary>
        public string spritePath = string.Empty;

        /// <summary>可装配件必须全部携带的 Tag 名称。</summary>
        public List<string> requireTags = new List<string>();

        /// <summary>禁止携带的 Tag 名称。</summary>
        public List<string> excludeTags = new List<string>();
    }

    public class ModifierData
    {
        public ModifierTarget target;
        public string key = string.Empty;
        public ModifierType type;
        public float value = 1F;
        public bool overrideOrder = false;
        public int overrideOrderValue = 0;
        public bool display = true;

        public ModifierDescription getModifier()
        {
            ModifierDescription modifierDescription = new ModifierDescription(target, key, type, value, overrideOrder, overrideOrderValue);
            modifierDescription.display = display;
            return modifierDescription;
        }
    }

    public class BlueprintData : ItemData
    {
        public new float weight = 0.1F;
        public new int value = 50;
        public new int maxStackCount = 1;
        public new float maxDurability = 0f;
        public new DisplayQuality displayQuality = DisplayQuality.None;
        public new string spritePath = string.Empty;
        public new UsageData? usages = null;

        /// <summary>配方 Identifier。FML 自动取 <see cref="Identifier.Path"/> 匹配游戏原生 <c>CraftingFormula.id</c>。</summary>
        public Identifier formulaID = new Identifier("fml", "unset");

        /// <summary>
        /// 配方标签。决定蓝图物品归属的研究台类别，<see cref="CreateCustomBluePrint"/> 自动注入到物品 tags。
        /// 默认 <see cref="DefaultBlueprintTag"/>（匹配游戏原生 BP 物品行为）。
        /// 可选: Formula_Normal / Formula_Medic / Formula_Cook / Formula_Printer，或自定义标签。
        /// </summary>
        public string FormulaTag = DefaultBlueprintTag;

        /// <summary>默认蓝图配方标签（对应游戏 Formula_Blueprint Tag ScriptableObject）。</summary>
        public const string DefaultBlueprintTag = "Formula_Blueprint";
    }

    public class UsageData
    {
        public string actionSound = string.Empty;
        public string useSound = string.Empty;
        public bool useDurability = false;
        public int durabilityUsage = 1;
        public float useTime = 2;

        public List<UsageBehaviorData> behaviors = new List<UsageBehaviorData>();
    }
    public abstract class UsageBehaviorData
    {
        public abstract UsageBehavior GetBehavior(Item item);
    }
    public class FoodData : UsageBehaviorData
    {
        public float energyValue;
        public float waterValue;
        public override UsageBehavior GetBehavior(Item item)
        {
            FoodDrink foodDrinkBehavior = item.AddComponent<FoodDrink>();
            foodDrinkBehavior.energyValue = this.energyValue;
            foodDrinkBehavior.waterValue = this.waterValue;
            return foodDrinkBehavior;
        }
    }

    public class HealData : UsageBehaviorData
    {
        public int healValue;
        public override UsageBehavior GetBehavior(Item item)
        {
            Drug drugBehavior = item.AddComponent<Drug>();
            drugBehavior.healValue = this.healValue;
            return drugBehavior;
        }
    }

    public class AddBuffData : UsageBehaviorData
    {
        public int buff;
        public float chance = 1f;
        public override UsageBehavior GetBehavior(Item item)
        {
            AddBuff addBuffBehavior = item.AddComponent<AddBuff>();
            addBuffBehavior.buffPrefab = FindBuff(buff);
            addBuffBehavior.chance = this.chance;
            return addBuffBehavior;
        }

        public static Buff FindBuff(int id)
        {
            // 优先查 FML Registry（支持自定义 Buff），回退到游戏内置列表
            return BuffUtils.FindBuff(id) ?? GameplayDataSettings.Buffs.allBuffs?.Find(buff => buff != null && buff.id == id)!;
        }
    }

    public class RemoveBuffData : UsageBehaviorData
    {
        public int buffID;
        public int removeLayerCount = 2;
        public override UsageBehavior GetBehavior(Item item)
        {
            RemoveBuff buffBehavior = item.AddComponent<RemoveBuff>();
            buffBehavior.buffID = this.buffID;
            buffBehavior.removeLayerCount = this.removeLayerCount;
            return buffBehavior;
        }
    }

    public class ReturnItemData : UsageBehaviorData
    {
        public int itemTypeID;
        public bool display;
        public override UsageBehavior GetBehavior(Item item)
        {
            ReturnItem behavior = item.AddComponent<ReturnItem>();
            behavior.ItemTypeID = this.itemTypeID;
            behavior.showItemName = this.display;
            return behavior;
        }
    }
}
