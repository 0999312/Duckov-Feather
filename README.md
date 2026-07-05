# Fast-Modding-Lib

*用于高效开发《逃离鸭科夫》(Duckov) 模组的声明式 Mod 框架。*

---

## 核心原则

**Identifier 优先**：所有 FML public API 统一使用 `Identifier("domain", "path")` 作为资源标识符。游戏原生的数字 ID（TypeID、EndowmentIndex 等）由 FML 内部自动管理，对 modder 完全透明。

```csharp
// ✅ 正确
ItemUtils.CreateCustomItem(new Identifier("mymod", "coffee"), config);

// ❌ 禁止：不再公开数字 ID API
ItemUtils.CreateCustomItem(50001, config);
```

**引用原版内容**：通过 `duckov` 域引用游戏原版物品：

```csharp
// 已知物品名称
ItemEntry.Of(Identifier("duckov", "AK-47"), 1)

// 只知道数字 TypeID → 反查
GameItemLookup.TryGetIdentifier(1001, out var id);

// 按标签浏览
GameItemLookup.TryFindByTag("Gun", out var guns);
```

---

## 文档

| 文档 | 说明 |
|------|------|
| [Docs/USAGE.md](Docs/USAGE.md) | 完整使用指南 — 快速开始、全模块 API |
| [Docs/MIGRATION.md](Docs/MIGRATION.md) | 迁移指南 — 从旧版 FML 升级 |
| [Docs/PROGRESS.md](Docs/PROGRESS.md) | 项目进度 — Phase 完成状态与变更记录 |

---

## 模块速览

| 模块 | 入口类 | 示例 |
|------|--------|------|
| **物品** | `ItemUtils` | `CreateCustomItem(id, config)` |
| **合成** | `CraftingUtils` | `AddCraftingFormula(data)` |
| **任务** | `QuestUtils` | `RegisterQuest(id, data)` |
| **商店** | `ShopUtils` | `AddGoods(data)` |
| **音频** | `AudioUtil` | `RegisterAudio(id, data)` |
| **本地化** | `I18n` | `InitI18n()` |
| **事件总线** | `EventBusManager` | `Sync.Register<T>(handler)` |
| **经济** | `EconomyUtils` | `UnlockItem(id)` |
| **Buff** | `BuffUtils` | `RegisterBuff(id, prefab)` |
| **建筑** | `BuildingUtils` | `RegisterBuilding(id, info, prefab)` |
| **Perk 技能树** | `PerkTreeUtils` | `AddPerk(id, req, icon)` |
| **天赋** | `EndowmentUtils` | `RegisterEndowment(id, config)` |
| **敌人** | `EnemyUtils` | `RegisterEnemy(id, aiConfig, preset)` |
| **NPC 武器注入** | `WeaponInjectionUtils` | `AddWeaponToPreset(pattern, item, chance)` |
| **抽奖箱注入** | `LotteryBoxUtils` | `AddItemToLotteryBox(pattern, item)` |
| **设置面板** | `ModOptionsRegistry` | `RegisterPanel(modId, name, builder)` |
| **AssetBundle** | `AssetUtil` | `LoadBundle("weapons")` |

**原版内容反查**：

| 模块 | 反查 API |
|------|---------|
| Item | `GameItemLookup.TryGetIdentifier(int, out Identifier)` |
| Buff | `BuffUtils.TryGetBuffIdentifier(int, out Identifier)` |
| Quest | `QuestUtils.TryGetQuestIdentifier(int, out Identifier)` |
| Endowment | `EndowmentRegistry.TryGetIdentifier(EndowmentIndex, out Identifier)` |

---

## 配置工程

1. 准备《逃离鸭科夫》游戏本体
2. 创建 **.NET Standard 2.1** 类库项目
3. 添加游戏 DLL 引用：

```xml
<ItemGroup>
  <Reference Include="$(DuckovPath)\Duckov_Data\Managed\TeamSoda.*" />
  <Reference Include="$(DuckovPath)\Duckov_Data\Managed\ItemStatsSystem.dll" />
  <Reference Include="$(DuckovPath)\Duckov_Data\Managed\Unity*" />
  <Reference Include="$(DuckovPath)\Duckov_Data\Managed\Newtonsoft.Json.dll" />
  <Reference Include="$(DuckovPath)\Duckov_Data\Managed\FMODUnity.dll" />
  <Reference Include="$(DuckovPath)\Duckov_Data\Managed\ParadoxNotion.dll" />
  <Reference Include="$(DuckovPath)\Duckov_Data\Managed\UniTask*" />
</ItemGroup>
```

4. 继承 `FastModdingLib.ModBehaviour` 编写主类，手动导入 FML 的 dll

---

## 快速开始

```csharp
public class MyMod : Duckov.Modding.ModBehaviour, IHasModid
{
    public string GetModid() => "MyMod";

    protected override void OnAfterSetup()
    {
        base.OnAfterSetup();

        // 注册自定义物品
        ItemUtils.CreateCustomItem(
            new Identifier("MyMod", "coffee"),
            new ItemData { itemId = 50001, maxStackCount = 10, ... });

        // 引用原版物品作为合成材料
        CraftingUtils.AddCraftingFormula(new CraftingFormulaData
        {
            Id = new Identifier("MyMod", "brew_coffee"),
            Money = 100,
            CostItems = new[] { ItemEntry.Of("duckov:Water", 1) },
            Result = ItemEntry.Of("MyMod:coffee", 1)
        });
    }
}
```

---

## 卸载生命周期

模组卸载时自动执行：

```
GameEventAdapters.TearDown()           → 解除原生事件
EventBusManager.Clear()                → 清空 handler
RegistryManager.RemoveAllByOwner()     → 批量卸载全部资源
```

无需手动处理。详见 [Docs/USAGE.md §20](Docs/USAGE.md)。

---

## 技术栈

- **目标框架**：.NET Standard 2.1
- **Harmony**：2.4.1.0（vendored）
- **Publicizer**：Krafs.Publicizer
- **异步**：UniTask
