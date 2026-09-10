using TheVeil.Sim;

namespace TheVeil.UI
{
    /// <summary>
    /// What the game's things are called on screen, in the current language.
    ///
    /// One place, because it was three: the troop screen, the shop and the run's own
    /// panel each had a switch naming the nine troops, and they had already drifted apart
    /// in order. Three copies of one list is three places to translate and two to forget.
    /// </summary>
    public static class Names
    {
        public static string Troop(TroopKind kind)
        {
            switch (kind)
            {
                case TroopKind.Spearmen: return Loc.T("Spearmen");
                case TroopKind.Swordsmen: return Loc.T("Swordsmen");
                case TroopKind.Archers: return Loc.T("Archers");
                case TroopKind.Cavalry: return Loc.T("Cavalry");
                case TroopKind.Mage: return Loc.T("Mage");
                case TroopKind.Scout: return Loc.T("Scout");
                case TroopKind.Shieldbearer: return Loc.T("Shieldbearers");
                case TroopKind.Priest: return Loc.T("Priest");
                case TroopKind.Engineer: return Loc.T("Engineer");
                default: return kind.ToString();
            }
        }

        /// <summary>A troop in a word, for the shop's chips.</summary>
        public static string TroopShort(TroopKind kind)
        {
            switch (kind)
            {
                case TroopKind.Spearmen: return Loc.T("SPEAR");
                case TroopKind.Swordsmen: return Loc.T("SWORD");
                case TroopKind.Archers: return Loc.T("BOW");
                case TroopKind.Cavalry: return Loc.T("CAVALRY");
                case TroopKind.Mage: return Loc.T("MAGE");
                case TroopKind.Scout: return Loc.T("SCOUT");
                case TroopKind.Shieldbearer: return Loc.T("SHIELD");
                case TroopKind.Priest: return Loc.T("PRIEST");
                default: return Loc.T("ENGINEER");
            }
        }

        /// <summary>The six posts of the line.</summary>
        public static string Post(FormationSlot slot)
        {
            switch (slot)
            {
                case FormationSlot.Van: return Loc.T("VAN");
                case FormationSlot.RightVan: return Loc.T("RIGHT FRONT");
                case FormationSlot.LeftVan: return Loc.T("LEFT FRONT");
                case FormationSlot.RightRear: return Loc.T("RIGHT REAR");
                case FormationSlot.LeftRear: return Loc.T("LEFT REAR");
                default: return Loc.T("REARGUARD");
            }
        }

        /// <summary>The ground underfoot, lower case, for the run's distance line.</summary>
        public static string Ground(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Road: return Loc.T("road");
                case TerrainType.Plains: return Loc.T("plains");
                case TerrainType.Forest: return Loc.T("forest");
                case TerrainType.Marsh: return Loc.T("marsh");
                case TerrainType.Ford: return Loc.T("ford");
                case TerrainType.MountainPass: return Loc.T("mountain pass");
                case TerrainType.Water: return Loc.T("water");
                case TerrainType.Cliff: return Loc.T("cliff");
                default: return terrain.ToString();
            }
        }
    }
}
