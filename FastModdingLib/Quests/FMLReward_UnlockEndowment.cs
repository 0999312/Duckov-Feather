using Duckov.Endowment;
using Duckov.Quests;
using FastModdingLib.Utils;
using SodaCraft.Localizations;
using System.Reflection;
using UnityEngine;

namespace FastModdingLib.Quests
{
    /// <summary>
    /// FML 扩展奖励：任务完成时自动解锁指定天赋。
    /// 继承自 <see cref="Reward"/>，AutoClaim + Start 检测 + OnClaim 兜底。
    /// </summary>
    public class FMLReward_UnlockEndowment : Reward
    {
        [SerializeField]
        internal string endowmentDomain = "";

        [SerializeField]
        internal string endowmentPath = "";

        private bool _claimed;

        private Identifier EndowmentId =>
            new Identifier(string.IsNullOrEmpty(endowmentDomain) ? "unknown" : endowmentDomain, endowmentPath);

        private string EndowmentDisplayName
        {
            get
            {
                if (EndowmentUtils.TryGetEndowment(EndowmentId, out var entry) && entry != null)
                    return entry.DisplayName;
                return endowmentPath;
            }
        }

        public override bool Claimed => _claimed;

        public override bool AutoClaim => true;

        public override Sprite? Icon => null;

        public override string Description
            => $"{"Reward_UnlockEndowment".ToPlainText()}: {EndowmentDisplayName}";

        private void Start()
        {
            // 读档场景：任务已完成但奖励未领取
            if (Master != null && Master.Complete && !_claimed)
            {
                TryUnlock();
                _claimed = true;
                ReportStatusChanged();
            }

            // 通过反射订阅 onCompleted（避免 Publicizer 导致的二义性）
            if (Master != null)
            {
                var fi = typeof(Quest).GetField("onCompleted",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var evt = fi?.GetValue(Master) as System.Action<Quest>;
                if (evt != null)
                {
                    evt += OnQuestCompleted;
                    fi?.SetValue(Master, evt);
                }
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
            if (EndowmentUtils.Registry.TryGetIndex(EndowmentId, out var idx)
                && !EndowmentManager.GetEndowmentUnlocked(idx))
            {
                EndowmentUtils.UnlockEndowment(EndowmentId);
            }
        }

        public override object GenerateSaveData() => _claimed;

        public override void SetupSaveData(object data)
        {
            if (data is bool claimed)
                _claimed = claimed;
        }
    }
}
