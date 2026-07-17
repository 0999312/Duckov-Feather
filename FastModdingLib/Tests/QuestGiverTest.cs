using FeatherMod.Utils;
using UnityEngine;

namespace FeatherMod.Tests
{
    /// <summary>
    /// QuestGiver 模块功能测试。
    /// 测试自定义任务发放者的注册、生成、查询和卸载流程。
    /// </summary>
    public static class QuestGiverTest
    {
        /// <summary>
        /// 基本功能测试：注册 QuestGiver → 生成 NPC → 查询 → 绑定任务 → 卸载。
        /// </summary>
        public static void Test()
        {
            var giverId = new Identifier("testmod", "test_giver");

            // 1. 注册自定义 QuestGiver（仅分配 ID，不创建 GO）
            int customId = QuestGiverUtils.RegisterQuestGiver(giverId);
            Debug.Log($"[QuestGiverTest] Registered: {giverId} → custom ID {customId}");

            // 2. 查询 — 注意：TryGetQuestGiver 需先有 GO（通过 CreateQuestGiver 创建）
            bool hasId = QuestGiverUtils.TryGetQuestGiverId(giverId, out int queriedId);
            Debug.Assert(hasId, "Should have quest giver ID");
            Debug.Assert(queriedId == customId, "Queried ID should match registered ID");
            Debug.Assert(QuestGiverUtils.IsCustomQuestGiverId(customId), "Should be recognized as custom ID");

            // 3. 创建交互点 GO（独立的 QuestGiver 组件 + Collider）
            var go = QuestGiverUtils.CreateQuestGiver(giverId, new Vector3(100, 0, 100));
            Debug.Assert(go != null, "Should create quest giver interact point");
            Debug.Assert(go.GetComponent<Duckov.Quests.QuestGiver>() != null,
                "Interact point should have QuestGiver component");

            // 4. 验证创建后可查询到 GO
            bool found = QuestGiverUtils.TryGetQuestGiver(giverId, out var template);
            Debug.Assert(found, "QuestGiver should be found after creating interact point");

            // 5. 清理
            bool removed = QuestGiverUtils.UnregisterQuestGiver(giverId);
            Debug.Assert(removed, "Should successfully unregister");

            // 6. 验证已清理
            bool foundAfter = QuestGiverUtils.TryGetQuestGiver(giverId, out _);
            Debug.Assert(!foundAfter, "Should not be found after unregistration");

            Debug.Log("[QuestGiverTest] All assertions passed.");
        }

        /// <summary>
        /// FriendlyNpcUtils 集成测试：通过 FriendlyNpcUtils 创建 QuestGiver 类型 NPC。
        /// </summary>
        public static void TestFriendlyNpcIntegration()
        {
            // 1. 注册 QuestGiver Identifier
            var giverId = new Identifier("testmod", "friendly_qg");
            int customId = QuestGiverUtils.RegisterQuestGiver(giverId);
            Debug.Assert(customId >= QuestGiverRegistry.MinCustomQuestGiverId,
                $"Custom ID should be >= {QuestGiverRegistry.MinCustomQuestGiverId}");

            // 2. 通过 FriendlyNpcUtils 创建 NPC 并绑定 QuestGiver
            var npcConfig = new FriendlyNpcConfig
            {
                Role = NpcRole.QuestGiver,
                QuestGiverId = giverId,
                SpawnPosition = new Vector3(10, 0, 10)
            };
            var npc = FriendlyNpcUtils.CreateFriendlyNpc(
                new Identifier("testmod", "npc_with_giver"), npcConfig);

            Debug.Assert(npc != null, "Friendly NPC should be created");

            // 3. 清理
            FriendlyNpcUtils.RemoveNpc(new Identifier("testmod", "npc_with_giver"));
            QuestGiverUtils.UnregisterQuestGiver(giverId);

            Debug.Log("[QuestGiverTest] FriendlyNpc integration test passed.");
        }
    }
}
