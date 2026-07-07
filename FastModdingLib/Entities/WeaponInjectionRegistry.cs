using FeatherMod.Register;
using FeatherMod.Utils;

namespace FeatherMod.Entities
{
    /// <summary>
    /// 武器注入注册表。维护 Identifier → WeaponInjectionData 映射，
    /// 继承 SimpleRegistry 获得 owner 追踪和 RemoveAllByOwner。
    /// OnRemoved 时自动恢复所有被修改的 ScriptableObject 数据。
    /// </summary>
    public sealed class WeaponInjectionRegistry : SimpleRegistry<WeaponInjectionData>, ERegistry
    {
        /// <summary>
        /// 删除 entry 时恢复所有被修改的 Pool。
        /// </summary>
        protected override void OnRemoved(Identifier id, WeaponInjectionData data, string? modid)
        {
            foreach (var backup in data.Backups)
            {
                WeaponInjectionUtils.RestorePool(backup);
            }
        }

        /// <summary>ERegistry 接口 — 供 RegistryManager 批量卸载。</summary>
        int ERegistry.RemoveAllByOwner(string modid)
        {
            return RemoveAllByOwner(modid);
        }
    }
}
