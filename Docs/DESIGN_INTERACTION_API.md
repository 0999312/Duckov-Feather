# 交互系统 API 设计文档

> **状态**: ✅ 已实施（v2.0 增强） | **版本**: v2.0 | **日期**: 2026-07-22

---

## 1. 背景与动机

### 1.1 现状问题

FML 当前通过 `InteractTemplates.cs` 提供了 3 个交互模板（Building/PerkTree/Endowment），每个模板直接硬编码了对应的 View 调用。这导致以下问题：

| 问题 | 影响 |
|------|------|
| 每个模板硬编码一种 View | 新增交互类型 = 新增类，不可组合 |
| `perkTreeID` / `buildingIdentifier` 为 `private` | modder 必须反射才能程序化设置 |
| 无生命周期追踪 | 生成物无法随 mod 卸载自动清理 |
| 仅支持 Unity Inspector 挂载 | 无法纯代码生成交互点、无法动态挂载到 NPC |
| `targetViewType` 是 `string` 比较 | 拼写错误零编译检查 |
| 无 View 注册/分发机制 | 每新增一种交互都要重复"创建 GameObject → 加 Collider → 加 InteractableBase" |

### 1.2 设计目标

1. **统一交互模型**：所有交互场景（建筑物件、NPC、独立交互点）走同一套 API
2. **View 分发抽象**：用 `Identifier` 标识 View 类型，支持 modder 注册自定义 View
3. **生命周期可追踪**：所有生成物进入 Registry，随 mod 卸载自动清理
4. **完全程序化可用**：零反射即可 spawn / attach / dispatch
5. **向后兼容**：现有 3 个 InteractTemplate 继续工作，逐步迁移

---

## 2. 核心概念

### 2.1 交互链路

```
玩家按F  →  InteractableBase.OnInteractFinished()
         →  IInteractHandler.HandleInteract()
         →  ViewDispatcher.Open(viewType, param)
         →  游戏原生 View.Show(...)
```

### 2.2 角色划分

| 角色 | 职责 | 类比 |
|------|------|------|
| `IInteractHandler` | 定义交互触发后做什么 | 替代当前硬编码 `OnInteractFinished` |
| `InteractPoint` | 封装一个可交互的 GameObject（含 Collider + Handler） | 当前 `PerkTreeInteractTemplate` 等 |
| `ViewDispatcher` | 将 `Identifier` 映射到具体的 View 调用 | 当前不存在，各模板自行调用 |
| `InteractionUtils` | 对外 public API：spawn / attach / dispatch | 对标 `BuildingUtils` / `PerkTreeUtils` |
| `InteractionRegistry` | 追踪所有生成物 + 批量卸载 | 对标 `BuildingRegistry` / `PerkTreeRegistry` |

---

## 3. 文件结构

```
FastModdingLib/Interaction/
├── InteractionUtils.cs               ← public API（spawn / attach / dispatch / cleanup）
├── InteractionRegistry.cs            ← SimpleRegistry<InteractionEntry>，OnRemoved 销毁 GameObject
├── InteractionEntry.cs               ← 数据模型：Identifier + GameObject + modid
├── InteractionFactory.cs             ← internal：GameObject 创建 + 组件挂载
├── Components/
│   ├── ViewInteractHandler.cs        ← InteractableBase 子类，打开指定 View
│   ├── DelegateInteractHandler.cs    ← InteractableBase 子类，调用自定义 delegate
│   └── IInteractHandler.cs           ← 交互处理器接口
├── ViewDispatcher.cs                 ← 内置 View 注册 + modder 扩展点
└── Patches/
    └── InteractPointGuard.cs          ← Harmony：确保生成物不受游戏原生逻辑干扰

FastModdingLib/UI/
└── InteractTemplates.cs              ← [保留] 旧模板迁移为 Handler 包装（向后兼容）
```

---

## 4. API 详细规格

### 4.1 `InteractionUtils` — 主入口

```csharp
namespace FeatherMod
{
    /// <summary>交互系统统一入口。</summary>
    public static class InteractionUtils
    {
        // ═══════════════════════════════════════════════════════
        //  初始化（由 RegisterBootstrap 调用）
        // ═══════════════════════════════════════════════════════

        /// <summary>将 InteractionRegistry 注册到元表。幂等。</summary>
        internal static void Init();
    }
}
```

---

### 4.2 Spawn — 生成交互点

#### `SpawnViewInteract`

在指定世界坐标生成一个交互点，交互时打开指定的游戏 UI。

```csharp
/// <summary>
/// 在指定世界坐标生成一个交互点，交互时打开指定的游戏视图。
/// 自动创建 GameObject + BoxCollider(Trigger) + "Interact" 图层 + ViewInteractHandler。
/// 生成物自动注册到 InteractionRegistry，mod 卸载时自动销毁。
/// </summary>
/// <param name="id">交互点 Identifier（Domain=modid, Path=唯一名称）。</param>
/// <param name="position">世界坐标。</param>
/// <param name="viewType">要打开的视图类型（如 GameViews.PerkTree）。</param>
/// <param name="viewParam">视图参数（如 PerkTree 的 treeId）。null 表示无参数。</param>
/// <param name="rotation">朝向。默认 Quaternion.identity。</param>
/// <param name="colliderSize">碰撞体尺寸。默认 (1, 1, 1)。</param>
/// <param name="interactNameKey">[v2.0] 交互提示文本本地化键（如 "UI_Craft_Drinks"）。非空时自动设置 overrideInteractName=true。</param>
/// <param name="markerOffset">[v2.0] 标记世界空间偏移（如头顶指示器位置）。null 使用默认值。</param>
/// <param name="coolTime">[v2.0] 交互冷却时间（秒）。0=无冷却。</param>
/// <returns>生成的交互点 GameObject（已挂载 ViewInteractHandler）。</returns>
/// <exception cref="ArgumentException">id 格式无效。</exception>
/// <example>
/// // 生成一个打开 PerkTree 的交互点（v2.0 含交互名）
/// InteractionUtils.SpawnViewInteract(
///     new Identifier("mymod", "combat_terminal"),
///     new Vector3(100f, 0f, 50f),
///     GameViews.PerkTree,
///     viewParam: "combat_tactics",
///     interactNameKey: "UI_Perk_Combat"
/// );
/// </example>
public static GameObject SpawnViewInteract(
    Identifier id,
    Vector3 position,
    Identifier viewType,
    string? viewParam = null,
    Quaternion? rotation = null,
    Vector3? colliderSize = null,
    string? interactNameKey = null,
    Vector3? markerOffset = null,
    float coolTime = 0f
);
```

**内部流程**：
1. `InteractionFactory.CreateInteractPoint(id, position, rotation, colliderSize)` → 创建 GameObject + BoxCollider + "Interact" 图层
2. `go.AddComponent<ViewInteractHandler>()` → 设置 `ViewType` / `ViewParam`
3. `_registry.Register(id, go, id.Domain)` → 追踪生命周期
4. 返回 GameObject

---

#### `SpawnCustomInteract`

生成自定义交互点（modder 完全控制交互行为）。

```csharp
/// <summary>
/// 在指定世界坐标生成一个自定义交互点，交互时调用指定的 delegate。
/// </summary>
/// <param name="id">交互点 Identifier。</param>
/// <param name="position">世界坐标。</param>
/// <param name="onInteract">交互触发回调。</param>
/// <param name="rotation">朝向。默认 identity。</param>
/// <param name="colliderSize">碰撞体尺寸。默认 (2, 3, 2)。</param>
/// <returns>生成的交互点 GameObject。</returns>
/// <example>
/// InteractionUtils.SpawnCustomInteract(
///     new Identifier("mymod", "secret_button"),
///     new Vector3(50f, 0f, 30f),
///     onInteract: () => {
///         Debug.Log("玩家触发了秘密按钮！");
///         // 播放音效、解锁物品等
///     }
/// );
/// </example>
public static GameObject SpawnCustomInteract(
    Identifier id,
    Vector3 position,
    Action onInteract,
    Quaternion? rotation = null,
    Vector3? colliderSize = null
);
```

---

#### `SpawnPrefabInteract`

使用 modder 自定义 Prefab 生成交互点（适用于复杂视觉需求）。

```csharp
/// <summary>
/// 使用已挂载 InteractableBase 的 Prefab 生成交互点。
/// FML 只负责生命周期追踪和位置放置，不修改 Prefab 上的组件。
/// </summary>
/// <param name="id">交互点 Identifier。</param>
/// <param name="prefab">已挂载 InteractableBase 子类的 Prefab。</param>
/// <param name="position">世界坐标。</param>
/// <param name="rotation">朝向。默认 identity。</param>
/// <returns>生成的实例。</returns>
/// <remarks>
/// 适用于 modder 需要在 Unity 编辑器中精心设计交互点外观的场景。
/// Prefab 根节点必须挂载 InteractableBase 或其子类。
/// </remarks>
public static GameObject SpawnPrefabInteract(
    Identifier id,
    GameObject prefab,
    Vector3 position,
    Quaternion? rotation = null
);
```

---

### 4.3 Attach — 挂载交互到已有对象

#### `AttachViewInteract`

给任意已有 GameObject 挂载交互行为。

```csharp
/// <summary>
/// 给已有 GameObject 挂载视图交互。GameObject 需要已有或 FML 自动添加碰撞体。
/// </summary>
/// <param name="id">交互点 Identifier。</param>
/// <param name="target">目标 GameObject。</param>
/// <param name="viewType">要打开的视图类型。</param>
/// <param name="viewParam">视图参数。</param>
/// <param name="addColliderIfMissing">
/// 如果 target 上无任何 Collider，是否自动添加 BoxCollider(Trigger)。
/// 默认 true。
/// </param>
/// <param name="interactNameKey">[v2.0] 交互提示文本本地化键。</param>
/// <param name="markerOffset">[v2.0] 标记世界空间偏移。</param>
/// <param name="coolTime">[v2.0] 交互冷却时间（秒）。</param>
/// <example>
/// // 给建筑 functionContainer 挂载带交互名的 Crafting 交互
/// InteractionUtils.AttachViewInteract(
///     new Identifier("mymod", "craft_01"),
///     BuildingUtils.GetFunctionContainer(building),
///     GameViews.Crafting,
///     viewParam: "Drink",
///     interactNameKey: "UI_Craft_Drinks"
/// );
/// </example>
public static void AttachViewInteract(
    Identifier id,
    GameObject target,
    Identifier viewType,
    string? viewParam = null,
    string? interactNameKey = null,
    Vector3? markerOffset = null,
    float coolTime = 0f,
    bool addColliderIfMissing = true
);
```

---

#### `AttachCustomInteract`

给已有 GameObject 挂载自定义交互。

```csharp
/// <summary>
/// 给已有 GameObject 挂载自定义交互行为。
/// </summary>
public static void AttachCustomInteract(
    Identifier id,
    GameObject target,
    Action onInteract,
    bool addColliderIfMissing = true
);
```

---

#### `AttachToNPC`

给指定名称的 NPC 挂载交互。

```csharp
/// <summary>
/// 在场景中按名称查找 NPC 并挂载交互。
/// 使用 <c>GameObject.Find(npcName)</c> + 遍历 <c>AICharacterController</c> 的兜底策略。
/// </summary>
/// <param name="id">交互点 Identifier。</param>
/// <param name="npcName">NPC 的 GameObject.name 或 nameKey。</param>
/// <param name="viewType">打开的视图类型。</param>
/// <param name="viewParam">视图参数。</param>
/// <returns>是否成功找到并挂载。false 表示场景中无此 NPC。</returns>
/// <remarks>
/// 若 NPC 已有 InteractableBase（如原生商人），会追加新的 Handler 通过多组件共存；
/// 若 NPC 无可交互组件，自动添加 ViewInteractHandler。
/// </remarks>
public static bool AttachToNPC(
    Identifier id,
    string npcName,
    Identifier viewType,
    string? viewParam = null
);
```

---

#### `AttachToNPCByPreset`

按 Preset 名称匹配 NPC 并批量挂载交互。

```csharp
/// <summary>
/// 对所有匹配指定 Preset 名称模式的 NPC 挂载交互。
/// 支持 <c>*</c> 通配符（与 WeaponInjectionUtils 一致）。
/// </summary>
/// <param name="id">交互点 Identifier。</param>
/// <param name="presetNamePattern">Preset 名称模式（如 "Merchant_*"）。</param>
/// <param name="viewType">打开的视图类型。</param>
/// <param name="viewParam">视图参数。</param>
/// <returns>成功挂载的 NPC 数量。</returns>
/// <example>
/// // 给所有商人挂载 PerkTree 入口
/// int count = InteractionUtils.AttachToNPCByPreset(
///     new Identifier("mymod", "merchant_perks"),
///     "Merchant_*",
///     GameViews.PerkTree,
///     viewParam: "combat_tactics"
/// );
/// </example>
public static int AttachToNPCByPreset(
    Identifier id,
    string presetNamePattern,
    Identifier viewType,
    string? viewParam = null
);
```

---

### 4.4 Dispatch — View 分发

#### `ViewDispatcher` — 内置 View 注册表

```csharp
namespace FeatherMod.Interaction
{
    /// <summary>
    /// View 分发器。维护 <see cref="Identifier"/> → View 打开方法的映射。
    /// 内置 FML 已知的游戏 View，并支持 modder 注册自定义 View。
    /// </summary>
    public static class ViewDispatcher
    {
        // ═══════════════════════════════════════════════════
        //  注册 / 注销
        // ═══════════════════════════════════════════════════

        /// <summary>注册一个 View 打开方法（modder 自定义 View）。</summary>
        /// <param name="viewType">View 类型标识（如 new Identifier("mymod", "my_view")）。</param>
        /// <param name="openAction">打开 View 的回调，参数为 viewParam（可为 null）。</param>
        /// <param name="modid">modid，用于卸载时清理。</param>
        public static void Register(
            Identifier viewType,
            Action<string?> openAction,
            string modid
        );

        /// <summary>移除指定 View 注册。</summary>
        public static bool Unregister(Identifier viewType);

        /// <summary>批量移除指定 mod 的全部 View 注册。</summary>
        public static int UnregisterAll(string modid);

        // ═══════════════════════════════════════════════════
        //  分发
        // ═══════════════════════════════════════════════════

        /// <summary>打开指定 View。viewType 未注册时打印警告并忽略。</summary>
        public static void Open(Identifier viewType, string? viewParam = null);

        /// <summary>检查指定 View 类型是否已注册。</summary>
        public static bool IsRegistered(Identifier viewType);
    }
}
```

#### `GameViews` — 内置 View 类型常量

```csharp
namespace FeatherMod
{
    /// <summary>
    /// FML 预置的交互 View 类型。
    /// 均为 <c>Identifier("fml", ...)</c>，与 modder 注册的 View 共用同一分发器。
    /// </summary>
    public static class GameViews
    {
        /// <summary>技能树视图。viewParam = treeId（由 RegisterPerkTree 注册时的 Path）。</summary>
        public static readonly Identifier PerkTree = new Identifier("fml", "perktree");

        /// <summary>建筑视图。viewParam = area id（可选）。</summary>
        public static readonly Identifier Building  = new Identifier("fml", "building");

        /// <summary>天赋选择面板。</summary>
        public static readonly Identifier Endowment = new Identifier("fml", "endowment");

        /// <summary>商店视图。viewParam = shop id。</summary>
        public static readonly Identifier Shop      = new Identifier("fml", "shop");

        /// <summary>合成台视图。</summary>
        public static readonly Identifier Crafting  = new Identifier("fml", "crafting");

        /// <summary>任务面板视图。</summary>
        public static readonly Identifier Quest     = new Identifier("fml", "quest");
    }
}
```

**启动时自动注册**（`InteractionUtils.Init()` 内部）：

```csharp
// 内置 View 注册（编译时绑定，不依赖反射）
ViewDispatcher.Register(GameViews.PerkTree,  param => { /* PerkTreeView.Show(...) */ },  FMLConstants.Domain);
ViewDispatcher.Register(GameViews.Building,  param => { /* BuilderView.Show(...) */  },  FMLConstants.Domain);
ViewDispatcher.Register(GameViews.Endowment, param => { /* EndowmentSelectionPanel */  }, FMLConstants.Domain);
ViewDispatcher.Register(GameViews.Shop,      param => { /* ShopView.Show(param) */  },  FMLConstants.Domain);
```

---

### 4.5 Components — 交互处理器

#### `IInteractHandler` 接口

```csharp
namespace FeatherMod.Interaction.Components
{
    /// <summary>交互处理器接口。替代直接继承 InteractableBase 的碎片化模式。</summary>
    public interface IInteractHandler
    {
        /// <summary>交互触发时调用。</summary>
        void HandleInteract();
    }
}
```

#### `ViewInteractHandler` — 视图交互处理器

```csharp
namespace FeatherMod.Interaction.Components
{
    /// <summary>
    /// 交互时通过 ViewDispatcher 打开指定游戏视图。
    /// 继承游戏原生 <see cref="InteractableBase"/>，
    /// 实现 FML 的 <see cref="IInteractHandler"/>。
    /// </summary>
    public class ViewInteractHandler : InteractableBase, IInteractHandler
    {
        /// <summary>要打开的视图类型。public 可程序化设置。</summary>
        public Identifier ViewType;

        /// <summary>视图参数（如 PerkTree 的 treeId、Shop 的 shopId）。</summary>
        public string? ViewParam;

        /// <summary>交互触发时调用 ViewDispatcher.Open。</summary>
        protected override void OnInteractFinished()
        {
            HandleInteract();
        }

        public void HandleInteract()
        {
            ViewDispatcher.Open(ViewType, ViewParam);
        }
    }
}
```

**对比旧版**：
```
旧: PerkTreeInteractTemplate  ──硬编码──→  PerkTreeView.Show(tree)
    BuildingInteractTemplate  ──硬编码──→  BuilderView.Show(null)
    EndowmentInteractTemplate ──硬编码──→  EndowmentSelectionPanel

新: ViewInteractHandler      ──ViewDispatcher──→  任意已注册 View
    (一个组件覆盖全部 View 类型)
```

#### `DelegateInteractHandler` — 自定义交互处理器

```csharp
namespace FeatherMod.Interaction.Components
{
    /// <summary>
    /// 交互时调用 modder 指定的 delegate。
    /// 适用于完全自定义的交互逻辑（播放音效、触发事件、修改状态等）。
    /// </summary>
    public class DelegateInteractHandler : InteractableBase, IInteractHandler
    {
        /// <summary>交互触发回调。public 可直接赋值。</summary>
        public Action? OnInteract;

        protected override void OnInteractFinished()
        {
            HandleInteract();
        }

        public void HandleInteract()
        {
            OnInteract?.Invoke();
        }
    }
}
```

---

### 4.6 查询与清理

```csharp
// ═══════════════════════════════════════════
//  查询
// ═══════════════════════════════════════════

/// <summary>按 Identifier 查找已生成的交互点。</summary>
public static GameObject? GetInteractPoint(Identifier id);

/// <summary>安全查找。</summary>
public static bool TryGetInteractPoint(Identifier id, out GameObject? point);

/// <summary>获取指定 mod 生成的全部交互点 Identifier 列表。</summary>
public static IReadOnlyList<Identifier> GetAllByOwner(string modid);

// ═══════════════════════════════════════════
//  清理
// ═══════════════════════════════════════════

/// <summary>按 Identifier 移除交互点（销毁 GameObject + 从 Registry 移除）。</summary>
public static bool RemoveInteract(Identifier id);

/// <summary>批量移除指定 mod 的全部交互点和 View 注册。返回移除总数。</summary>
public static int RemoveAllInteracts(string modid);
```

---

### 4.7 `InteractionRegistry` — 注册表

```csharp
namespace FeatherMod.Interaction
{
    /// <summary>
    /// 交互点注册表。维护 Identifier → InteractionEntry 映射。
    /// OnRemoved 时自动销毁 GameObject。
    /// </summary>
    public sealed class InteractionRegistry : SimpleRegistry<InteractionEntry>
    {
        protected override void OnRemoved(Identifier id, InteractionEntry entry, string? modid)
        {
            if (entry?.Target != null)
            {
                UnityEngine.Object.Destroy(entry.Target);
            }
        }
    }

    /// <summary>交互点条目。</summary>
    public class InteractionEntry
    {
        /// <summary>交互点 GameObject（拥有 InteractableBase 组件）。</summary>
        public GameObject Target;

        /// <summary>交互处理器（ViewInteractHandler 或 DelegateInteractHandler）。</summary>
        public IInteractHandler? Handler;

        /// <summary>归属 modid。</summary>
        public string Modid;
    }
}
```

---

## 5. 现有代码迁移路径

### 5.1 `InteractTemplates.cs` → 保留为兼容层

```csharp
// 旧版保留，内部委托给新 Handler
[Obsolete("Use InteractionUtils.SpawnViewInteract + GameViews.PerkTree instead.")]
public class PerkTreeInteractTemplate : InteractableBase
{
    [SerializeField] private string? perkTreeID;

    protected override void OnInteractFinished()
    {
        if (!string.IsNullOrEmpty(perkTreeID))
            ViewDispatcher.Open(GameViews.PerkTree, perkTreeID);
    }
}
```

三个旧模板标记 `[Obsolete]`，内部改为调用 `ViewDispatcher.Open()`。

### 5.2 `perkTreeID` → 通过 Handler 公开

不再需要反射。新 API 中所有字段均为 `public`：

```csharp
// 旧（需要反射）
typeof(PerkTreeInteractTemplate)
    .GetField("perkTreeID", ...)
    ?.SetValue(interact, "combat_tactics");

// 新（直接赋值）
var handler = go.GetComponent<ViewInteractHandler>();
handler.ViewType = GameViews.PerkTree;
handler.ViewParam = "combat_tactics";
```

### 5.3 `BuildingInteractTemplate.targetViewType` → 消灭 string 比较

```csharp
// 旧（string 比较，拼写错误零提示）
if (targetViewType == "BuilderView")  // ❌

// 新（编译时检查）
ViewDispatcher.Register(GameViews.Building, param => BuilderView.Show(param), ...);
ViewDispatcher.Open(GameViews.Building, areaId);  // ✅
```

---

## 6. 使用场景示例

### 场景 A：生成 PerkTree 终端

```csharp
protected override void OnAfterSetup()
{
    // 注册 PerkTree
    PerkTreeUtils.RegisterPerkTree(
        new Identifier("mymod", "combat_tactics"), horizontal: false);

    // 在世界坐标生成交互点
    InteractionUtils.SpawnViewInteract(
        new Identifier("mymod", "combat_terminal"),
        new Vector3(100f, 0f, 50f),
        GameViews.PerkTree,
        viewParam: "combat_tactics"
    );
}
// 一键生成 = 旧方案 15 行 → 新方案 1 行
```

### 场景 B：给商人 NPC 添加 PerkTree 入口

```csharp
InteractionUtils.AttachToNPC(
    new Identifier("mymod", "merchant_perks"),
    "Merchant_Ivan",
    GameViews.PerkTree,
    viewParam: "combat_tactics"
);
```

### 场景 C：自定义交互（密码门）

```csharp
InteractionUtils.SpawnCustomInteract(
    new Identifier("mymod", "secret_door"),
    new Vector3(200f, 0f, 80f),
    onInteract: () => {
        AudioUtil.PlaySound(new Identifier("mymod", "door_open"));
        // 开门逻辑...
    }
);
```

### 场景 D：注册自定义 View 类型

```csharp
// modder 自己的 View
ViewDispatcher.Register(
    new Identifier("mymod", "skill_tree"),
    param => MySkillTreeView.Show(param),
    "mymod"
);

// 然后像内置 View 一样使用
InteractionUtils.SpawnViewInteract(
    new Identifier("mymod", "skill_terminal"),
    Vector3.zero,
    new Identifier("mymod", "skill_tree"),
    viewParam: "advanced_skills"
);
```

### 场景 E：批量给所有商人挂载

```csharp
int count = InteractionUtils.AttachToNPCByPreset(
    new Identifier("mymod", "all_merchants"),
    "Merchant_*",       // 通配符匹配
    GameViews.Shop,
    viewParam: "black_market"
);
Debug.Log($"已为 {count} 个商人挂载黑市入口");
```

### 场景 F：完整卸载

```csharp
// mod 卸载时，一行清理全部交互点 + View 注册
int removed = InteractionUtils.RemoveAllInteracts("mymod");
// 自动销毁 GameObject、移除 Registry 条目、清理 ViewDispatcher 注册
```

---

## 7. 补充 API 清单

### 7.1 新增文件

| 优先级 | 文件 | 行数估算 | 说明 |
|--------|------|---------|------|
| 🔴 P0 | `Interaction/Components/IInteractHandler.cs` | ~15 | 交互处理器接口 |
| 🔴 P0 | `Interaction/Components/ViewInteractHandler.cs` | ~40 | 视图交互处理器（替代 3 个旧模板的核心） |
| 🔴 P0 | `Interaction/Components/DelegateInteractHandler.cs` | ~30 | 自定义 delegate 处理器 |
| 🔴 P0 | `Interaction/InteractionEntry.cs` | ~20 | 交互点数据模型 |
| 🔴 P0 | `Interaction/InteractionRegistry.cs` | ~30 | Registry（OnRemoved 销毁 GameObject） |
| 🔴 P0 | `Interaction/InteractionFactory.cs` | ~80 | GameObject 创建（图层/碰撞体/组件挂载） |
| 🔴 P0 | `Interaction/ViewDispatcher.cs` | ~100 | View 注册表 + 分发 |
| 🔴 P0 | `Interaction/InteractionUtils.cs` | ~250 | 全部 public API |
| 🟡 P1 | `Interaction/Patches/InteractPointGuard.cs` | ~30 | Harmony 保护自定义交互点 |
| 🟢 P2 | `GameViews.cs` | ~30 | 内置 View 类型常量（可并入 InteractionUtils 顶部） |

### 7.2 修改现有文件

| 优先级 | 文件 | 改动 |
|--------|------|------|
| 🔴 P0 | `UI/InteractTemplates.cs` | 3 个旧模板标记 `[Obsolete]`，`OnInteractFinished` 改为调用 `ViewDispatcher.Open()` |
| 🔴 P0 | `Register/RegisterBootstrap.cs` | 新增 `InteractionUtils.Init()` 调用 |
| 🟡 P1 | `PerkTreeUtils.cs` | `RegisterPerkTree` 末尾可选调用 `InteractionUtils` 自动登记（或保持独立） |
| 🟢 P2 | `Docs/USAGE.md` | 新增 §14.5 "交互系统（InteractionUtils）" |

### 7.3 新 API 方法总览

| 方法 | 所属类 | 说明 |
|------|--------|------|
| `SpawnViewInteract(id, pos, viewType, param, rot, size)` | `InteractionUtils` | 生成视图交互点 |
| `SpawnCustomInteract(id, pos, onInteract, rot, size)` | `InteractionUtils` | 生成自定义交互点 |
| `SpawnPrefabInteract(id, prefab, pos, rot)` | `InteractionUtils` | 从 Prefab 生成交互点 |
| `AttachViewInteract(id, target, viewType, param)` | `InteractionUtils` | 挂载视图交互到已有对象 |
| `AttachCustomInteract(id, target, onInteract)` | `InteractionUtils` | 挂载自定义交互到已有对象 |
| `AttachToNPC(id, npcName, viewType, param)` | `InteractionUtils` | 按名称挂载到 NPC |
| `AttachToNPCByPreset(id, pattern, viewType, param)` | `InteractionUtils` | 按 Preset 模式挂载到 NPC |
| `GetInteractPoint(id)` / `TryGetInteractPoint(id, out)` | `InteractionUtils` | 查询交互点 |
| `GetAllByOwner(modid)` | `InteractionUtils` | 按 modid 查询 |
| `RemoveInteract(id)` | `InteractionUtils` | 移除单个交互点 |
| `RemoveAllInteracts(modid)` | `InteractionUtils` | 批量清理 |
| `Register(viewType, openAction, modid)` | `ViewDispatcher` | 注册自定义 View |
| `Unregister(viewType)` | `ViewDispatcher` | 注销 View |
| `UnregisterAll(modid)` | `ViewDispatcher` | 批量注销 View |
| `Open(viewType, param)` | `ViewDispatcher` | 打开 View |
| `IsRegistered(viewType)` | `ViewDispatcher` | 检查 View 已注册 |

---

## 8. 实现路线图

### Phase 1 — 核心骨架（P0，约 4 小时）

1. 创建 `InteractionEntry` + `InteractionRegistry`
2. 创建 `IInteractHandler` + `ViewInteractHandler` + `DelegateInteractHandler`
3. 创建 `InteractionFactory`（GameObject 创建逻辑）
4. 创建 `ViewDispatcher` + 注册内置 6 种 View
5. 创建 `InteractionUtils`（Spawn / Attach / Query / Cleanup）
6. 修改 `RegisterBootstrap` 添加 `InteractionUtils.Init()`
7. 编译通过

### Phase 2 — 兼容迁移（P1，约 1 小时）

1. 修改 `InteractTemplates.cs` 标记 `[Obsolete]`，内部改为 `ViewDispatcher.Open()`
2. 验证旧 mod 兼容

### Phase 3 — 文档与测试（P2，约 2 小时）

1. `Docs/USAGE.md` 新增交互系统章节
2. 编写 `InteractionUtilsTest.cs`
3. 编写 `ViewDispatcherTest.cs`

---

## 9. 现状审计发现（2026-07-11 代码探索结论）

> 以下是对现有交互代码的完整审计，作为本设计的依据。

### 9.1 现有 3 个 InteractTemplate 的实际状态

| 模板 | `OnInteractFinished` 实际行为 | 问题 |
|------|------------------------------|------|
| `BuildingInteractTemplate` | `if (targetViewType == "BuilderView") BuilderView.Show(null)` | 字符串比较、不可扩展、`buildingIdentifier` 字段未使用 |
| `PerkTreeInteractTemplate` | 查树存在但 `PerkTreeView.Show(tree)` **被注释掉**，仅 Warn | 实际不打开任何 View |
| `EndowmentInteractTemplate` | **完全为空** | 无任何实现 |

### 9.2 关键发现

1. **3 个模板在代码中从未被 `AddComponent` 实例化** — 设计意图是 Unity 编辑器手动挂载或预制体克隆附带
2. **`BuildingUtils.CreateBuildingFromScratch`** 创建了 `functionContainer` + `BoxCollider(Trigger)` + `"Interact"` 图层，但**没有挂载任何 InteractTemplate**
3. **`BuildingUtils.PlaceBuilding` + `OnBuildingBuilt` 回调**通过反射订阅原生 `BuildingManager.OnBuildingBuiltComplex` 事件，是唯一的"交互后"回调机制
4. **`LotteryBoxPatch`** 是唯一直接 patch `InteractableBase.Awake()` 的补丁 — 交互系统与 EventBus 完全无关联
5. **EventBus 桥接了 15 个游戏事件，无一与交互相关**
6. **FML 代码中唯一的 View 打开调用**是 `BuilderView.Show(null)`（`InteractTemplates.cs:28`）

### 9.3 生命周期模式参考（来自各 Registry）

| 模式 | 示例 | 说明 |
|------|------|------|
| **A: Registry OnRemoved 销毁** | `PerkTreeRegistry` / `EndowmentRegistry` | `OnRemoved` 中 `Object.Destroy(value.gameObject)`，自动追踪 |
| **B: 手动字典追踪** | `PerkTreeUtils._treeIdsByOwner` | 额外的清理字典，在 `RemoveAllPerks` 中手动清理 |
| **C: 独立 SimpleRegistry（缺口）** | `MinigameUtil` | 有 Registry 但无 `OnRemoved`，卸载时 GameObject 不销毁 |

本设计采用 **模式 A**（`InteractionRegistry.OnRemoved` 自动销毁），对标 `PerkTreeRegistry` 的成熟模式。

---

## 10. 设计决策记录

### Q: 为什么用 `Identifier` 而非 `enum` 标识 View 类型？

enum 不可扩展——modder 无法注册自定义 View。`Identifier` 允许任意 `"modid:viewname"` 格式的 View 注册，与 FML 其他模块保持一致。

### Q: 为什么保留旧 InteractTemplates 而非直接删除？

已有 mod 可能引用了这些类。标记 `[Obsolete]` 给出迁移窗口，内部改为调用新 API 确保行为一致。

### Q: 为什么需要独立的 `InteractionRegistry` 而非复用 PerkTreeRegistry？

交互点生命周期独立于 PerkTree——删除 PerkTree 不应删除交互点 GameObject，反之亦然。独立 Registry 是正交设计。

### Q: `AttachToNPCByPreset` 的通配符匹配时机？

使用 `InteractableBase.Awake` 的 Harmony Postfix（对标 `LotteryBoxPatch` 的注入时机），在场景加载时自动匹配并挂载，无需 modder 手动查找。

---

## 10. 附录：与现有模块的交互矩阵

| 现有模块 | 旧交互方式 | 新交互方式 |
|----------|-----------|-----------|
| PerkTree | `PerkTreeInteractTemplate`（private perkTreeID） | `SpawnViewInteract(id, pos, GameViews.PerkTree, "treeId")` |
| Building | `BuildingInteractTemplate`（string targetViewType） | `SpawnViewInteract(id, pos, GameViews.Building, areaId)` |
| Endowment | `EndowmentInteractTemplate` | `SpawnViewInteract(id, pos, GameViews.Endowment)` |
| Shop | 无独立交互模板 | `SpawnViewInteract(id, pos, GameViews.Shop, shopId)` |
| Crafting | 无独立交互模板 | `SpawnViewInteract(id, pos, GameViews.Crafting)` |
| Quest | 无独立交互模板 | `SpawnViewInteract(id, pos, GameViews.Quest)` |
| NPC | 无 | `AttachToNPC(id, npcName, viewType, param)` |
| 自定义 View | 不支持 | `ViewDispatcher.Register(viewType, action, modid)` |

---

## 11. v2.0 新增 API（FEATHER_API_GAPS 修复）

> 以下为 2026-07-22 交互系统重新设计中新增的 API，解决 `Docs/TODO/FEATHER_API_GAPS.md` 中报告的 5 个缺口。

### 11.1 `InteractionUtils.SetupInteractionGroup` — 多交互组装

从 `FriendlyNpcUtils` 提取并公开化的多交互组装 API，支持任意 `InteractableBase` 组合。

```csharp
/// <summary>
/// 组装多交互组。指定 primary 为主交互体，其余 member 加入其交互组。
/// 自动禁用 member 的独立碰撞体和标记，同步坐标到 primary。
/// </summary>
/// <param name="primary">主交互体——玩家按 E 键直接触发。</param>
/// <param name="members">其他交互体，加入 primary.otherInterablesInGroup。</param>
public static void SetupInteractionGroup(
    InteractableBase primary, params InteractableBase[] members
);

// 用法示例：
var primary = func.GetComponent<ViewInteractHandler>(); // 第一个交互
var member = ...; // 第二个交互
InteractionUtils.SetupInteractionGroup(primary, member);
```

### 11.2 `InteractionGroupBuilder` — 声明式多交互组 Builder

对标 `DialogueSequence.Build` 的 Builder 模式，支持链式声明。

```csharp
/// <summary>声明式多交互组构建器。</summary>
public class InteractionGroupBuilder
{
    public InteractionGroupBuilder Add(
        Identifier id, Identifier viewType,
        string? viewParam = null, string? interactNameKey = null,
        Vector3? markerOffset = null);
    public InteractionGroupBuilder WithPrimary(int index);
    public ViewInteractHandler BuildOn(GameObject target);
}

// 用法示例：
new InteractionGroupBuilder()
    .Add(id1, GameViews.Crafting, "drink", interactNameKey: "UI_Craft")
    .Add(id2, GameViews.PerkTree, "brewmaster", interactNameKey: "UI_Perk")
    .WithPrimary(0)
    .BuildOn(functionContainer);
```

### 11.3 `CraftingInteractTemplate` — 合成交互模板

```csharp
public class CraftingInteractTemplate : InteractableBase
{
    public string? CraftingTag;      // 配方标签过滤（对应 Recipe.Tags）
    public string? InteractNameKey;  // 交互提示本地化键
    // OnInteractFinished → ViewDispatcher.Open(GameViews.Crafting, CraftingTag)
}
```

### 11.4 `BuildingUtils` 容器访问公开

```csharp
/// <summary>获取建筑的 functionContainer（交互碰撞体所在）。</summary>
public static GameObject? GetFunctionContainer(Building building);

/// <summary>获取建筑的 graphicsContainer（模型和物理碰撞体所在）。</summary>
public static GameObject? GetGraphicsContainer(Building building);
```

### 11.5 Feather 原版组件封装（`Interaction/Components/`）

| 类 | 封装对象 | 用途 |
|---|---------|------|
| `FeatherShopInteract` | `NpcShopInteract` + `StockShop` | 非 NPC 场景的商店交互，自动管理 `StockShop` 生命周期 |
| `FeatherQuestGiverInteract` | `QuestGiver` | 非 NPC 场景的任务交互入口 |
| `FeatherPerkTreeInteract` | `PerkTreeUIInvoker` | 非 NPC 场景的技能树交互入口 |

每个封装类：
- 继承 `InteractableBase`
- 提供 `InteractNameKey` / `InteractId` 字段
- 提供 `Attach` 静态工厂方法（deactivate→AddComponent→set fields→reactivate 模式）
- 自动注册到 `InteractionRegistry`，mod 卸载时自动清理

```csharp
// 用法示例：
FeatherShopInteract.Attach(
    new Identifier("mymod", "shop"), target, merchantId: "myMerchant");
```

### 11.6 对照：v2.0 增强前后对比

| 场景 | v1.0（旧） | v2.0（新） |
|------|----------|----------|
| 单交互+交互名 | 无法设置交互名 | `AttachViewInteract(..., interactNameKey: "UI_XXX")` |
| 建筑多交互 | 手动创建子 GO + 管理碰撞体 | `InteractionGroupBuilder.Add().Add().BuildOn()` |
| 获取建筑容器 | `transform.Find("Function")` 硬编码 | `BuildingUtils.GetFunctionContainer(building)` |
| NPC 多交互 | 私有方法，不可复用 | `InteractionUtils.SetupInteractionGroup()` 公开 |
| 非 NPC 商店交互 | 不支持 | `FeatherShopInteract.Attach()` |

---

## 12. v2.1 新增：Formulas 系列视图集成

> 以下为 2026-07-22 补充，将游戏原生的 3 个合成相关视图接入 Feather 交互系统。

### 12.1 背景

游戏中存在 4 个合成相关视图，v2.0 仅集成了 `CraftView`（合成执行）。v2.1 补全其余 3 个。

### 12.2 新增 `GameViews` 常量

```csharp
public static readonly Identifier Formulas         = new Identifier("fml", "formulas");
public static readonly Identifier FormulasRegister = new Identifier("fml", "formulas_register");
public static readonly Identifier Decompose        = new Identifier("fml", "decompose");
```

### 12.3 View↔Handler 映射

| GameViews 常量 | Handler 行为 | 游戏原生 API |
|---------------|-------------|-------------|
| `Formulas` | 打开配方索引，浏览全部配方 | `FormulasIndexView.Show()` |
| `FormulasRegister` | 打开配方注册，提交物品学配方（显示全部） | `FormulasRegisterView.Show(null)` |
| `Decompose` | 打开物品分解，拆解物品为材料 | `ItemDecomposeView.Show()` |

### 12.4 GameUIUtils 快捷方法

```csharp
GameUIUtils.OpenFormulasIndexView();     // 配方索引浏览
GameUIUtils.OpenFormulasRegisterView();  // 配方注册
GameUIUtils.OpenDecomposeView();         // 物品分解
```

### 12.5 新增交互模板

| 模板类 | 对应 GameViews | 特殊字段 |
|-------|---------------|---------|
| `FormulasIndexInteractTemplate` | `GameViews.Formulas` | `InteractNameKey` |
| `FormulasRegisterInteractTemplate` | `GameViews.FormulasRegister` | `RegisterTag`, `InteractNameKey` |
| `DecomposeInteractTemplate` | `GameViews.Decompose` | `InteractNameKey` |

### 12.6 已知限制

- **Tag 过滤不可用**：`FormulasRegisterView.Show(ICollection<Tag>)` 的 tag 过滤参数在 handler 中传 `null`。`Tag` 是 `ScriptableObject`，游戏无 `Tag.GetTag(string)` 静态查找方法，运行时无法通过字符串解析 Tag 引用。
