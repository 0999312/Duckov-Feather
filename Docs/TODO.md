# TODO 列表

> 本文件是项目统一的待办清单。专项文档见 `Docs/TODO_*.md`。
> 历史已完成项已归档至 `Docs/PROGRESS.md`（不在本文件重复记录）。

---

## 待修复 / To Fix

### 1. WithDurabilityCost 耐久折算合成 — ⏳ 待修复

📄 专项文档：[TODO_DurabilityCost.md](./TODO_DurabilityCost.md)（2026-07-25 创建，2026-08-07 复核仍全部有效）

| # | 问题 | 现状 |
|---|------|------|
| Bug 1 | 耐久消耗模式下物品被直接扣 StackCount 而非优先降低 Durability | 未修复（`TagCostValidator.ConsumeFromItems` 仍走 `StackCount -= toRemove`） |
| Bug 2 | 标签匹配成本的配方在 CraftView 不显示物品图标 | 未修复（标签成本只进 `TagCostRegistry`，未写原生 `formula.cost.items`） |
| 额外发现 | 非标签成本的 `DurabilityCost` 在 `ResolveItems` 中被静默丢弃 | 未修复（`ResolveItems` 只提取 typeID + amount） |

### 2. BuildingSlotsWatcher / Machine 生产系统未接线 — ⏳ 待修复

> 来源：`PROGRESS.md` 2026-07-31 遗留问题；2026-08-07 用户确认**问题仍存在且尚未修复**。
> 说明：`MachineRecipe` / `BuildingSlotsWatcher` / `ConfigureBuildingUI` 类已存在，但 Machine 生产系统的整体接线（子库存变化 → CanExecute → Execute 的完整链路）仍未完成，属大改动，需后续 Phase。

---

## 待验证 / To Verify（需游戏内实测）

> 以下功能已实现，但需运行游戏/测试模组验证，未通过前不得视为完成。

- [ ] **ItemGraphic 游戏内实测**：掉落/装备场景 3D 模型显示、挂角色后层切换跟随（`PROGRESS.md` ItemGraphic Phase）
- [ ] **OBJ 解析基准实测**：万级顶点 <100ms（`ModelUtils` 性能目标）

---

## 未来功能 / Future Features

| 项目 | 说明 |
|------|------|
| `EquipmentUtils` 运行时装备（Set/Get/Clear on model） | 依赖游戏物品槽位系统语义（`ArmorSlot`/`HelmatSlot`/`BackpackSlot`），需基于 `CharacterItemControl` 实现新功能；当前正确用法为生成前配置 |
| OBJ 多 submesh（`usemtl` 分组）支持 | 首版按单 submesh 合并，多材质需扩展 |
| FBX / glTF 导入 | 游戏内无 FBX SDK；glTF 2.0 路线独立立项 |
| `SetItemGraphicFromOriginal` 悬空引用 | 复用原版 ItemGraphic 后，若原版物品被 mod 卸载，共享引用会悬空（与原版共享机制一致，风险已知） |
| Phase 6 质量工程 | 测试 / 示例模组 / CI-CD（README 项目状态：⏳ 待启动） |

---

## 已解决 / Resolved

| 项 | 结果 |
|----|------|
| `Docs/TODO/FEATHER_API_GAPS.md`（交互系统 5 个缺口） | ✅ 全部实现（2026-07-22 交互系统重设计解决），文档已删除 |
| 交互系统功能测试（Crafting 交互 / 多交互组装 / 交互名显示 / functionContainer 访问） | ✅ 2026-08-07 经 DuckovDrinks 测试模组验证通过 |
| DuckovDrinks 功能测试 | ✅ 2026-08-07 验证通过 |
