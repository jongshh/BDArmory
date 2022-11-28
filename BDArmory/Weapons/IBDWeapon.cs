namespace BDArmory.Weapons
{
    public interface IBDWeapon
    {
        WeaponClasses GetWeaponClass();

        string GetShortName();

        string GetSubLabel();

        string GetMissileType();

        string GetPartName();

        Part GetPart();

        // extensions for feature_engagementenvelope
    }

    // extensions for feature_engagementenvelope

    public enum WeaponClasses
    {
        Missile,
        Bomb,
        Gun,
        Rocket,
        DefenseLaser,
        SLW
    }
}
