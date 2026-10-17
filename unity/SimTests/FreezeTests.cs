using NUnit.Framework;
using Rats.Sim;

namespace Rats.SimTests
{
    public class FreezeTests
    {
        static ScriptedInput FireOnFirstTick()
        {
            var input = new ScriptedInput();
            input.Set(0, new InputFrame { Fire = true });
            return input;
        }

        static void Run(Simulation sim, IInputSource input, int ticks)
        {
            for (int i = 0; i < ticks; i++) sim.Step(input.Read(sim.Tick));
        }

        [Test]
        public void ProjectileResumesExactlyAfterFreeze()
        {
            // Reference: fire, then run uninterrupted.
            var reference = new Simulation();
            Run(reference, FireOnFirstTick(), 61);

            // Same inputs, but frozen after 31 ticks for 5 s of render frames.
            var sim = new Simulation();
            var input = FireOnFirstTick();
            var clock = new TickClock();
            Run(sim, input, 31);
            var p = sim.Projectiles[0];
            var frozenPos = p.Position;
            var frozenVel = p.Velocity;

            sim.Freeze();
            for (int frame = 0; frame < 300; frame++)
            {
                int ticks = clock.Advance(1.0 / 60.0, sim.IsFrozen);
                for (int t = 0; t < ticks; t++) sim.Step(input.Read(sim.Tick));
                sim.Step(input.Read(sim.Tick)); // a driver that ignores the clock must not matter either
            }
            Assert.That(p.Position, Is.EqualTo(frozenPos), "projectile moved during freeze");
            Assert.That(p.Velocity, Is.EqualTo(frozenVel), "projectile velocity changed during freeze");

            sim.Resume();
            Run(sim, input, 30);

            var refP = reference.Projectiles[0];
            Assert.That(sim.Tick, Is.EqualTo(reference.Tick));
            Assert.That(p.Position, Is.EqualTo(refP.Position), "position not bit-exact with uninterrupted run");
            Assert.That(p.Velocity, Is.EqualTo(refP.Velocity), "velocity not bit-exact with uninterrupted run");
        }

        [Test]
        public void FrozenSimIgnoresSteps()
        {
            var sim = new Simulation();
            Run(sim, FireOnFirstTick(), 10);
            sim.Freeze();
            var playerPos = sim.Player.Position;
            var mash = new InputFrame { Fire = true, Melee = true, MoveX = 1f };
            for (int i = 0; i < 100; i++) sim.Step(mash);

            Assert.That(sim.Tick, Is.EqualTo(10), "tick advanced while frozen");
            Assert.That(sim.Projectiles.Count, Is.EqualTo(1), "fired while frozen");
            Assert.That(sim.Player.IsSwinging, Is.False, "swing started while frozen");
            Assert.That(sim.Player.Position, Is.EqualTo(playerPos), "player moved while frozen");
        }

        [Test]
        public void ClockDiscardsTimeWhileFrozen()
        {
            var clock = new TickClock();
            for (int frame = 0; frame < 300; frame++)
                Assert.That(clock.Advance(1.0 / 60.0, true), Is.Zero);

            Assert.That(clock.Advance(1.0 / 60.0, false), Is.EqualTo(1), "resume fast-forwarded");
            Assert.That(clock.Advance(2.0, false), Is.EqualTo(Tunables.MaxTicksPerFrame), "hitch not capped");
        }

        [Test]
        public void ClockRunsOneTickPerFrameAt60Fps()
        {
            // Regression: float TickDt (1f/60 rounds up) made 1/60 s frames yield 0 ticks.
            var clock = new TickClock();
            for (int frame = 0; frame < 600; frame++)
                Assert.That(clock.Advance(1.0 / 60.0, false), Is.EqualTo(1), $"frame {frame}");
        }

        [Test]
        public void SwingHoldsMidSwingAndFinishesOnTime()
        {
            var sim = new Simulation();
            var input = new ScriptedInput();
            input.Set(0, new InputFrame { Melee = true });
            Run(sim, input, 15);
            float progress = sim.Player.SwingProgress;
            Assert.That(sim.Player.IsSwinging && progress > 0f && progress < 1f, $"expected mid-swing, got {progress}");

            sim.Freeze();
            for (int i = 0; i < 200; i++) sim.Step(input.Read(sim.Tick));
            Assert.That(sim.Player.SwingProgress, Is.EqualTo(progress), "swing progressed during freeze");
            sim.Resume();

            // The swing ends exactly SwingTotalTicks after it started, freeze or not.
            Run(sim, input, Tunables.SwingTotalTicks - 15 - 1);
            Assert.That(sim.Player.IsSwinging, Is.True, "swing ended a tick early");
            Run(sim, input, 1);
            Assert.That(sim.Player.IsSwinging, Is.False, "swing did not end on time");
        }
    }
}
