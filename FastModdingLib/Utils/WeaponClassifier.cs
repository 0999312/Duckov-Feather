using ItemStatsSystem;
using UnityEngine;

namespace FeatherMod.Utils
{
    /// <summary>
    /// 武器分类工具。供 WeaponInjectionUtils / LotteryBoxPatch 等模块共用。
    /// 通过 Item prefab 组件 + MetaData Tag 回退判断 weapon 类型。
    /// </summary>
    public static class WeaponClassifier
    {
        public enum Kind { None, Gun, Melee }

        /// <summary>
        /// 通过 Item prefab 组件判断 weapon 类型，Tag 回退。
        /// </summary>
        internal static Kind Classify(int typeId)
        {
            if (typeId <= 0) return Kind.None;

            var prefab = ItemAssetsCollection.GetPrefab(typeId);
            if (prefab == null) return Kind.None;

            // 优先：组件检测
            if (prefab.GetComponent<ItemSetting_Gun>() != null) return Kind.Gun;
            if (prefab.GetComponent<ItemSetting_MeleeWeapon>() != null) return Kind.Melee;

            // Tag 回退
            var meta = ItemAssetsCollection.GetMetaData(typeId);
            if (meta.tags != null)
            {
                foreach (var tag in meta.tags)
                {
                    if (tag == null) continue;
                    if (tag.name == "Gun") return Kind.Gun;
                    if (tag.name == "MeleeWeapon") return Kind.Melee;
                }
            }

            return Kind.None;
        }
    }
}
