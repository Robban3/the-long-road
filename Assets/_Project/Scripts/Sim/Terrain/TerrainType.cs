namespace Arna.Sim
{
    /// <summary>
    /// Terrain types as defined in docs/GDD.md §3.2. The numeric values are
    /// persisted in save data and analytics, so entries may be appended but never
    /// reordered or renumbered.
    /// </summary>
    public enum TerrainType : byte
    {
        Road = 0,
        Plains = 1,
        Forest = 2,
        Marsh = 3,
        Ford = 4,
        MountainPass = 5,
        Water = 6,
        Cliff = 7
    }
}
