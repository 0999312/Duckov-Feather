# DuckovDrinks 对接 FML Bugfix 版 — 交接清单

> FML DLL 路径：`Duckov-Fast-Modding-Lib\FastModdingLib\bin\Debug\netstandard2.1\FeatherMod.dll`
> 替换到：`DuckovDrinks` 项目的引用路径

---

## 🔴 必须修改（不修改会有运行时问题）

### 1. 注册 `CoffeeBean` Tag

`CoffeeBean` Tag 现在**必须显式注册**后才能被物品使用。

**位置**：`ModBehaviour.cs` → `RegisterItems()` **之前**

```csharp
protected override void OnAfterSetup()
{
    ModPathResolver.Register(GetModid(), dllPath);

    // ★ 新增：Tag 必须在物品创建前注册
    TagUtils.RegisterTag("CoffeeBean", new TagConfig { Show = true });

    RegisterItems();       // ← CoffeeBeanPack 物品使用了 "CoffeeBean" tag
    RegisterBlueprints();
    // ...
}
```

添加 using：
```csharp
using FeatherMod.Items;  // TagUtils / TagConfig
```

---

## 🟡 建议修改（改善交互体验）

### 2. 修复 Building 交互 handler 获取方式

当前代码用 `funcContainer.GetComponent<ViewInteractHandler>()` 获取 Crafting handler，但 FML 的 Machine 系统也会在同一个 `funcContainer` 下创建 `ViewInteractHandler` 子节点（Machine_juicer、Machine_ice_cream）。`GetComponent` 可能拿到错误的 handler。

**位置**：`Building/BuildingConfig.cs` → `RegisterOnBuiltCallback()`

```csharp
// ❌ 旧代码——可能拿到 Machine 的 handler
var craftHandler = funcContainer.GetComponent<ViewInteractHandler>();

// ✅ 改为用 InteractionUtils.AttachViewInteract 的返回值
var craftHandler = InteractionUtils.AttachViewInteract(
    new Identifier(Constants.MODID, "interact_drink_station"),
    funcContainer,
    GameViews.Crafting,
    viewParam: Constants.CRAFT_TAG_DRINK_STATION,
    interactNameKey: "interact_drink_station_craft");

var perkHandler = FeatherPerkTreeInteract.Attach(
    new Identifier(Constants.MODID, "interact_drink_station_perk"),
    funcContainer,
    Constants.PERK_TREE_DRINK_STATION,
    "interact_drink_station_perk");

if (craftHandler != null && perkHandler != null)
{
    InteractionUtils.SetupInteractionGroup(craftHandler, perkHandler);
}
```

> **说明**：`AttachViewInteract` 现在返回挂载的 `ViewInteractHandler` 实例，直接用返回值避免 `GetComponent` 歧义。

---

### 3. （可选）使用 `InteractionGroupBuilder` 替代手动编组

如果想更声明式地管理交互组：

```csharp
new InteractionGroupBuilder()
    .Add(new Identifier(Constants.MODID, "interact_drink_station"),
         GameViews.Crafting, viewParam: Constants.CRAFT_TAG_DRINK_STATION,
         interactNameKey: "interact_drink_station_craft")
    .Add(new Identifier(Constants.MODID, "interact_drink_station_perk"),
         GameViews.PerkTree, viewParam: Constants.PERK_TREE_DRINK_STATION,
         interactNameKey: "interact_drink_station_perk")
    .WithPrimary(0)  // Crafting 为主交互
    .BuildOn(funcContainer);
```

---

## 🟢 FML 侧已修复（无需 Mod 侧改动）

| 修复 | 说明 |
|------|------|
| Machine 交互编组 | `SetupBuildingMachines` 现在将多台 Machine 编为单交互组 |
| PerkTree 持久化 | 注入原版树的 Perk 节点状态现在会随存档保存/恢复 |
| NPC 去重 | `SpawnFriendlyNpcAsync` 再生成前会先移除已存在的实例 |

---

## 📋 完整修改清单（DuckovDrinks 侧）

| 文件 | 操作 | 内容 |
|------|------|------|
| `ModBehaviour.cs` | 添加 using | `using FeatherMod.Items;` |
| `ModBehaviour.cs` | 在 `RegisterItems()` 前添加 | `TagUtils.RegisterTag("CoffeeBean", new TagConfig { Show = true });` |
| `Building/BuildingConfig.cs` | 修改 `RegisterOnBuiltCallback` | 用 `AttachViewInteract` 返回值替代 `GetComponent` |
