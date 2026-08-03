using Duckov.Endowment;
using Duckov.Quests;
using FeatherMod.Utils;
using SodaCraft.Localizations;
using System;
using UnityEngine;

namespace FeatherMod.Quests
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
        private bool _subscribed;

        private Identifier EndowmentId =>
            new Identifier(string.IsNullOrEmpty(endowmentDomain) ? "unknown" : endowmentDomain, endowmentPath);

        private string EndowmentDisplayName
        {
            get
            {
                if (EndowmentUtils.TryGetEndowment(EndowmentId, out var entry) && entry != null)
                {
                    try
                    {
                        return entry.DisplayName;
                    }
                    catch
                    {
                        // 第三方 mod（如 CustomTalentFrame）可能对 FML 动态创建的天赋打不判空的
                        // Harmony prefix，导致 DisplayName 抛异常；回退到 Identifier Path，保证 UI 不崩。
                    }
                }
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
                Debug.LogWarning($"[FMLReward_UnlockEndowment] Failed to subscribe onCompleted: {e.Message}");
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
