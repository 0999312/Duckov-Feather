using FeatherMod.Utils;

namespace FeatherMod
{
    /// <summary>
    /// 钓鱼池配置 DTO。定义一片水域中可钓到的鱼种、权重和条件。
    /// </summary>
    public class FishingPoolConfig
    {
        /// <summary>水域标识符。</summary>
        public Identifier WaterId;

        /// <summary>鱼种列表（含权重和品质）。</summary>
        public FishingPoolEntry[] Entries = System.Array.Empty<FishingPoolEntry>();

        /// <summary>限制可钓天气的标签（为空则不限制）。使用游戏原生的 Tag 类型。</summary>
        public string[] RequiredWeatherTags = System.Array.Empty<string>();

        /// <summary>最低运气值（影响上钩品质）。默认 0.1f。</summary>
        public float MinLuck = 0.1f;

        /// <summary>最高运气值（影响上钩品质）。默认 1.0f。</summary>
        public float MaxLuck = 1.0f;
    }

    /// <summary>
    /// 钓鱼池中的单条鱼种条目。包含权重和品质要求。
    /// </summary>
    public struct FishingPoolEntry
    {
        /// <summary>鱼物品的 Identifier。</summary>
        public Identifier FishId;

        /// <summary>权重（越高越容易钓到）。</summary>
        public float Weight;

        /// <summary>最低品质等级（null = 无限制）。</summary>
        public int? MinQuality;

        /// <summary>匹配标签（为空则无标签限制）。</summary>
        public string[] Tags;
    }
}
