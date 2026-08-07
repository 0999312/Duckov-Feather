# API Reference — Items / 物品 API

> **模块**：物品创建、蓝图、子弹、Sprite、3D 模型、标签、原版反查
> **教程**：[USAGE.md 物品系统](../USAGE.md#3-物品系统--items)

---

## 目录

- [ItemUtils — 物品工具](#itemutils)
- [ItemData — 物品数据模型](#itemdata)
- [BlueprintData / BulletData — 蓝图与子弹](#blueprintdata--bulletdata)
- [UsageData / UsageBehaviorData — 使用行为](#usagedata--usagebehaviordata)
- [ModifierData — 属性修正](#modifierdata)
- [SlotData / SlotKeys — 物品槽位](#slotdata--slotkeys--物品槽位)
- [GameItemLookup — 原版物品反查](#gameitemlookup)
- [TagUtils — 标签系统](#tagutils)
- [ItemGraphicUtils — 3D 展示](#itemgraphicutils)
- [ModelUtils — OBJ 模型加载](#modelutils)
- [废弃 API](#废弃-api--obsolete)

---

## ItemUtils

**命名空间**：`FeatherMod` | **源码**：`Items/ItemUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| **创建注册** | | |
| `CreateCustomItem` | `static void CreateCustomItem(Identifier id, ItemData config)` | 同步创建并注册 |
| `CreateCustomItemAsync` | `static async UniTask CreateCustomItemAsync(Identifier id, ItemData config)` | 异步（推荐，Sprite IO 在线程池） |
| `GetCustomItem` | `static Item GetCustomItem(ItemData config)` / `(Identifier id, ItemData config)` | 仅构造不注册 |
| `GetCustomItemAsync` | `static async UniTask<Item> GetCustomItemAsync(ItemData config)` / `(Identifier id, ItemData config)` | 异步构造 |
| `RegisterItem` | `static void RegisterItem(Identifier id, Item item)` | 手动注册已构造物品 |
| `CreateCustomBullet` | `static void CreateCustomBullet(Identifier id, BulletData config)` | 创建子弹 |
| `CreateCustomBulletAsync` | `static async UniTask CreateCustomBulletAsync(Identifier id, BulletData config)` | 异步创建子弹 |
| `CreateCustomCartridge` | `static void CreateCustomCartridge(Identifier id, Identifier gameId, ItemData config)` | 基于原版弹药克隆 |
| `CreateCustomCartridgeAsync` | `static async UniTask CreateCustomCartridgeAsync(Identifier id, Identifier gameId, ItemData config)` | |
| `CreateCustomBluePrint` | `static void CreateCustomBluePrint(Identifier id, BlueprintData config)` | 创建蓝图物品 |
| `CreateCustomBluePrintAsync` | `static async UniTask CreateCustomBluePrintAsync(Identifier id, BlueprintData config)` | |
| **Bundle 注册** | | |
| `RegisterGun` | `static void RegisterGun(Identifier id, AssetBundle assetBundle, string name, int originGunID = 654)` | 注册枪支（自动复制基础枪属性） |
| `RegisterItemFromBundle` | `static void RegisterItemFromBundle(Identifier id, AssetBundle assetBundle, string name)` | 从 Bundle 注册普通物品 |
| `SetItemGraphic` | `static void SetItemGraphic(Item item, AssetBundle assetBundle, string name)` | 挂 3D 图形（AssetBundle 路径） |
| **Sprite 加载** | | |
| `LoadSprite` | `static Sprite? LoadSprite(string resourceName)` / `(Identifier id)` | 同步（旧代码兼容） |
| `LoadSpriteAsync` | `static async UniTask<Sprite?> LoadSpriteAsync(string resourceName)` / `(Identifier id)` | 异步（推荐） |
| `LoadSpriteFromDir` | `static Sprite? LoadSpriteFromDir(string modDirectory, string resourceName)` | 指定目录同步 |
| `LoadSpriteFromDirAsync` | `static async UniTask<Sprite?> LoadSpriteFromDirAsync(string modDirectory, string resourceName)` | 指定目录异步 |
| **查询 / 卸载** | | |
| `TryGetCustomItem` | `static bool TryGetCustomItem(Identifier id, out Item? item)` | Identifier 反查物品 |
| `UnregisterItem` | `static void UnregisterItem(Item item)` | 卸载单个 |
| `UnregisterAllItem` | `static void UnregisterAllItem(string? modid = null)` | 批量卸载 |
| **工具** | | |
| `SetItemProperties` | `static void SetItemProperties(Item item, ItemData config)` | 应用 ItemData 属性 |
| `GetTargetTag` | `static Tag GetTargetTag(string tagName)` | 取/注册 Tag |
| `HasTag` | `static bool HasTag(Item item, string tag)` | 物品是否带标签 |

> **TypeID 冲突自动处理**：`itemId` 冲突时从指定位置向后扫描（+10000），无空闲兜底从 90000 起。

---

## ItemData

**命名空间**：`FeatherMod` | **源码**：`Items/ItemData.cs`

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `itemId` | `int` | — | 建议起始数字 ID（冲突自动重分配） |
| `order` | `int` | `0` | 排序 |
| `localizationKey` | `string` | — | 物品名 I18n key |
| `localizationDesc` | `string` | — | 描述 I18n key |
| `weight` | `float` | — | 重量 |
| `value` | `int` | — | 价值 |
| `maxStackCount` | `int` | `1` | 最大堆叠 |
| `maxDurability` | `float` | `0f` | 最大耐久（0 = 不可损坏） |
| `quality` | `int` | — | 品质 |
| `displayQuality` | `DisplayQuality` | `None` | 显示品质 |
| `spritePath` | `string` | — | 图标路径（`assets/textures/` 下） |
| `tags` | `List<string>` | — | 标签（需先 `TagUtils.RegisterTag`） |
| `usages` | `UsageData?` | — | 使用行为 |
| `modifiers` | `List<ModifierData>` | — | 属性修正 |
| `slots` | `List<SlotData>` | 空表（无槽位） | 槽位配置，见 [SlotData](#slotdata--slotkeys--物品槽位) |

---

## BlueprintData / BulletData

### BlueprintData : ItemData

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `formulaID` | `Identifier` | `"fml:unset"` | 关联配方（自动取 `.Path` 匹配游戏 `CraftingFormula.id`） |
| `FormulaTag` | `string` | `"Formula_Blueprint"` | 研究台类别标签（见下） |
| `DefaultBlueprintTag` | `const string` | `"Formula_Blueprint"` | 默认标签 |

**FormulaTag 可选值**：`Formula_Normal`（基础工作台）/ `Formula_Blueprint`（高级工作台）/ `Formula_Medic`（医疗台）/ `Formula_Cook`（厨房）/ `Formula_Printer`（打印台），或自定义标签。

> `CreateCustomBluePrint` 自动 `TagUtils.RegisterTag` 注册 `FormulaTag` 并注入通用标签 `"Formula"`。

### BulletData : ItemData

| 字段 | 类型 | 说明 |
|------|------|------|
| `Caliber` | `string` | 口径（如 `"5.56x45"`） |
| `SFX_Put` | `string` | `"e_Item_Bullet"` |
| `damageMultiplier` | `float` | 伤害倍率 |
| `CritDamageFactorGain` / `CritRateGain` | `float` | 暴击增益 |
| `ArmorPiercingGain` / `ArmorBreakGain` | `float` | 穿甲/破甲 |
| `DurabilityCost` | `float` | 耐久消耗 |
| `ExplosionRange` / `ExplosionDamage` | `float` | 爆炸 |
| `buffChanceMultiplier` | `float` | Buff 概率倍率 |
| `bleedChance` | `float` | 出血概率 |

---

## UsageData / UsageBehaviorData

### UsageData

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `actionSound` | `string` | — | 动作音效 |
| `useSound` | `string` | — | 使用音效 |
| `useDurability` | `bool` | — | 使用消耗耐久 |
| `durabilityUsage` | `int` | `1` | 每次消耗耐久量 |
| `useTime` | `float` | `2` | 使用耗时（秒） |
| `behaviors` | `List<UsageBehaviorData>` | — | 行为列表 |

### 使用行为子类

| 类 | 关键字段 | 效果 |
|----|----------|------|
| `FoodData` | `energyValue` / `waterValue` | 食物/饮水 |
| `HealData` | `healValue` | 治疗 |
| `AddBuffData` | `buff`(Buff ID) / `chance` | 添加 Buff |
| `RemoveBuffData` | `buffID` / `removeLayerCount` | 移除 Buff |
| `ReturnItemData` | `itemTypeID` / `display` | 使用后返还物品 |

---

## ModifierData

| 字段 | 类型 | 说明 |
|------|------|------|
| `target` | `ModifierTarget` | 作用目标（如 `Player`） |
| `key` | `string` | 属性键（如 `moveSpeed`、`maxHealth`） |
| `type` | `ModifierType` | 类型（`Add` / `PercentageAdd` / `Multiplier` 等） |
| `value` | `float` | 数值 |
| `overrideOrder` | `bool` | 覆盖计算顺序 |
| `overrideOrderValue` | `int` | 顺序值 |
| `display` | `bool` | 是否在 Tooltip 显示 |

---

## SlotData / SlotKeys — 物品槽位

**命名空间**：`FeatherMod` | **源码**：`Items/ItemData.cs` / `Items/SlotKeys.cs`

### SlotData

游戏 `ItemStatsSystem.Items.Slot` 的抽象层，经 `ItemData.slots` 配置后由 ItemUtils 构造带槽位物品。**槽位兼容性完全由 Tag 决定**（游戏 `Slot.CheckAbleToPlug` 只校验 requireTags/excludeTags，不查 typeID）。

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `key` | `string` | — | 槽位唯一标识（内建 key 见 SlotKeys） |
| `spritePath` | `string` | 空 | 槽位图标路径（`assets/textures/` 下）；留空显示 UI 默认槽位图标 |
| `requireTags` | `List<string>` | — | 可装配件必须全部携带的 Tag 名称 |
| `excludeTags` | `List<string>` | — | 禁止携带的 Tag 名称 |

> **Tag 解析规则**：引用的 Tag 必须已存在（游戏原生 Tag 或 `TagUtils.RegisterTag` 注册）；不存在的 Tag 会被舍弃并告警，**槽位本身保留**。

> **装配入口**：槽位物品可通过 `Slot.Plug(item)` / `ItemUtilities.TryPlug(item)` 装配，改装界面（`ItemCustomizeView`）自动生效。

### SlotKeys — 游戏内建槽位 key 常量

仅约定标识字符串，**不固定 Tag 约束**（例如枪械槽位的 Tag 实际来自 Bundle 内枪械预制体，不同武器可能不同）。

| 常量 | 值 | 归属 |
|------|----|------|
| `Scope` / `Muzzle` / `Grip` / `Stock` / `Tec` / `Mag` | 同名 | 枪械（`1_Rifle-A_template.prefab`） |
| `PrimaryWeapon` / `SecondaryWeapon` / `MeleeWeapon` | 同名 | 角色 |
| `Helmet` | `"Helmat"`（游戏原生拼写） | 角色头盔 |
| `Armor` / `FaceMask` / `Headset` / `Backpack` / `Totem1` / `Totem2` | 同名 | 角色 |
| `Bait` | `"Bait"` | 鱼竿饵料 |
| `MonitorSlot` / `ConsoleSlot` | 同名 | 游戏机 |

---

## GameItemLookup

**命名空间**：`FeatherMod.Items` | **源码**：`Items/GameItemLookup.cs`

FML 启动时自动扫描全量原版物品，建立 `Identifier ↔ TypeID` 双向映射（约数千条）。

| 方法 | 签名 | 说明 |
|------|------|------|
| `TryGetIdentifier` | `static bool TryGetIdentifier(int typeId, out Identifier id)` | TypeID → Identifier 反查 |
| | `static bool TryGetIdentifier(string displayName, out Identifier id)` | displayName → Identifier |
| `TryFindByTag` | `static bool TryFindByTag(string tag, out IReadOnlyList<Identifier> results)` | 按标签浏览原版物品 |
| `GetAllIdentifiers` | `static IReadOnlyList<Identifier> GetAllIdentifiers()` | 遍历全部 |
| `Count` | `static int Count` | 条目数 |

> 查询顺序：FML 注册的自定义物品 → 原版物品反查表。

---

## TagUtils

**命名空间**：`FeatherMod.Items` | **源码**：`Items/TagUtils.cs`

Tags 是 FML 唯一不走 Identifier 的系统——全部视为 Common Tag（纯字符串）。

| 方法 | 签名 | 说明 |
|------|------|------|
| `RegisterTag` | `static Tag RegisterTag(string tagName, TagConfig? config = null)` | 注册（创建物品**前**调用） |
| `GetTag` | `static Tag? GetTag(string tagName)` | 查询（不创建） |
| `TagExists` | `static bool TagExists(string tagName)` | 是否存在 |
| `GetCustomTagNames` | `static IReadOnlyList<string> GetCustomTagNames()` | 全部自定义标签名 |

### TagConfig

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `Show` | `bool` | `true` | Tooltip 显示此 Tag |
| `ShowDescription` | `bool` | `false` | 显示描述文本 |
| `Color` | `Color?` | — | 图标/文字颜色 |
| `Priority` | `int` | `0` | 显示优先级（越大越靠前） |

> **注册时机**：必须在 `CreateCustomItem` 之前，否则 Tag 不会挂到物品。
> 合成配方的 `Tags` 字段是纯字符串工作台过滤，**不经过** TagUtils。

---

## ItemGraphicUtils

**命名空间**：`FeatherMod` | **源码**：`Items/ItemGraphicUtils.cs`

OBJ 简化路径的 ItemGraphic 构建（单 mesh + 单材质物品）。

| 方法 | 签名 | 说明 |
|------|------|------|
| `SetItemGraphic` | `static void SetItemGraphic(Item item, Identifier meshId, Identifier? textureId = null)` | 构建并绑定（同步） |
| `SetItemGraphicAsync` | `static async UniTask SetItemGraphicAsync(Item item, Identifier meshId, Identifier? textureId = null)` | 异步（推荐） |
| `CreateItemGraphic` | `static GameObject? CreateItemGraphic(Identifier meshId, Identifier? textureId = null)` | 仅构建（返回活动副本） |
| `CreateItemGraphicAsync` | `static async UniTask<GameObject?> CreateItemGraphicAsync(Identifier meshId, Identifier? textureId = null)` | 异步构建 |
| `SetItemGraphicFromOriginal` | `static void SetItemGraphicFromOriginal(Item item, Identifier originalItemId)` | 复用原版物品 3D 展示（纯引用，无 IO） |
| `ReleaseItemGraphic` | `static void ReleaseItemGraphic(Identifier meshId, Identifier? textureId = null)` | 释放模板缓存 |
| `ReleaseAllItemGraphics` | `static void ReleaseAllItemGraphics(string? modid = null)` | 批量释放 |

> 构建产物含 `ItemGraphicInfo` + `CharacterSubVisuals`（renderers 仅主模型 1 项）+ `GroundPoint` + `Model` 子物体。
> GO 模板按 `(meshId, textureId)` 缓存，对外返回副本。

---

## ModelUtils

**命名空间**：`FeatherMod` | **源码**：`Models/ModelUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `LoadMesh` | `static Mesh? LoadMesh(string resourceName)` / `(Identifier id)` | 同步加载 OBJ |
| `LoadMeshAsync` | `static async UniTask<Mesh?> LoadMeshAsync(string resourceName)` / `(Identifier id)` | 异步（推荐） |
| `LoadMeshFromDir` / `LoadMeshFromDirAsync` | `static (async UniTask<>) Mesh? LoadMeshFromDir(string modDirectory, string resourceName)` | 指定目录 |
| `GetModelMaterial` | `static Material? GetModelMaterial(Identifier? textureId = null)` | 纹理材质（全局缓存） |
| `GetModelMaterialAsync` | `static async UniTask<Material?> GetModelMaterialAsync(Identifier? textureId = null)` | 异步 |
| `CreateModel` | `static GameObject? CreateModel(Mesh mesh, Material? material = null)` | 纯组装（MeshFilter + MeshRenderer，无碰撞体） |
| `ReleaseModel` | `static void ReleaseModel(Identifier id)` | 释放 Mesh 缓存 |
| `ReleaseAllModels` | `static void ReleaseAllModels(string? modid = null)` | 批量释放 |

**约定**：
- 模型放 `assets/models/`（可含子目录），纹理放 `assets/textures/models/`
- OBJ 导出要求：三角面（Triangulate）、Y-up；支持 `v/vt/vn/f`（含负索引、n 边形）
- **FBX 运行时导入不支持**，`.fbx` 返回 null
- shader 取 `SodaCraft/SodaLit`，未命中降级 URP Lit
- 同 Identifier 的 Mesh 有缓存

---

## 废弃 API / Obsolete

| 成员 | 替代 |
|------|------|
| （本模块暂无） | |
