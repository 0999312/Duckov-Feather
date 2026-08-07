namespace FeatherMod
{
    /// <summary>
    /// 游戏内建槽位 key 常量。
    /// 注意：槽位 key 只约定标识字符串，Tag 约束（requireTags）不固定——例如枪械槽位的 Tag
    /// 实际来自 Bundle 内枪械预制体（见 <see cref="ItemUtils.RegisterGun"/>），不同武器可能不同。
    /// </summary>
    public static class SlotKeys
    {
        // ═══════════════════════════════════════════════════
        // 枪械槽位（1_Rifle-A_template.prefab）
        // ═══════════════════════════════════════════════════
        public const string Scope = "Scope";
        public const string Muzzle = "Muzzle";
        public const string Grip = "Grip";
        public const string Stock = "Stock";
        public const string Tec = "Tec";
        public const string Mag = "Mag";

        // ═══════════════════════════════════════════════════
        // 角色槽位（Character_0.prefab）
        // ═══════════════════════════════════════════════════
        public const string PrimaryWeapon = "PrimaryWeapon";
        public const string SecondaryWeapon = "SecondaryWeapon";
        public const string MeleeWeapon = "MeleeWeapon";

        /// <summary>头盔槽位。值为 "Helmat"（游戏原生拼写如此，非笔误）。</summary>
        public const string Helmet = "Helmat";
        public const string Armor = "Armor";
        public const string FaceMask = "FaceMask";
        public const string Headset = "Headset";
        public const string Backpack = "Backpack";
        public const string Totem1 = "Totem1";
        public const string Totem2 = "Totem2";

        // ═══════════════════════════════════════════════════
        // 其他内建槽位
        // ═══════════════════════════════════════════════════
        /// <summary>鱼竿饵料槽位。</summary>
        public const string Bait = "Bait";
        /// <summary>游戏机显示器槽位。</summary>
        public const string MonitorSlot = "MonitorSlot";
        /// <summary>游戏机手柄槽位。</summary>
        public const string ConsoleSlot = "ConsoleSlot";
    }
}
