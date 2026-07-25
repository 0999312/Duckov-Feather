namespace FeatherMod
{
    /// <summary>
    /// FML 框架级常量。所有模块共享的内部标识符统一从此处引用。
    /// </summary>
    internal static class FMLConstants
    {
        /// <summary>FML 自身的 modid，用于 Registry 元表注册。</summary>
        internal const string Domain = "feather";

        /// <summary>原版游戏内容的 domain，用于 GameItemLookup 反查表和原版资源 Identifier。</summary>
        internal const string DuckovDomain = "duckov";

        /// <summary>懒注册原版资源时的 owner 标记，确保不被模组卸载误清。</summary>
        internal const string VanillaOwner = "__vanilla__";
    }
}
