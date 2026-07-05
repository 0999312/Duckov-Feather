namespace FastModdingLib.Utils
{
    /// <summary>
    /// 前缀通配符匹配工具。供 WeaponInjectionUtils / LotteryBoxPatch 等模块共用。
    /// </summary>
    public static class WildcardHelper
    {
        /// <summary>
        /// 前缀通配匹配。pattern 以 "*" 结尾时做 StartsWith 匹配；否则精确匹配。
        /// </summary>
        public static bool Match(string pattern, string input)
        {
            if (pattern == input) return true;
            if (pattern.EndsWith("*"))
            {
                var prefix = pattern.Substring(0, pattern.Length - 1);
                return input.StartsWith(prefix);
            }
            return false;
        }
    }
}
