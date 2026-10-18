using NUnit.Framework;
using Rats.Sim;
using static Rats.SimTests.TestSim;

namespace Rats.SimTests
{
    /// <summary>P2: fire ring, downed state, bleed-out, free executions, the round cap.</summary>
    public class EndgameTests
    {
        static void KnockDown(Simulation sim, int victim)
        {
            sim.Fighters[victim].Hp = 0;          // the next tick's down check downs them
            sim.Step(default, default);
        }

        [Test]
        public void TheRingStartsLateAndClosesAtTheCap()
        {
            var sim = Create(t => { t.FireStartTicks = 100; t.RoundMaxTicks = 300; t.TurnTicks = 10000; });
            Run(sim, 99);
            Assert.That(sim.FireScale, Is.EqualTo(1), "no fire before FireStartTicks");
            Run(sim, 101);
            Assert.That(sim.FireScale, Is.EqualTo(0.5).Within(1e-9), "halfway between start and cap");
        }

        [Test]
        public void FireDamageRampsWithExposureAndResetsWhenYouStepOut()
        {
            var sim = Create(t => { t.FireStartTicks = 0; t.RoundMaxTicks = 100000; t.TurnTicks = 10000; t.FireRampTicks = 60; });
            var f = sim.Fighters[0];
            sim.Fighters[1].Position = new Vec3(0, 0, 0);
            sim.Fighters[1].LockedOn = f.LockedOn = false;
            RunUntil(sim, s => s.FireScale < 0.9);
            f.Position = new Vec3(13.2, 0, 0);    // near the wall: inside the fire now
            int firstPulse = -1, laterPulse = -1;
            for (int i = 0; i < 200; i++)
            {
                int before = f.Hp;
                sim.Step(default, default);
                int taken = before - f.Hp;
                if (taken > 0 && firstPulse < 0) firstPulse = taken;
                if (taken > 0 && f.FireExposureTicks > 120) { laterPulse = taken; break; }
            }
            Assert.That(firstPulse, Is.EqualTo(sim.T.FireBaseDamage));
            Assert.That(laterPulse, Is.GreaterThanOrEqualTo(sim.T.FireBaseDamage + 2), "damage ramps after 2 s in the fire");

            f.Position = new Vec3(0, 0, 1);
            sim.Step(default, default);
            Assert.That(f.FireExposureTicks, Is.Zero, "stepping out resets the ramp");
        }

        [Test]
        public void FireDoesNotBurnDuringAFreeze()
        {
            var sim = Create(t => { t.FireStartTicks = 0; t.RoundMaxTicks = 100000; t.TurnTicks = 60; t.FreezeTicks = 200; });
            sim.Fighters[0].Position = new Vec3(13.2, 0, 0);
            RunUntil(sim, s => s.IsFrozen);
            int hp = sim.Fighters[0].Hp;
            Run(sim, 150);
            Assert.That(sim.IsFrozen, Is.True);
            Assert.That(sim.Fighters[0].Hp, Is.EqualTo(hp));
        }

        [Test]
        public void ZeroHpDownsInsteadOfEndingTheRoundThenBleedsOut()
        {
            var sim = Create(t => t.TurnTicks = 10000);
            KnockDown(sim, 1);
            var them = sim.Fighters[1];
            Assert.That(them.Downed, Is.True);
            Assert.That(sim.Phase, Is.EqualTo(Phase.Combat), "being downed doesn't end the round by itself");

            var pos = them.Position;
            Run(sim, 10, in1: HoldFrom(0, 100000, new InputFrame { MoveX = 1, Melee = true, Dash = true }));
            Assert.That(them.Position, Is.EqualTo(pos), "downed fighters can't move");
            Assert.That(sim.ActionLog.Exists(e => e.Fighter == 1), Is.False, "or act");

            RunUntil(sim, s => s.Phase != Phase.Combat, sim.T.DownedBleedTicks + 5);
            Assert.That(sim.Phase, Is.EqualTo(Phase.RoundOver));
            Assert.That(sim.Winner, Is.EqualTo(0), "bled out");
        }

        [Test]
        public void DownedInTheFireBleedsOutFaster()
        {
            var sim = Create(t => { t.FireStartTicks = 0; t.RoundMaxTicks = 100000; t.TurnTicks = 10000; });
            RunUntil(sim, s => s.FireScale < 0.9);
            sim.Fighters[1].Position = new Vec3(13.2, 0, 0);
            KnockDown(sim, 1);
            int start = sim.Tick;
            RunUntil(sim, s => s.Phase != Phase.Combat, 1000);
            Assert.That(sim.Tick - start, Is.LessThanOrEqualTo(sim.T.FireBleedTicks + 1));
        }

        [Test]
        public void ExecutionIsFreeTakesItsTimeAndEndsTheRound()
        {
            var sim = Create(t => t.TurnTicks = 10000);
            Place(sim, new Vec3(0, 0, 0), new Vec3(1.2, 0, 0));
            KnockDown(sim, 1);
            sim.Fighters[0].ActionPoints = 0;      // free: works with no points
            sim.Step(new InputFrame { Melee = true }, default);
            Assert.That(sim.Fighters[0].Action, Is.EqualTo(ActionKind.Execute));
            Assert.That(sim.Fighters[0].ActionPoints, Is.Zero);

            Run(sim, sim.T.ExecuteTicks - 1);
            Assert.That(sim.Phase, Is.EqualTo(Phase.Combat), "not finished early");
            Run(sim, 1);
            Assert.That(sim.Phase, Is.EqualTo(Phase.RoundOver));
            Assert.That(sim.Winner, Is.EqualTo(0));
        }

        [Test]
        public void AStartedExecutionCompletesEvenIfTheExecutionerGoesDown()
        {
            var sim = Create(t => t.TurnTicks = 10000);
            Place(sim, new Vec3(0, 0, 0), new Vec3(1.2, 0, 0));
            KnockDown(sim, 1);
            sim.Step(new InputFrame { Melee = true }, default);
            Run(sim, 10);
            sim.Fighters[0].Hp = 0;                // executioner goes down mid-execution
            RunUntil(sim, s => s.Phase != Phase.Combat, sim.T.ExecuteTicks + 5);
            Assert.That(sim.Fighters[0].Downed, Is.True);
            Assert.That(sim.Winner, Is.EqualTo(0), "the execution still lands");
        }

        [Test]
        public void DownedFightersTakeNoFurtherHits()
        {
            var sim = Create(t => t.TurnTicks = 10000);
            Place(sim, new Vec3(0, 0, 0), new Vec3(4, 0, 0));
            KnockDown(sim, 1);
            int lastHit = sim.Fighters[1].LastHitTick;
            Run(sim, 60, PressAt(sim.Tick, new InputFrame { Ranged = true }));
            Assert.That(sim.Fighters[1].LastHitTick, Is.EqualTo(lastHit), "bolts fly over a downed fighter");
            Assert.That(sim.Fighters[1].Hp, Is.Zero);
        }

        [Test]
        public void AtTheCapHigherHpPercentWins()
        {
            var sim = Create(t => { t.RoundMaxTicks = 200; t.FireStartTicks = 100000; t.TurnTicks = 10000; });
            sim.Fighters[0].Hp = 50;                                   // 50%
            sim.Fighters[1].MaxHp = 125; sim.Fighters[1].Hp = 60;      // 48%
            RunUntil(sim, s => s.Phase != Phase.Combat, 300);
            Assert.That(sim.Phase, Is.EqualTo(Phase.RoundOver));
            Assert.That(sim.Winner, Is.EqualTo(0));

            var tie = Create(t => { t.RoundMaxTicks = 200; t.FireStartTicks = 100000; t.TurnTicks = 10000; });
            RunUntil(tie, s => s.Phase != Phase.Combat, 300);
            Assert.That(tie.Winner, Is.EqualTo(Simulation.Draw), "equal HP% at the cap is a draw");
        }

        [Test]
        public void FinalStageTurnsAreShorterOnceTheRingCloses()
        {
            var sim = Create(t => { t.TurnTicks = 100; t.FreezeTicks = 10; t.FireStartTicks = 150; t.RoundMaxTicks = 100000; t.FinalStageTurnTicks = 40; });
            RunUntil(sim, s => s.Turn == 2);
            Assert.That(sim.PhaseTicksLeft, Is.EqualTo(100), "before the ring: normal turns");
            RunUntil(sim, s => s.Turn == 3);
            Assert.That(sim.PhaseTicksLeft, Is.EqualTo(40), "ring closing: shorter turns");
        }
    }
}
