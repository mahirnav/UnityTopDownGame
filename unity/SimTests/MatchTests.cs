using NUnit.Framework;
using Rats.Sim;
using static Rats.SimTests.TestSim;

namespace Rats.SimTests
{
    /// <summary>Best-of-5 match flow: round breaks, resets, scoring, draws, match end.</summary>
    public class MatchTests
    {
        static void KnockOut(Simulation sim, int loser)
        {
            sim.Fighters[loser].Hp = 1;
            var fire = new ScriptedInput();
            fire.Set(sim.Tick, new InputFrame { Ranged = true });
            if (loser == 1) RunUntil(sim, s => s.Phase != Phase.Combat && s.Phase != Phase.Freeze, 600, in0: fire);
            else RunUntil(sim, s => s.Phase != Phase.Combat && s.Phase != Phase.Freeze, 600, in1: fire);
        }

        [Test]
        public void KnockoutEndsTheRoundThenABreakFreezesGameplay()
        {
            var sim = Create();
            KnockOut(sim, 1);
            Assert.That(sim.Phase, Is.EqualTo(Phase.RoundOver));
            Assert.That(sim.Winner, Is.EqualTo(0));
            Assert.That(sim.Score(0), Is.EqualTo(1));

            var hp = sim.Fighters[0].Hp;
            var pos = sim.Fighters[0].Position;
            Run(sim, sim.T.RoundBreakTicks - 1, HoldFrom(0, 100000, new InputFrame { MoveX = 1, Dash = true }));
            Assert.That(sim.Phase, Is.EqualTo(Phase.RoundOver), "still in the break");
            Assert.That(sim.Fighters[0].Position, Is.EqualTo(pos), "no movement during the break");
            Assert.That(sim.Fighters[0].Hp, Is.EqualTo(hp));
        }

        [Test]
        public void NextRoundResetsFightersUpgradesAndDrafts()
        {
            var sim = Create(t => t.DraftsPerRound = 3);
            sim.Fighters[0].Upgrades = 0b101;
            sim.Fighters[0].MaxHp = 125;
            KnockOut(sim, 1);
            RunUntil(sim, s => s.Phase == Phase.Combat);

            Assert.That(sim.Round, Is.EqualTo(2));
            Assert.That(sim.Turn, Is.EqualTo(1));
            Assert.That(sim.DraftsThisRound, Is.Zero);
            Assert.That(sim.Winner, Is.EqualTo(Simulation.NoWinner));
            Assert.That(sim.Projectiles, Is.Empty);
            foreach (var f in sim.Fighters)
            {
                Assert.That(f.Upgrades, Is.Zero, "upgrades reset each round");
                Assert.That(f.MaxHp, Is.EqualTo(sim.T.MaxHp));
                Assert.That(f.Hp, Is.EqualTo(sim.T.MaxHp));
                Assert.That(f.ActionPoints, Is.EqualTo(sim.T.ActionPointsPerTurn));
                Assert.That(f.Position.X, Is.EqualTo((f.Index == 0 ? -1 : 1) * sim.T.StartOffsetX));
            }
            Assert.That(sim.Score(0), Is.EqualTo(1), "score carries across rounds");
        }

        [Test]
        public void FirstToThreeRoundWinsTakesTheMatch()
        {
            var sim = Create();
            int[] losers = { 1, 0, 1, 1 };
            foreach (var loser in losers)
            {
                KnockOut(sim, loser);
                if (sim.Phase == Phase.MatchOver) break;
                RunUntil(sim, s => s.Phase == Phase.Combat);
            }
            Assert.That(sim.Phase, Is.EqualTo(Phase.MatchOver));
            Assert.That(sim.MatchWinner, Is.EqualTo(0));
            Assert.That(sim.Score(0), Is.EqualTo(3));
            Assert.That(sim.Score(1), Is.EqualTo(1));

            int tick = sim.Tick;
            Run(sim, 60);
            Assert.That(sim.Tick, Is.EqualTo(tick), "nothing advances after the match ends");
        }

        [Test]
        public void ADrawScoresNobodyAndTheRoundIsReplayed()
        {
            var sim = Create();
            sim.Fighters[0].Hp = sim.Fighters[1].Hp = sim.T.RangedDamage;
            var fire = PressAt(0, new InputFrame { Ranged = true });
            RunUntil(sim, s => s.Phase == Phase.RoundOver, 600, fire, fire);
            Assert.That(sim.Winner, Is.EqualTo(Simulation.Draw));
            Assert.That(sim.Score(0) + sim.Score(1), Is.Zero);
            RunUntil(sim, s => s.Phase == Phase.Combat);
            Assert.That(sim.Round, Is.EqualTo(2));
        }
    }
}
