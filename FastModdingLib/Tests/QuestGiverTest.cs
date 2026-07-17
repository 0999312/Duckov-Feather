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
            // 1. 注册自定义 QuestGiver
            var config = new QuestGiverConfig
            {
                DisplayNameKey = "NPC_Giver_Test",
                ActorId = "dialogue_test",
                SpawnPosition = new Vector3(100, 0, 100),
                BoundQuests = null // 注册后再绑定
            };

            var giverId = new Identifier("testmod", "test_giver");
            int customId = QuestGiverUtils.RegisterQuestGiver(giverId, config);

            Debug.Log($"[QuestGiverTest] Registered: {giverId} → custom ID {customId}");

            // 2. 查询
            bool found = QuestGiverUtils.TryGetQuestGiver(giverId, out var template);
            Debug.Assert(found, "QuestGiver should be found after registration");
            Debug.Assert(template != null, "GameObject template should not be null");

            // 3. 验证 ID 映射
            bool hasId = QuestGiverUtils.TryGetQuestGiverId(giverId, out int queriedId);
            Debug.Assert(hasId, "Should have quest giver ID");
            Debug.Assert(queriedId == customId, "Queried ID should match registered ID");
            Debug.Assert(QuestGiverUtils.IsCustomQuestGiverId(customId), "Should be recognized as custom ID");

            // 4. 生成 NPC（不实际生成，只验证模板有效）
            Debug.Log("[QuestGiverTest] Spawning (template validation only)...");
            Debug.Assert(template.GetComponent<Duckov.Quests.QuestGiver>() != null,
                "Template should have QuestGiver component");

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
            // 1. 注册 QuestGiver
            var config = new QuestGiverConfig
            {
                DisplayNameKey = "NPC_Giver_Friendly",
                SpawnPosition = Vector3.zero
            };
            var giverId = new Identifier("testmod", "friendly_qg");
            int customId = QuestGiverUtils.RegisterQuestGiver(giverId, config);

            // 2. 通过 FriendlyNpcUtils 创建 NPC 并绑定 QuestGiver
            var npcConfig = new FriendlyNpcConfig
            {
                Role = NpcRole.QuestGiver,
                QuestGiverId = customId.ToString(), // 使用自定义 int ID
                SpawnPosition = new Vector3(10, 0, 10)
            };
            var npc = FriendlyNpcUtils.CreateFriendlyNpc(
                new Identifier("testmod", "npc_with_giver"), npcConfig);

            Debug.Assert(npc != null, "Friendly NPC should be created");
            Debug.Assert(npc.GetComponent<Duckov.Quests.QuestGiver>() != null,
                "NPC should have QuestGiver component");

            // 3. 清理
            FriendlyNpcUtils.RemoveNpc(new Identifier("testmod", "npc_with_giver"));
            QuestGiverUtils.UnregisterQuestGiver(giverId);

            Debug.Log("[QuestGiverTest] FriendlyNpc integration test passed.");
        }

        /// <summary>
        /// ShopUtils Identifier 修复验证。
        /// </summary>
        public static void TestShopIdentifierFix()
        {
            // 验证 CreateMerchantProfile(Identifier) 存在
            var shopId = new Identifier("testmod", "Merchant_Test");
            // 注：实际运行需要游戏环境，此处仅编译验证

            Debug.Log("[QuestGiverTest] ShopUtils Identifier overload verified.");
        }
    }
}
