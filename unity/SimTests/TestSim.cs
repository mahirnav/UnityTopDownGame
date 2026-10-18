using Rats.Sim;

namespace Rats.SimTests
{
    /// <summary>Small helpers so tests read as scenarios.</summary>
    static class TestSim
    {
        public static readonly IInputSource Idle = new ScriptedInput();

        public static Simulation Create(System.Action<Tunables> tweak = null, ulong seed = 1)
        {
            // Legacy scenario defaults: no drafting, instant turning, no startup tracking, no backstabs.
            // Tests that cover those features turn them on.
            var t = new Tunables { DraftsPerRound = 0, IdleTurnRateDeg = 0, StartupTurnRateDeg = 0, BackstabPercent = 0 };
            tweak?.Invoke(t);
            return new Simulation(t, seed);
        }

        /// <summary>Places fighters directly, facing each other, for close-range scenarios.</summary>
        public static void Place(Simulation sim, Vec3 p0, Vec3 p1)
        {
            sim.Fighters[0].Position = p0;
            sim.Fighters[1].Position = p1;
            sim.Fighters[0].Facing = (p1 - p0).Flat.Normalized;
            sim.Fighters[1].Facing = (p0 - p1).Flat.Normalized;
        }

        public static void Run(Simulation sim, int ticks, IInputSource in0 = null, IInputSource in1 = null)
        {
            in0 ??= Idle;
            in1 ??= Idle;
            for (int i = 0; i < ticks; i++) sim.Step(in0.Read(sim.Tick), in1.Read(sim.Tick));
        }

        public static void RunUntil(Simulation sim, System.Func<Simulation, bool> done, int maxTicks = 100000,
                                    IInputSource in0 = null, IInputSource in1 = null)
        {
            in0 ??= Idle;
            in1 ??= Idle;
            for (int i = 0; i < maxTicks && !done(sim); i++) sim.Step(in0.Read(sim.Tick), in1.Read(sim.Tick));
        }

        /// <summary>One press on the given tick.</summary>
        public static ScriptedInput PressAt(int tick, InputFrame frame)
        {
            var s = new ScriptedInput();
            s.Set(tick, frame);
            return s;
        }

        /// <summary>The same press on every tick from start (inclusive): "mash until accepted".</summary>
        public static ScriptedInput HoldFrom(int start, int end, InputFrame frame)
        {
            var s = new ScriptedInput();
            for (int t = start; t < end; t++) s.Set(t, frame);
            return s;
        }
    }
}
