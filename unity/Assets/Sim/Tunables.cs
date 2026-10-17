namespace Rats.Sim
{
    /// <summary>Spike tunables. P0 moves these into a data file Nick can edit.</summary>
    public static class Tunables
    {
        public const int TickRate = 60;
        public const float TickDt = 1f / TickRate;
        /// <summary>Cap on ticks run in one render frame, so a hitch can't snowball.</summary>
        public const int MaxTicksPerFrame = 5;

        public const float MoveSpeed = 5f;               // m/s
        public const float ProjectileSpeed = 12f;        // m/s
        public const int ProjectileLifetimeTicks = 180;
        public const float ProjectileSpawnHeight = 1f;   // m above the floor
        public const float ProjectileSpawnForward = 0.6f; // m in front of the fighter

        public const int SwingStartupTicks = 10;
        public const int SwingActiveTicks = 6;
        public const int SwingRecoveryTicks = 20;
        public const int SwingTotalTicks = SwingStartupTicks + SwingActiveTicks + SwingRecoveryTicks;
    }
}
