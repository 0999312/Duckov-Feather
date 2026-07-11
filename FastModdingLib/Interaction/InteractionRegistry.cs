using FeatherMod.Register;
using FeatherMod.Utils;
using UnityEngine;

namespace FeatherMod.Interaction
{
    /// <summary>
    /// 交互点注册表。维护 Identifier → InteractionEntry 映射，
    /// OnRemoved 时自动 Destroy 对应的 GameObject。
    /// </summary>
    public sealed class InteractionRegistry : SimpleRegistry<InteractionEntry>
    {
        protected override void OnRemoved(Identifier id, InteractionEntry entry, string? modid)
        {
            if (entry?.Target != null)
                Object.Destroy(entry.Target);
        }
    }

    /// <summary>
    /// 交互点注册条目，记录交互点 GameObject 和所属 mod。
    /// </summary>
    public class InteractionEntry
    {
        public GameObject Target = null!;
        public string Modid = string.Empty;
    }
}
