using NUnit.Framework;
using Rats.Sim;
using static Rats.SimTests.TestSim;

namespace Rats.SimTests
{
    /// <summary>
    /// Guards that keep host-authoritative multiplayer cheap later. If one fails, some gameplay
    /// state escaped the snapshot or something other than InputFrames leaked into the sim.
    /// </summary>
    public class MultiplayerReadinessTests
    {
        static Simulation Fast() => Create(t => { t.TurnTicks = 240; t.FreezeTicks = 60; });

        [Test]
        public void SnapshotRoundTripKeepsSimsIdentical()
        {
            var sim = Fast();
            var bot0 = new RecordingInput(new SimpleBot(sim, 0, 11));
            var bot1 = new RecordingInput(new SimpleBot(sim, 1, 22));
            Run(sim, 700, bot0, bot1); // mid-match: projectiles, actions in progress, a freeze or two

            var copy = Simulation.LoadSnapshot(sim.SaveSnapshot(), sim.T);
            Assert.That(copy.SaveSnapshot(), Is.EqualTo(sim.SaveSnapshot()), "load then save changed the bytes");

            // Continue both with the same inputs (the original's recorded bot presses).
            var future0 = new RecordingInput(new SimpleBot(sim, 0, 33));
            var future1 = new RecordingInput(new SimpleBot(sim, 1, 44));
            for (int i = 0; i < 1500 && sim.Phase != Phase.RoundOver; i++)
            {
                int tick = sim.Tick;
                var a = future0.Read(tick);
                var b = future1.Read(tick);
                sim.Step(a, b);
                copy.Step(a, b);
                Assert.That(copy.SaveSnapshot(), Is.EqualTo(sim.SaveSnapshot()), $"diverged at tick {tick}");
            }
        }

        /// <summary>
        /// Sets every mutable field of the sim, fighters and projectiles (found by reflection, so new
        /// fields are covered automatically) to an unusual value, then round-trips through a snapshot.
        /// Catches state that was added to the sim but forgotten in SaveSnapshot/LoadSnapshot.
        /// </summary>
        [Test]
        public void SnapshotCapturesEveryStateField()
        {
            var sim = Create();
            sim.Projectiles.Add(new Projectile());
            int seed = 1;
            Fill(sim, ref seed);
            foreach (var f in sim.Fighters) Fill(f, ref seed);
            Fill(sim.Projectiles[0], ref seed);

            var copy = Simulation.LoadSnapshot(sim.SaveSnapshot(), sim.T);
            Compare(sim, copy, "Simulation");
            for (int i = 0; i < 2; i++) Compare(sim.Fighters[i], copy.Fighters[i], $"Fighters[{i}]");
            Assert.That(copy.Projectiles.Count, Is.EqualTo(1));
            Compare(sim.Projectiles[0], copy.Projectiles[0], "Projectile");
        }

        static System.Collections.Generic.IEnumerable<System.Reflection.FieldInfo> StateFields(object o) =>
            System.Linq.Enumerable.Where(
                o.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic),
                f => !f.IsInitOnly);

        static void Fill(object o, ref int seed)
        {
            foreach (var f in StateFields(o))
            {
                seed++;
                var t = f.FieldType;
                object v = t == typeof(int) ? seed + 3
                         : t == typeof(double) ? seed * 1.25
                         : t == typeof(bool) ? true
                         : t == typeof(Vec3) ? new Vec3(seed, seed + 0.5f, -seed)
                         : t.IsEnum ? System.Enum.GetValues(t).GetValue(1)
                         : throw new AssertionException($"{o.GetType().Name}.{f.Name}: add {t.Name} to this test's Fill()");
                f.SetValue(o, v);
            }
        }

        static void Compare(object expected, object actual, string label)
        {
            foreach (var f in StateFields(expected))
                Assert.That(f.GetValue(actual), Is.EqualTo(f.GetValue(expected)), $"{label}.{f.Name} not carried by the snapshot");
        }

        [Test]
        public void ReplayingRecordedInputsReproducesTheMatch()
        {
            var live = Fast();
            var p0 = new RecordingInput(new SimpleBot(live, 0, 5));
            var p1 = new RecordingInput(new SimpleBot(live, 1, 6));
            Run(live, 3000, p0, p1);
            Assert.That(live.ActionLog.Count, Is.GreaterThan(10), "bots should actually fight");

            var replay = Fast();
            Run(replay, 3000, p0.Recorded, p1.Recorded);
            Assert.That(replay.SaveSnapshot(), Is.EqualTo(live.SaveSnapshot()), "replay diverged from the live match");
        }

        [Test]
        public void InputFrameRoundTripsThroughBytes()
        {
            var frame = new InputFrame { MoveX = -0.25f, MoveZ = 0.75f, Melee = true, ToggleLock = true };
            using var ms = new System.IO.MemoryStream();
            using (var w = new System.IO.BinaryWriter(ms, System.Text.Encoding.UTF8, true)) frame.Write(w);
            Assert.That(ms.Length, Is.EqualTo(9), "keep the per-tick input tiny");
            ms.Position = 0;
            var back = InputFrame.Read(new System.IO.BinaryReader(ms));
            Assert.That(back, Is.EqualTo(frame));
        }
    }
}
