namespace Rats.Sim
{
    /// <summary>
    /// Turns real frame time into a whole number of fixed sim ticks.
    /// The sim keeps ticking through freezes (its freeze timer counts ticks); the freeze
    /// itself is a sim phase, not a clock state.
    /// </summary>
    public sealed class TickClock
    {
        // Double, not Tunables.TickDt: 1f/60 rounds up, so a 1/60 s frame would
        // fall just short of a tick and the game would stutter at 60 fps.
        const double TickSeconds = 1.0 / Tunables.TickRate;
        /// <summary>Cap on ticks run in one render frame, so a hitch can't snowball.</summary>
        public const int MaxTicksPerFrame = 5;
        double accumulator;

        public int Advance(double realDelta)
        {
            accumulator += realDelta;
            int ticks = (int)(accumulator / TickSeconds);
            accumulator -= ticks * TickSeconds;
            if (ticks > MaxTicksPerFrame)
            {
                ticks = MaxTicksPerFrame;
                accumulator = 0;
            }
            return ticks;
        }
    }
}
