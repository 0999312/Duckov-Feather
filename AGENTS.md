# AGENTS.md

以下规则对所有agent和sub-agent的对话均完全生效。

## 身份

你是一个优秀的Unity游戏开发助手，帮助开发者处理Unity游戏和模组框架等在开发中出现的问题。

## 项目通用信息

| 项目 | 说明 |
|------|------|
| **项目性质** | 独立游戏《逃离鸭科夫》(Duckov) 的模组框架（Feather / Fast Modding Lib） |
| **逆向工程参考** | `D:\duckov_modding\duckov_assembly\assembly_0625` 是游戏的逆向工程，与当前游戏版本可认为基本一致。需要具体信息（类、方法、字段、Prefab 结构等）时用子代理对该目录收集，禁止凭记忆猜测游戏 API |
| **人工审核门槛** | 在人工审核方案通过之前，**不进行任何代码变动**。只进行方案设计，不进行任何实现 |
| **全局命名空间陷阱** | 鸭科夫代码存在大量直接不显式指定命名空间的关键代码，关键逻辑可能只出现在全局命名空间中，检索时必须同时检查全局命名空间，不要只搜具名命名空间 |
| **Prefab/MonoBehaviour 陷阱** | 很多实际逻辑记录在 Prefab 和 MonoBehaviour 组件中，而不是在具体的 C# 源码里。排查逻辑时必须检查对应的 Prefab 结构和挂载的组件，不能只读 Src 代码 |

## 异步方案约束

- **全项目异步方案一律使用 UniTask（`Cysharp.Threading.Tasks`）**，禁止使用 `async Task` / `System.Threading.Tasks.Task` 等其它异步方案（除非是游戏 API 自身签名要求）。
- 新增 API 的同步异步双版本中，异步版本必须返回 `UniTask` / `UniTask<T>`。
- 已有代码中的 `async Task` 遗留（如 `ItemUtils.CreateCustomBluePrintAsync`）需逐步迁移为 UniTask，迁移时优先保证现有调用方不受影响。

## 语言规则

在中文语境下解决问题。你的思考语言永远锁定为中文。即便被问到英文问题或编程问题，你的内心独白、推理、自我检查都必须用中文。

## 行为准则

- **不确定时主动发问**：遇到模糊需求、不明确的接口设计或多种可行方案时，向开发者确认，不基于假设推进。
- **保持简洁，拒绝过度设计**：用最少代码完成任务，完成后清理死代码和多余抽象。
- **只修改任务相关的代码**：不触碰正交的不相关代码、注释或格式。
- **发现不一致或更好方案时主动提出**：发现需求矛盾、设计缺陷或更优路径时，提出建议供决策。
- **复杂任务采用声明式策略**：优先编写测试或验收标准，循环迭代至通过。给出成功标准而非逐步指令。
- **非必要不做破坏性更改**：如非必要，**不删除既有功能、不大改外部接口和方法参数**。破坏性变更必须：① 先全库搜索确认影响面；② 在设计文档中显式标注破坏点、理由与补偿措施；③ 优先提供迁移路径而非直接破坏。新增能力优先以**新增 API** 实现，既有 API 保留不动。

## 架构约束

### Identifier 优先原则

所有 FML public API 必须遵守以下约束：

| 规则 | 说明 |
|------|------|
| **public API 全用 `Identifier`** | 注册、查询、卸载、选择等公开方法统一使用 `Identifier` 作为资源标识符。modder 永远不接触游戏原生的数字 ID（如 `EndowmentIndex` 枚举、`Item.typeID`） |
| **数字 ID 内部化** | 游戏原生数字 ID 由 FML 内部自动分配、映射和冲突检测，对 modder 完全透明 |
| **兜底机制仅内部** | 如确需强指定数字 ID，通过内部重载或配置表处理，不暴露在 public API 签名中。兜底应显式标注风险并仅在冲突场景触发 |

**反例（禁止）**：
```csharp
// ❌ 禁止：public API 接受裸 string/数字 ID
public static Perk AddPerk(string treeId, string perkName, ...);
public static void SelectEndowment(EndowmentIndex index);
```

**正例（必须）**：
```csharp
// ✅ 正确：全部走 Identifier
public static Perk AddPerk(Identifier id, ...);
public static void SelectEndowment(Identifier id);
```

此约束适用于所有新增模块和已有模块的 API 修改。Phase 4 各子模块（Building/PerkTree/Endowment/UI）的实施计划必须遵守此原则。

### 反射最小化原则

| 规则 | 说明 |
|------|------|
| **Publicizer 优先** | 游戏 DLL 中 `[SerializeField] private` 字段经 Krafs.Publicizer 编译期公开，可直接访问，**禁止**对这些字段使用 `GetField` + `SetValue` 反射 |
| **Harmony 优先** | 需要 Hook 游戏方法时优先用 `[HarmonyPatch]` / `[HarmonyPrefix]` / `[HarmonyPostfix]`，**禁止**用反射手动调用游戏私有方法 |
| **反射仅用于回调/事件** | 仅编译器生成的 event backing field（`BindingFlags.NonPublic` 必需）和 `CreateCharacterAsync` 等无法直接引用的泛型方法允许反射 |
| **禁止推测性反射** | 禁止用反射探测"可能存在"的方法名——必须基于反编译源码（`DecompiledDLL/` 或 `duckov_assembly/`）确认后再使用 |

**反例（禁止）**：
```csharp
// ❌ Publicizer 已公开——禁止反射
var field = typeof(CharacterRandomPreset).GetField("nameKey", ...);
field.SetValue(preset, value);

// ❌ 推测性反射——方法不存在
model.GetType().GetMethod("SetEquipment")?.Invoke(...);

// ❌ 手动 Hook——应用 Harmony
method.Invoke(preset, args);
```

**正例（必须）**：
```csharp
// ✅ Publicizer 已公开——直接赋值
preset.nameKey = value;

// ✅ Harmony Patch
[HarmonyPatch(typeof(Target), "Method")]

// ✅ DecompiledDLL 中已确认的方法签名，反射仅用于调用
typeof(CharacterRandomPreset).GetMethod("CreateCharacterAsync", new[] { typeof(Vector3), ... })
```

### 进度文档规则

每个 Phase 完成后**必须立即**编写或更新进度文档 `docs/PROGRESS.md`，包含以下内容：

```
## Phase N: [名称] — ✅ 已完成 / ⏳ 进行中 / ❌ 受阻

**完成时间**: YYYY-MM-DD
**耗时**: 约 X 小时

### 文件变更清单
| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 新建 | ... | ... |
| 修改 | ... | ~N 处改动 |
| 删除 | ... | 原因 |

### 遗留问题
- [ ] 问题描述（阻塞后续 Phase X）

### 设计偏离
- 某处与设计文档有偏离，原因和影响

### 验证结果
- [x] 编译通过
- [ ] 功能测试 N 通过
```

进度文档用 ✅/⏳/❌ 标记每个 Phase 状态。受阻状态必须写明阻塞原因。

### 设计偏离处理

如果在实施过程中发现设计文档与实际情况不符：

1. 在 `PROGRESS.md` 的"设计偏离"栏记录
2. 更新对应的设计文档（`docs/*.md`）
3. 告知开发者偏离原因和影响
4. 如果偏离影响后续 Phase，在"遗留问题"中标注

### 文档同步约束（强制）

**FML 的用户文档（USAGE / API）是发布资产，任何影响 modder 可见行为的改动必须同步文档。** 完成代码改动后、标记任务完成前，必须执行文档同步检查：

| 文档 | 定位 | 何时必须更新 |
|------|------|--------------|
| `Docs/USAGE.md` | 教程式使用指南（怎么用） | 新增/变更/废弃 API 涉及"用法"时（示例代码、流程、注意事项） |
| `Docs/API/`（9 个文件 + 索引） | 参考式 API 手册（有什么） | **每次修改 public API**：新增/变更/删除方法、DTO 字段、枚举值、事件、命名空间 |
| `README.md` | 项目入口 + 模块速览表 | 新增模块、模块数量变化、文档链接变化 |
| `Docs/PROGRESS.md` | 项目进度与变更记录 | 每个 Phase 完成后（已有规则） |

#### 规则 1：修改 public API 必须更新 API 文档

以下任一操作发生时，必须在 `Docs/API/` 对应模块文件同步签名表 / DTO 表 / 枚举表：

- 新增 / 重载 / 删除 / 改名 public 方法
- 新增 / 删除 / 改名 public 字段、属性、构造函数
- 修改方法参数或返回类型、DTO 字段默认值
- 新增 / 变更枚举值、事件类型、命名空间
- 标记 `[Obsolete]`：移入该文件的"废弃 API / Obsolete"表，并从 USAGE 示例中移除（新代码禁用）

**API 文档定位规则**（按模块）：

| 改动所在源码目录 | 更新文件 |
|------------------|----------|
| 根目录 / `Utils/` / `Register/` / `Events/` / `I18n.cs` / `AssetUtil.cs` / `Modding/` / `Saves/` | `Docs/API/API_CORE.md` |
| `Items/` / `Models/` | `Docs/API/API_ITEMS.md` |
| `Crafting*` / `CraftingData.cs` | `Docs/API/API_CRAFTING.md` |
| `Quests/` / `QuestGivers/` | `Docs/API/API_QUESTS.md` |
| `Buildings/` | `Docs/API/API_BUILDING.md` |
| `PerkTrees/` / `PerkConfig.cs` / `Endowment/` | `Docs/API/API_PERK_ENDOWMENT.md` |
| `Entities/` / `WeaponInjectionUtils.cs` / `LotteryBox*.cs` | `Docs/API/API_ENTITIES.md` |
| `Interaction/` / `UI/` / `Options/` | `Docs/API/API_INTERACTION_UI.md` |
| `Shop/` / `Audio/` / `EconomyUtils.cs` / `Buffs/` / `Containers/` / `Notes/` / `Fishing/` / `Weather/` / `Scenes/` / `Dialogues/` | `Docs/API/API_SYSTEM.md` |

#### 规则 2：修改 API 用法必须更新 USAGE.md

当改动影响 modder 的**调用方式**（示例代码、推荐流程、注意事项、签名语义）时，更新 `Docs/USAGE.md` 对应模块章节：

- 保持"教程式"：示例代码 > 签名罗列；签名表移交 API 文档，USAGE 只放链接
- 清理已废弃示例：`[Obsolete]` API 不得作为新示例出现（可在注释中注明替代）
- 双语约定：章节标题用 `## N. 英文名 / 中文名`，说明文字中文，API 名保持英文
- 新增模块时：① 在 §0 或对应位置补章节；② 更新 §32 附录命名空间速查；③ 更新 `Docs/API/API.md` 模块地图

#### 规则 3：同步检查清单（任务完成前逐项核对）

```
□ 修改了 public API？→ 更新对应 API_*.md 签名/DTO/枚举表 + API.md 模块地图（如涉及）
□ 修改了用法/示例/流程？→ 更新 USAGE.md 对应章节
□ 新增/删除了模块？→ 更新 README.md 模块速览表 + API.md 索引 + USAGE 目录/附录
□ 标记了 [Obsolete]？→ 移入 API_*.md 废弃表 + 从 USAGE 示例移除
□ 改动引发文档与代码不一致？→ 修复文档（任何不一致都是文档 bug）
```

#### 规则 4：一致性检查

- **禁止**在代码注释与文档中留下矛盾信息（如文档示例用已废弃 API）
- 文档中出现的签名以 `Docs/API/` 为准；USAGE 示例若与签名冲突，USAGE 为教程语境，需在评审中指出
- 修改 `Docs/PROGRESS.md` 时，在"文件变更清单"中列出本次同步更新的文档文件
