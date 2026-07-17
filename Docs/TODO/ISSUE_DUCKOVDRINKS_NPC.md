# DuckovDrinks NPC 问题诊断与修复记录

> 创建日期：2026-07-17
> 测试 Mod：DuckovDrinks v0.5.0 (NPC 老政)

---

## ✅ 已修复

### P0-1: Preset 在进入存档时被意外清空

- **现象**：新开档放置建筑正常生成 NPC，但读档后报 `Preset not registered`
- **根因**：游戏加载存档时调 `BuildingManager.ReturnBuildingsOfType` → 触发 `OnBuildingDemolishedHandler` → `RemoveNpc` → 删除了 `_presetReg`/`_configCache`/`_ownerCache`。随后建筑重建时 `SpawnFriendlyNpcAsync` 找不到 preset。
- **修复**：`FriendlyNpcUtils.RemoveNpc` 不再清 presets/config/owner。只销毁运行时 GO + 清 `_registry`。批量清理由 `RemoveAllNpcs` 在 mod 卸载时负责。
- **文件**：`Entities/FriendlyNpcUtils.cs`

### P0-2: QuestGiver 交互子对象无 Collider

- **根因**：`CreateInteractChild("Interact_Quest")` 创建的 GO 只有 layer=Interact，没有 Collider。物理交互检测（OverlapSphere/Raycast）无法发现此子对象。
- **修复**：加 `BoxCollider(isTrigger=true, size=1.5×2.5×1.5)`。
- **文件**：`Entities/FriendlyNpcUtils.cs` → `CreateInteractChild`

### P0-3: 对话 ActorId 不匹配

- **根因**：`NpcProximityTrigger` 用 `NpcId.Path`（="npc_laozheng"）作为 actorId，但 `DuckovDialogueActor` 注册时用 `config.ActorId`（="laozheng"）。
- **修复**：`NpcProximityTrigger` 新增 `ActorId` 字段；`AttachInteractionComponents` 注入 `config.ActorId`；`PlayDialogue` 优先用 `ActorId`。
- **文件**：`Dialogues/NpcProximityTrigger.cs`, `Entities/FriendlyNpcUtils.cs`

### P0-4: NPC 不面向玩家

- **根因**：`BuildFriendlyPreset` 硬编码 `sightDistance=0`，AI 不知道玩家位置，`NpcFacePlayer` 设置的方向被 AI 覆盖。
- **修复**：`FriendlyNpcConfig` 新增 `SightDistance` 字段（默认 8f）；`BuildFriendlyPreset` 使用 `config.SightDistance`。
- **文件**：`Entities/FriendlyNpcConfig.cs`, `Entities/FriendlyNpcUtils.cs`

---

## ❌ 进行中：对话系统重写（DialogueTreeController 方案）

### 背景

原方案通过反射获取 `DialogueTree.OnDialogueStarted` backing field delegate 并手动 invoke，但 Mono 编译器 backing field 命名不可靠，`GetField("OnDialogueStarted")` 返回 null → 对话面板永不打开。

### 新方案

运行时动态创建 `DialogueTreeController` → 注入 minimal JSON → `StartDialogue()` → NodeCanvas 接管全流程（OnDialogueStarted → 面板+镜头 → RequestSubtitles → 字幕 → OnDialogueFinished）。

### 当前状态：StartDialogue() 抛出 NRE

```
NodeCanvas.DialogueTrees.DialogueTreeController.StartDialogue () [0x0003a]
  at DialogueUtils.PlayDialogue (line 77)
```

### 已尝试

| 尝试 | 结果 |
|------|------|
| 直接赋值 `controller._boundGraphSerialization = json` | 编译失败（`{ get; private set; }` 属性） |
| `PropertyInfo.SetValue` 注入 JSON | NRE（首次） |
| `PropertyInfo.SetValue` + 等一帧 `NextFrame()` | NRE（二次） |
| 手动构建 FullSerializer 精确格式 JSON（去 Newtonsoft） | 待测试 |
| `_blackboard` 也加 PropertyInfo 兜底 | 待测试 |

### 尚未确认

- [ ] `_boundGraphSerialization` setter 是否真正触发 graph 反序列化（还是延迟到 Start()）
- [ ] 是否需要设置 `_boundGraphSource` 字段
- [ ] `DialogueTreeController` 是否需要额外初始化（如手动调 `Awake`/`Start`）
- [ ] 完整 JSON 格式是否完全匹配 FullSerializer（NodeCanvas 的序列化器）
- [ ] `_blackboard` 是 property 还是 publicized field

### 相关文件

- `Dialogues/DialogueUtils.cs` — PlayDialogue + BuildDialogueJson
- `Dialogues/DialogueTrigger.cs` — Quest 事件触发
- `Dialogues/NpcProximityTrigger.cs` — 接近触发

---

## ⚠️ 待处理

### P1-1: `InteractableBase.Awake_Patch1` NRE

- **现象**：`AddComponent<NpcShopInteract>()` 和 `AddComponent<QuestGiver>()` 时均触发 NRE
- **来源**：`MonoMod.Utils.DynamicMethodDefinition.InteractableBase.Awake_Patch1` — Harmony 动态方法（EliteEnemies 或其他 Mod 的补丁）
- **影响**：NpcShopInteract 虽然有 NRE 但商店交互仍可用；QuestGiver 的 NRE 在其自己的 `Awake()` 中，可能阻止完整初始化，导致任务交互不可用
- **待查**：是否可通过禁用测试环境中特定 Mod 来隔离

### P1-2: 商人界面文字为空

- **现象**：NPC 商店可打开但商品名为空
- **可能原因**：`StockShop.InitializeEntries()` 反射调用静默失败（方法不存在或签名不对）
- **待查**：已在 `AttachInteractionComponents` 中加了 Warning 日志，等待验证

### P1-3: 合成表不可见

- **现象**：用户确认使用高级工作台但 DuckovDrinks 配方不可见
- **可能原因**：游戏版本更新导致 CraftingManager 内部结构改变，FML Harmony patch 注入失败
- **待查**：独立于 NPC 系统

---

## 架构发现

### 游戏对话系统完整链路（已验证）

```
场景 Trigger (BoxCollider, layer=16)
  └─ OnTriggerEnter
       └─ CutScene.Play()
            └─ DialogueTreeController.StartBehaviour()
                 ├─ OnDialogueStarted → DialogueUI 开面板 + 转镜头
                 ├─ 图节点调用 RequestSubtitles → 字幕 + 音频
                 └─ OnDialogueFinished → 面板关闭 + 恢复输入
```

- `DialogueTreeController` 内嵌 JSON 图（`_boundGraphSerialization`），不是外部 `.asset` 文件
- JSON 使用 FullSerializer 格式（`$type`、`$ref`、`$id`）
- `NodeCanvas.DialogueTrees` 已在 csproj Publicizer 中（第74行），但 `_boundGraphSerialization` 是 `{ get; private set; }` 属性，Publicizer 不处理 property accessor

### 原版 NPC 交互结构（SpecialAttachment_XiaoMing）

```
SpecialAttachment_XiaoMing (prefab)
├── StockShop (merchantID="Merchant_Ming")
├── InteractableBase (trade) → interactCollider=SphereCollider(radius=4)
├── AISpecialAttachment_Shop (hideIfFoundEnemy)
└── SphereCollider (isTrigger=0, radius=4)
    └── 子GO: Interact_Quest (QuestGiver + 独立 collider)
```

- 原版 NPC 用 `SphereCollider(isTrigger=false)` 做交互碰撞体
- 任务交互通过独立子 GO + 独立 collider 实现
- `AISpecialAttachment_Shop` 只做"发现敌人时隐藏商店"

---
