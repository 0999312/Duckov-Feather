# 游戏对话系统逆向分析

> 基于 Duckov Base_SceneV2 + DuckovDrinks NPC 调试过程。
> 2026-07-17

---

## 1. 对话架构总览

```
场景中 Trigger (BoxCollider, layer=16)
  │ OnTriggerEnter
  ▼
Intro GO                          ← CutScene (playTiming=OnTriggerEnter)
  ├── DialogueTreeController      ← _boundGraphSerialization = JSON graph
  │     ├── StartBehaviour()      → 内部触发 OnDialogueStarted
  │     ├── RequestSubtitles()    → 静态方法，派发字幕请求
  │     └── SetActorReference()   → 绑定 DuckovDialogueActor
  ├── Blackboard                  ← NodeCanvas 黑板（必需）
  └── CutScene                    ← 触发器组件
```

### 启动链路

```
CutScene.Play()
  → 遍历 actorParameters
      → dialogueTreeOwner.SetActorReference(name, DuckovDialogueActor.Get(name))
  → dialogueTreeOwner.StartBehaviour()
      → 内部触发 DialogueTree.OnDialogueStarted
      → 执行图节点
          → LocalizedStatementNode.OnExecute()
              → new LocalizedStatement(key.value)
              → DialogueTree.RequestSubtitles(new SubtitlesRequestInfo(actor, statement, callback))
                  → 触发 DialogueTree.OnSubtitlesRequest
                      → DialogueUI.DoSubtitle()  ← 打字机 + 音频
      → 图结束 → 内部触发 DialogueTree.OnDialogueFinished
```

---

## 2. DialogueTree 图结构（JSON）

图以 JSON 字符串形式直接嵌入 `DialogueTreeController._boundGraphSerialization`，非外部 `.asset` 文件。

### 序列化格式：FullSerializer（不是 Newtonsoft.Json）

```json
{
  "type": "NodeCanvas.DialogueTrees.DialogueTree",
  "nodes": [
    {
      "_actorName": "Jeff",
      "_actorParameterID": "uuid",
      "key": {"_value": "Base_Intro"},
      "_tag": "Base_Intro_0",
      "_position": {"x": 720.0, "y": 140.0},
      "$type": "Dialogues.LocalizedStatementNode",
      "$id": "0"
    }
  ],
  "connections": [
    {
      "_sourceNode": {"$ref": "0"},
      "_targetNode": {"$ref": "1"},
      "$type": "NodeCanvas.DialogueTrees.DTConnection"
    }
  ],
  "canvasGroups": [],
  "localBlackboard": {"_variables": {}},
  "derivedData": {
    "actorParameters": [
      {"_keyName": "Jeff", "_id": "uuid"}
    ],
    "$type": "NodeCanvas.DialogueTrees.DialogueTree+DerivedSerializationData"
  }
}
```

### 节点类型

| $type | 用途 | 关键字段 |
|-------|------|---------|
| `Dialogues.LocalizedStatementNode` | 播放一行字幕 | `key._value`=本地化键, `_actorName`, `_actorParameterID` |
| `NodeCanvas.DialogueTrees.ActionNode` | 执行动作（切镜头等） | `_action.target._value`=VCam索引, `$type`=动作类型 |
| `NodeCanvas.DialogueTrees.DTConnection` | 节点连接 | `_sourceNode.$ref`, `_targetNode.$ref` |
| `Dialogues.LocalizedStatementSequence` | 序列字幕（keyPrefix+index） | 见 `DecompiledDLL/Core/Dialogues/LocalizedStatementSequence.cs` |

### 关键机制

- `key._value` 作为本地化键传给 `new LocalizedStatement(key)` → 经 `ToPlainText()` 解析
- 无翻译时 `ToPlainText()` 回退到原 key，因此可直接用文本做 key
- `$type` / `$ref` / `$id` 是 FullSerializer 引用机制
- `derivedData.actorParameters` 定义图的 actor 参数列表，`_keyName` 与 `DuckovDialogueActor.id` 对应

---

## 3. DialogueUI（游戏原生对话 UI）

`DecompiledDLL/Core/Dialogues/DialogueUI.cs`：

```csharp
// 注册事件（Awake 中调用）
DialogueTree.OnDialogueStarted += OnDialogueStarted;    // 开面板 + 禁用输入
DialogueTree.OnDialogueFinished += OnDialogueFinished;   // 关面板 + 恢复输入
DialogueTree.OnSubtitlesRequest += OnSubtitlesRequest;    // 播放字幕 + 音频
DialogueTree.OnMultipleChoiceRequest += OnMultipleChoiceRequest;
```

- `OnDialogueStarted`: `mainFadeGroup.Show()` + `InputManager.DisableInput()`
- `DoSubtitle`: 逐字打字机动画 + `AudioManager.Post("UI/dialogue_bump")`
- `OnDialogueFinished`: `mainFadeGroup.Hide()` + `InputManager.ActiveInput()`
- **镜头切换**由 `OnDialogueStarted` 隐式处理（DialogueUI 不需要手动控制）

---

## 4. DialogueTreeController API

| 成员 | 类型 | 说明 |
|------|------|------|
| `_boundGraphSerialization` | `string { get; private set; }` | 图 JSON（**property，非 field**） |
| `_blackboard` | `Blackboard` reference | NodeCanvas 黑板 |
| `_graph` | `Graph` (runtime) | 反序列化后的图实例 |
| `StartDialogue()` | void | 启动对话（无参重载） |
| `StartBehaviour()` | void | 同 StartDialogue |
| `SetActorReference(string, IDialogueActor)` | void | 绑定 actor |
| `isRunning` | bool | 图是否运行中 |

**注意**：`_boundGraphSerialization` 是 `{ get; private set; }` — Publicizer 不处理 property accessor，必须用 `PropertyInfo.SetValue` 注入。

---

## 5. DuckovDialogueActor（Actor 注册）

`DecompiledDLL/Core/DuckovDialogueActor.cs`：

```csharp
public class DuckovDialogueActor : MonoBehaviour, IDialogueActor
{
    [SerializeField] private string id;            // ← 对话查找用的 ID
    [SerializeField] private Sprite _portraitSprite;
    [SerializeField] private string nameKey;       // 本地化键（显示名）
    [SerializeField] private Vector3 offset;       // 世界空间 UI 指示器偏移

    public string ID => id;
    public string NameKey => nameKey;
    public Sprite portraitSprite => _portraitSprite;
    public Vector3 Offset => offset;

    private void OnEnable()  => Register(this);   // 自动注册
    private void OnDisable() => Unregister(this);  // 自动注销
    public static DuckovDialogueActor Get(string id) => ActiveActors.Find(e => e.ID == id);
}
```

- `AddComponent<DuckovDialogueActor>()` 时 `OnEnable` 立即触发 `Register(this)`，此时 `id` 尚为空
- `Get(id)` 遍历 `ActiveActors` 动态检查当前 `ID` 属性值，因此先 AddComponent 再设 `id` 仍能查到
- 如果 GO 在此期间被 disable，`OnEnable` 不会触发 → actor 未注册

---

## 6. 任务对话触发（PlayDialogueGraphOnQuestActive）

`DecompiledDLL/Core/Duckov.Quests/PlayDialogueGraphOnQuestActive.cs`：

```
Quest.onActivated
  → SetupActors()
      → dialogueTreeController.SetActorReference(name, DuckovDialogueActor.Get(name))
  → PlayDialogue()
      → dialogueTreeController.StartDialogue()
```

- 对话图直接挂 Quest GO 上
- `QuestGiverID` 枚举含 `Mud = 5`（泥巴 NPC 关联 Quest 36-39）

---

## 7. 相关 DecompiledDLL 文件索引

| 文件 | 内容 |
|------|------|
| `Core/Dialogues/DialogueUI.cs` | 对话 UI 面板 + 事件订阅 |
| `Core/Dialogues/LocalizedStatementSequence.cs` | 序列字幕节点 |
| `Core/Dialogues/LocalizedStatementNode.cs` | 单行字幕节点 |
| `Core/Dialogues/DialogueUIChoice.cs` | 多选 UI |
| `Core/NodeCanvas.DialogueTrees/LocalizedMultipleChoiceNode.cs` | 多选节点 |
| `Core/CutScene.cs` | 场景对话触发器（Trigger + VCam） |
| `Core/Duckov.Quests/PlayDialogueGraphOnQuestActive.cs` | 任务激活对话 |
| `Core/Duckov.Quests/QuestGiverID.cs` | QuestGiver 枚举（Jeff=1, Mud=5, Ming=7, Fo=8, Alex=10） |
| `Core/DuckovDialogueActor.cs` | Actor 注册/查找 |
| `Core/AISpecialAttachmentBase.cs` | NPC 特殊附件基类 |
| `Core/AISpecialAttachment_Shop.cs` | 商人 NPC 附件 |

## 8. 导出的 Asset 文件索引

| 文件 | 内容 |
|------|------|
| `PrefabInstance/Quest 36_Sub.prefab` | Quest 36 (Mud NPC, DialogueTree JSON 完整示例) |
| `PrefabInstance/SpecialAttachment_XiaoMing.prefab` | XiaoMing NPC (StockShop + Interact + Collider 参考) |
| `MonoBehaviour/EnemyPreset_Boss_NPC_XiaoMing.asset` | XiaoMing preset 参数 |

---

## 9. 导出项目已确认不存在的资源

| 类型 | 说明 |
|------|------|
| `DialogueTree` `.asset` 文件 | 图以 JSON 字符串形式内嵌在 prefab 中，非独立文件 |
| `DialogueTreeController` C# 源码 | 在 ParadoxNotion/NodeCanvas 中，未反编译 |
