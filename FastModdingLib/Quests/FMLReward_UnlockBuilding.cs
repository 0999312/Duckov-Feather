using Duckov.Buildings;
using Duckov.Quests;
using Duckov.Utilities;
using FeatherMod.Utils;
using SodaCraft.Localizations;
using System;
using UnityEngine;

namespace FeatherMod.Quests
{
    /// <summary>
    /// FML 扩展奖励：任务完成时解锁指定建筑的建造权限。
    /// 继承自 <see cref="Reward"/>，AutoClaim + Start 检测 + OnClaim 兜底。
    /// </summary>
    public class FMLReward_UnlockBuilding : Reward
    {
        [SerializeField]
        internal string buildingDomain = "";

        [SerializeField]
        internal string buildingPath = "";

        /// <summary>BuildingInfo JSON 序列化字符串（存储完整 struct）。</summary>
        [SerializeField]
        internal string buildingInfoJson = "";

        /// <summary>Building prefab 名称（游戏已有的 Building prefab）。</summary>
        [SerializeField]
        internal string prefabName = "";

        private bool _claimed;
        private bool _subscribed;

        private Identifier BuildingId =>
            new Identifier(string.IsNullOrEmpty(buildingDomain) ? "unknown" : buildingDomain, buildingPath);

        private string BuildingDisplayName => buildingPath;

        public override bool Claimed => _claimed;

        public override bool AutoClaim => true;

        public override Sprite? Icon => null;

        public override string Description
            => $"{"Reward_UnlockBuilding".ToPlainText()}: {BuildingDisplayName}";

        private void Start()
        {
            if (Master != null && Master.Complete && !_claimed)
            {
                TryUnlock();
                _claimed = true;
                ReportStatusChanged();
            }

            SubscribeQuestCompleted();
        }

        /// <summary>
        /// 订阅 Quest.onCompleted。该事件是 internal event：backing 字段为编译器生成的
        /// private 字段，GetField 必然返回 null（旧实现因此静默失效）；
        /// 用 GetEvent + AddEventHandler 标准订阅（事件反射，非 backing field 反射）。
        /// </summary>
        private void SubscribeQuestCompleted()
        {
            if (_subscribed || Master == null) return;
            try
            {
                var evt = typeof(Quest).GetEvent("onCompleted",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public);
                if (evt != null)
                {
                    evt.AddEventHandler(Master, new Action<Quest>(OnQuestCompleted));
                    _subscribed = true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FMLReward_UnlockBuilding] Failed to subscribe onCompleted: {e.Message}");
            }
        }

        private void OnQuestCompleted(Quest quest)
        {
            if (!_claimed)
            {
                TryUnlock();
                _claimed = true;
                ReportStatusChanged();
            }
        }

        public override void OnClaim()
        {
            if (!_claimed)
            {
                TryUnlock();
                _claimed = true;
                ReportStatusChanged();
            }
        }

        private void TryUnlock()
        {
            if (string.IsNullOrEmpty(prefabName)) return;

            // 从 BuildingDataCollection 获取 prefab
            var prefab = BuildingDataCollection.GetPrefab(prefabName);
            if (prefab == null)
            {
                Debug.LogWarning($"[FMLReward_UnlockBuilding] Prefab '{prefabName}' not found.");
                return;
            }

            // 反序列化 BuildingInfo
            BuildingInfo info = default;
            if (!string.IsNullOrEmpty(buildingInfoJson))
            {
                info = JsonUtility.FromJson<BuildingInfo>(buildingInfoJson);
            }

            if (string.IsNullOrEmpty(info.id))
            {
                info.id = BuildingId.Path;
            }

            // 注册建筑（如果尚未注册）
            var collection = GameplayDataSettings.BuildingDataCollection;
            if (collection == null) return;

            if (!collection.infos.Exists(i => i.id == info.id))
                collection.infos.Add(info);

            if (!collection.prefabs.Exists(p => p != null && p.name == prefab.name))
                collection.prefabs.Add(prefab);
        }

        public override object GenerateSaveData() => _claimed;

        public override void SetupSaveData(object data)
        {
            if (data is bool claimed)
                _claimed = claimed;
        }
    }
}
