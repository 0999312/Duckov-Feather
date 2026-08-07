# Feather 使用指南 / Usage Guide

_面向全新模组项目的完整使用指南。如果你是第一次使用 FML 开发《逃离鸭科夫》模组，请从此处开始。_

> **API 签名速查请见 [API 参考](API/API.md)**（本文档为教程式指南）。

---

## 目录 / Table of Contents

| 章节 | | |
|------|---|---|
| [§0 阅读指引](#0-阅读指引--how-to-read) | [§11 天赋系统](#11-天赋系统--endowment) | [§22 多场景](#22-多场景--multi-scene) |
| [§1 快速开始](#1-快速开始--quick-start) | [§12 敌人系统](#12-敌人系统--enemy) | [§23 对话系统](#23-对话系统--dialogue) |
| [§2 核心概念](#2-核心概念--core-concepts) | [§13 友善 NPC](#13-友善-npc--friendly-npc) | [§24 音频系统](#24-音频系统--audio) |
| [§3 物品系统](#3-物品系统--items) | [§14 捏脸系统](#14-捏脸系统--custom-face) | [§25 本地化](#25-本地化--i18n) |
| [§4 合成系统](#4-合成系统--crafting) | [§15 NPC 注入](#15-npc-注入--weapon--lotterybox-injection) | [§26 事件总线](#26-事件总线--eventbus) |
| [§5 任务系统](#5-任务系统--quests) | [§16 交互系统](#16-交互系统--interaction) | [§27 设置面板](#27-自定义设置面板--mod-options) |
| [§6 商店系统](#6-商店系统--shop) | [§17 UI 系统](#17-ui-系统与控件桥接--gameui) | [§28 存档](#28-存档--save) |
| [§7 经济系统](#7-经济系统--economy) | [§18 物品容器](#18-物品容器--containers) | [§29 AssetBundle](#29-assetbundle-加载--assetbundle) |
| [§8 Buff 状态](#8-buff-状态--buffs) | [§19 笔记系统](#19-笔记系统--notes) | [§30 注册表系统](#30-注册表系统--registry) |
| [§9 建筑系统](#9-建筑系统--building) | [§20 钓鱼系统](#20-钓鱼系统--fishing) | [§31 跨模组联动](#31-跨模组联动--cross-mod-integration) |
| [§10 Perk 技能树](#10-perk-技能树--perk-trees) | [§21 天气系统](#21-天气系统--weather) | [§32 附录](#32-附录--appendix) |

---

## 0. 阅读指引 / How to Read

### 0.1 文档体系 / Document Tree

| 文档 | 定位 | 适合 |
|------|------|------|
| `README.md` | 项目入口 + 模块速览 | 第一次了解 FML |
| **本文档 `Docs/USAGE.md`** | 教程式使用指南：怎么用（流程 + 示例 + 注意事项） | 开发模组时按场景查阅 |
| `Docs/API/*.md` | 参考式 API 手册：有什么（完整签名 / DTO / 枚举 / 事件） | 速查签名、Agent 检索 |
| `Docs/PROGRESS.md` | 项目进度与变更记录 | 了解框架本身开发状态 |

### 0.2 读者导航 / Reader Navigation

- **AI Agent**：先读 §2 核心概念（约定必须遵守），再按任务场景跳转对应模块章节；查签名去 [API 索引](API/API.md)。
- **人类开发者**：从 §1 快速开始搭好工程，之后按模块查阅。

### 0.3 约定符号 / Conventions

- 🆕 新 API / 新能力
- ⚠️ 重要约束（不遵守会出 bug）
- 🔗 指向 API 文档

---

## 1. 快速开始 / Quick Start

### 1.1 创建工程 / Create Project

1. 通过 Visual Studio 创建一个 **.NET 类库**（Class Library）。
2. 目标框架（Target Framework）设置为 **.NET Standard 2.1**。
3. 注意删除 `<ImplicitUsings>`（.NET Standard 2.1 不支持）。

### 1.2 配置 csproj / Configure csproj

在 `.csproj` 中添加游戏 DLL 引用和 FML 引用：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <!-- 游戏 DLL 引用（通过环境变量 DUCKOV_PATH 指定游戏路径） -->
    <Reference Include="$(DUCKOV_PATH)\Duckov_Data\Managed\TeamSoda.*" />
    <Reference Include="$(DUCKOV_PATH)\Duckov_Data\Managed\ItemStatsSystem.dll" />
    <Reference Include="$(DUCKOV_PATH)\Duckov_Data\Managed\Unity*" />
    <Reference Include="$(DUCKOV_PATH)\Duckov_Data\Managed\Newtonsoft.Json.dll" />
    <Reference Include="$(DUCKOV_PATH)\Duckov_Data\Managed\FMODUnity.dll" />
    <Reference Include="$(DUCKOV_PATH)\Duckov_Data\Managed\ParadoxNotion.dll" />
    <Reference Include="$(DUCKOV_PATH)\Duckov_Data\Managed\UniTask*" />
    <!-- FML dll -->
    <Reference Include="path\to\FeatherMod.dll" />
  </ItemGroup>
</Project>
```

### 1.3 编写第一个模组 / First Mod

```csharp
using FeatherMod;
using FeatherMod.Utils;
using HarmonyLib;
using System.Reflection;

public class MyFirstMod : Duckov.Modding.ModBehaviour, IHasModid
{
    string dllPath = Assembly.GetExecutingAssembly().Location;

    public string GetModid() => "MyFirstMod";

    protected override void OnAfterSetup()
    {
        // 注册 mod 路径，供 I18n / Sprite / Bundle 自动解析
        ModPathResolver.Register(GetModid(), dllPath);
        I18n.InitI18n(GetModid());

        // 自行创建 Harmony 实例并 patch 自身的 [HarmonyPatch]
        var harmony = new Harmony(GetModid());
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        // 在这里注册你的物品、配方、任务……
    }
}
```

> **关键点**：
> - 继承 **`Duckov.Modding.ModBehaviour`**（游戏引擎基类），**不**继承 `FeatherMod.ModBehaviour`
> - 实现 **`IHasModid`** 接口 — FML 工具通过此接口获取你的 mod 身份
> - `FMLBootstrap` 自动管理 Registry / EventBus 等游戏级单例——你只需调用 FML 工具方法即可

### 1.4 fml.json — 声明式模组配置 / Declarative Mod Config

每个模组可在其根目录放置 `fml.json`，声明优先级与依赖关系。FML 在游戏 Rescan 模组列表时自动加载并应用。

```jsonc
{
    "modid": "MyMod",           // 必填：模组标识符，必须与 info.ini 中的 name 一致
    "priority": 100,            // 可选：加载优先级（越小越先加载，默认 int.MaxValue 即最低）
    "dependencies": [           // 可选：硬依赖，被依赖的 mod 必须存在且已激活
        "FeatherMod",
        "SomeOtherMod"
    ],
    "loadAfter": [              // 可选：软依赖，仅排在目标之后加载（不要求目标存在或激活）
        "OptionalMod"
    ]
}
```

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `modid` | string | **是** | — | 必须与 `info.ini` 的 `name` 完全一致，否则 fml.json 被忽略 |
| `priority` | int | 否 | `int.MaxValue` | 越小越优先加载。FML 自身固定为最高优先级 |
| `dependencies` | string[] | 否 | `[]` | **硬依赖**：目标必须存在，排序时强制排在目标之后 |
| `loadAfter` | string[] | 否 | `[]` | **软依赖**：仅保证排在目标之后，目标不存在或未激活时不报错 |

**加载机制**：
1. 游戏 `Rescan` 模组列表时，FML 遍历所有 mod 目录读取 `fml.json`
2. **排序**：先按 `priority` 升序排列，再拓扑排序满足 `dependencies` + `loadAfter` 约束
3. **循环依赖检测**：存在环时输出具体参与 mod 名称，回退为仅按 priority 排序

---

## 2. 核心概念 / Core Concepts

### 2.1 模组主类与生命周期 / Mod Class & Lifecycle

所有依赖 FML 的模组应直接继承 `Duckov.Modding.ModBehaviour`（游戏引擎基类）并实现 `IHasModid` 接口。

> **注意**：`FeatherMod.ModBehaviour` 是 FML 自身的入口类，由 ModManager 实例化。**外部模组不应继承它。**

| 阶段 | 方法 | 说明 |
|------|------|------|
| 游戏启动 | `Awake()` | 游戏引擎调用 |
| 初始化就绪 | `OnAfterSetup()` | 执行自定义初始化：注册路径 → Harmony.PatchAll → 调用 FML 工具方法注册内容 |
| 模组卸载 | `OnBeforeDeactivate()` | FML 自动清理注册的资源，一般无需覆写 |

### 2.2 Identifier 标识符 / Identifier System

Identifier 是 FML 统一的资源标识符，格式为 `domain:path`，类似 Minecraft 的 ResourceLocation。

```csharp
// 双段构造
Identifier id = new Identifier("mymod", "rifle_ak47");

// 从字符串解析
Identifier id = Identifier.Parse("mymod:rifle_ak47");

// 安全解析
if (Identifier.TryParse("mymod:rifle_ak47", out Identifier? parsed))
{
    // parsed 可用
}

id.Domain    // → "mymod"
id.Path      // → "rifle_ak47"
id.ToString()// → "mymod:rifle_ak47"
```

**校验规则**：
- 禁止 `:`（冒号）、`\\`（反斜杠）、`..`（双点）、空字符串
- `domain` 禁止 `/`（斜杠）；`path` 允许 `/` 以支持子目录资源（如 `mymod:items/weapons/rifle`）
- 所有异常在构造时立即抛出

**语义约定**：
- `domain` = 你的 modid（`Identifier.Domain` 自动推导 owner）
- `duckov` 域 = 游戏原版内容

### 2.3 引用原版内容 / Referencing Vanilla Content

引用游戏原版物品时，使用 `duckov` 域。如果只知道数字 TypeID，可以通过反查 API 获取 Identifier：

```csharp
// 已知 displayName → 直接构造
ItemEntry.Of(new Identifier("duckov", "AK-47"), 1);

// 只知道数字 TypeID → 反查
if (GameItemLookup.TryGetIdentifier(1001, out var id))
    ItemEntry.Of(id, 1);

// 按标签浏览全部原版物品
if (GameItemLookup.TryFindByTag("Gun", out var guns))
{
    foreach (var gun in guns)
        Debug.Log($"Gun: {gun}");  // duckov:AK-47, duckov:M4A1, ...
}

// 遍历全部索引
foreach (var id in GameItemLookup.GetAllIdentifiers())
    Debug.Log($"Vanilla item: {id}");
```

> `GameItemLookup` 在 FML 启动时自动扫描全量原版物品（约数千条），建立 `Identifier ↔ TypeID` 双向映射。
> 查询顺序：FML 注册的自定义物品 → 原版物品反查表。🔗 [API_ITEMS.md](API/API_ITEMS.md#gameitemlookup)

### 2.4 路径解析 / ModPathResolver

`ModPathResolver` 是 FML 的 mod 目录解析器。在 `OnAfterSetup` 中显式注册后，`I18n`、`ItemUtils.LoadSprite`、`AssetUtil.LoadBundle` 等的便捷重载才能正确解析 mod 目录：

```csharp
protected override void OnAfterSetup()
{
    base.OnAfterSetup(); // GetModid() 在此之后可用
    string dllPath = Assembly.GetExecutingAssembly().Location;
    ModPathResolver.Register(GetModid(), dllPath);
}
```

> 未注册时，`ResolveDirectory(modid)` 返回 `null`，便捷重载将退回到 FML 自身 DLL 目录路径，可能导致资源加载失败。

### 2.5 异步约定 / Async Convention

- 全项目异步一律使用 **UniTask**（`Cysharp.Threading.Tasks`），禁止 `async Task`。
- 同步/异步双版本 API：**加载阶段优先异步版**（Sprite / Mesh / Bundle IO 走线程池，避免主线程卡顿）。
- 并行注册用 `UniTask.WhenAll`，分帧处理用 `AsyncEventBus`（见 §26.3）。

### 2.6 自动卸载 / Auto Cleanup

FML 自动处理模组卸载时的清理工作，**无需手动编写卸载逻辑**。当游戏卸载你的模组时，`OnBeforeDeactivate` 自动执行：

```
1. GameEventAdapters.TearDown()
   → 解除所有原生事件订阅（-=）

2. EventBusManager.Clear()
   → 清空同步/异步总线所有 handler

3. RegistryManager.RemoveAllByOwner("mymod")
   → 遍历元表所有注册表
   → 按 modid 批量卸载
   → 各注册表的 OnRemoved 回调自动清理 native 侧
```

这意味着：
- ✅ 所有通过 FML API 注册的物品 / 配方 / 任务 / Buff / 建筑 / Perk / 商店商品 / 音频等自动卸载
- ✅ 所有 EventBus 订阅自动解除
- ✅ 所有原生事件桥接自动解除
- ❌ 不需要手动维护 `Dictionary` 追踪注册资源
- ❌ 不需要手动 `UnregisterAll`

---

## 3. 物品系统 / Items

🔗 [API_ITEMS.md](API/API_ITEMS.md) | 入口类：`ItemUtils` / `TagUtils` / `GameItemLookup`

### 3.1 注册 Tag（前置步骤）/ Register Tags First

⚠️ **必须先注册 Tag 再创建物品**，否则物品创建时会输出 warning 且 Tag 不会被添加到物品上。

```csharp
// 简单注册
TagUtils.RegisterTag("DrinkStation");

// 带配置注册（show、color、priority）
TagUtils.RegisterTag("CoffeeBean", new TagConfig
{
    Show = true,
    Color = new Color(0.6f, 0.3f, 0.1f),
    Priority = 10,
});

// 查询
Tag? tag = TagUtils.GetTag("CoffeeBean");
bool exists = TagUtils.TagExists("CoffeeBean");
```

> Tags 是 FML 中唯一不走 `Identifier` 的系统——所有 Tag 均视为 Common Tag，以纯字符串名称标识。
> 注意：Crafting 配方的 `Tags` 字段（`string[]`）是纯字符串工作台过滤标签，**不经过** TagUtils 系统，无需注册。

### 3.2 创建并注册物品 / Create & Register Item

```csharp
var itemData = new ItemData
{
    itemId = 150001,
    localizationKey = "item_coffee",   // I18n key
    weight = 0.3f,
    value = 500,
    maxStackCount = 5,
    maxDurability = 0f,
    quality = 3,
    spritePath = "coffee_icon.png",
    tags = new List<string> { "Food", "Drink" },
    usages = new UsageData
    {
        useTime = 2f,
        useSound = "e_Item_Drink",
        behaviors = new List<UsageBehaviorData>
        {
            new FoodData { energyValue = 30, waterValue = 20 },
        }
    },
    modifiers = new List<ModifierData>
    {
        new ModifierData { target = ModifierTarget.Player, key = "moveSpeed", type = ModifierType.Multiplier, value = 1.1f }
    }
};

// 异步创建并注册（推荐：加载阶段用，Sprite 加载走线程池 IO）
await ItemUtils.CreateCustomItemAsync(new Identifier("mymod", "coffee"), itemData);

// 同步版本（兼容保留，加载阶段不推荐）
ItemUtils.CreateCustomItem(new Identifier("mymod", "coffee"), itemData);
```

`ItemData.consts` / `ItemData.variables` 可注入物品常量与变量（值类型支持 float / int / bool / string，`(value, display)` 元组第二项控制 Tooltip 显示）：

```csharp
itemData.consts["GameID"] = ("mymod_coffee", false);
itemData.variables["freshness"] = (100f, true);
```

> `ItemData.AddTags(Identifier)` 可按 Identifier 解析原生 Tag 名加入 `tags`（Tag 未注册时抛 `IndexOutOfRangeException`，与 `TagUtils` 字符串注册为两套方式）。

**可用 UsageBehavior**：

| 类 | 用途 | 关键属性 |
|----|------|----------|
| `FoodData` | 食物/饮水 | `energyValue`, `waterValue` |
| `HealData` | 治疗 | `healValue` |
| `AddBuffData` | 添加 Buff | `buff` (Buff ID), `chance` |
| `RemoveBuffData` | 移除 Buff | `buffID`, `removeLayerCount` |
| `ReturnItemData` | 使用后返还物品 | `itemTypeID`, `display` |

#### 带槽位物品 / Items with Slots

通过 `ItemData.slots` 给物品装配槽位（如自定义枪口槽），槽位兼容性完全由 Tag 决定：

```csharp
// 自定义 Tag 需先注册；游戏原生 Tag（"Muzzle"、"Scope" 等）已存在，无需注册
TagUtils.RegisterTag("Rail");
TagUtils.RegisterTag("CustomRail");

var weaponData = new ItemData
{
    itemId = 150002,
    localizationKey = "item_custom_weapon",
    weight = 3.5f,
    value = 1200,
    tags = new List<string> { "Gun" },          // 枪械类物品需携带 "Gun" Tag 才能装入角色武器槽
    slots = new List<SlotData>
    {
        new SlotData
        {
            key = SlotKeys.Muzzle,              // 复用游戏内建槽位 key
            spritePath = "muzzle_slot.png",     // 槽位图标（可选，留空显示默认图标）
            requireTags = new List<string> { "Muzzle" },  // 只允许携带 Muzzle Tag 的配件装入
        },
        new SlotData
        {
            key = "Rails",                      // 自定义槽位 key
            requireTags = new List<string> { "Rail", "CustomRail" }, // 必须全部满足
        },
    },
};

await ItemUtils.CreateCustomItemAsync(new Identifier("mymod", "custom_weapon"), weaponData);

// 配件物品：携带槽位要求的 Tag 即可被装入
var muzzleData = new ItemData
{
    itemId = 150003,
    localizationKey = "item_custom_muzzle",
    tags = new List<string> { "Muzzle", "Accessory" },
};
await ItemUtils.CreateCustomItemAsync(new Identifier("mymod", "custom_muzzle"), muzzleData);
```

> **注意**：`requireTags` / `excludeTags` 引用的 Tag 必须已存在（原生 Tag 或 `TagUtils.RegisterTag`）。不存在的 Tag 会被舍弃并打印警告，**槽位本身保留**（该槽位将永远无法装入任何配件）。

### 3.3 仅构造不注册 / Construct Only

```csharp
// 异步构造（推荐：加载阶段用）
Item item = await ItemUtils.GetCustomItemAsync(new Identifier("mymod", "coffee"), itemData);

// 同步版本（兼容保留）
Item item = ItemUtils.GetCustomItem(new Identifier("mymod", "coffee"), itemData);
// 自己设置额外属性...
// 然后手动注册
ItemUtils.RegisterItem(new Identifier("mymod", "coffee"), item);
```

> **便捷重载**：若已通过 `ModPathResolver.Register` 注册路径，可使用简化签名：
> ```csharp
> Item item = ItemUtils.GetCustomItem(itemData); // 自动推导 modid 和路径
> ```

### 3.4 从 AssetBundle 注册 / Register from Bundle

```csharp
// 加载 AssetBundle
AssetBundle bundle = AssetUtil.LoadBundle(new Identifier("mymod", "weapons"));
// 便捷重载（需先 ModPathResolver.Register）：
// AssetBundle bundle = AssetUtil.LoadBundle("weapons");

// 注册枪支（自动复制基础枪支的属性）
ItemUtils.RegisterGun(new Identifier("mymod", "rifle"), bundle, "Rifle_Prefab");

// 注册普通物品
ItemUtils.RegisterItemFromBundle(new Identifier("mymod", "armor"), bundle, "Armor_Prefab");
```

### 3.5 创建蓝图 / Blueprints

```csharp
var blueprintData = new BlueprintData
{
    itemId = 200001,
    localizationKey = "bp_coffee",
    formulaID = new Identifier("mymod", "coffee_recipe"),  // FML 自动取 .Path 匹配游戏原生 CraftingFormula.id
    FormulaTag = "Formula_Cook",  // 决定蓝图归属的研究台类别（默认 "Formula_Blueprint"）
};
ItemUtils.CreateCustomBluePrint(new Identifier("mymod", "coffee_bp"), blueprintData);
```

> **重要**：`formulaID` 为 `Identifier` 类型。FML 内部自动取 `.Path` 写入游戏原生的 `CraftingFormula.id`。请勿手动拼接 domain 前缀。
>
> **`FormulaTag` 说明**：决定蓝图物品属于哪个研究台类别。`CreateCustomBluePrint` 自动调用 `TagUtils.RegisterTag` 注册该标签并注入物品 tags，同时自动注入通用标签 `"Formula"`（对应游戏原生 `Formula.asset` Tag）。
> 可选值：`Formula_Normal`（基础工作台）/ `Formula_Blueprint`（高级工作台，默认）/ `Formula_Medic`（医疗台）/ `Formula_Cook`（厨房）/ `Formula_Printer`（打印台），或自定义标签。

### 3.6 创建子弹 / Bullets

```csharp
var bulletData = new BulletData
{
    itemId = 300001,
    localizationKey = "bullet_556",
    Caliber = "5.56x45",
    damageMultiplier = 1.2f,
    ArmorPiercingGain = 0.3f,
    ExplosionRange = 0f,
};
ItemUtils.CreateCustomBullet(new Identifier("mymod", "bullet_556"), bulletData);
```

### 3.7 TypeID 冲突自动处理 / TypeID Conflict

若 `itemId` 与已有物品（游戏原生或已注册）冲突，FML 从指定位置向后扫描（范围 +10000），无空闲则兜底从 90000 开始：

```csharp
// config.itemId = 150001，若被占用则扫描 150002~160001，不行再从 90000 起
ItemUtils.CreateCustomItem(new Identifier("mymod", "coffee"), itemData);
```

### 3.8 查询与卸载 / Query & Unregister

```csharp
// 按 Identifier 反查（推荐）
if (ItemUtils.TryGetCustomItem(new Identifier("mymod", "coffee"), out Item? item))
{
    // 找到物品
}

// 批量卸载
ItemUtils.UnregisterAllItem("mymod");
```

### 3.9 Sprite 加载 / Sprite Loading

**推荐使用异步版本**（加载阶段：文件 IO 在线程池执行，减少主线程阻塞）：

```csharp
// 异步加载（推荐：加载阶段用，IO 在线程池 + Texture2D 在主线程）
Sprite? icon = await ItemUtils.LoadSpriteAsync(
    new Identifier("mymod", "coffee_icon.png"));

// 便捷重载（需先 ModPathResolver.Register）：
// Sprite? icon = await ItemUtils.LoadSpriteAsync("coffee_icon.png");
```

同步版本（兼容保留，加载阶段不推荐）：

```csharp
// 同步加载（仅兼容旧代码，新项目请用异步版）
Sprite? icon = ItemUtils.LoadSprite(
    new Identifier("mymod", "coffee_icon.png"));
```

### 3.10 3D 展示模型 / ItemGraphic & Model

给物品挂 3D 展示模型有两条路径（**并存**，按模型复杂度选择）：

| 路径 | 适用场景 | 代价 |
|------|----------|------|
| **AssetBundle（既有）**：`ItemUtils.SetItemGraphic(item, bundle, name)` / `RegisterGun` | 枪械（手持挂点/动画）、多材质、FBX、粒子等复杂模型 | 需 Unity 工程制作 Bundle |
| **OBJ 简化路径（新增）**：`ItemGraphicUtils` + `ModelUtils` | 简单物品（单 mesh、单材质、无动画） | 零编辑器流程：OBJ 文件放 mod 目录即用 |

> **选型准则**：单 mesh + 单材质 → OBJ 简化路径；涉及多材质/动画/枪械挂点/复杂 socket → AssetBundle。
> 两条路径结果一致：都写入 `item.ItemGraphic`，原版掉落/装备/手持展示链路自动生效。

#### 3.10.1 目录约定与 OBJ 导出要求

- 模型文件放 mod 目录 `assets/models/`（可含子目录），如 `assets/models/apple.obj`
- 纹理放 `assets/textures/`（**建议与物品 sprite 隔离**，如 `assets/textures/models/apple.png`）
- OBJ 导出参数（Blender: File → Export → Wavefront OBJ）：**三角面（Triangulate）、Y-up**
- 支持关键字：`v` / `vt` / `vn` / `f`（含负索引、n 边形）；自动做右手→Unity 左手坐标变换与 UV 翻转
- **FBX 运行时导入不支持**（游戏内无 FBX SDK）——`.fbx` 会返回 null 并提示转 OBJ，或改用 AssetBundle 路径

#### 3.10.2 加载模型（ModelUtils）

```csharp
// 同步（加载阶段不推荐大模型）
Mesh? mesh = ModelUtils.LoadMesh(new Identifier("mymod", "apple.obj"));
// 便捷重载：ModelUtils.LoadMesh("apple.obj") —— modid 自动推导

// 异步（推荐：IO + 解析在线程池，主线程零阻塞）
Mesh? mesh = await ModelUtils.LoadMeshAsync(new Identifier("mymod", "apple.obj"));

// 材质（全局缓存：同 textureId 共享一个实例）
Material? mat = ModelUtils.GetModelMaterial(new Identifier("mymod", "models/apple"));  // 纹理读 assets/textures/models/apple.png
Material? defaultMat = ModelUtils.GetModelMaterial();  // 默认无纹理材质

// 纯组装（MeshFilter + MeshRenderer 成对，无碰撞体）
GameObject model = ModelUtils.CreateModel(mesh, mat);
```

> shader 取 `SodaCraft/SodaLit`（游戏物品主 shader），未命中自动降级 `Universal Render Pipeline/Lit`。
> 同一 Identifier 的 Mesh 有缓存，二次调用零 IO；`ReleaseModel(id)` / `ReleaseAllModels(modid)` 释放。

#### 3.10.3 构建 ItemGraphic 并绑定（ItemGraphicUtils）

```csharp
// 一步到位：构建 ItemGraphic + 绑定到物品（同步）
ItemGraphicUtils.SetItemGraphic(item, new Identifier("mymod", "apple.obj"));

// 异步版（推荐：加载阶段用）
await ItemGraphicUtils.SetItemGraphicAsync(item, new Identifier("mymod", "apple.obj"));

// 同模型 + 独立贴图（模型复用、材质隔离）
await ItemGraphicUtils.SetItemGraphicAsync(item,
    new Identifier("mymod", "apple.obj"),
    new Identifier("mymod", "models/apple_gold"));

// 单独构建 ItemGraphic GameObject（返回活动副本，可自由摆放/调整）
GameObject? graphic = await ItemGraphicUtils.CreateItemGraphicAsync(
    new Identifier("mymod", "apple.obj"));
```

构建出的 GameObject 同时包含 `ItemGraphicInfo` + `CharacterSubVisuals`：
- `CharacterSubVisuals.renderers` 仅 1 个元素（主模型的 Mesh Renderer）——挂到角色后自动跟随角色层的隐藏/显示
- 自动创建 `GroundPoint`（落地对齐锚点）与 `Model` 子物体（MeshFilter + MeshRenderer）

GO 模板按 `(meshId, textureId)` 缓存复用（对外返回副本，互不影响）；`ReleaseItemGraphic(meshId, textureId?)` / `ReleaseAllItemGraphics(modid)` 释放。

#### 3.10.4 复用原版物品模型 / Reuse Vanilla Model

不加载外部模型，直接让物品使用指定原版物品的 3D 展示（纯引用赋值，无 IO）：

```csharp
// 从 displayName 发现原版物品 Identifier
if (GameItemLookup.TryGetIdentifier("AK-47", out var originalId))
{
    // 复用 AK-47 的 ItemGraphic（sockets / ShowIf / HideIf / 材质全量生效）
    ItemGraphicUtils.SetItemGraphicFromOriginal(item, originalId);
}

// 或直接构造 duckov 域 Identifier
ItemGraphicUtils.SetItemGraphicFromOriginal(item, new Identifier("duckov", "AK-47"));
```

> 原版物品无 3D 图形（纯 Sprite 物品）时输出 Warning 且不赋值。此方法同时兼容引用其它 mod 已注册的物品（查询顺序：FML 注册表 → 原版反查表）。

---

## 4. 合成系统 / Crafting

🔗 [API_CRAFTING.md](API/API_CRAFTING.md) | 入口类：`CraftingUtils`

### 4.1 物品引用（ItemEntry）/ Item Reference

`ItemEntry` 同时支持 Identifier 和 int typeID，可在同一数组中混合使用：

```csharp
// 原版物品（纯 typeID）
ItemEntry.Of(1001, 5)

// 框架物品（Identifier）
ItemEntry.Of(new Identifier("mymod", "coffee"), 10)

// 字符串快捷方式
ItemEntry.Of("mymod:coffee", 10)
```

### 4.2 添加合成配方 / Add Crafting Formula

```csharp
// struct 方式（推荐）
CraftingUtils.AddCraftingFormula(new CraftingFormulaData
{
    Id = new Identifier("mymod", "coffee"),
    Money = 100,
    CostItems = new[] {
        ItemEntry.Of(1001, 5),                       // 原版物品
        ItemEntry.Of("mymod:beans", 2)               // 框架物品
    },
    Result = ItemEntry.Of("mymod:coffee", 10),
    Tags = new[] { "WorkBenchAdvanced" },
    RequirePerk = new Identifier("duckov", "hacker/cooking")
});

// Builder 方式
CraftingUtils.AddCraftingFormula(
    CraftingFormulaData.Builder
        .Create("mymod:coffee")
        .Money(100)
        .AddCost(1001, 5)
        .AddCost("mymod:beans", 2)
        .Result("mymod:coffee", 10)
        .Tags("WorkBenchAdvanced")
        .Build());
```

### 4.3 添加分解配方 / Decompose

```csharp
// struct 方式（推荐）
CraftingUtils.AddDecomposeFormula(new DecomposeFormulaData
{
    Id = new Identifier("mymod", "scrap_old_gun"),
    SourceItemId = new Identifier("mymod", "old_gun"),  // 被分解物品
    Money = 50,
    ResultItems = new[] {
        ItemEntry.Of(1001, 3),
        ItemEntry.Of(1002, 1)
    }
});

// Builder 方式
CraftingUtils.AddDecomposeFormula(
    DecomposeFormulaData.Builder
        .Create("mymod:scrap_old_gun")
        .Source("mymod:old_gun")
        .Money(50)
        .AddResult(1001, 3)
        .AddResult(1002, 1)
        .Build());
```

### 4.4 卸载配方 / Unregister Formulas

```csharp
CraftingUtils.RemoveAllAddedFormulas("mymod");
CraftingUtils.RemoveAllAddedDecomposeFormulas("mymod");
```

### 4.5 标签匹配与耐久折算 / Tag Match & Durability Cost

`ItemEntry.ByTag()` 和 `WithDurabilityCost()` 支持按物品**标签**（而非精确 typeID）匹配合成成本：

```csharp
CraftingUtils.AddCraftingFormula(new CraftingFormulaData
{
    Id = new Identifier("mymod", "repair_kit"),
    Money = 200,
    CostItems = new[] {
        ItemEntry.ByTag("Armor", 1)           // 匹配任意"Armor"标签物品 ×1
            .WithDurabilityCost(true),         // 按耐久度折算消耗量
        ItemEntry.Of("Metal", 10)
    },
    Result = ItemEntry.Of("mymod:repair_kit", 1),
});
```

> `WithDurabilityCost(true)` 启用后，满耐久度物品 = 1 个，50% 耐久度 = 0.5 个。
> FML 内部通过 `TagCostRegistry` + `TagCostValidator` + `CraftingManagerPatch` 自动拦截合成流程。

---

## 5. 任务系统 / Quests

🔗 [API_QUESTS.md](API/API_QUESTS.md) | 入口类：`QuestUtils` / `QuestGiverUtils`

### 5.1 注册任务 / Register Quest

FML 提供 6 种任务类型和 6 种奖励类型：

```csharp
var questData = new QuestData
{
    // ID 由 Identifier 管理（不再使用数字 ID）
    Id = new Identifier("mymod", "coffee_run"),
    displayName = "quest_coffee_run",
    description = "quest_coffee_run_desc",
    questGiver = QuestGiverID.Fence,
    // 🆕 或使用 QuestGiverIdentifier 绑定自定义 QuestGiver（推荐）
    QuestGiverIdentifier = new Identifier("mymod", "daily_giver"),
    requireLevel = 5,
    tasks = new List<TaskData>
    {
        new TaskRequireItem
        {
            itemIdentifier = new Identifier("mymod", "coffee"),
            requiredAmount = 5
        },
        new TaskKillCount
        {
            requireAmount = 10,
            requireEnemy = "Scav"
        }
    },
    rewards = new List<RewardData>
    {
        new RewardMoney { amount = 5000 },
        new RewardEXP { amount = 200 },
        new RewardUnlockEndowmentData
        {
            endowmentId = new Identifier("mymod", "assassin")
        },
        new RewardUnlockBuildingData
        {
            buildingId = new Identifier("mymod", "bounty_shop"),
            buildingInfo = new BuildingInfo
            {
                id = "bounty_shop",
                prefabName = "Building_Workbench",
                maxAmount = 1,
                dimensions = new Vector2Int(3, 3),
                cost = new BuildingCost { money = 5000 }
            },
            prefabName = "Building_Workbench"
        }
    }
};

// Identifier 方式（推荐）—— domain 自动推导为 owner modid
QuestUtils.RegisterQuest(new Identifier("mymod", "coffee_run"), questData);
```

**可用 TaskData**：

| 类 | 用途 | 关键属性 |
|----|------|----------|
| `TaskRequireItem` | 提交物品 | `itemIdentifier` (Identifier?), `requiredAmount` |
| `TaskRequireMoney` | 提交金钱 | `money` |
| `TaskRequireUseItem` | 使用物品 | `itemIdentifier` (Identifier?), `amount` |
| `TaskKillCount` | 击杀目标 | `requireAmount`, `weaponIdentifier` (Identifier?), `requireEnemy`, `requireHeadshot` |
| `TaskKillByTagData` | 按武器标签击杀 | `weaponTag`, `requireAmount`, `requireEnemyName`, `requireHeadShot` |
| `TaskSubmitItemByTagData` | 按标签提交物品 | `itemTag`, `requireAmount`, `minQuality`, `durabilityCost` |

**可用 RewardData**：

| 类 | 用途 | 关键属性 |
|----|------|----------|
| `RewardGiveItem` | 给予物品 | `itemIdentifier` (Identifier?), `amount` |
| `RewardEXP` | 给予经验 | `amount` |
| `RewardMoney` | 给予金钱 | `amount` |
| `RewardUnlockItem` | 解锁商店物品 | `itemIdentifier` (Identifier?) |
| `RewardUnlockEndowmentData` | 自动解锁天赋 | `endowmentId` (Identifier) |
| `RewardUnlockBuildingData` | 自动解锁建筑 | `buildingId`, `buildingInfo`, `prefabName` |

> **数字 ID 全自分配 + 冲突检测**：`QuestData.ID`（从 1000 起递增）、`TaskData.id`、`RewardData.id`（从 1 起递增）均由 FML 在注册时自动分配，带**冲突检测**——若候选 ID 已被原生游戏任务或已注册 FML 任务占用，自动递增至空闲位置。modder 无需手动设置。
> `RewardUnlockEndowmentData` 在任务完成时自动解锁指定天赋（AutoClaim），无需 modder 手动处理解锁逻辑。
>
> **QuestGiverIdentifier 自动绑定**：设置 `QuestData.QuestGiverIdentifier` 后，`RegisterQuest` 时自动将任务绑定到指定的自定义 QuestGiver（通过 `QuestGiverUtils.RegisterQuestGiver` 注册），无需额外调用 `BindQuest`。`questGiver`（原生枚举）仍可用于引用游戏原生任务发放者，两者兼容。

### 5.2 任务关系图 / Quest Relations

> **⚠️ 重要约束**：注册任务后，modder **必须手动调用 `AddQuestRelation`** 才能在游戏中正确管理任务的前后置关系。
> `RegisterQuest` 只负责将任务登入 `QuestCollection`，**不会自动建立关系图**。
> 如果忘记调用 `AddQuestRelation`，任务将不会出现在任何前置/后续任务的关联中，导致任务链断裂、后续任务无法解锁。

```csharp
// 设置任务前置/后置关系（Identifier 版本）
var current = new Identifier("mymod", "coffee_run");
var preReq  = new Identifier("mymod", "intro_quest");
var unlocks = new Identifier("mymod", "next_quest");

QuestUtils.AddQuestRelation(current, before: preReq, after: unlocks);
//                            ↑ 当前任务      ↑ 前置任务       ↑ 后置任务（完成当前后解锁）

// 也可省略前后置（只有前置或只有后置）：
QuestUtils.AddQuestRelation(current, before: preReq);      // 仅设置前置
QuestUtils.AddQuestRelation(current, after: unlocks);      // 仅设置后置
```

### 5.3 任务 ID 反查 / ID Lookup

```csharp
// 数字 ID → Identifier（O(1) 反查，通过内部反向索引）
if (QuestUtils.TryGetQuestIdentifier(1001, out var id))
    QuestUtils.UnregisterQuest(id);

// Identifier → 数字 ID（需传给游戏原生 API 时）
if (QuestUtils.TryGetQuestId(id, out var questId))
    QuestUtils.AddQuestRelation(questId, 1002);
```

### 5.4 卸载任务 / Unregister

```csharp
// 按 Identifier 移除单个任务
QuestUtils.UnregisterQuest(new Identifier("mymod", "coffee_run"));

// 批量卸载
QuestUtils.UnregisterQuestAll("mymod");
```

### 5.5 自定义 QuestGiver / Custom QuestGiver

游戏原生 `QuestGiverID` 是固定枚举。`QuestGiverUtils` 提供自定义 QuestGiver ID 注册和交互点创建。自定义 ID 从 **50** 起分配，与原生枚举值（0~11）无冲突。

> **设计原则**：QuestGiver 是纯交互层——仅管理 questGiverID 映射和交互点组件。模型、捏脸、对话角色等显示层属性由 `FriendlyNpcUtils` 管理，两者通过 `BindQuestGiver` 关联。

```csharp
// 1. 注册并分配自定义 questGiverID（int，从 50 起）
QuestGiverUtils.RegisterQuestGiver(new Identifier("mymod", "daily_giver"));

// 2. 在世界空间创建独立的 QuestGiver 交互点
var qgGo = QuestGiverUtils.CreateQuestGiver(
    new Identifier("mymod", "daily_giver"),
    position: new Vector3(20f, 0f, 10f),
    spawnPOI: true);

// 3. 任务可以随时绑定到 QuestGiver
QuestGiverUtils.BindQuest(
    new Identifier("mymod", "daily_giver"),
    new Identifier("mymod", "daily_01"));

// 4. 查询与卸载
if (QuestGiverUtils.TryGetQuestGiverId(new Identifier("mymod", "daily_giver"), out int id))
    Debug.Log($"Custom ID: {id}");

QuestGiverUtils.UnregisterQuestGiver(new Identifier("mymod", "daily_giver"));
QuestGiverUtils.UnregisterAllQuestGivers("mymod");
```

> 挂载到 FriendlyNPC：`FriendlyNpcConfig` 中设置 `Role = NpcRole.QuestGiver` + `QuestGiverId = new Identifier("mymod", "laozheng")`，生成时自动绑定（见 §13）。

---

## 6. 商店系统 / Shop

🔗 [API_SYSTEM.md](API/API_SYSTEM.md#shoputils) | 入口类：`ShopUtils`

### 6.1 注册商品 / Add Goods

```csharp
// 使用 itemIdentifier（Identifier 方式，推荐）
ShopUtils.AddGoods(new ShopGoodsData
{
    merchantProfileID = "Merchant_Normal",
    itemIdentifier = new Identifier("mymod", "coffee"),  // 物品 Identifier，优先解析
    typeID = 150001,                                      // 回退 typeID（itemIdentifier 解析失败时使用）
    maxStock = 10,
    priceFactor = 1.0f
}, "mymod");
```

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `merchantProfileID` | `string` | `"Merchant_Normal"` | 商人 profile 名称 |
| `typeID` | `int` | — | 物品 TypeID（`itemIdentifier` 未设置或解析失败时使用） |
| `itemIdentifier` | `Identifier?` | `null` | 物品 Identifier。设置后优先解析为 typeID |
| `maxStock` | `int` | `0` | 最大库存量 |
| `forceUnlock` | `bool` | `false` | 是否强制解锁 |
| `priceFactor` | `float` | `1F` | 价格倍率 |
| `possibility` | `float` | `1F` | 出现概率 |

### 6.2 查询与编辑 / Query & Edit

```csharp
// 查询商人全部商品
IReadOnlyList<ShopGoodsData> allGoods = ShopUtils.GetAllGoods("Merchant_Normal");
var goods = ShopUtils.GetAllGoods(new Identifier("mymod", "Merchant_Drink"));

// 按 Identifier 编辑（推荐）
ShopUtils.EditGoods(new Identifier("mymod", "coffee"), new ShopGoodsData
{
    maxStock = 20,
    priceFactor = 1.5f
});

// 查询商人 profile 是否存在
if (ShopUtils.TryGetMerchantProfile(new Identifier("mymod", "Merchant_Drink"), out var profile))
    Debug.Log($"Merchant found: {profile.merchantID}");
```

### 6.3 移除与创建商人 / Remove & Create Merchant

```csharp
// 移除单个商品（按 Identifier，推荐）
ShopUtils.RemoveGoods(new Identifier("mymod", "coffee"));

// 移除指定商人下的所有 FML 注册商品
ShopUtils.RemoveAllGoods("Merchant_Normal");

// 按 mod 批量卸载商品 / 商人 profile
ShopUtils.UnregisterAllGoods("mymod");
ShopUtils.RemoveAllProfiles("mymod");

// 创建新商人（Identifier 方式：Path 作为 merchantID，Domain 作为 modid）
ShopUtils.CreateMerchantProfile(new Identifier("mymod", "Merchant_Drink"));
```

---

## 7. 经济系统 / Economy

🔗 [API_SYSTEM.md](API/API_SYSTEM.md#economyutils) | 入口类：`EconomyUtils`

```csharp
// 查询金钱
long money = EconomyUtils.GetMoney();

// 增删
EconomyUtils.AddMoney(1000);
EconomyUtils.RemoveMoney(500);

// 直接设置
EconomyUtils.SetMoney(5000);

// 解锁物品（仅 Identifier 版本公开）
EconomyUtils.UnlockItem(new Identifier("mymod", "coffee"));
EconomyUtils.UnlockItem(new Identifier("mymod", "coffee"), needConfirm: false, showUI: true);

// 查询解锁状态
bool unlocked = EconomyUtils.IsItemUnlocked(new Identifier("mymod", "coffee"));

// 物品解锁确认流程（needConfirm = true 时使用）
EconomyUtils.ConfirmUnlockItem(new Identifier("mymod", "coffee"));
if (EconomyUtils.IsItemWaitingForUnlockConfirm(new Identifier("mymod", "coffee")))
{
    Debug.Log("Item is pending confirm...");
}

// 订阅金钱变化
EconomyUtils.OnMoneyChanged(handler);
EconomyUtils.OnItemUnlockStateChanged(e => {
    Debug.Log($"Item unlock state changed.");
});

// 简化版回调
EconomyUtils.RegisterMoneyChangedCallback((oldMoney, nowMoney) =>
{
    Debug.Log($"Money changed: {oldMoney} → {nowMoney}");
});
```

---

## 8. Buff 状态 / Buffs

🔗 [API_SYSTEM.md](API/API_SYSTEM.md#buffutils) | 入口类：`BuffUtils`

```csharp
// 注册自定义 Buff（modid 从 id.Domain 自动推导）
BuffUtils.RegisterBuff(
    new Identifier("mymod", "mybuff"),
    buffPrefab  // Buff 预制体
);

// 按 ID 反查 Buff Identifier（自定义 + 游戏内置）
if (BuffUtils.TryGetBuffIdentifier(buffID, out var buffId))
{
    // buffId = Identifier("duckov", buffName) 或自定义 buff 的 Identifier
}

// 移除单个 Buff
BuffUtils.UnregisterBuff(new Identifier("mymod", "mybuff"));

// 批量卸载
BuffUtils.UnregisterAllBuffs("mymod");
```

---

## 9. 建筑系统 / Building

🔗 [API_BUILDING.md](API/API_BUILDING.md) | 入口类：`BuildingUtils` / `MachineRecipe` / `BuildingBehaviour`

### 9.1 快速开始 / Quick Start

```csharp
// 一行注册：纯代码创建 2×2 工作台，花费 5000 金币 + 20 根原木
BuildingUtils.RegisterBuilding(new BuildingConfig
{
    Id = new Identifier("mymod", "workshop"),
    Money = 5000,
    CostItems = new[] { ItemEntry.Of("duckov:Wood", 20) }
});
```

> `RegisterBuilding(BuildingConfig)` 自动完成四件事：创建 Building Prefab（Cube 模型 + 碰撞体）→ 构建 `BuildingInfo` → 解析 Identifier 为 TypeID → 写入游戏和 FML 双注册表。

### 9.2 BuildingConfig 完整配置 / Full Config

```csharp
BuildingUtils.RegisterBuilding(new BuildingConfig
{
    // ── 必填 ──
    Id = new Identifier("mymod", "forge"),          // Identifier（domain=modid, path=建筑名）

    // ── 尺寸与外观 ──
    Dimensions = new Vector2Int(3, 3),               // 占地 3×3 网格（默认 2×2）
    PrefabName = "Building_Forge",                   // prefab 名称（用于注册标识）
    ExistingPrefabName = "Building_Workbench",        // 可选：克隆游戏已有建筑结构

    // ── 成本（走 FML ItemEntry） ──
    Money = 5000,                                    // 金币
    CostItems = new[]
    {
        ItemEntry.Of("duckov:Iron", 20),             // 精确物品
        ItemEntry.Of("duckov:Stone", 10),
        ItemEntry.ByTag("Wood", 30),                 // 标签匹配：任意木制品
        ItemEntry.ByTag("Food", 5, minQuality: 3)    // 标签 + 最低品质
    },

    // ── 数量与解锁 ──
    MaxAmount = 2,                                   // 最多同时建造 2 个（默认 1）
    UnlockedByDefault = false,                       // 需任务解锁？
    RequireBuildings = new[] { new Identifier("mymod", "workshop") },  // 前置建筑
    RequireQuests = new[] { new Identifier("mymod", "quest_intro") }   // 前置任务
});
```

> 注：`RequireBuildings` / `RequireQuests` 接受 `Identifier[]`。

### 9.3 三种注册模式 / Registration Modes

#### 模式 A：全自动（零 Unity 依赖）

```csharp
// 自动生成 Cube 模型 + 碰撞体，适合快速原型
BuildingUtils.RegisterBuilding(new BuildingConfig
{
    Id = new Identifier("mymod", "storage"),
    Dimensions = new Vector2Int(2, 2),
    Money = 1000,
    CostItems = new[] { ItemEntry.Of("duckov:Wood", 10) }
});
```

#### 模式 B：克隆游戏原生建筑结构

```csharp
// 保留原建筑的 graphics/function 容器布局，替换模型
BuildingUtils.RegisterBuilding(new BuildingConfig
{
    Id = new Identifier("mymod", "advanced_bench"),
    Dimensions = new Vector2Int(3, 2),
    ExistingPrefabName = "Building_Workbench",  // ← 克隆原生工作台的容器结构
    Money = 3000,
    CostItems = new[] { ItemEntry.Of("duckov:Iron", 15) }
});
```

#### 模式 C：AssetBundle 自定义模型

```csharp
// 1. 加载 AssetBundle
var bundle = AssetUtil.LoadBundle("my_buildings");
var modelPrefab = bundle.LoadAsset<GameObject>("Building_Forge_Model");

// 2. 注册建筑（自动创建外壳 + 注册 + 注入模型）
BuildingUtils.RegisterBuilding(new BuildingConfig
{
    Id = new Identifier("mymod", "forge"),
    Dimensions = new Vector2Int(3, 3),
    Money = 5000,
    CostItems = new[] { ItemEntry.Of("duckov:Iron", 20) },
    PrefabName = "Building_Forge"
});

// 3. 注入自定义模型（替换 graphicsContainer 下的默认 Cube）
BuildingUtils.SetBuildingModel(
    new Identifier("mymod", "forge"),
    modelPrefab);
```

> **顺序要求**：`RegisterBuilding` → `SetBuildingModel`。后者依赖 Registry 中已存在的条目。

#### 模式 D：手动创建 Prefab + 原始 API

```csharp
// 用 CreateSimpleBuilding 手动创建 prefab
var prefab = BuildingUtils.CreateSimpleBuilding(
    new Identifier("mymod", "lab"),
    new Vector2Int(4, 3),
    existingPrefabName: "Building_Workbench");

// 用原始 API 注册（传入自定义 BuildingInfo）
var info = new BuildingInfo
{
    id = "lab",
    prefabName = prefab.name,
    maxAmount = 1,
    cost = BuildingUtils.CreateCost(8000,
        ItemEntry.Of("duckov:Electronics", 5))
};
BuildingUtils.RegisterBuilding(
    new Identifier("mymod", "lab"), info, prefab);
```

### 9.4 放置、查询与卸载 / Place, Query & Unregister

```csharp
// ===== 放置建筑 =====
BuildingUtils.PlaceBuilding(
    new Identifier("base", "area1"),         // 区域
    new Identifier("mymod", "forge"),        // 建筑
    new Vector2Int(5, 3),                     // 坐标
    BuildingRotation.Rot90                    // 旋转（Rot0 / Rot90 / Rot180 / Rot270）
);

// ===== 查询 =====
BuildingInfo? info = BuildingUtils.GetBuildingInfo(
    new Identifier("mymod", "forge"));
IReadOnlyList<Identifier> allIds = BuildingUtils.GetAllBuildingIds();
Building? prefab = BuildingUtils.GetBuildingPrefab(
    new Identifier("mymod", "forge"));

// ===== 成本查询与校验 =====
Cost? cost = BuildingUtils.GetBuildingCost(
    new Identifier("mymod", "forge"));
if (BuildingUtils.CanAffordBuilding(new Identifier("mymod", "forge")))
    Debug.Log("资源充足！");

// ===== 卸载 =====
BuildingUtils.UnregisterBuilding(new Identifier("mymod", "forge"));
BuildingUtils.UnregisterAllBuildings("mymod");
```

> `PlaceBuilding` 内部自动扣费，一般无需手动 `SpendBuildingCost`。

### 9.5 建造完成回调 / Build Callbacks

```csharp
private Action<Building>? _onBuiltCallback;  // 保存引用以便取消

void RegisterCallbacks()
{
    _onBuiltCallback = building =>
    {
        // 建筑建成后自动生成 NPC 商人
        FriendlyNpcUtils.RegisterFriendlyNpc(
            new Identifier("mymod", "merchant_forge"),
            new FriendlyNpcConfig
            {
                Role = NpcRole.Merchant,
                ActorId = "merchant_alex",
                SpawnPosition = building.transform.position + new Vector3(2f, 0f, 0f),
                Face = FaceRef.Preset("Default"),
                ShopId = "mymod:forge_shop"
            });
    };

    BuildingUtils.OnBuildingBuilt(
        new Identifier("mymod", "forge"),
        _onBuiltCallback);
}

// 取消回调
void UnregisterCallbacks()
{
    if (_onBuiltCallback != null)
        BuildingUtils.OffBuildingBuilt(
            new Identifier("mymod", "forge"),
            _onBuiltCallback);
}

// ── 建筑回收回调 ──
void SetupDemolishCleanup()
{
    BuildingUtils.OnBuildingDemolished(
        new Identifier("mymod", "forge"),
        building =>
        {
            // 清理建筑中生成的 NPC
            FriendlyNpcUtils.RemoveNpc(new Identifier("mymod", "merchant_forge"));
        });
}
```

### 9.6 Building Prefab 结构标准 / Prefab Structure

游戏原生的 `Building` 组件要求 Prefab 包含以下层级：

```
Building_XXX (GameObject)
├── Building (MonoBehaviour)         ← id, dimensions, graphicsContainer, functionContainer
├── Graphics (GameObject)            ← graphicsContainer：纯视觉层（3D 模型、材质、渲染）
│   ├── Model_XXX (MeshFilter + MeshRenderer)
│   └── (可选) Collider（放置预览时自动禁用）
└── Function (GameObject)            ← functionContainer：交互层
    ├── BoxCollider (isTrigger=true) ← 交互点击检测 + 占地碰撞
    └── areaMesh                     ← 运行时自动生成
```

| 容器 | 用途 | 碰撞体规范 |
|------|------|-----------|
| `graphicsContainer` | 展示用 3D 模型，纯视觉 | Collider 可选；放置时自动禁用 |
| `functionContainer` | 交互检测 + 格子占位 | **必须**有 BoxCollider(isTrigger=true) |

**模型 Prefab 规格**（`SetBuildingModel` 注入的纯视觉 Prefab）：根节点单个 GameObject **无** `Building` 组件；Transform 强制 (0,0,0)/(0,0,0)/(1,1,1)；材质游戏原生 Shader（`ShaderReplacer.ApplyTo()` 自动修复）；**严禁**放 Collider；尺寸 1 单位 ≈ 1 米；放 `assets/bundle/`。

### 9.7 MachineRecipe — 建筑设备配方 / Machine Recipes

MachineRecipe 是建筑设备的"配方"——区别于 `CraftingFormula`（玩家手动合成），MachineRecipe 由建筑**自动执行**，从子库存读取物品，产出产物到子库存或主库存。

#### 抽象基类

```csharp
public abstract class MachineRecipe
{
    public Identifier Id;                                    // 合成表标识

    // ── modder 覆写 ──
    public abstract bool CanExecute();                       // 槽位满足条件？
    public abstract void Execute();                          // 执行配方
    public virtual float GetProgress() => 0f;                // 进度（0~1），用于 UI 进度条
    public virtual bool IsRunning => false;                  // 是否正在生产

    // ── 自动存档：modder 无需覆写任何序列化方法 ──
    protected void SetState<T>(string key, T value);         // 存状态
    protected T GetState<T>(string key, T defaultValue);     // 取状态

    // ── 运行时引用（由 BuildingSlotsWatcher 注入） ──
    protected Inventory MainInventory;
    protected IReadOnlyDictionary<string, Inventory> SubInventories;
}
```

**关键**：`SetState<T>` / `GetState<T>` 存取的所有值自动参与存档序列化。modder 无需手写 `SerializeState` / `DeserializeState`。

#### 内置 SimpleMachineRecipe（声明式）

覆盖 80% 场景：

```csharp
var recipe = new SimpleMachineRecipe
{
    Id = new Identifier("mymod", "brew_coffee"),
    Inputs = new[]
    {
        new MachineInput { FromSubKey = "water", ItemId = new Identifier("duckov", "Water"), Amount = 1 },
        new MachineInput { FromSubKey = "beans", ItemId = new Identifier("duckov", "CoffeeBean"), Amount = 2 }
    },
    Outputs = new[]
    {
        new MachineOutput { ToSubKey = "output", ItemId = new Identifier("mymod", "coffee_cup"), Amount = 1 }
    },
    DurationSeconds = 300f, // 5 游戏分钟
};
```

| 类型 | 关键字段 | 说明 |
|------|----------|------|
| `MachineInput` | `FromSubKey` / `ItemId` / `Amount` / `Consume`(默认 true) | 输入（Consume=false = 仅检测，如"发电机只需有电"） |
| `MachineOutput` | `ToSubKey` / `ItemId` / `Amount` / `Chance`(默认 1.0f) | 产物（ToSubKey=null → 主库存）；`Byproducts` 为概率副产品 |
| `DurabilityCost` | `SubKey` / `DurabilityPerCycle` | 每周期耐久消耗 |

#### 自定义 MachineRecipe（复杂逻辑）

```csharp
public class GpuMiningRecipe : MachineRecipe
{
    public override bool CanExecute()
    {
        // 至少有一个 GPU 槽有 GPU
        return SubInventories["gpu_slots"].Content.Any(i => i != null && i.HasTag("GPU"));
    }

    public override void Execute()
    {
        float totalPower = 0f;
        foreach (var gpu in SubInventories["gpu_slots"].Content)
        {
            if (gpu == null) continue;
            totalPower += gpu.Modifiers.Find(m => m.Key == "ComputingPower")?.Value ?? 1f;
        }

        // 按算力生成 CatCoin
        int coins = Mathf.FloorToInt(totalPower * 1.5f);
        var coin = ItemAssetsCollection.InstantiateSync(20480509);
        coin.StackCount = coins;
        MainInventory.AddAndMerge(coin);

        // 🔑 自动存档：modder 无需写任何序列化代码
        SetState("accumulated", GetState<float>("accumulated") + totalPower);
    }

    public override float GetProgress()
        => GetState<float>("accumulated") / 100f;
}
```

### 9.8 ConfigureBuildingUI — 建筑 UI 自定义 / Building UI

声明式配置建筑的 DetailsView 布局，包括多 Machine、子库存、进度条和按钮。所有 UI 元素继承游戏原生风格。

```csharp
BuildingUtils.RegisterBuilding(new BuildingConfig
{
    Id = new Identifier("mymod", "kitchen_station"),
    Dimensions = new Vector2Int(2, 2),
    Money = 8000
});

BuildingUtils.ConfigureBuildingUI(
    new Identifier("mymod", "kitchen_station"),
    new BuildingUIConfig
    {
        DisplayName = "厨房工作站",
        Machines = new[]
        {
            // Machine 1: 咖啡机（默认解锁）
            new MachineDef
            {
                MachineKey = "coffee_maker",
                DisplayName = "咖啡机",
                UnlockedByDefault = true,
                SubInventories = new[]
                {
                    new SubInventoryDef { SubKey = "water",  DisplayName = "水箱",   SlotCount = 1, SlotTags = new[] { "Water" } },
                    new SubInventoryDef { SubKey = "beans",  DisplayName = "咖啡豆", SlotCount = 1, SlotTags = new[] { "CoffeeBean" } },
                    new SubInventoryDef { SubKey = "output", DisplayName = "出品",   SlotCount = 3, ReadOnly = true },
                },
                Recipe = new SimpleMachineRecipe
                {
                    Id = new Identifier("mymod", "brew_coffee"),
                    Inputs = new[]
                    {
                        new MachineInput { FromSubKey = "water", ItemId = new Identifier("duckov", "Water"), Amount = 1 },
                        new MachineInput { FromSubKey = "beans", ItemId = new Identifier("duckov", "CoffeeBean"), Amount = 2 }
                    },
                    Outputs = new[]
                    {
                        new MachineOutput { ToSubKey = "output", ItemId = new Identifier("mymod", "coffee_cup"), Amount = 1 }
                    },
                    DurationSeconds = 300f,
                },
            },

            // Machine 2: 烤面包机（需解锁 "mymod:perk_toast_master" Perk）
            new MachineDef
            {
                MachineKey = "toaster",
                DisplayName = "烤面包机",
                UnlockedByDefault = false,
                RequiredPerk = new Identifier("mymod", "perk_toast_master"),
                SubInventories = new[]
                {
                    new SubInventoryDef { SubKey = "bread",  DisplayName = "面包", SlotCount = 2, SlotTags = new[] { "Bread" } },
                    new SubInventoryDef { SubKey = "output", DisplayName = "出品", SlotCount = 2, ReadOnly = true },
                },
                Recipe = new SimpleMachineRecipe { /* ... */ },
            }
        }
    },
    "mymod"
);
```

**核心 DTO**：

| 类型 | 字段 | 说明 |
|------|------|------|
| `BuildingUIConfig` | `DisplayName` / `Machines` | 主面板 |
| `MachineDef` | `MachineKey` / `DisplayName` / `UnlockedByDefault` / `RequiredPerk` / `SubInventories` / `Recipe` / `ProgressBars` / `Buttons` | 单个机器（Perk 门控） |
| `SubInventoryDef` | `SubKey` / `DisplayName` / `SlotCount`(默认 4) / `SlotTags` / `ReadOnly` | 子库存 |
| `ProgressBarDef` | `Label` / `GetProgress`(Func\<float\>) | 进度条 |
| `BuildingButtonDef` | `Label` / `OnClick`(Action\<Inventory\>?) | 按钮 |

#### RegisterMachineRecipe — 运行时动态挂载

```csharp
// Perk 解锁后动态挂载 Machine
BuildingUtils.RegisterMachineRecipe(
    new Identifier("mymod", "kitchen_station"),   // buildingId
    "juicer",                                      // machineKey
    new SimpleMachineRecipe { /* ... */ },         // recipe
    "mymod"
);

// 移除
BuildingUtils.UnregisterMachineRecipe(
    new Identifier("mymod", "kitchen_station"),
    "juicer"
);
```

### 9.9 BuildingBehaviour — 建筑行为组件 / Building Behaviour

与 `PerkBehaviour` 模式一致的 MonoBehaviour 抽象基类。modder 继承此基类实现自定义建筑运行时逻辑。

```csharp
public class MyBuildingLogic : BuildingBehaviour
{
    public override void OnBuildingPlaced() { }     // 建筑放置到场景
    public override void OnBuildingDemolished() { } // 建筑拆除
}

// 挂载
BuildingUtils.AttachBehaviour<MyBuildingLogic>(new Identifier("mymod", "forge"));
```

### 9.10 TimeUtils — 游戏时间工具 / Game Time

提供 GameClock 访问和时间差计算，用于建筑设备的离线进度计算。

```csharp
// 获取当前游戏内时间
TimeSpan now = TimeUtils.Now;

// 序列化/反序列化（用于 Item.SetString 持久化时间戳）
string timestamp = TimeUtils.NowAsString();
TimeUtils.TryStringToTimeSpan(timestamp, out var restored);

// 计算时间差（正数，不受真实时间影响）
float hoursPassed = TimeUtils.GetPositiveHoursSince(pastTime);
float secondsPassed = TimeUtils.GetPositiveSecondsSince(pastTime);
```

---

## 10. Perk 技能树 / Perk Trees

🔗 [API_PERK_ENDOWMENT.md](API/API_PERK_ENDOWMENT.md) | 入口类：`PerkTreeUtils`

```csharp
// ===== 注册完整 PerkTree =====

// 注册一棵自定义技能树
PerkTreeUtils.RegisterPerkTree(
    new Identifier("mymod", "combat_perks"),  // Domain=modid, Path=treeID
    horizontal: false
);

// ===== 添加 Perk（treeId + PerkConfig） =====

// 往自定义树添加 Perk
PerkTreeUtils.AddPerk(
    new Identifier("mymod", "combat_perks"),     // treeId
    new PerkConfig
    {
        PerkId          = new Identifier("mymod", "ExtraHealth"),
        Icon            = myIcon,
        DisplayNameKey  = "Perk_ExtraHealth",
        RequiredLevel   = 5,
        CostItems       = new[] { ItemEntry.Of("duckov:GoldCoin", 1000) },
        Money           = 500,
        RequireTimeTicks = TimeSpan.FromMinutes(30).Ticks,
        RequiredPerks   = new[] { new Identifier("mymod", "BasicTraining") }
    }
);

// 往原版树注入 Perk（treeId.Domain = "duckov"）
PerkTreeUtils.AddPerk(
    new Identifier("duckov", "CombatTree"),      // 原版树
    new PerkConfig
    {
        PerkId         = new Identifier("mymod", "RapidFire"),
        RequiredLevel  = 10,
        CostItems      = new[] { ItemEntry.Of("duckov:Ammo_556", 200) },
        RequireTimeTicks = TimeSpan.FromHours(1).Ticks,
        // 原版 Perk 作为前置：Domain="duckov", Path="treeID/perkName"
        RequiredPerks  = new[] { new Identifier("duckov", "CombatTree/Marksman") }
    }
);

// ===== 建立前置关系 =====

// 自定义 Perk 互连 / 跨 mod 连接 / 连接原版 Perk（首次引用自动懒注册）
PerkTreeUtils.ConnectPerks(
    new Identifier("mymod", "ExtraHealth"),
    new Identifier("mymod", "IronWill")
);

// ===== 挂载 Behaviour =====

// 方式 A：PerkConfig 声明式（推荐，7 种原版 Behaviour 有 FML 封装）
PerkTreeUtils.AddPerk(
    new Identifier("mymod", "combat_perks"),
    new PerkConfig
    {
        PerkId = new Identifier("mymod", "StorageMaster"),
        Behaviours = new PerkBehaviourConfig[]
        {
            new AddPlayerStorageConfig { Capacity = 100 },
            new UnlockFormulaConfig(),    // 自动解锁 requirePerk 匹配的配方
            new UnlockAchievementConfig { AchievementKey = "STORAGE_MASTER" },
            new ModifyStatsConfig
            {
                Entries = new[]
                {
                    new StatModifierEntry { Key = "MaxHealth", Value = 25, Percentage = false },
                    new StatModifierEntry { Key = "MoveSpeed", Value = 0.1f, Percentage = true }
                }
            }
        }
    });

// 方式 B：自定义 PerkBehaviour 走泛型 API
MyPerkBehaviour behaviour = PerkTreeUtils.AddPerkBehaviour<MyPerkBehaviour>(
    new Identifier("mymod", "ExtraHealth"));

// ===== 解锁与移除 =====

PerkTreeUtils.ForceUnlock(new Identifier("mymod", "ExtraHealth"));

// 检查 Perk 是否已解锁（用于 Machine 门控等场景）
bool unlocked = PerkTreeUtils.IsPerkUnlocked(new Identifier("mymod", "ExtraHealth"));

PerkTreeUtils.RemovePerk(new Identifier("mymod", "ExtraHealth"));
PerkTreeUtils.RemoveAllPerks("mymod");
```

**Identifier 语义**：
- 自定义树：`("mymod", "combat_perks")` — Domain=modid, Path=treeID
- 原版树注入：`("duckov", "CombatTree")`
- 原版 Perk 引用：`("duckov", "CombatTree/Marksman")` — Path = `treeID/perkName`

---

## 11. 天赋系统 / Endowment

🔗 [API_PERK_ENDOWMENT.md](API/API_PERK_ENDOWMENT.md) | 入口类：`EndowmentUtils`

```csharp
// ===== 注册天赋（推荐：使用 FML DTO） =====

// 加载图标（从 assets/textures/ 目录的 PNG 文件）
var icon = ItemUtils.LoadSprite("endowment_assassin");

// modder 用纯 C# EndowmentConfig DTO 配置，FML 内部负责转换为游戏原生 EndowmentEntry
EndowmentUtils.RegisterEndowment(
    new Identifier("mymod", "assassin"),
    new EndowmentConfig
    {
        Modifiers = new[]
        {
            new EndowmentModifier
            {
                StatKey = "moveSpeed",
                Type = ModifierType.PercentageAdd,
                Value = 0.15f    // +15% 移动速度
            },
            new EndowmentModifier
            {
                StatKey = "maxHealth",
                Type = ModifierType.PercentageAdd,
                Value = -0.1f    // -10% 最大生命
            }
        },
        Icon = icon,                         // 天赋图标（null 则使用默认）
        UnlockedByDefault = false,           // false = 需任务解锁
        RequirementTextKey = "endowment_assassin_requirement"
    }
    // modid 可选，默认从 id.Domain 推导
);

// ===== 通过任务解锁天赋（Endowment 的常规解锁方式） =====

// 推荐：直接在任务 rewards 中放 RewardUnlockEndowmentData（任务完成自动解锁）
QuestUtils.RegisterQuest(new Identifier("mymod", "quest_assassin_training"), new QuestData
{
    displayName = "quest_assassin_training_name",
    description = "quest_assassin_training_desc",
    tasks = new List<TaskData>
    {
        new TaskKillCount { requireAmount = 10, requireEnemy = "Scav" }
    },
    rewards = new List<RewardData>
    {
        new RewardMoney { amount = 500 },
        new RewardUnlockEndowmentData { endowmentId = new Identifier("mymod", "assassin") }
    }
});

// 高级：如需在任务完成时执行自定义逻辑，订阅事件手动解锁
EventBusManager.Instance.Sync.Register<QuestTaskFinishedEvent>(e =>
{
    EndowmentUtils.UnlockEndowment(new Identifier("mymod", "assassin"));
}, 0, "mymod");

// ===== 默认解锁天赋（无需任务） =====
EndowmentUtils.RegisterEndowment(
    new Identifier("mymod", "survivor"),
    new EndowmentConfig
    {
        Modifiers = new[]
        {
            new EndowmentModifier { StatKey = "maxHealth", Type = ModifierType.Add, Value = 20 }
        },
        Icon = ItemUtils.LoadSprite("endowment_survivor"),
        UnlockedByDefault = true,    // 默认解锁——直接显示在面板中并可选择
        RequirementTextKey = ""      // 默认解锁时无需解锁条件文本
    }
);

// ===== 查询 =====

if (EndowmentUtils.TryGetEndowment(
    new Identifier("mymod", "assassin"), out var result))
{
    // 使用 result
}
IReadOnlyList<Identifier> ids = EndowmentUtils.GetAllEndowments("mymod");

// ===== 状态操作 =====

bool unlocked = EndowmentUtils.IsEndowmentUnlocked(
    new Identifier("mymod", "assassin"));
EndowmentUtils.UnlockEndowment(new Identifier("mymod", "assassin"));
EndowmentUtils.SelectEndowment(new Identifier("mymod", "assassin"));
Identifier? current = EndowmentUtils.GetCurrentSelection();

// ===== 卸载 =====

EndowmentUtils.UnregisterEndowment(new Identifier("mymod", "assassin"));
EndowmentUtils.UnregisterAllEndowments("mymod");
```

> **推荐解锁方式**：直接在 `QuestData.rewards` 中使用 `RewardUnlockEndowmentData`（AutoClaim，见 §5.1），无需订阅事件。

---

## 12. 敌人系统 / Enemy

🔗 [API_ENTITIES.md](API/API_ENTITIES.md#enemyutils) | 入口类：`EnemyUtils`

### 12.1 注册与生成 / Register & Spawn

```csharp
// 注册自定义敌人（modid 从 id.Domain 自动推导）
EnemyUtils.RegisterEnemy(
    new Identifier("mymod", "super_scav"),
    aiConfig,        // IStateConfig 状态机
    preset           // CharacterRandomPreset 预设
);

// 查询敌人预设（不存在时抛 ArgumentException）
CharacterRandomPreset preset = EnemyUtils.GetPreset("super_scav");

// 移除 / 批量卸载
EnemyUtils.UnregisterEnemy(new Identifier("mymod", "super_scav"));
EnemyUtils.UnregisterAllEnemies("mymod");
```

### 12.2 自定义 AI 状态机 / Custom AI

实现 `IStateConfig` 接口来定义敌人的 AI 行为：

```csharp
using FeatherMod.Entities;

public class MyScavAI : IStateConfig
{
    public string GetInitialState() => "patrol";

    public void OnStateEnter(string state) { }
    public void OnStateUpdate(string state, float deltaTime) { }
    public void OnStateExit(string state) { }

    public Transition[] GetTransitions(string stateName)
    {
        return stateName switch
        {
            "patrol" => new[]
            {
                new Transition("chase", () => PlayerDetected(), priority: 1),
                new Transition("investigate", () => HeardNoise(), priority: 0),
            },
            "chase" => new[]
            {
                new Transition("patrol", () => PlayerLost(), priority: 1),
            },
            _ => Array.Empty<Transition>(),
        };
    }

    private bool PlayerDetected() { /* 检测到玩家 */ return false; }
    private bool HeardNoise() { /* 听到声音 */ return false; }
    private bool PlayerLost() { /* 丢失玩家视野 */ return false; }
}
```

FML 的 `StateMachineToBT` 会将状态机编译为 NodeCanvas BehaviourTree。

### 12.3 生成敌人 / Spawn

```csharp
// 在指定位置生成已注册的敌人
CharacterMainControl enemy = EnemyUtils.SpawnEnemy(
    new Identifier("mymod", "super_scav"),
    new Vector3(10, 0, 5),
    onSpawned: (character) =>
    {
        Debug.Log($"Enemy spawned: {character.name}");
    });

// 使用 CharacterSpawnerGroup 生成（复用游戏原生生成点配置）
EnemyUtils.SpawnEnemy(
    new Identifier("mymod", "super_scav"),
    spawnerGroup,
    onSpawned: (character) => { /* ... */ });
```

### 12.4 查询与编译 / Query & Compile

```csharp
// 按 Identifier 查询已注册敌人（不存在返回 false）
if (EnemyUtils.TryGetEnemy(
    new Identifier("mymod", "super_scav"),
    out CharacterRandomPreset foundPreset))
{
    Debug.Log($"Found preset: {foundPreset.LocalizationKey}");
}

// 预编译状态机为 BehaviourTree（可在注册前验证 AI 配置合法性）
object bt = EnemyUtils.CompileStateMachine(aiConfig);
```

---

## 13. 友善 NPC / Friendly NPC

🔗 [API_ENTITIES.md](API/API_ENTITIES.md#friendlynpcutils) | 入口类：`FriendlyNpcUtils`

基于 `CharacterRandomPreset.CreateCharacterAsync` 创建完整的可见 NPC（自动附带 `CharacterModel`、`CustomFaceInstance`、`Animator` 等组件）。

> **重要**：`FriendlyNpcConfig.ActorId` 既是 `DuckovDialogueActor.id`（系统查找用），也自动作为 `nameKey` 缺省值（对话 UI 发言者名——游戏经 `ToPlainText` 翻译，modder 可用 `I18n` 注册对应翻译）。`DisplayNameKey` 设后优先级更高，同时影响 NPC 头顶名字和商店名。

### 13.1 注册与生成 / Register & Spawn（两步）

```csharp
// ── 第 1 步：注册预设 ──
var config = new FriendlyNpcConfig
{
    DisplayNameKey = "npc_merchant_name",
    ActorId = "merchant_actor",         // DuckovDialogueActor.id（对话系统查找用，兼缺省显示名 key）
    Role = NpcRole.Merchant | NpcRole.QuestGiver, // Merchant / QuestGiver / Companion / DialogueOnly（可复合）
    Face = FaceRef.Preset("Duck_Default"), // 捏脸（见下方 FaceRef 模式表）
    Model = ModelRef.GamePrefab("CharacterModel_Duck_Jeff"), // 模型
    Team = Teams.middle,                // 友善阵营
    SpawnPosition = new Vector3(10f, 0f, 5f),
    ShopId = "merchant_shop",
    QuestGiverId = new Identifier("mymod", "quest_giver"), // Identifier 引用已注册的 QuestGiver
    PerkTreeId = new Identifier("mymod", "MyPerkTree"),    // 绑定技能树（Path = perkTreeID）——无需额外 Role 标志
    ShopAccountAvaliable = true,        // 支持账户余额支付（false=仅现金）
    ShopReturnCash = false,             // 卖出物品后是否给现金物品（而非加账户余额）
    ShopSellFactor = 0.5f,              // 回收价格倍率
    HeadEquipment = ItemEntry.Of("duckov:CowboyHat", 1),  // 头部装备
    BodyEquipment = ItemEntry.Of("duckov:Vest_A", 1),     // 身体装备

    AutoFacePlayer = true,              // 默认 true——NPC 跟随玩家视线（经游戏原生瞄准管线平滑转向）
    FacePlayerRange = 10f,              // 跟随玩家的最大距离（超出后保持当前朝向）
    ProximityDialogue = DialogueSequence.Build("merchant_actor")  // 玩家接近时自动播放对话
        .Then("dialogue_hello")
        .Build(),
};
var preset = FriendlyNpcUtils.RegisterFriendlyNpc(new Identifier("mymod", "merchant_01"), config);

// ── 第 2 步：异步生成 ──
var npc = await FriendlyNpcUtils.SpawnFriendlyNpcAsync(new Identifier("mymod", "merchant_01"));
// npc 现在是一个完整的可见角色，带 CharacterModel + CustomFaceInstance + Collider
```

> ⚠️ 旧版 `CreateFriendlyNpc` 已废弃（返回临时占位 GameObject，NPC 不可见），一律使用 `RegisterFriendlyNpc` + `SpawnFriendlyNpcAsync`。

### 13.2 FaceRef 捏脸模式 / Face Modes

| 模式 | 用法 | 效果 |
|------|------|------|
| `Preset` | `FaceRef.Preset("Duck_Default")` | 引用 Resources 中已有 `CustomFacePreset` |
| `PlayerFace` | `FaceRef.PlayerFace()` | 使用玩家当前捏脸数据 |
| `Custom` | `FaceRef.Custom(parts)` | 按 8 个部件 ID 自定义组合 |
| `FromJson` | `FaceRef.FromJson(json)` | 从游戏原生 `CustomFaceSettingData` JSON 字符串创建 |

```csharp
// ── FromJson 示例：从文件加载已保存的捏脸数据 ──
string json = File.ReadAllText(Path.Combine(modDir, "faces", "npc_laozheng.json"));
Face = FaceRef.FromJson(json),
```
> JSON 格式与 `CustomFaceUtils.GetPlayerFaceJson()` 输出一致，也可从游戏存档导出。

### 13.3 角色类型（NpcRole）/ Roles

`NpcRole` 为 `[Flags]` 枚举，支持复合角色（交互键切换"交易/任务/技能"）。

| 枚举值 | 位值 | 行为 |
|--------|------|------|
| `None` | `0` | 无角色 |
| `Merchant` | `1 << 1` | 交互打开商店 UI（自动挂载 `StockShop` 组件，需 `ShopId`） |
| `QuestGiver` | `1 << 2` | 交互打开任务 UI（需 `QuestGiverId`） |
| `Companion` | `1 << 4` | NPC 跟随玩家 |
| `DialogueOnly` | `1 << 5` | 仅对话，不绑定额外交互 |
| `Neutral` | `1 << 3` | 中立 NPC（不攻击也不交互） |
| `Enemy` | `1 << 0` | 敌对敌人 |

> **PerkTree 绑定**：不需要将其加入 `Role` 标志——设置 `config.PerkTreeId` 即可。自定义树需先 `PerkTreeUtils.RegisterPerkTree()`，原版树用 `Identifier("duckov", "PerkTree_Hacker")`。

### 13.4 技能树绑定 / PerkTree Binding

通过 `config.PerkTreeId`（`Identifier?`）直接将技能树绑定到 NPC。生成后在 NPC 上自动挂载原版 `PerkTreeUIInvoker`（`Interact_Skill` 子对象）。交互名本地化键默认为 `perkTreeID`（原版惯例）。

### 13.5 NPC 朝向控制 / Facing Control

`AutoFacePlayer`（默认 `true`）使 NPC 经游戏原生瞄准管线平滑转向玩家（与原版 XiaoMing 行为树 `AimToPlayer` 同机制）。可通过 `FacePlayerRange` 设定最大跟随距离。

```csharp
// 固定朝向（覆盖跟随玩家）
FriendlyNpcUtils.SetNpcFaceDirection(npcId, Vector3.right);  // 面向世界 +X
FriendlyNpcUtils.SetNpcFaceAngle(npcId, 90f);                 // 面向世界 90°（+X）

// 恢复跟随玩家（若 AutoFacePlayer=true）或冻结当前朝向
FriendlyNpcUtils.ClearNpcFaceDirection(npcId);
```

> **技术说明**：朝向控制内部调用 `CharacterMainControl.SetAimPoint` 走游戏原生瞄准→旋转管线，避免直接写 `Movement.targetAimDirection` 被 `UpdateAiming` 覆盖。

### 13.6 其他 API / Others

```csharp
// 世界空间对话气泡
FriendlyNpcUtils.ShowBubble(new Identifier("mymod", "merchant_01"), "欢迎！", 3f);
FriendlyNpcUtils.ShowBubbleLocalized(id, "dialogue_welcome", 3f);

// 绑定商店 / 任务
FriendlyNpcUtils.BindShop(id, new Identifier("mymod", "shop"));
FriendlyNpcUtils.BindQuestGiver(id, new Identifier("mymod", "quest_giver_custom"));

// 查询 NPC 的 ActorId（对话系统联动）
if (FriendlyNpcUtils.TryGetNpcActorId(npcId, out var actorId))
    await DialogueManager.PlayDialogue(actorId, lines);

// 销毁 / 批量卸载
FriendlyNpcUtils.RemoveNpc(id);
FriendlyNpcUtils.RemoveAllNpcs("MyMod");
```

> **技术说明**：NPC 创建基于游戏原生的 `CharacterRandomPreset` + `CreateCharacterAsync`（与 `EnemyUtils.SpawnEnemy` 同路径）。所有 `[SerializeField] private` 字段经 Krafs.Publicizer 编译期公开，直接赋值无需反射。

---

## 14. 捏脸系统 / Custom Face

🔗 [API_ENTITIES.md](API/API_ENTITIES.md#customfaceutils) | 入口类：`CustomFaceUtils`

提供从官方捏脸数据串（JSON）导入/导出捏脸数据的能力，让 Mod 可以动态修改玩家或任意角色的外观。游戏原生的捏脸数据格式是 `CustomFaceSettingData` 结构体（Duckov 内置），通过 `DataToJson()` / `JsonToData()` 进行 JSON 序列化。

### 14.1 玩家主角捏脸 / Player Face

```csharp
// 从官方捏脸 JSON 字符串设置玩家外观
string faceJson = "{\"savedSetting\":false,\"headSetting\":{...}}";
bool ok = CustomFaceUtils.SetPlayerFaceFromJson(faceJson);

// 导出玩家当前捏脸为 JSON 字符串
string current = CustomFaceUtils.GetPlayerFaceJson();

// 使用原生 CustomFaceSettingData 结构体
CustomFaceUtils.SetPlayerFaceFromData(nativeFaceData);
CustomFaceSettingData data = CustomFaceUtils.GetPlayerFaceAsData();

// 验证 JSON 串是否合法
bool valid = CustomFaceUtils.ValidateJson(faceJson);
```

### 14.2 任意角色捏脸 / Any Character

通过 `CustomFaceInstance` 组件对任意角色（包括 NPC）进行捏脸操作：

```csharp
// 获取任意角色上的 CustomFaceInstance 组件
var faceInstance = someCharacter.GetComponent<CustomFaceInstance>();

// 从 JSON 设置 / 导出
CustomFaceUtils.SetFaceFromJson(faceInstance, faceJson);
string json = CustomFaceUtils.GetFaceJson(faceInstance);

// 使用原生结构体
CustomFaceUtils.LoadFaceFromData(faceInstance, nativeData);
CustomFaceSettingData data = CustomFaceUtils.GetFaceAsData(faceInstance);
```

### 14.3 将捏脸应用到 NPC / Apply to NPC

`CustomFaceUtils` 用于**运行时**修改已存在角色的捏脸。如果要在 NPC **创建时**指定捏脸，使用 `FaceRef.FromJson()`（详见 §13.2）：

```csharp
var config = new FriendlyNpcConfig
{
    Face = FaceRef.FromJson(faceJson),  // 从 JSON 直接创建捏脸
    // ...其他配置
};
FriendlyNpcUtils.RegisterFriendlyNpc(id, config);
```

> **区别**：`CustomFaceUtils.SetFaceFromJson(instance, json)` 修改**已生成**角色的捏脸（需要先获取 `CustomFaceInstance` 组件）；`FaceRef.FromJson(json)` 在 NPC **创建时**通过 `CharacterRandomPreset.facePreset` 设置捏脸（更早、更可靠）。

---

## 15. NPC 注入 / Weapon & LotteryBox Injection

### 15.1 武器注入（WeaponInjectionUtils）

🔗 [API_ENTITIES.md](API/API_ENTITIES.md#weaponinjectionutils) | 零 Harmony Hook，直接修改 `CharacterRandomPreset.itemsToGenerate` 数据。

```csharp
// 按预设名注入（前缀通配：所有以 "Cname_Scav" 开头的 NPC 预设）
WeaponInjectionUtils.AddWeaponToPreset("Cname_Scav*", ItemEntry.Of("mymod", "ak47"), chance: 0.5f);

// 精确匹配单个预设
WeaponInjectionUtils.AddWeaponToPreset("Cname_Boss_Wolf", ItemEntry.Of("mymod", "sniper"), chance: 0.3f);

// 按阵营注入（向所有 Scav 阵营 NPC 注入武器）
using Duckov.Utilities;
WeaponInjectionUtils.AddWeaponToTeam(Teams.scav, ItemEntry.Of("mymod", "shotgun"), chance: 0.4f);

// 卸载
WeaponInjectionUtils.RemoveWeaponFromPreset("Cname_Scav*", ItemEntry.Of("mymod", "ak47"));
WeaponInjectionUtils.RemoveWeaponFromTeam(Teams.scav, ItemEntry.Of("mymod", "shotgun"));
WeaponInjectionUtils.UnregisterAllWeaponInjections("mymod");
```

> 系统自动识别注入武器的类型（枪 / 近战），仅注入到兼容的预设槽位中——枪替换枪、刀替换刀，不跨类型 fallback。
> **注意**：`AddWeaponToPreset` / `AddWeaponToTeam` 在调用时立即执行注入（修改 ScriptableObject 数据）。建议在 `OnAfterSetup` 中调用。

### 15.2 抽奖箱注入（LotteryBoxUtils）

🔗 [API_ENTITIES.md](API/API_ENTITIES.md#lotteryboxutils) | 通过 Harmony Patch 在 LotteryBox 被使用时自动注入物品到候选池。modder 只需调用一次注册，后续场景加载时自动生效。

```csharp
// 向所有名为 "LotteryBox_Gun" 开头的抽奖箱注入武器（默认与原生条目等权）
LotteryBoxUtils.AddItemToLotteryBox("LotteryBox_Gun*", ItemEntry.Of("mymod", "ak47"));

// 精确匹配
LotteryBoxUtils.AddItemToLotteryBox("LotteryBox_Boss", ItemEntry.Of("mymod", "sniper"));

// 卸载
LotteryBoxUtils.RemoveItemFromLotteryBox("LotteryBox_Gun*", ItemEntry.Of("mymod", "ak47"));
LotteryBoxUtils.UnregisterAllLotteryInjections("mymod");
```

> `weight` 参数（默认 1.0）为相对权重倍数：`实际权重 = weight × 原生条目平均权重`。只追加，不缩放原生条目。
> 枪只注入到枪箱，刀只注入到刀箱。类型不匹配时跳过并输出警告日志。
> **注意**：`AddItemToLotteryBox` 仅存储规则，地图加载时由 Harmony `Awake` Postfix 自动触发注入——比 WeaponInjection 更适合"场景未加载时提前注册"的场景。

---

## 16. 交互系统 / Interaction

🔗 [API_INTERACTION_UI.md](API/API_INTERACTION_UI.md) | 入口类：`InteractionUtils` / `ViewDispatcher` / `InteractionGroupBuilder`

### 16.1 两种交互模式 / Two Modes

| 模式 | Handler | 用途 |
|------|---------|------|
| **View 模式** | `ViewInteractHandler` | 交互→通过 `ViewDispatcher` 打开指定 View |
| **Delegate 模式** | `DelegateInteractHandler` | 交互→调用自定义委托 |

### 16.2 Spawn（创建新交互点）/ Spawn

```csharp
using FeatherMod.Interaction;
using UnityEngine;

// 在世界坐标创建 View 交互点
var point = InteractionUtils.SpawnViewInteract(
    new Identifier("mymod", "trading_post"),
    position: new Vector3(10, 0, 5),
    viewType: GameViews.Shop,
    viewParam: "merchant_stock");
// → 自动创建 GameObject + BoxCollider(Trigger) + "Interact" 图层

// 在世界坐标创建自定义委托交互点
InteractionUtils.SpawnCustomInteract(
    new Identifier("mymod", "secret_button"),
    position: new Vector3(3, 1, 8),
    onInteract: () => Debug.Log("Secret triggered!"));
```

### 16.3 Attach（挂载到已有对象）/ Attach

```csharp
// 给已有 GameObject 挂载交互
InteractionUtils.AttachViewInteract(
    new Identifier("mymod", "door_terminal"),
    target: someGameObject,
    viewType: GameViews.PerkTree,
    viewParam: "mymod_perktree");

// 按名称查找 NPC 并挂载交互
InteractionUtils.AttachToNPC(
    new Identifier("mymod", "talk_to_merchant"),
    npcName: "Merchant_Fence",
    viewType: GameViews.Shop);
```

### 16.4 查询与卸载 / Query & Unregister

```csharp
// 查询已注册交互点
if (InteractionUtils.TryGetInteractPoint(id, out var go))
    Debug.Log($"Found: {go.name}");

// 移除单个交互点（自动 Destroy GameObject）
InteractionUtils.RemoveInteract(id);

// 批量卸载指定 mod 的全部交互点
InteractionUtils.RemoveAllInteracts("mymod");
```

### 16.5 InteractionGroupBuilder — 多交互组合 / Multi-Interact Groups

```csharp
var handler = new InteractionGroupBuilder()
    .Add(new Identifier("mymod", "shop"),    GameViews.Shop,    viewParam: "merchant_01",
         interactNameKey: "UI_Trade",         markerOffset: new Vector3(0, 1.5f, 0))
    .Add(new Identifier("mymod", "research"), GameViews.FormulasRegister, viewParam: "Formula_Blueprint",
         interactNameKey: "UI_Research")
    .Add(new Identifier("mymod", "craft"),    GameViews.Crafting, viewParam: "WorkBenchAdvanced",
         interactNameKey: "UI_Crafting")
    .WithPrimary(0)  // 主交互（玩家靠近时优先显示）
    .BuildOn(functionContainer);
```

> **注意**：单条目时直接挂载到目标（不创建子 GO、不编组）；多条目时自动创建子节点 + BoxCollider + `ViewInteractHandler` 并编组为 `interactableGroup`（主交互体可交互，成员碰撞体禁用）。`viewParam` 的语义由各 View handler 定义。

### 16.6 蓝图研究台交互 / FormulasRegisterInteract

```csharp
FeatherFormulasRegisterInteract.Attach(
    new Identifier("mymod", "medic_research"),
    functionContainer,
    registerTag: "Formula_Medic",        // 仅接受带 Formula_Medic 标签的蓝图
    interactNameKey: "UI_Research_Medic" // 可选交互提示文本
);
```

### 16.7 内置 View 类型 / GameViews

```csharp
// 以下 10 个内置 View 类型已由 InteractionUtils.Init() 自动注册打开方法：
GameViews.PerkTree         // Perk 技能树
GameViews.Building         // 建造面板（BuilderView）
GameViews.Endowment        // 天赋选择面板
GameViews.Crafting         // 过滤式合成界面
GameViews.Shop             // 商店（自动查找 NPC 的 StockShop 并调用 ShowUI()）
GameViews.Quest            // 任务（打开 QuestView.Show()）
GameViews.FormulasRegister // 配方注册/研究界面（viewParam 为标签名过滤可提交物品）
GameViews.Formulas         // 配方索引浏览（FormulasIndexView）
GameViews.Decompose        // 分解界面
GameViews.Machine          // 机器界面

// 自定义 View 注册打开方法：
ViewDispatcher.Register(
    new Identifier("mymod", "custom_view"),
    param => MyCustomView.Open(param),
    "mymod");
```

---

## 17. UI 系统与控件桥接 / GameUI

🔗 [API_INTERACTION_UI.md](API/API_INTERACTION_UI.md) | 入口类：`GameUIUtils` / `SimpleViewBuilder`

### 17.1 控件克隆 / Clone Controls

克隆自 `GameplayDataSettings.UIPrefabs`，自动继承精灵/材质/字体/着色器，视觉与游戏原生一致：

```csharp
using FeatherMod.UI;

// 克隆游戏原生按钮（含正确颜色/字体/精灵）
GameUIUtils.CloneButton(parentTransform, "确认", () => Debug.Log("Clicked"));

// 克隆物品图标显示 / 槽位 / 库存条目 / 滚动区域
var itemDisplay = GameUIUtils.CloneItemDisplay(parentTransform);
var slot = GameUIUtils.CloneSlotDisplay(parentTransform);
var inventoryEntry = GameUIUtils.CloneInventoryEntry(parentTransform);
var scrollRect = GameUIUtils.CloneScrollRect(parentTransform);
```

### 17.2 样式查询 / Style Lookup

```csharp
// 获取游戏主字体（从活跃 View 的 TextMeshProUGUI 提取）
var font = GameUIUtils.GetGameFont();

// 提取 UI 配色方案
var palette = GameUIUtils.GetColorPalette();
// palette.TextPrimary / PanelBackground / ButtonNormal / ButtonHighlight
```

### 17.3 快捷 View 打开 / Quick Open

```csharp
// 打开过滤式合成界面（仅显示指定工作台的配方）
GameUIUtils.OpenCraftingView(new[] { "Forge", "WorkBenchAdvanced" });

// 打开库存设备面板
GameUIUtils.OpenInventoryDevice(playerInventory);
```

### 17.4 代码端 UI 构建器 / SimpleViewBuilder

`SimpleViewBuilder` 适用于简单面板场景，已内置游戏原生按钮支持：

```csharp
using FeatherMod.UI;

var panel = SimpleViewBuilder.Create("MyModPanel")
    .AddTitle("欢迎使用")
    .AddText("这是一个代码创建的 UI 面板。")
    .AddGameButton("游戏风格按钮", () => Debug.Log("Clicked!"))
    //      ↑ 克隆自 GameplayDataSettings.UIPrefabs.Button，视觉与游戏原生按钮完全一致
    .AddGamePanel("子面板标题")
    .AddButton("普通按钮", () => Debug.Log("Basic"))
    .AddCloseButton()
    .Build();
```

> **注意**：`SimpleViewBuilder` 适用于 15% 的简单 UI 场景。对于更复杂的 UI，推荐使用 Harmony Postfix 注入模式或 `GameUIUtils` 控件克隆。

---

## 18. 物品容器 / Containers

🔗 [API_SYSTEM.md](API/API_SYSTEM.md#containerutils) | 入口类：`ContainerUtils`

`ContainerUtils` 提供轻量级物品容器管理，包装游戏原生 API，不实现完整的 Inventory 系统。

```csharp
using FeatherMod;

// 创建容器
var config = ContainerUtils.CreateContainer(
    new Identifier("mymod", "storage_box"),
    slotCount: 20,
    modid: "mymod");

// 查询容器
var existing = ContainerUtils.GetContainer(new Identifier("mymod", "storage_box"));

// 放入物品
ContainerUtils.PutItem(
    containerId: new Identifier("mymod", "storage_box"),
    slot: 0,
    item: ItemEntry.Of("mymod:coffee", 5));

// 取出物品（自动通过 ItemUtilities 转移到玩家库存）
ItemEntry? taken = ContainerUtils.TakeItem(
    containerId: new Identifier("mymod", "storage_box"),
    slot: 0,
    amount: 3);

// 绑定到建筑——建筑建造完成时自动挂载交互处理器
ContainerUtils.BindDeviceToBuilding(
    buildingId: new Identifier("mymod", "storage_warehouse"),
    containerId: new Identifier("mymod", "storage_box"),
    viewType: GameViews.Crafting);

// 销毁容器（注意：不转移容器内物品）
ContainerUtils.DestroyContainer(new Identifier("mymod", "storage_box"));

// 批量卸载（仅清 FML 内部跟踪数据，不销毁容器中的游戏内物品对象）
ContainerUtils.RemoveAllContainers("mymod");
```

---

## 19. 笔记系统 / Notes

🔗 [API_SYSTEM.md](API/API_SYSTEM.md#noteutils) | 入口类：`NoteUtils`

提供游戏内可收集笔记的注册、解锁和世界空间拾取物生成。笔记有"已解锁"和"已阅读"两个状态。

```csharp
// 注册笔记（运行时注入到 NoteIndex）
NoteUtils.RegisterNote(
    new Identifier("mymod", "lore_01"),
    new NoteConfig
    {
        TitleKey = "Note_lore_01_Title",
        ContentKey = "Note_lore_01_Content",
        Image = myImage,
        Hidden = false        // true 则不计入总数
    });

// 解锁笔记 / 解锁并打开笔记 UI
NoteUtils.Unlock(new Identifier("mymod", "lore_01"));
NoteUtils.UnlockAndShow(new Identifier("mymod", "lore_01"));

// 状态查询 / 统计
bool unlocked = NoteUtils.IsUnlocked(new Identifier("mymod", "lore_01"));
bool read = NoteUtils.IsRead(new Identifier("mymod", "lore_01"));
int total = NoteUtils.GetTotalCount();
int unlockedCount = NoteUtils.GetUnlockedCount();

// 在世界空间生成可拾取笔记（支持拾取交互）
NoteUtils.SpawnPickup(new Identifier("mymod", "lore_01"), new Vector3(10f, 0f, 5f));

// 按 modid 批量卸载
NoteUtils.UnregisterAllNotes("MyMod");
```

> 笔记的本地化键遵循 `Note_{key}_Title` / `Note_{key}_Content` 规则，与游戏原生一致。
> FML 通过 `SetNoteDynamic()` 运行时注入，无需修改 Excel 资产。

**事件**：

```csharp
EventBusManager.Instance.Sync.Register<NoteUnlockedEvent>(evt =>
    Debug.Log($"笔记解锁: {evt.NoteId}"));

EventBusManager.Instance.Sync.Register<NoteReadEvent>(evt =>
    Debug.Log($"笔记已读: {evt.NoteId}"));
```

---

## 20. 钓鱼系统 / Fishing

🔗 [API_SYSTEM.md](API/API_SYSTEM.md#fishingutils) | 入口类：`FishingUtils`

```csharp
// 注册钓鱼池（水域 → 鱼种 + 权重）
FishingUtils.RegisterFishingPool(
    new Identifier("mymod", "mountain_lake"),
    new FishingPoolConfig
    {
        WaterId = new Identifier("mymod", "mountain_lake"),
        Entries = new[]
        {
            new FishingPoolEntry { FishId = new Identifier("mymod", "salmon"), Weight = 0.5f, MinQuality = 2 },
            new FishingPoolEntry { FishId = new Identifier("mymod", "trout"), Weight = 0.3f }
        },
        MinLuck = 0.1f,
        MaxLuck = 1.0f
    });

// 注册特殊配对（精确 baitID → fishID 映射，含概率）
FishingUtils.RegisterSpecialCatch(
    new Identifier("mymod", "worm_bait"),
    new Identifier("mymod", "golden_fish"),
    0.1f);  // 10% 概率

// 钓鱼统计属性查询
float fishingTime = FishingUtils.GetFishingTime(mainCharacter);
float difficulty  = FishingUtils.GetFishingDifficulty(fishItem);
float quality     = FishingUtils.GetFishingQualityFactor(mainCharacter);

// 按 modid 批量卸载
FishingUtils.UnregisterAll("MyMod");
```

> 鱼物品仍需通过 `ItemUtils.CreateCustomItem` 创建——`FishingUtils` 只管理"什么鱼可以从哪钓到"。
> 特殊配对在 `FishSpawner.Awake` 时通过 Harmony Postfix 自动注入，对 modder 透明。

---

## 21. 天气系统 / Weather

🔗 [API_SYSTEM.md](API/API_SYSTEM.md#weatherutils) | 入口类：`WeatherUtils`

```csharp
// 天气查询（FML WeatherType 枚举，隐藏 Snow=22 细节）
WeatherType weather = WeatherUtils.GetCurrentWeather();
// → Sunny / Cloudy / Rainy / Snow / Stormy / SevereStormy

// 季节查询
SeasonType season = WeatherUtils.GetCurrentSeason();
// → Spring / Summer / Autumn / Winter

// 强制覆盖天气（调试/剧情用）
WeatherUtils.ForceWeather(WeatherType.Stormy);
WeatherUtils.ResetWeather();

// 便捷判断
bool isRaining = WeatherUtils.IsRaining();
bool isSnowing = WeatherUtils.IsSnowing();

// 风暴等级（0=无风暴，1=Stormy_I，2=Stormy_II）
int level = WeatherUtils.GetStormLevel();
bool inStorm = WeatherUtils.IsStormActive();

// 温度查询
float cold = WeatherUtils.GetColdLevel();  // -10 ~ +10
float heat = WeatherUtils.GetHeatLevel();

// 防护属性（基于 ItemStatsSystem 的 StormProtection / ColdProtection / HeatProtection）
float stormProt = WeatherUtils.GetStormProtection(playerCharacter);
float coldProt  = WeatherUtils.GetColdProtection(playerCharacter);
float heatProt  = WeatherUtils.GetHeatProtection(playerCharacter);
```

**事件**：

```csharp
EventBusManager.Instance.Sync.Register<StormStartedEvent>(_ =>
    Debug.Log("风暴开始！"));

EventBusManager.Instance.Sync.Register<StormEndedEvent>(_ =>
    Debug.Log("风暴结束。"));
```

---

## 22. 多场景 / Multi-Scene

🔗 [API_SYSTEM.md](API/API_SYSTEM.md#multisceneutils) | 入口类：`MultiSceneUtils`

提供关卡内子场景加载、传送和跨场景持久数据存储。

```csharp
// 注册自定义场景（Identifier → 游戏原生 sceneID 映射）
MultiSceneUtils.RegisterScene(
    new Identifier("mymod", "custom_boss_room"),
    "Level_Desert_Boss");

// 加载子场景
MultiSceneUtils.LoadSubScene(new Identifier("mymod", "custom_boss_room"));

// 传送（加载 + 传送到指定位置/坐标）
MultiSceneUtils.TeleportTo(new Identifier("mymod", "custom_boss_room"), "boss_spawn");
MultiSceneUtils.TeleportTo(new Identifier("mymod", "custom_boss_room"), new Vector3(100f, 0f, 200f));

// 查询当前场景
Identifier? current = MultiSceneUtils.GetCurrentSubScene();
string displayName = MultiSceneUtils.GetSceneDisplayName(new Identifier("duckov", "Base"));

// 关卡内跨场景持久数据（基于 MultiSceneCore.inLevelData）
MultiSceneUtils.SetLevelData("boss_defeated", true);
bool defeated = MultiSceneUtils.GetLevelData<bool>("boss_defeated") ?? false;

// 物体场景归属迁移
MultiSceneUtils.MoveToScene(myNpc, new Identifier("mymod", "custom_boss_room"));
MultiSceneUtils.MoveToMainScene(myNpc);
```

**事件**：

```csharp
EventBusManager.Instance.Sync.Register<SceneLoadFinishedEvent>(evt =>
    Debug.Log($"场景加载完成: {evt.SceneId}"));

EventBusManager.Instance.Sync.Register<SubSceneChangedEvent>(evt =>
    Debug.Log($"子场景切换: {evt.FromScene} → {evt.ToScene}"));
```

---

## 23. 对话系统 / Dialogue

🔗 [API_SYSTEM.md](API/API_SYSTEM.md#dialoguemanager) | 入口类：`DialogueManager` / `DialogueTrigger`

基于游戏原生 `DialogueTreeController` 驱动，自动处理 DialogueUI 面板、镜头和字幕全流程。

> **前置条件**：`PlayDialogue` 依赖 `DuckovDialogueActor.Get(actorId)` 查找发言者——必须先在 NPC 的 `FriendlyNpcConfig.ActorId` 中注册匹配的 id。
> ⚠️ 旧版 `DialogueUtils` / `SubtitleLine` / `ProximityDialogueConfig` 已废弃，**新代码一律使用 `DialogueManager` / `DialogueLine` / `DialogueSequence`**。

### 23.1 构建对话序列 / Build a Sequence

```csharp
using FeatherMod;

// 链式构建（SequenceBuilder）
var seq = DialogueSequence.Build("merchant_actor")     // 默认发言者
    .Then("dialogue_hello")                             // 文本（I18n key）
    .Then("player", "dialogue_reply_01")                // 指定发言者
    .Then("dialogue_farewell")
    .Build();

// 或直接构造 DialogueLine 数组
DialogueLine[] lines = new[]
{
    new DialogueLine { TextKey = "dialogue_hello" },
    new DialogueLine { ActorId = "player_actor", TextKey = "dialogue_reply_01" },
    new DialogueLine { TextKey = "dialogue_farewell" },
};
```

**镜头控制（SequenceBuilder）**：

```csharp
var seq = DialogueSequence.Build("merchant_actor")
    .CutTo(new Vector3(0f, 2f, 5f), npcId)      // 镜头切到 NPC 位置
    .Then("dialogue_hello")
    .LookAtActor("player_actor")                 // 镜头看向玩家
    .Then("player", "dialogue_reply_01")
    .ResumeCamera(1f)                            // 恢复游戏镜头
    .Build();
```

### 23.2 播放对话 / Play

```csharp
// 全屏字幕对话（面板 + 镜头 + 字幕全流程自动处理）
await DialogueManager.PlayDialogue("merchant_actor", seq);

// 气泡对话（不打断操作）
await DialogueManager.PlayBubbleDialogue("merchant_actor", lines);

// 世界空间气泡
DialogueManager.ShowNpcBubble(new Identifier("mymod", "merchant"), "欢迎！", 3f);
DialogueManager.ShowBubbleAt(new Vector3(10f, 1.5f, 5f), "这里看起来很有趣…");
```

**对话流程说明**：

```
DialogueManager.PlayDialogue(actorId, lines)
  → 构建 minimal DialogueTree JSON
  → 创建 DialogueTreeController（运行时 GO）
  → 注入 JSON + SetActorReference
  → StartDialogue()
      ├── OnDialogueStarted  → DialogueUI 开面板 + 禁用输入 + 转镜头
      ├── RequestSubtitles   → 打字机动画 + 音效（每行自动播放）
      └── OnDialogueFinished → 面板关闭 + 恢复输入
  → 销毁临时 GO
```

### 23.3 对话触发链条 / DialogueTrigger

```csharp
// ── 接近触发（需 NPC 已生成）──
DialogueTrigger.OnProximity(npcId, distance: 3f, lines: new[]
{
    new DialogueLine { TextKey = "dialogue_hey_need_help" },
});

// ── 任务激活/完成时触发 ──
DialogueTrigger.OnQuestAccepted(questId, npcId, lines);
DialogueTrigger.OnQuestCompleted(questId, npcId, lines);

// ── NPC 配置中声明式接近触发 ──
config.ProximityDialogue = DialogueSequence.Build("merchant_actor")
    .Then("dialogue_welcome")
    .Build();

// ── 移除触发器 ──
DialogueTrigger.RemoveAllTriggers(npcId);
```

> **技术说明**：`PlayDialogue` 内部运行时创建 `DialogueTreeController` + 注入 JSON graph，与原版 `CutScene` 机制完全一致。`DialogueTreeController.StartDialogue()` 触发 NodeCanvas 全流程，无需反射。

---

## 24. 音频系统 / Audio

🔗 [API_SYSTEM.md](API/API_SYSTEM.md#audioutil) | 入口类：`AudioUtil`

### 24.1 SFX 注册 / Register SFX

```csharp
using FeatherMod.Audio;

AudioUtil.Instance.RegisterAudio(
    new Identifier("mymod", "gun_shot"),
    new AudioData
    {
        Path = "events/Weapons/GunShot",       // FMOD event 路径
        Eventname = "GunShot",                  // 事件名称（用于反向查询）
        MinDistance = 1f,
        MaxDistance = 500f
    }
);
```

### 24.2 BGM 控制 / BGM Control

```csharp
// 播放内置 BGM / 自定义 BGM 文件
AudioUtil.PlayBGM("theme");
AudioUtil.PlayCustomBGM("path/to/music.ogg");

// 停止 / 切换 / 检查播放状态
AudioUtil.StopBGM();
AudioUtil.SwitchBGM("battle");
bool isPlaying = AudioUtil.IsBGMPlaying();
```

### 24.3 音量控制 / Volume Control

```csharp
// 总音量
AudioUtil.SetMasterVolume(0.8f);
float vol = AudioUtil.GetMasterVolume();

// 音乐音量 / SFX 音量
AudioUtil.SetMusicVolume(0.5f);
AudioUtil.SetSFXVolume(1.0f);

// 静音控制
AudioUtil.SetMasterMute(true);
AudioUtil.SetMusicMute(false);
AudioUtil.SetSFXMute(false);
```

---

## 25. 本地化 / I18n

🔗 [API_CORE.md](API/API_CORE.md#i18n) | 入口类：`I18n`

### 25.1 初始化 / Init

在 `OnAfterSetup` 中调用（需先 `ModPathResolver.Register`）：

```csharp
protected override void OnAfterSetup()
{
    base.OnAfterSetup();
    ModPathResolver.Register(GetModid(), dllPath);
    I18n.InitI18n(GetModid());  // 传入 mod 标识符（如 "MyFirstMod"）
}
```

> `InitI18n` 参数为 **modid**（模组标识符字符串），不是 DLL 路径。库内部通过 `ModPathResolver.ResolveDirectory(modid)` 解析 mod 目录。

### 25.2 语言文件 / Language Files

在 mod 目录下创建 `assets/lang/` 文件夹，放入以下 JSON 文件：

| 文件名 | 语言 |
|--------|------|
| `en_us.json` | 英语 |
| `zh_cn.json` | 简体中文 |
| `zh_tw.json` | 繁体中文 |
| `ja_jp.json` | 日语 |
| `ru_ru.json` | 俄语 |
| `ko_kr.json` | 韩语 |
| `it_it.json` | 意大利语 |
| `fr_fr.json` | 法语 |
| `sv_se.json` | 瑞典语 |

JSON 格式：

```json
{
    "item_coffee": "Coffee",
    "item_coffee_desc": "A hot cup of coffee. Restores energy.",
    "quest_coffee_run": "Coffee Run",
    "quest_coffee_run_desc": "Bring me 5 cups of coffee."
}
```

> I18n 自动监听游戏语言切换事件（`LanguageChangedEvent`），切换语言时自动重读对应文件。

---

## 26. 事件总线 / EventBus

🔗 [API_CORE.md](API/API_CORE.md#eventbus) | 入口类：`EventBusManager`

FML 提供统一的同步事件总线，自动桥接了 17 个游戏原生事件。

### 26.1 订阅事件 / Subscribe

```csharp
using FeatherMod.Events;
using FeatherMod.Events.GameEvents;

// 订阅玩家金钱变化
EventBusManager.Instance.Sync.Register<MoneyChangedEvent>(e =>
{
    Debug.Log($"Money: {e.OldMoney} → {e.NowMoney}");
});

// 订阅角色受伤
EventBusManager.Instance.Sync.Register<HurtEvent>(OnHurt);

// 以 mod 身份注册（卸载时自动清理）
EventBusManager.Instance.Sync.Register<HurtEvent>(
    OnHurt, 0, RegistryManager.CurrentModid);
```

### 26.2 可订阅的游戏事件 / Game Events

| 事件类型 | 触发时机 | 说明 |
|----------|----------|------|
| `HurtEvent` | 角色受伤 | **可标记**（effect 已应用） |
| `EntityDeathEvent` | 角色死亡 | 仅观察 |
| `LevelInitializedEvent` | 关卡初始化完成 | 仅观察 |
| `MoneyChangedEvent` | 金钱变化 | 仅观察 |
| `LanguageChangedEvent` | 游戏语言切换 | 仅观察 |
| `PlayerHearSoundEvent` | 玩家听到声音 | 仅观察 |
| `SoundSpawnedEvent` | 声音产生 | 仅观察 |
| `PlayerDeathEvent` | 玩家死亡 | 仅观察 |
| `ControllingCharacterChangedEvent` | 切换控制角色 | 仅观察 |
| `ItemUnlockStateChangedEvent` | 物品解锁状态变化 | 仅观察 |
| `ItemCraftedEvent` | 物品制作成功 | 仅观察 |
| `FormulaUnlockedEvent` | 配方解锁 | 仅观察 |
| `QuestTaskFinishedEvent` | 任务目标完成 | 仅观察 |
| `CollectSaveDataEvent` | 收集存档数据 | 仅观察 |
| `KillCountChangedEvent` | 击杀数变化 | 仅观察 |
| `MainSceneLoadedEvent` | 主场景加载完成 | 仅观察 |
| `SaveDeletedEvent` | 存档删除 | 仅观察 |

> 另有各模块自有事件（`WeatherChangedEvent` / `NoteUnlockedEvent` / `NpcCreatedEvent` / `FishCaughtEvent` / `SceneLoadFinishedEvent` 等），见对应模块章节。

### 26.3 异步事件总线 / AsyncEventBus

`AsyncEventBus` 适用于**需要分帧执行**的场景——handler 为 `Func<T, UniTask>` 异步方法。典型用例：大量 Sprite 加载、分批注册物品、避免单帧 IO 阻塞。

```csharp
using FeatherMod.Events;
using System.Collections.Generic;

/// <summary>Sprite 批量加载请求。</summary>
[Cancelable]
public class SpriteLoadRequestEvent : Event
{
    /// <summary>待加载 Sprite 的物品列表（Identifier → 在 ItemData 中定义的 spritePath）。</summary>
    public List<(Identifier itemId, string spriteName)> Items = new();
}
```

**加载阶段：并行加载（推荐）**——游戏加载中（`OnAfterSetup`），目标是最小化加载时间：

```csharp
protected override async void OnAfterSetup()
{
    base.OnAfterSetup();
    ModPathResolver.Register(GetModid(), dllPath);

    // 并行创建物品 — 每个内部的 Sprite 加载走线程池 IO
    var coffee = ItemUtils.CreateCustomItemAsync(new Identifier("mymod", "coffee"), coffeeData);
    var rifle  = ItemUtils.CreateCustomItemAsync(new Identifier("mymod", "rifle"), rifleData);
    var pistol = ItemUtils.CreateCustomItemAsync(new Identifier("mymod", "pistol"), pistolData);

    // WhenAll 等待所有并行任务完成（文件 IO 在线程池并行执行）
    await UniTask.WhenAll(coffee, rifle, pistol);

    Debug.Log("[MyMod] All items created and sprites loaded.");
}
```

> **设计考量**：`CreateCustomItemAsync` 内部调用 `LoadSpriteFromDirAsync`，文件 IO 通过 `UniTask.RunOnThreadPool` 在线程池执行。多个物品用 `UniTask.WhenAll` 并行创建，文件读取在线程池并发，Texture2D 创建串行回到主线程。相比逐个同步 `File.ReadAllBytes`，加载时间可减少 50-70%。

**运行时：分帧加载**——游戏运行中需要加载大量 Sprite，用 `async UniTask` handler + `await UniTask.Yield()` 分帧避免卡顿：

```csharp
// 注册异步 handler：每帧加载一张 Sprite
EventBusManager.Instance.Async.Register<SpriteLoadRequestEvent>(
    LoadSpritesFrameByFrame, 0, RegistryManager.CurrentModid);

var evt = new SpriteLoadRequestEvent();
evt.Items.Add((new Identifier("mymod", "rifle"), "rifle_icon.png"));
evt.Items.Add((new Identifier("mymod", "pistol"), "pistol_icon.png"));

await EventBusManager.Instance.Async.Post(evt);

/// <summary>异步：逐帧加载 Sprite，await UniTask.Yield() 分帧避免卡顿。</summary>
private async UniTask LoadSpritesFrameByFrame(SpriteLoadRequestEvent e)
{
    foreach (var (itemId, spriteName) in e.Items)
    {
        await ItemUtils.LoadSpriteAsync(itemId);
        await UniTask.Yield();  // 等待下一帧
    }
}
```

> **设计考量**：注册大量物品时，如果每件物品都同步调用 `LoadSprite`（内部 `File.ReadAllBytes`），单帧累计 IO 可能超过 16ms 导致掉帧。`AsyncEventBus` 基于 UniTask 的 PlayerLoop 调度，handler 通过 `await UniTask.Yield()` 将 IO 分散到多帧，保持 60fps 流畅度。相比协程方案（MonoBehaviour + StartCoroutine），UniTask 零 GC 分配，无需 MonoBehaviour，性能更优。不需要分帧的场景继续用 `Sync` 总线即可。

**关键 API**：

| 操作 | Async（UniTask） | Sync（同步） |
|------|-----------------|------------|
| 注册 | `Async.Register<T>(Func<T, UniTask> handler)` | `Sync.Register<T>(Action<T> handler)` |
| 发送 | `await Async.Post(evt)` | `Sync.Post(evt)` |
| 批量卸载 | `Async.UnregisterAll(ownerMod)` | `Sync.UnregisterAll(ownerMod)` |

---

## 27. 自定义设置面板 / Mod Options

🔗 [API_INTERACTION_UI.md](API/API_INTERACTION_UI.md#modoptionsregistry) | 入口类：`ModOptionsRegistry`

```csharp
using FeatherMod.Options;

ModOptionsRegistry.RegisterPanel("mymod", "My Mod Settings", builder =>
{
    // 开关
    builder.AddToggle("enable_feature", true, "Enable Feature");

    // 滑块
    builder.AddSlider("difficulty", 1.0f, 0.5f, 3.0f, "Difficulty Multiplier");

    // 下拉菜单
    builder.AddDropdown("mode", new[] {"Easy", "Normal", "Hard"}, 1, "Mode");

    // 按钮
    builder.AddButton("Reset Settings", () => ResetDefaults());
});
```

面板出现在游戏设置 → Custom Options 标签页中。所有设置值自动通过 `OptionsManager` 持久化。

---

## 28. 存档 / Save

🔗 [API_CORE.md](API/API_CORE.md#saveutils) | 入口类：`SaveUtils`

```csharp
using FeatherMod.Saves;

// 以 Identifier 为 key 存取任意 ES3 可序列化数据
SaveUtils.Save(new Identifier("mymod", "boss_defeated"), true);
bool defeated = SaveUtils.Load(new Identifier("mymod", "boss_defeated"), false);

// 检查存在性 / 删除
bool exists = SaveUtils.KeyExists(new Identifier("mymod", "boss_defeated"));
SaveUtils.Delete<bool>(new Identifier("mymod", "boss_defeated"));

// 预检类型可序列化性（复杂自定义类型）
bool ok = ES3Validator.CanBeSerializedByES3<MyCustomType>();
```

---

## 29. AssetBundle 加载 / AssetBundle

🔗 [API_CORE.md](API/API_CORE.md#assetutil) | 入口类：`AssetUtil`

```csharp
// 从 mod 目录加载（路径: assets/bundle/{bundleName}）
AssetBundle? bundle = AssetUtil.LoadBundle(new Identifier("mymod", "weapons"));

// 便捷重载（需先 ModPathResolver.Register）：
// AssetBundle? bundle = AssetUtil.LoadBundle("weapons");

// 从指定目录加载
AssetBundle? bundle = AssetUtil.LoadBundleFromDir(modDirectory, "weapons");

// 加载好的 AssetBundle 会被缓存，重复调用返回同一实例

// 卸载指定 Bundle
AssetUtil.UnloadBundle(modDirectory, "weapons");

// 卸载全部已缓存 Bundle（通常在 OnBeforeDeactivate 中调用）
AssetUtil.UnloadAllBundles();
```

> AssetBundle 文件放在 `assets/bundle/` 目录下。

---

## 30. 注册表系统 / Registry

🔗 [API_CORE.md](API/API_CORE.md#registry) | 入口类：`RegistryManager` / `IRegistry<T>`

所有模块的数据都通过 `IRegistry<T>` 管理。高级用法——创建自定义注册表：

```csharp
using FeatherMod.Register;

// 获取元注册表
var meta = RegistryManager.Instance.Registry;

// 读取注册表 / 遍历
var audioRegistry = meta.Get(new Identifier("FeatherMod", "audio"));
foreach (var entry in meta)
{
    Debug.Log($"{entry.Key}: {entry.Value}");
}

// 三种 Registry 实现
// SimpleRegistry<T>               CRUD + owner 追踪 + OnRemoved 回调（常规模块）
// NonAlterableSimpleRegistry<T>   写入后不可覆盖（元注册表）
// ReverseLookupRegistry<T,TKey>   按 native key 反查 Identifier（Audio / Items）

// 创建自定义注册表
public class MyCustomRegistry : SimpleRegistry<MyType>
{
    protected override void OnRemoved(Identifier id, MyType value, string? modid)
    {
        // 自动清理 native 侧资源
        GameObject.Destroy(value.gameObject);
    }
}

// 注册到元表
var meta = RegistryManager.Instance.Registry;
meta.Set(new Identifier("mymod", "myregistry"), myRegistry, "mymod");
```

---

## 31. 跨模组联动 / Cross-Mod Integration

🔗 [API_CORE.md](API/API_CORE.md#modutils) | 入口类：`ModUtils`

`ModUtils` 提供跨模组状态查询 API，允许模组在运行时检查其他模组是否已安装/激活，实现条件内容注册。**命名空间**：`FeatherMod.Modding`。

| 方法 | 说明 |
|------|------|
| `ModUtils.IsModLoaded(string modid)` | 已安装**且处于激活状态**（等价于 ModManager 中存在该名称且 `IsModActive` 返回 true） |
| `ModUtils.IsModInstalled(string modid)` | 已安装（**不论玩家是否手动启用**），仅检查 `modInfos` 中存在该名称 |

### 31.1 使用场景：条件内容注册 / Conditional Registration

```csharp
using FeatherMod.Modding;

public class MyMod : Duckov.Modding.ModBehaviour, IHasModid
{
    public string GetModid() => "MyMod";

    protected override void OnAfterSetup()
    {
        base.OnAfterSetup();

        // 通用内容始终注册
        ItemUtils.CreateCustomItem(
            new Identifier("MyMod", "common_sword"), commonConfig);

        // 条件联动：仅在 ExpansionMod 激活时注册联动内容
        if (ModUtils.IsModLoaded("ExpansionMod"))
        {
            // 注册联动物品
            ItemUtils.CreateCustomItem(
                new Identifier("MyMod", "expansion_sword"), expansionConfig);

            // 注册联动合成配方（引用 ExpansionMod 中的材料）
            CraftingUtils.AddCraftingFormula(new CraftingFormulaData
            {
                Id = new Identifier("MyMod", "expansion_upgrade"),
                CostItems = new[]
                {
                    ItemEntry.Of("MyMod:common_sword", 1),
                    ItemEntry.Of("ExpansionMod:rare_material", 3),
                },
                Result = ItemEntry.Of("MyMod:expansion_sword", 1)
            });

            // 注册联动任务（在 ExpansionMod 的 QuestGiver 上）
            var questData = new QuestData
            {
                Id = new Identifier("MyMod", "expansion_quest"),
                QuestGiverIdentifier = new Identifier("ExpansionMod", "main_giver"),
            };
            QuestUtils.RegisterQuest(new Identifier("MyMod", "expansion_quest"), questData);
        }
    }
}
```

### 31.2 与 fml.json 的配合 / With fml.json

两者的关系：
- **fml.json `dependencies`**：加载时硬阻断——依赖缺失时模组不会被激活
- **fml.json `loadAfter`**：仅控制加载顺序，缺失静默跳过
- **`ModUtils.IsModLoaded()`**：运行时软查询——在代码中按需做条件分支

典型实践：在 fml.json 中声明 `loadAfter` 确保加载顺序，在代码中用 `ModUtils.IsModLoaded` 决定是否注册联动内容。

### 31.3 注意事项 / Notes

1. **调用时机**：`IsModLoaded` 依赖 `ModManager.modInfos`，应在 `OnAfterSetup` 及之后调用。在 `Awake` 阶段调用可能返回 false（mod 列表尚未就绪）
2. **不缓存结果**：每次调用实时查询 `modInfos` 列表。如多处使用，建议保存到局部变量
3. **modid 匹配**：区分大小写，必须与目标模组 `info.ini` 中的 `name` 字段完全一致

---

## 32. 附录 / Appendix

### 32.1 推荐目录结构 / Project Structure

```
MyMod/
├── MyMod.csproj
├── MyMod.cs                    # ModBehaviour 主类
├── fml.json                    # 优先级/依赖声明（可选）
├── assets/
│   ├── bundle/
│   │   └── weapons             # AssetBundle 文件
│   ├── lang/
│   │   ├── en_us.json          # 语言文件
│   │   └── zh_cn.json
│   ├── models/
│   │   └── apple.obj           # OBJ 模型（ItemGraphicUtils / ModelUtils）
│   └── textures/
│       ├── coffee_icon.png     # 物品图标
│       └── models/             # 模型纹理（建议与物品 sprite 隔离）
├── bin/                        # 构建输出
└── README.md
```

### 32.2 常用命名空间速查 / Namespace Reference

> 完整列表见 [API.md 命名空间速查](API/API.md#命名空间速查--namespace-quick-reference)。

| 命名空间 | 包含 |
|----------|------|
| `FeatherMod` | 全部工具类入口（ItemUtils / CraftingUtils / QuestUtils / BuildingUtils / ...） |
| `FeatherMod.Utils` | `Identifier`, `Singleton<T>`, `ModPathResolver`, `TimeUtils` |
| `FeatherMod.Modding` | `ModUtils`, `ModMetaCache` |
| `FeatherMod.Interaction` | `InteractionUtils`, `ViewDispatcher`, `GameViews`, `InteractionGroupBuilder` |
| `FeatherMod.Interaction.Components` | `ViewInteractHandler`, `DelegateInteractHandler`, `FeatherShopInteract` 等 |
| `FeatherMod.UI` | `GameUIUtils`, `GameUIColorPalette`, `SimpleViewBuilder` |
| `FeatherMod.Register` | `IRegistry<T>`, `SimpleRegistry<T>`, `RegistryManager` |
| `FeatherMod.Events` | `EventBusManager`, `EventBus`, `AsyncEventBus` |
| `FeatherMod.Events.GameEvents` | `HurtEvent`, `MoneyChangedEvent` 等 16 个桥接事件 |
| `FeatherMod.Audio` | `AudioUtil`, `AudioData` |
| `FeatherMod.Options` | `ModOptionsRegistry`, `ModOptionsBuilder` |
| `FeatherMod.Entities` | `IStateConfig`, `Transition`, `EnemyPresetData`, `FriendlyNpcConfig`, `FaceRef`, `NpcRole`, `ModelRef` |
| `FeatherMod.Items` | `GameItemLookup`, `TagUtils`, `TagConfig` |
| `FeatherMod.Crafting` | `CraftingFormulaData`, `DecomposeFormulaData`, `ItemEntry` |
| `FeatherMod.Quests` | `QuestData`, `TaskData`, `RewardData` 及其子类 |
| `FeatherMod.Saves` | `SaveUtils`, `ES3Validator` |

### 32.3 已废弃 API 汇总 / Obsolete API Summary

| 已废弃 | 替代 | 位置 |
|--------|------|------|
| `DialogueUtils`（整个类） | `DialogueManager` | §23 |
| `SubtitleLine` | `DialogueLine` | §23 |
| `ProximityDialogueConfig` | `DialogueSequence` | §23 |
| `FriendlyNpcUtils.CreateFriendlyNpc` | `RegisterFriendlyNpc` + `SpawnFriendlyNpcAsync` | §13 |
| `FriendlyNpcUtils.BindQuestGiver(npcId, string)` | `BindQuestGiver(npcId, Identifier)` | §13 |
| `EndowmentUtils.RegisterEndowment(EndowmentEntry 版)` | `RegisterEndowment(Identifier, EndowmentConfig)` | §11 |
| `EndowmentUtils.RegisterEndowment(object[] 版)` | `RegisterEndowment(Identifier, EndowmentConfig)` | §11 |
| `BuildingUtils.GetBuildingInfo(string)` | `GetBuildingInfo(Identifier)` | §9 |
| `BuildingUtils.PlaceBuilding(string, string, ...)` | `PlaceBuilding(Identifier, Identifier, ...)` | §9 |
| `CraftingUtils` 传统签名（formulaId/money/costItems 裸参） | `AddCraftingFormula(CraftingFormulaData)` | §4 |

---

_如有疑问，请在 GitHub Issues 中提出。_
