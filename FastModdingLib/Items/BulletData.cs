
namespace FeatherMod
{
    public class BulletData : ItemData
    {
        public new UsageData? usages = null;
        public string Caliber = string.Empty;
        public string SFX_Put = "e_Item_Bullet";

        public float CritDamageFactorGain = 0F;
        public float damageMultiplier = 0F;
        public float CritRateGain = 0F;
        public float ArmorPiercingGain = 0F;

        public float ArmorBreakGain = 0F;
        public float DurabilityCost = 0F;
        public float ExplosionRange = 0F;
        public float ExplosionDamage = 0F;

        public float buffChanceMultiplier = 0F;
        public float bleedChance = 0F;
    }
}
