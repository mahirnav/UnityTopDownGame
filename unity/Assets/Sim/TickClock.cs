namespace Rats.Sim
{
    /// <summary>
    /// Turns real frame time into a whole number of fixed sim ticks.
    /// While frozen, elapsed time is thrown away so resuming never fast-forwards.
    /// </summary>
    public sealed class TickClock
    {
        // Double, not Tunables.TickDt: 1f/60 rounds up, so a 1/60 s frame would
        // fall just short of a tick and the game would stutter at 60 fps.
        const double TickSeconds = 1.0 / Tunables.TickRate;
        double accumulator;

        public int Advance(double realDelta, bool frozen)
        {
            if (frozen)
            {
                accumulator = 0;
                return 0;
            }
            accumulator += realDelta;
            int ticks = (int)(accumulator / TickSeconds);
            accumulator -= ticks * TickSeconds;
            if (ticks > Tunables.MaxTicksPerFrame)
            {
                ticks = Tunables.MaxTicksPerFrame;
                accumulator = 0;
            }
            return ticks;
        }
    }
}
