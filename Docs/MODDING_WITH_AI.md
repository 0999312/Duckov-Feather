# 使用 AI Agent 开发 FML 模组 / Modding with AI Agents

_面向**模组开发者**：让 Claude Code / opencode 等 AI Agent 为你的模组项目高效配置 FML 开发环境。_
_面向**FML 维护者**：本文档也是 Release 发布时人工放置内容的核对清单（见 §5）。_

---

## 1. 一句话提示词 / One-Line Prompt

> 复制下面任一句到模组项目的 AI Agent 对话中，即可完成配置并开始开发。

**中文版（推荐）**：

> 你正在用 FML（Feather Modding Lib）为《逃离鸭科夫》开发模组。开始前请先阅读工作区 `Docs/` 下的 FML 文档（`USAGE.md` 教程 + `API/` 目录的 API 参考），严格遵循 FML 约定：模组主类继承 `Duckov.Modding.ModBehaviour` 并实现 `IHasModid`；所有公开注册 API 使用 `Identifier`（`duckov` 域引用原版内容）；异步一律使用 UniTask；资源注册后由框架自动卸载。API 签名以 `Docs/API/` 为准，用法不确定时先查 `USAGE.md` 对应章节，不要凭记忆猜测游戏 API。

**English version**:

> You are developing an Escape From Duckov mod with FML (Feather Modding Lib). Before coding, read the FML docs in `Docs/` (tutorial `USAGE.md` + API reference in `API/`). Follow FML conventions: the mod main class inherits `Duckov.Modding.ModBehaviour` and implements `IHasModid`; use `Identifier` for all public registration APIs (use the `duckov:` domain for vanilla content); use UniTask for async; registered resources auto-cleanup on mod unload. Treat `Docs/API/` as the authoritative signature reference, and never guess game APIs from memory.

---

## 2. 复制到模组项目工作区的文件 / Files to Copy

> **重要**：以下文件**均不包含 FML 源码**。`FeatherMod.dll` 请从 **Steam 创意工坊**或 **GitHub Release** 下载（不要从源码构建）；文档可从本仓库 `Docs/` 直接复制，或使用 Release 附带的 Docs 包。
>
> **Harmony 由前置 mod 提供**：FML 的 `fml.json` 已声明依赖 **HarmonyLoadMod**（创意工坊 `workshopId: 3589088839`），运行时由它加载 Harmony，FML Release **不**打包 `0Harmony.dll`。模组代码若直接使用 `Harmony.PatchAll` 等 API，可从 HarmonyLoadMod 的 mod 目录复制 `0Harmony.dll` 作为编译引用（仅编译期需要）。

### 必需 / Required

| 文件 | 来源 | 说明 |
|------|------|------|
| `libs/FeatherMod.dll` | GitHub Release / 创意工坊 | FML 运行时库，模组 csproj 引用 |
| `Docs/USAGE.md` | 本仓库 `Docs/` | 教程式使用指南（怎么用：快速开始、核心概念、全模块示例） |
| `Docs/API/`（10 个文件） | 本仓库 `Docs/API/` | API 参考（`API.md` 索引 + 9 个模块文件，Agent 检索签名用） |

### 可选 / Optional

| 文件 | 来源 | 说明 |
|------|------|------|
| `libs/0Harmony.dll` | HarmonyLoadMod 的 mod 目录 | 模组代码直接使用 `Harmony.PatchAll` 时的**编译期**引用（运行时由 HarmonyLoadMod 提供） |
| `Docs/MIGRATION.md` | 本仓库 `Docs/` | 从旧版 FML 迁移时查阅 |
| `AGENTS.md`（模板） | 见 §4 | 模组项目专属的 Agent 指南（强烈推荐） |

### 推荐目录结构 / Recommended Layout

```
MyMod/
├── AGENTS.md              # 模组项目 Agent 指南（§4 模板）
├── Docs/                  # ← 从 FML 复制（只读参考，勿改）
│   ├── USAGE.md
│   ├── MIGRATION.md            # （可选）
│   └── API/
│       ├── API.md
│       ├── API_CORE.md
│       ├── API_ITEMS.md
│       ├── API_CRAFTING.md
│       ├── API_QUESTS.md
│       ├── API_BUILDING.md
│       ├── API_PERK_ENDOWMENT.md
│       ├── API_ENTITIES.md
│       ├── API_INTERACTION_UI.md
│       └── API_SYSTEM.md
├── libs/
│   └── FeatherMod.dll    # ← 从 GitHub Release / 创意工坊下载
├── MyMod.csproj
└── MyMod.cs              # ModBehaviour 主类
```

### csproj 引用示例 / csproj Reference

```xml
<ItemGroup>
  <!-- 游戏 DLL 引用（见 Docs/USAGE.md §1.2）... -->
  <Reference Include="FeatherMod">
    <HintPath>libs\FeatherMod.dll</HintPath>
  </Reference>
  <!-- 仅当模组代码直接调用 Harmony API 时（编译期引用，运行时由 HarmonyLoadMod 提供）：
  <Reference Include="0Harmony">
    <HintPath>path\to\HarmonyLoadMod\0Harmony.dll</HintPath>
  </Reference>
  -->
</ItemGroup>
```

---

## 3. 配置步骤 / Setup Steps

1. **下载**：从 GitHub Release（或 Steam 创意工坊）下载 FML，取 `FeatherMod.dll` 放入 `libs/`。若模组代码直接调用 Harmony API，再从 HarmonyLoadMod 的 mod 目录复制 `0Harmony.dll` 作编译引用
2. **复制文档**：将 `Docs/` 目录（`USAGE.md` + `API/`）复制到项目工作区（Agent 需要文档作为上下文）
3. **创建模组项目**：.NET Standard 2.1 类库，配置游戏 DLL 引用（见 `Docs/USAGE.md` §1.2）
4. **粘贴 §4 的 `AGENTS.md` 模板**（可自定义）
5. **启动 Agent**：把 §1 的一句话提示词发给 Agent，开始开发

---

## 4. 模组项目 AGENTS.md 模板 / Mod Project AGENTS.md Template

> 复制以下内容到模组项目根目录 `AGENTS.md`，让 Agent 自动加载 FML 约定。

````markdown
# AGENTS.md

以下规则对模组项目的所有 Agent 会话完全生效。

## 身份

你是《逃离鸭科夫》(Duckov) 模组开发者，使用 FML（Feather Modding Lib）框架开发。

## 文档（工作区内）

- `Docs/USAGE.md` — 教程式使用指南（怎么用）。示例代码、推荐流程以它为准。
- `Docs/API/` — API 参考（有什么）。**签名以 `API/` 目录为准**；USAGE 与 API 冲突时以 API 为准并指出。

## 强制约定

1. **Identifier 优先**：所有 FML 公开注册/查询 API 使用 `Identifier("domain", "path")`。
   - `domain` = 本模组 modid；`duckov` 域 = 游戏原版内容。
   - 禁止向 FML API 传游戏原生数字 ID（如 Item.typeID、EndowmentIndex）——它们由 FML 内部分配映射。
2. **模组主类**：继承 `Duckov.Modding.ModBehaviour`（游戏基类）+ 实现 `IHasModid`。
   - **禁止**继承 `FeatherMod.ModBehaviour`（那是 FML 自身的入口）。
   - 在 `OnAfterSetup()` 中：`ModPathResolver.Register` → `I18n.InitI18n` → `Harmony.PatchAll` → 注册内容。
3. **异步一律 UniTask**（`Cysharp.Threading.Tasks`），禁止 `async Task`。加载阶段优先异步版本（Async 后缀）。
4. **自动卸载**：FML 自动清理注册资源，无需手动 `UnregisterAll`（除非明确需要）。
5. **Tag 顺序**：`TagUtils.RegisterTag` 必须在 `CreateCustomItem` 之前。
6. **任务关系图**：`QuestUtils.RegisterQuest` 不会自动建立前后置关系，必须手动 `AddQuestRelation`。
7. **已废弃 API 禁用**：`Docs/USAGE.md` §32.3 汇总了 Obsolete API（如 `DialogueUtils`、`SubtitleLine`），新代码一律使用替代 API。
8. **禁止凭记忆猜测游戏 API**：游戏类/方法/字段签名不确定时，查阅工作区文档或游戏逆向工程目录；禁止推测性反射。

## 工作区

- `libs/FeatherMod.dll` — FML 运行时引用（不要提交修改）。
- 请保持 `Docs/` 只读；如需记录模组自己的决策，写入 `MOD_NOTES.md` 或代码注释。
````

---

## 5. Release 放置核对清单 / Release Checklist（FML 维护者）

> GitHub Release 由人工放置。建议每个 Release 附带**两类包**（与版本匹配）：
> ① **完整 Mod ZIP**（大多数玩家装这个）；② **最小 DLL 包**（模组开发者 / Agent 用）。

### 5.1 完整 Mod ZIP（必需）/ Full Mod ZIP

玩家下载后解压整个 `FeatherMod/` 目录到游戏 `Mods/` 目录即可使用（框架自身作为一个模组加载）。

```
FeatherMod_v{版本}.zip
└── FeatherMod/                      # ← 解压后整体放入游戏 Mods/ 目录
    ├── info.ini                     # 模组元数据（name=FeatherMod，必须与 fml.json modid 一致）
    ├── FeatherMod.dll               # 主 DLL（构建产物）
    ├── fml.json                     # 优先级与依赖声明（priority=999，依赖 HarmonyLoadMod）
    └── assets/
        └── lang/                    # 9 个语言文件（en_us/zh_cn/zh_tw/ja_jp/ru_ru/ko_kr/it_it/fr_fr/sv_se）
```

**打包要点**：

- `info.ini` 的 `name` 必须与 `fml.json` 的 `modid`（`FeatherMod`）一致，否则 fml.json 被忽略
- **不打包 Harmony**：运行时 Harmony 由前置 mod **HarmonyLoadMod**（创意工坊 `workshopId: 3589088839`）提供——`fml.json` 已声明硬依赖，README 中注明安装指引
- **不打包 `Extra/Shaders/`**：该目录是给开发者制作 AssetBundle 的 Unity 工程资源（.shader 源文件），不是运行时资源
- **不要**包含源码工程（`FastModdingLib/`）、`bin/`、`obj/`、`.pdb` 等调试产物
- ZIP 根目录放 `FeatherMod/` 文件夹（而非散文件），避免用户解压错位

### 5.2 最小 DLL 包（必需）/ Minimal DLL Pack

面向模组开发者与 AI Agent 配置（见 §2 文件清单）：

| 文件 | 说明 |
|------|------|
| `FeatherMod.dll` | 主 DLL（构建产物） |
| `docs/`（可选） | 打包 `Docs/USAGE.md` + `Docs/API/`，方便用户免复制 |

> Harmony 编译引用（`0Harmony.dll`）由 HarmonyLoadMod 提供，不随 FML Release 分发。
> 注意：Release 中**不要**包含 FML 源码工程（`FastModdingLib/`），社区用户只需要 DLL 与文档。

---

## 6. 常见问题 / FAQ

| 问题 | 回答 |
|------|------|
| 必须复制文档吗？ | 不必须，但强烈推荐——Agent 的 FML 知识可能过时，工作区文档是唯一可靠上下文 |
| 文档版本与 DLL 版本不匹配？ | 以 Release 附带的文档为准；从仓库复制文档时确认与 DLL 版本对应 |
| 模组项目需要 `0Harmony.dll` 吗？ | 仅当模组代码直接调用 Harmony API（如 `PatchAll`）时需要**编译期**引用；运行时由 HarmonyLoadMod 提供，FML 不打包分发 |
| 可以用 FML 源码引用来调试吗？ | 可以（源码在 FML 仓库），但发布时务必只依赖 DLL |
