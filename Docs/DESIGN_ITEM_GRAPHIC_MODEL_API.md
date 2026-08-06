# 设计方案：Item Graphic 封装 + OBJ/FBX 模型导入 + Item API 双版本核查

> 创建时间: 2026-08-07
> 状态: ✅ 已批准并实现（2026-08-07）
> 对应 TODO: `Docs/TODO.md` 新功能 1/2/3
> 约束: 本方案仅设计，不实现。审核通过前不做任何代码变动。

---

## 1. 背景与目标

| # | 功能 | 目标 |
|---|------|------|
| 1 | Item Graphic 封装 | 整理原游戏 ItemGraphic 系统，提供封装方法快速构建简单物品的 ItemGraphic GameObject（同时包含 `ItemGraphicInfo` + `CharacterSubVisuals`，`renderers` 仅 1 个元素 = 主模型 Mesh Renderer） |
| 2 | 外部模型导入 | 从 mod 目录直接读取 OBJ/FBX 构建 Mesh + MeshRenderer，与功能 1 联动；同步 + 异步（UniTask）双版本 |
| 3 | 双版本核查 | 检查 Item 相关方法同步/异步双版本完整性；迁移遗留 `async Task` 为 UniTask |

**关键决策（用户已确认）**：

- 模型导入在**保留既有 AssetBundle 路径**的基础上，**新增一条简化路径**：类似现有 Sprite 加载的"从 mod 目录直接读取 OBJ 文件 + 运行时解析"模式。**AssetBundle 仍然允许且必要场景必须使用**（复杂模型仍需作者制作 Bundle 导入），简化路径面向简单物品、零编辑器流程。
- FBX 首版**不支持**（可接受）：仅 OBJ + 转 OBJ 降级提示；未来 glTF 路线独立立项。
- 模型纹理沿用现有约定 `assets/textures/`（文档注明**建议与物品 sprite 隔离**，如放 `assets/textures/models/` 子目录）。
- **GO 需要缓存**（防模型丢失）+ 支持**同 Mesh 不同 GO**（模型复用、仅材质变化）：Mesh 缓存 + 材质缓存 + GO 模板缓存三级。
- 新增需求：**复用原版 ItemGraphic**——让物品直接使用指定原版物品的模型。
- **简化路径做时间/空间双维性能优化**（见 §3.5）。

### 1.1 模型路径选型（并存）

| 路径 | 适用场景 | 代价 | 定位 |
|---|---|---|---|
| **AssetBundle（既有，保留）** | 枪械（`ItemGraphicInfo_Gun`）、多材质/multi-submesh、FBX、动画、粒子等复杂模型 | 需 Unity 工程 + 专用工作流制作 Bundle（`SetItemGraphic` / `RegisterGun` / `RegisterItemFromBundle` 已支持） | 复杂场景唯一通道 |
| **OBJ 简化路径（新增）** | 简单物品（单 mesh、单材质、无动画） | 作者用 Blender 等导出 OBJ（三角面、Y-up）放 `assets/models/`，零编辑器流程 | 快速原型与简单物品默认通道 |

**决策准则（写入 USAGE.md）**：作者先评估模型复杂度——单 mesh + 单材质 → OBJ 简化路径；涉及多材质/动画/枪械挂点/复杂 socket → AssetBundle。两条路径均可绑定到 `item.ItemGraphic`，运行时链路一致。

## 2. 游戏逆向结论（数据来源：assembly_0625）

### 2.1 ItemGraphicInfo（全局命名空间）

`Src\Item\ItemGraphicInfo.cs`，`MonoBehaviour`。关键字段：

| 字段 | 类型 | 说明 |
|---|---|---|
| `groundPoint` | `Transform` (public) | 地面锚点，落地对齐用 |
| `sockets` | `List<ItemGraphicSocket>` (`[SerializeField] protected` → Publicizer 公开) | 子物品挂点 |
| `fallbackSprite` | `SpriteRenderer` (public) | 无 3D 模型时兜底图标（可留 null） |
| `spriteGraphicPfb` | `static ItemGraphicInfo` | 全局 Sprite 兜底 prefab（`GameManager.Awake` 注入） |

唯一静态工厂：`CreateAGraphic(Item item, Transform parent, bool snapGround = false, bool useSpriteIfNoGraphic = true)`——实例化 `item.ItemGraphic` → SetParent + 归零 → `Setup(item)` 绑定 `itemRefrence` + 订阅 `onSlotContentChanged` → 递归子物品展示。`snapGround=true` 时 `SnapGroundPointToParent()`（`localPosition = -parent.InverseTransformPoint(groundPoint.position)`，**groundPoint 为 null 会 NRE**）。

`Item.itemGraphic` 为 `[SerializeField] private`（`Item.cs` L57），Publicizer 已公开，可直接赋值。

### 2.2 CharacterSubVisuals（全局命名空间）

`Src\Core\CharacterSubVisuals.cs`。关键字段与方法：

| 成员 | 说明 |
|---|---|
| `renderers` | `List<Renderer>`（**字段名小写 `renderers`**） |
| `SetRenderers()` | `GetComponentsInChildren<Renderer>(includeInactive: true)` 全量收集：ParticleSystem → particles、SodaPointLight → sodaPointLights、其余 → renderers；纯收集，**不涉及角色注册，无 NRE 风险** |
| `AddRenderer(Renderer)` | 单个添加 + 层设置 + 重注册到角色（`character.RemoveVisual/AddSubVisuals`，**character 为 null 时存在 NRE 风险**） |
| `SetRenderersHidden(bool)` | 层切换（`"SpecialCamera"` / `"Character"` / `"SodaLight"`），非 SetActive |

角色注册链路：`CharacterModel.AddSubVisuals` 把 `visuals.renderers` 合并进 `CharacterModel.renderers` → 跟随角色隐藏/显示。**挂到角色上的物品模型必须注册 renderers，否则不跟随角色层切换**。

### 2.3 Prefab 实证（IG_*.prefab）

- **IG prefab 根物体同时挂 `CharacterSubVisuals` + `ItemGraphicInfo`**（738 个 IG prefab 均为该结构）
- 示例 `IG_Acc_Grip_ALL_REC_1.prefab`：根 → `CharacterSubVisuals`（renderers 预填 1 个）+ `ItemGraphicInfo`（groundPoint → `GroundPoint` 子物体）；子物体 `GroundPoint` + 主模型（MeshFilter + MeshRenderer）
- 命名约定：`GroundPoint`、`Sockets/<socket名>`、`ShowIf_<名>`、`HideIf_<名>`
- 枪械走 `ItemGraphicInfo_Gun` 特殊路径（手持 agent 数据源），简单物品不需要

### 2.4 模型渲染要点

- 游戏物品 mesh 为 **MeshFilter + MeshRenderer**（非 SkinnedMeshRenderer）
- 主 shader：`SodaCraft/SodaLit`（`Shader.Find` 可命中，`Universal Render Pipeline/Lit` 可兜底）
- 展示物体为纯视觉，无碰撞体
- 游戏内无 OBJ 解析器、无 FBX SDK（Managed/Plugins 全量核对）→ OBJ 自实现，FBX 首版降级

## 3. 功能 2 设计：`ModelUtils`（OBJ/FBX → Mesh）

### 3.1 文件与目录约定

- 新文件：`FastModdingLib/Models/ModelUtils.cs`（含内部 `ObjParser`）
- mod 目录约定：`assets/models/<resourceName>`（对齐现有 `assets/textures/` 模式，`Path.Combine(modDir, "assets/models/")`）
- 支持扩展名：`.obj`（必需）；`.fbx`（不支持，降级提示转 OBJ）

### 3.2 public API（全部 Identifier 优先 + 双版本）

```csharp
// ===== 加载（对齐 LoadSprite 三阶梯模式） =====
public static Mesh? LoadMesh(string resourceName);                                    // [MethodImpl(NoInlining)] modid 自动推导
public static Mesh? LoadMesh(Identifier id);
public static Mesh? LoadMeshFromDir(string modDirectory, string resourceName);

public static UniTask<Mesh?> LoadMeshAsync(string resourceName);
public static UniTask<Mesh?> LoadMeshAsync(Identifier id);
public static UniTask<Mesh?> LoadMeshFromDirAsync(string modDirectory, string resourceName);

// ===== 材质（缓存版，textureId 读 assets/textures/，null = 默认无纹理材质） =====
public static Material? GetModelMaterial(Identifier? textureId = null);               // Shader.Find("SodaCraft/SodaLit") + _MainTex；null 降级 URP Lit

// ===== 纯组装（不缓存，供自定义材质场景） =====
public static GameObject CreateModel(Mesh mesh, Material? material = null);           // MeshFilter + MeshRenderer 成对，返回根 GO

// ===== 缓存与卸载 =====
public static void ReleaseModel(Identifier id);                                       // 释放 Mesh + 其相关材质缓存
public static void ReleaseAllModels(string? modid = null);                            // 对齐 UnregisterAllItem 语义
```

**缓存设计（ModelUtils 层）**：

| 缓存 | key | value | 说明 |
|---|---|---|---|
| Mesh 缓存 | `Identifier` (meshId) | `Mesh` | `LoadMesh` 内部查询，miss 时解析并缓存 |
| Material 缓存 | `Identifier?` (textureId) | `Material` | `GetModelMaterial` 内部查询，key 含 domain 以支持 mod 卸载语义；同 meshId 配不同 textureId → 不同材质 → 不同 GO（满足"模型复用、仅材质变化"） |

### 3.3 内部 `ObjParser` 规格（自实现，零第三方依赖）

| 项 | 规格 |
|---|---|
| 关键字 | `v` / `vt` / `vn` / `f` 完整支持；`o` / `g` / `usemtl` 分组（首版单 submesh 合并，文档注明）；忽略 `#` / `vp` / `l` / `p` / `s` / `mtllib` |
| 面索引 | 1-based → 0-based；负索引 `-n` = 倒数第 n；支持 `f v` / `f v/vt` / `f v//vn` / `f v/vt/vn` 四形态 |
| 多边形 | n 边形扇形三角化 |
| 顶点唯一化 | `Dictionary<(pos, uv, nrm), int>` 按三元组展开（避免同位置不同 UV 渲染错误） |
| 坐标变换 | 默认开启：`v.y=-y, v.z=-z`（绕 X 轴 180°，保绕序）；`vn` 同变换；`vt.v = 1 - v`（UV 翻转）；提供 `coordinateFlip` 选项 |
| 法线 | 缺 `vn` → `RecalculateNormals()` |
| 索引格式 | 顶点数 > 65535 → `IndexFormat.UInt32`，否则 16-bit |
| 收尾 | 数据齐全后 `Mesh.Optimize()`（之后只读）；可选 `UploadMeshData(true)` |
| 错误处理 | 对齐 Sprite：`Debug.LogError` + 行号 + 返回 null，不抛异常 |
| 线程模型 | IO + 文本解析在线程池（`UniTask.RunOnThreadPool`，返回纯数据类 `ObjMeshData`）；`new Mesh` / 赋值 / Optimize 全部主线程 |
| 性能 | 见 §3.5 性能设计 |

### 3.4 FBX 处理（首版明确不支持）

- `.fbx` 扩展名 → `Debug.LogWarning("[FML] FBX runtime import is not supported. 请导出为 OBJ（Blender: File→Export→Wavefront OBJ，三角面，Y-up）或使用 AssetBundle 路径")` + 返回 null
- 依据：游戏进程内无 Autodesk/FbxSharp/Assimp 等任何 FBX SDK（Managed 143 DLL + Plugins 全量核对）；二进制 FBX 自实现工作量数周起步
- 未来路线（单独立项）：glTF 2.0（SharpGLTF 纯托管）/ ASCII FBX 子集

### 3.5 性能设计（简化路径时间/空间双维优化）

#### 时间性能

| 优化 | 说明 |
|---|---|
| 解析放线程池 | 异步版 IO + 解析全程 `UniTask.RunOnThreadPool`，主线程零阻塞；同步版文档标注"大模型会有主线程卡顿，推荐异步" |
| 零字符串分配解析 | 行内不用 `string.Split`（每次调 3+ 次分配）——手动扫描空白符定位 token，`char` 逐字符解析；token 仅用 `ReadOnlySpan<char>` 视图 + `float.TryParse(ReadOnlySpan<char>)`（Unity 2022.3 Mono 完整支持，见 §3.3 调研结论），解析循环零托管分配 |
| 预容量 | 顶点/UV/法线/面 `List<T>` 预容量（如 4096，超限自动扩容），避免频繁 realloc |
| 单飞缓存 | Mesh/Material/GO 模板三级缓存 + lock 双检，同一 Identifier 并发重复调用只解析一次 |
| 并行加载 | 异步版多个模型可并发解析（`UniTask.WhenAll` 场景天然支持，互不干扰，各自线程池任务） |
| 主线程组装最小化 | 主线程只做：`new Mesh` + `SetVertices/SetNormals/SetUVs/SetTriangles`（**全部用 List 重载**，免数组拷贝）+ 按需 `RecalculateNormals` + `Optimize`；不在此阶段做任何文本/转换工作 |

#### 空间性能

| 优化 | 说明 |
|---|---|
| 顶点唯一化零装箱 | 展开 key 用**自定义 `struct VertKey`（实现 `IEquatable<VertKey>`）** 或位打包 `ulong`（pos 21bit / uv 21bit / nrm 21bit，支持 ≤2M 顶点），避免 `Dictionary<(int,int,int), int>` 的 ValueTuple 分配；`EqualityComparer<VertKey>.Default` 对实现 `IEquatable` 的 struct 直接调用，不装箱 |
| 数据直传 List | `Mesh.SetVertices(List<Vector3>)` / `SetNormals` / `SetUVs` / `SetTriangles(List<int>)`——直接传给 Unity 内部 native 拷贝，省去中间 `ToArray()` 的整块复制 |
| 16-bit 索引优先 | 顶点 ≤ 65535 保持 `IndexFormat.UInt16`（内存减半），仅超限升 UInt32 |
| 释放 CPU 侧数据 | 组装完成后 `UploadMeshData(true)`（Mesh 只读化，释放托管侧顶点/索引副本）；`Optimize()` 合并重复顶点 |
| 共享材质 | 同 textureId 全局共享一个 Material（不重复创建）；OBJ 首版忽略 `mtllib/mtl`，统一走 `GetModelMaterial`，避免 per-mesh 材质实例 |
| 空分组剔除 | `o/g` 分组若 0 面直接丢弃，不产生空 submesh |
| 三级缓存 + 释放 API | Mesh（meshId）→ Material（textureId）→ GO 模板（meshId, textureId）；`ReleaseModel` / `ReleaseItemGraphic` / `ReleaseAll*` 支持 mod 卸载时全量回收 |

#### 实测基准（验收目标）

- 万级顶点（~10k 顶点 / ~30k 面）OBJ：线程池解析 **< 100ms**（典型几十 ms）；主线程组装 **< 5ms**
- 解析过程（线程池段）**零托管分配**（GC Alloc 为 0）；主线程段仅 Mesh 数据直传
- 同 (meshId, textureId) 二次调用：命中缓存，主线程 **< 1ms**（仅 Instantiate 模板）

## 4. 功能 1 设计：`ItemGraphicUtils`（Item Graphic 封装）

### 4.1 文件

- 新文件：`FastModdingLib/Items/ItemGraphicUtils.cs`

### 4.2 public API（全部 Identifier 优先 + 双版本）

```csharp
// ===== 构建 ItemGraphic GameObject（功能 1 核心，带 GO 模板缓存） =====
public static GameObject CreateItemGraphic(Identifier meshId, Identifier? textureId = null);
public static UniTask<GameObject> CreateItemGraphicAsync(Identifier meshId, Identifier? textureId = null);

// ===== 一步到位：构建 + 绑定到 Item =====
public static void SetItemGraphic(Item item, Identifier meshId, Identifier? textureId = null);
public static UniTask SetItemGraphicAsync(Item item, Identifier meshId, Identifier? textureId = null);

// ===== 复用原版物品模型（纯引用赋值，无 IO，仅同步版本） =====
public static void SetItemGraphicFromOriginal(Item item, Identifier originalItemId);

// ===== 缓存与卸载 =====
public static void ReleaseItemGraphic(Identifier meshId, Identifier? textureId = null);
public static void ReleaseAllItemGraphics(string? modid = null);                       // 按 meshId.Domain 过滤
```

### 4.3 构建规格（`CreateItemGraphicAsync` 内部流程）

```
1. mesh = await ModelUtils.LoadMeshAsync(meshId)          // 功能 2 联动；null 时返回 null 并 LogError
2. material = ModelUtils.GetModelMaterial(textureId)      // 材质缓存；同 meshId 不同 textureId → 独立材质
3. 查询 GO 模板缓存 key = (meshId, textureId)：
   - 命中 → 跳过构建，直接进入第 8 步
   - miss  → 构建模板（第 4-7 步）
4. 创建模板根 GO（名称 = meshId.Path）：
   - AddComponent<ItemGraphicInfo>()
   - AddComponent<CharacterSubVisuals>()
5. 创建子 GO "Model"：MeshFilter + MeshRenderer（sharedMesh = mesh, sharedMaterial = material）
6. CharacterSubVisuals.renderers 填充 → 调用 SetRenderers()（GetComponentsInChildren 恰好收集到
   唯一的 Model MeshRenderer → renderers.Count == 1，满足需求）
   不用 AddRenderer() —— 其内部依赖 character 注册，无角色时存在 NRE 风险
7. 创建子 GO "GroundPoint"（localPosition 默认 (0,0,0)，modder 可调整）
   —— 必须存在：CreateAGraphic(snapGround:true) 的 SnapGroundPointToParent 对 null 会 NRE
   ItemGraphicInfo.sockets 置空 List（无挂点）；fallbackSprite 留 null（安全）
8. 模板入缓存 + 放入 inactive 容器（参照 BuildingUtils.PrefabHolder 防 Curtain 相机误渲染，
   容器 DontDestroyOnLoad）
9. 返回 Instantiate(模板) 的活动副本（副本可自由修改 transform/材质，互不影响）
```

**缓存设计（ItemGraphicUtils 层）**：

| 缓存 | key | value | 说明 |
|---|---|---|---|
| GO 模板缓存 | `(Identifier meshId, string? textureKey)` | `GameObject`（inactive 模板） | 同 key 复用模板；同 Mesh 不同材质 → 不同 key → 不同模板 → 不同 GO（模型复用、材质隔离）。`CreateItemGraphic` 返回 `Instantiate(模板)` 副本 |
| 释放顺序 | — | — | 先 `ReleaseItemGraphic`（销毁模板副本，解除对 Mesh 的引用）再 `ReleaseModel`（销毁 Mesh/材质），避免悬挂引用 |

### 4.4 使用示例（预期行为）

```csharp
// modder 侧
Item item = ItemUtils.GetCustomItem(id, config);
ItemGraphicUtils.SetItemGraphic(item, new Identifier("mymod", "apple.obj"));            // 默认材质
ItemGraphicUtils.SetItemGraphic(item, new Identifier("mymod", "apple.obj"),
    new Identifier("mymod", "models/apple_paint"));                                     // 同模型 + 独立贴图
// 或异步版 SetItemGraphicAsync
// 之后游戏内 item.ItemGraphic 非空 → CreateAGraphic 直接实例化 3D 模型，不走 Sprite 兜底
```

- 装备/掉落/手持链路**零改动**：只要 `item.ItemGraphic` 非空，原版 `CreateAGraphic` 三条链路（掉落 `InteractablePickup` / 装备 `CharacterEquipmentController` / 手持 `ItemAgentHolder`）自动生效
- 手持枪械（`ItemGraphicInfo_Gun` 路径）**不在本方案范围**——仅覆盖简单物品（非枪械）

### 4.5 复用原版物品模型（新增需求）

**目标**：不加载外部模型，直接复用游戏内指定原版物品的 ItemGraphic。

```csharp
public static void SetItemGraphicFromOriginal(Item item, Identifier originalItemId)
```

**内部流程**：

```
1. ItemUtils.TryResolveTypeId(originalItemId, out int typeId)   // internal，同程序集可访问；
                                                                // 查询顺序 FML 注册表 → 原版反查表，
                                                                // 因此也兼容引用其它 mod 已注册物品
2. ItemAssetsCollection.GetPrefab(typeId) → Item prefab         // 游戏公开 API
3. prefab.ItemGraphic（Publicizer 公开字段）→ ItemGraphicInfo
4. item.ItemGraphic = original.ItemGraphic                       // 共享引用赋值
```

**要点**：

- **共享引用安全**：`CreateAGraphic` 内部 `Instantiate(item.ItemGraphic)` 按引用实例化新物体，多个物品共享同一 ItemGraphicInfo 引用互不干扰（与原版 ItemAssetsCollection 全表共享同一 IG prefab 的机制一致）
- 原版物品无 3D 图形（`ItemGraphic` 为 null，如纯 Sprite 物品）→ `Debug.LogWarning` + 不赋值
- 原版物品 Identifier 形式：`Identifier("duckov", displayName)`（`GameItemLookup.TryGetIdentifier(string displayName, out Identifier)` 是公开发现 API，如 `"AK-47"` → `Identifier("duckov", "AK-47")`）
- 复用后附带原版 sockets / ShowIf / HideIf / 材质 全量生效——对简单物品与枪械均适用（间接获得 `ItemGraphicInfo_Gun` 能力）
- **仅同步版本**：纯引用赋值无 IO（对齐 `RegisterItem` 等同步注册语义，功能 3 双版本核查不要求无 IO 方法配异步版）

## 5. 功能 1 + 2 联动关系

```
┌─ AssetBundle 路径（既有，保留）─────────────────────────────┐
│  ItemUtils.SetItemGraphic(item, assetBundle, name)         │ ← 复杂模型（枪械/多材质/FBX/动画）
│  ItemUtils.RegisterGun / RegisterItemFromBundle            │    必须作者制 Bundle
└───────────────────────────────────────────────────────────┘
┌─ OBJ 简化路径（新增）──────────────────────────────────────┐
│  ItemGraphicUtils.SetItemGraphicAsync(item, meshId)       │ ← 内部 = LoadMeshAsync + GetModelMaterial
│  ItemGraphicUtils.CreateItemGraphic(meshId, textureId)    │    + GO 模板缓存组装（§4.3）
│  ItemGraphicUtils.SetItemGraphicFromOriginal(item, id)    │ ← 复用原版 ItemGraphic（零构建）
└───────────────────────────────────────────────────────────┘
                                  ↓
              ModelUtils.LoadMesh / LoadMeshAsync  ← 功能 2 提供 OBJ 解析（含 §3.5 性能优化）
```

- 两路径结果一致：都落到 `item.ItemGraphic`（非空）→ 原版 `CreateAGraphic` 三条链路（掉落/装备/手持）自动生效
- `ModelUtils` 不依赖 `ItemGraphicUtils`（通用能力）；`ItemGraphicUtils` 依赖 `ModelUtils`（单向）

- `ModelUtils` 不依赖 `ItemGraphicUtils`（通用能力）；`ItemGraphicUtils` 依赖 `ModelUtils`（单向）
- 既有 `ItemUtils.SetItemGraphic(Item, AssetBundle, string)` 保留不动，避免破坏已有 mod

## 6. 功能 3：Item 相关 API 同步/异步双版本核查

### 6.1 核查结果（基于 ItemUtils.cs 全量阅读）

| 方法 | 同步 | 异步 | 状态 |
|---|---|---|---|
| `LoadSprite(string / Identifier / FromDir)` | ✅ | ✅ `UniTask<Sprite?>` | 通过 |
| `GetCustomItem(Identifier, ItemData)` | ✅ | ✅ `UniTask<Item>` | 通过 |
| `GetCustomItem(ItemData)`（config 简化重载） | ✅ | ❌ 缺 | **补 `GetCustomItemAsync(ItemData)`** |
| `CreateCustomItem` | ✅ | ✅ `UniTask` | 通过 |
| `CreateCustomCartridge` | ✅ | ✅ `UniTask` | 通过 |
| `GetCustomCartridge` | ✅ | ✅ `UniTask<Item>` | 通过 |
| `CreateCustomBluePrint` | ✅ | ⚠️ `async Task` | **迁移为 `async UniTask`** |
| `CreateCustomBullet` | ✅ | ❌ 缺 | **新增 `CreateCustomBulletAsync`** |
| `SetItemGraphic`(AssetBundle) / `RegisterGun` / `RegisterItemFromBundle` | ✅ | ❌ | AssetBundle 路径，不属本 TODO 范围，保持同步 |
| `RegisterItem` / `UnregisterItem` / `UnregisterAllItem` / `HasTag` / `GetTargetTag` / `TryGetCustomItem` | ✅ | — | 同步注册/查询语义，无需异步版本 |

### 6.2 迁移明细

**① `CreateCustomBluePrintAsync`：`async Task` → `async UniTask`**
- 全库 grep 无外部调用方（仅 `QuestTest.cs:19` 调用同步版 `CreateCustomBluePrint`），迁移零影响
- 方法体无改动，仅返回类型 `Task` → `UniTask` + `using System.Threading.Tasks` 移除
- 注意：该方法目前**未用 ReserveTypeId 模式**（与其它 Async 方法不一致）——迁移时一并补上 `ReserveTypeId / CancelReservation`（await 前预定，防止并发抢占），对齐 `CreateCustomItemAsync` 模式

**② 新增 `CreateCustomBulletAsync(Identifier, BulletData)`**
- 对齐同步版 + ReserveTypeId 预定模式（复制 `CreateCustomCartridgeAsync` 的骨架）

**③ 新增 `GetCustomItemAsync(ItemData config)` 简化重载**
- 对齐同步 `GetCustomItem(ItemData)`：`[MethodImpl(NoInlining)]` + `Assembly.GetCallingAssembly()` 推导 modid → 转发到 `GetCustomItemAsync(Identifier, ItemData)`

## 7. 风险与约束

| 风险 | 说明 | 对策 |
|---|---|---|
| 线程亲和 | `new Mesh` / 赋值 / `Shader.Find` / 组件创建必须主线程 | IO + 解析在线程池返回纯数据；主线程组装（逐字对齐 `LoadSpriteFromDirAsync` 模式） |
| `Shader.Find` 时机 | 主场景加载前返回 null | 材质创建延迟到首次使用；null 降级 `Universal Render Pipeline/Lit` |
| 并发重复加载 | 同一 Identifier 并发调用重复解析 | Mesh/Material/GO 模板三级缓存 + lock 双检单飞 |
| Mesh 只读 | `Optimize()` / `UploadMeshData(true)` 后不可改 | 组装完成后调用；文档注明"加载后 Mesh 不可变" |
| 模板共享修改 | modder 修改模板缓存影响其它复用者 | 模板 inactive 入缓存，对外只暴露 Instantiate 副本 |
| 悬挂引用 | 释放顺序错误 → 模板仍引用已销毁 Mesh | 文档强制顺序：先 `ReleaseItemGraphic` 再 `ReleaseModel` |
| groundPoint 为 null | `SnapGroundPointToParent` NRE | 构建时强制创建 `GroundPoint` 子物体 |
| 坐标系 | 绕序/镜像错误导致背面剔除 | 默认绕 X 轴 180° 保定向变换；文档附 Blender 导出参数 |
| 卸载生命周期 | Mesh 为 UnityEngine.Object 需释放 | `ReleaseModel` / `ReleaseAllModels`、`ReleaseItemGraphic` / `ReleaseAllItemGraphics`（对齐 Registry 卸载语义） |
| 16-bit 索引上限 | >65535 顶点渲染错误 | 自动升 `IndexFormat.UInt32` |
| 缩放单位 | OBJ 无单位，游戏物品约 0.9m AABB | 文档注明 modder 导出时注意缩放 |
| Publicizer 依赖 | `Item.itemGraphic` / `ItemGraphicInfo.sockets` 为 private/protected 字段 | 已有 Krafs.Publicizer，直接赋值，零反射（符合反射最小化原则） |

## 8. 验收标准

- [ ] 编译通过（FeatherMod.sln，netstandard2.1）
- [ ] `ModelUtils.LoadMesh` / `LoadMeshAsync`：解析含 `v/vt/vn/f` 四形态、负索引、n 边形、无法线 OBJ 均返回正确 Mesh（顶点数、UV 翻转、法线正确）
- [ ] `.fbx` 文件返回 null + Warning 日志（降级提示）
- [ ] `ModelUtils.CreateModel` 产出 GO 含 MeshFilter + MeshRenderer（成对）、无 collider
- [ ] `ModelUtils.GetModelMaterial`：同 textureId 返回同一 Material 实例（缓存命中）；null → 默认材质；纹理放 `assets/textures/` 可读
- [ ] `ItemGraphicUtils.CreateItemGraphic` 产出 GO：根挂 `ItemGraphicInfo` + `CharacterSubVisuals`，`renderers.Count == 1` 且为主模型 Mesh Renderer，含 `GroundPoint` 子物体
- [ ] GO 模板缓存：同 (meshId, textureId) 二次调用返回新副本但模板复用（共享 Mesh）；同 meshId 不同 textureId → 不同材质、互不影响
- [ ] `SetItemGraphicFromOriginal`：指定原版物品（`Identifier("duckov", ...)`）后 `item.ItemGraphic` 非空；无 3D 图形的原版物品 → Warning 且不赋值
- [ ] 绑定后游戏内掉落/装备场景显示 3D 模型（原版 `CreateAGraphic` 链路生效，不走 Sprite 兜底）
- [ ] `ReleaseItemGraphic` → `ReleaseModel` 顺序卸载无悬挂引用（无 MissingReferenceException 日志）
- [ ] 性能基准（§3.5）：万级顶点 OBJ 线程池解析 < 100ms、主线程组装 < 5ms；解析段零托管分配；缓存命中二次调用 < 1ms
- [ ] AssetBundle 既有路径（`SetItemGraphic` / `RegisterGun` / `RegisterItemFromBundle`）行为不变（回归验证）
- [ ] 全 Item API 核查表（§6.1）逐项符合：`CreateCustomBluePrintAsync` 为 `UniTask`、`CreateCustomBulletAsync` / `GetCustomItemAsync(ItemData)` 存在
- [ ] 同步/异步版本行为一致（除 IO 调度外）
- [ ] 无新增反射、无新增第三方依赖、无 `System.Threading.Tasks` 使用（新增代码）

## 9. 预估文件变更清单

| 操作 | 文件 | 摘要 |
|---|---|---|
| 新建 | `FastModdingLib/Models/ModelUtils.cs` | OBJ 解析 + LoadMesh 双版本 + GetModelMaterial（缓存）+ CreateModel + Mesh/Material 缓存/卸载 |
| 新建 | `FastModdingLib/Items/ItemGraphicUtils.cs` | CreateItemGraphic 双版本 + SetItemGraphic 双版本 + SetItemGraphicFromOriginal + GO 模板缓存/卸载 |
| 修改 | `FastModdingLib/Items/ItemUtils.cs` | `CreateCustomBluePrintAsync` Task→UniTask + ReserveTypeId；新增 `CreateCustomBulletAsync`、`GetCustomItemAsync(ItemData)` |
| 文档 | `Docs/USAGE.md` | 模型目录约定（`assets/models/`）、纹理约定（`assets/textures/`，建议 `models/` 子目录与 sprite 隔离）、**AssetBundle 与 OBJ 路径选型准则（§1.1）**、OBJ 导出参数、FBX 不支持说明、复用原版物品模型用法 |

## 10. 已确认决策

| # | 决策 | 结论 |
|---|---|---|
| 1 | FBX 首版不支持 | ✅ 接受，仅 OBJ + 转 OBJ 提示 |
| 2 | 纹理约定 | ✅ 沿用 `assets/textures/`；文档注明建议与物品 sprite 隔离（`assets/textures/models/`） |
| 3 | GO 缓存 | ✅ 三级缓存：Mesh（meshId）+ Material（textureId）+ GO 模板（meshId, textureId）；同 Mesh 不同材质 → 独立模板/GO；对外只返回 Instantiate 副本 |
| 4 | 复用原版 ItemGraphic | ✅ 新增 `SetItemGraphicFromOriginal`（纯引用赋值，仅同步） |
| 5 | AssetBundle 与简化路径 | ✅ 并存：AssetBundle 保留为复杂模型唯一通道，OBJ 简化路径面向简单物品；选型准则写入 USAGE.md |
| 6 | 简化路径性能 | ✅ 时间/空间双维优化（§3.5）：零分配解析、List 直传、位打包/struct key、16-bit 优先、UploadMeshData、三级缓存；附验收基准 |

### 实现阶段仍可选的小决策（不阻塞审核）

1. `CreateModel(mesh, material)` 纯组装方法是否保留在首版（ItemGraphicUtils 内部流程已内联该逻辑）？建议保留（低成本的通用能力，供 modder 自由组合）。
2. OBJ 解析的 `coordinateFlip` 开关是否暴露到 public API？建议首版固定默认行为，不暴露。

## 11. 破坏性更改自审（按"非必要不破坏性更改"约束）

约束来源：AGENTS.md「行为准则」。审查对象：本方案全部改动点。

| # | 改动点 | 类型 | 影响面（全库搜索结论） | 结论 |
|---|---|---|---|---|
| 1 | `CreateCustomBluePrintAsync` 返回类型 `Task` → `UniTask` | **破坏性**（签名变更，异步返回类型不兼容） | 全库 grep：**无任何外部调用方**（仅 `QuestTest.cs:19` 调用同步版 `CreateCustomBluePrint`）；返回语义完全一致 | **允许**：符合 AGENTS.md「异步方案约束」既定迁移规则（遗留 async Task 逐步迁移、保证调用方不受影响）；补偿措施 = 语义不变 + 影响面为零 + 迁移方式写入 CHANGELOG/USAGE |
| 2 | `CreateCustomBluePrintAsync` 内部补 `ReserveTypeId/CancelReservation` | 行为增强（内部逻辑），签名不变 | 无调用方；与 `CreateCustomItemAsync` 等既有异步模式对齐 | **允许**：非破坏，反而修正与其它 Async 方法不一致的并发隐患 |
| 3 | 新增 `ModelUtils` / `ItemGraphicUtils` 全部 API | 新增 | 无 | **允许**：纯新增，无既有 API 变动 |
| 4 | 新增 `CreateCustomBulletAsync` / `GetCustomItemAsync(ItemData)` | 新增 | 无 | **允许**：纯新增；同步版不动 |
| 5 | AssetBundle 路径 `SetItemGraphic` / `RegisterGun` / `RegisterItemFromBundle` | 保留 | — | **允许**：零改动（§5 回归验收项保证行为不变） |
| 6 | `ItemUtils` 全部既有 public 方法 | 保留 | — | **允许**：除 #1 外零签名变动 |
| 7 | 原版代码 `ItemGraphicInfo` / `CharacterSubVisuals` | 不修改（Publicizer 赋值） | — | **允许**：零反射、零游戏代码改动 |
| 8 | `SetItemGraphicFromOriginal` 复用原版 ItemGraphic | 共享引用（不实例化、不克隆） | 与原版 `ItemAssetsCollection` 全表共享 IG prefab 的既有机制一致 | **允许**：不修改原版资源，运行时 `CreateAGraphic` 按引用实例化，互不干扰 |

**自审结论**：方案共 1 处破坏性变更（#1），已按约束完成影响面搜索（零调用方）并在本表标注破坏点、理由与补偿措施；其余全部为新增/保留。本方案通过"非必要不破坏性更改"自审。
