using NUnit.Framework;
using Rats.Sim;
using static Rats.SimTests.TestSim;

namespace Rats.SimTests
{
    /// <summary>Action points and the turn cycle.</summary>
    public class TurnTests
    {
        [Test]
        public void ActionPointConsumedOnTheTickThePressIsAccepted()
        {
            var sim = Create();
            sim.Step(new InputFrame { Ranged = true }, default);
            Assert.That(sim.Fighters[0].ActionPoints, Is.EqualTo(sim.T.ActionPointsPerTurn - sim.T.RangedCost));
            Assert.That(sim.Fighters[0].Action, Is.EqualTo(ActionKind.Ranged));
        }

        [Test]
        public void PressWhileBusyIsIgnoredAndCostsNothing()
        {
            var sim = Create();
            sim.Step(new InputFrame { Melee = true }, default);
            int points = sim.Fighters[0].ActionPoints;
            for (int i = 0; i < 5; i++) sim.Step(new InputFrame { Melee = true, Ranged = true, Dash = true }, default);
            Assert.That(sim.Fighters[0].ActionPoints, Is.EqualTo(points));
            Assert.That(sim.ActionLog.Count, Is.EqualTo(1));
        }

        [Test]
        public void NoActionsAfterPointsRunOut()
        {
            var sim = Create();
            var mash = HoldFrom(0, sim.T.TurnTicks, new InputFrame { Dash = true });
            Run(sim, sim.T.TurnTicks - 1, mash);
            Assert.That(sim.ActionLog.Count, Is.EqualTo(sim.T.ActionPointsPerTurn), "one action per point, then nothing");
            Assert.That(sim.Fighters[0].ActionPoints, Is.Zero);
        }

        [Test]
        public void UnspentPointsExpireAtTurnEndAndResetNextTurn()
        {
            var sim = Create();
            sim.Step(new InputFrame { Dash = true }, default); // spend one, keep two
            RunUntil(sim, s => s.IsFrozen);
            Assert.That(sim.Fighters[0].ActionPoints, Is.Zero, "unspent points should expire at the freeze");
            Assert.That(sim.Fighters[1].ActionPoints, Is.Zero);

            RunUntil(sim, s => !s.IsFrozen);
            Assert.That(sim.Turn, Is.EqualTo(2));
            Assert.That(sim.Fighters[0].ActionPoints, Is.EqualTo(sim.T.ActionPointsPerTurn));
            Assert.That(sim.Fighters[1].ActionPoints, Is.EqualTo(sim.T.ActionPointsPerTurn));
        }

        [Test]
        public void TurnAndFreezeLengthsComeFromTunables()
        {
            var sim = Create(t => { t.TurnTicks = 100; t.FreezeTicks = 30; });
            RunUntil(sim, s => s.IsFrozen);
            Assert.That(sim.Tick, Is.EqualTo(100));
            RunUntil(sim, s => !s.IsFrozen);
            Assert.That(sim.Tick, Is.EqualTo(130));
            Assert.That(sim.CombatTick, Is.EqualTo(100), "combat time does not run during the freeze");
        }

        /// <summary>
        /// Known risk from the design doc, measured not prevented: spend 3 actions right before the
        /// freeze and 3 right after. Frame data spaces actions by their duration, so the burst is
        /// 6 actions in 5 action-lengths of combat time across the boundary, versus 3 per turn normally.
        /// </summary>
        [Test]
        public void TurnBoundaryDoubleBurstIsMeasurable()
        {
            var sim = Create();
            var t = sim.T;
            int dashTotal = t.Dash.Total;
            int firstPress = t.TurnTicks - 3 * dashTotal; // the third dash starts just before the turn ends
            var mash = HoldFrom(firstPress, 100000, new InputFrame { Dash = true, MoveZ = 1f });
            RunUntil(sim, s => s.ActionLog.Count >= 6, in0: mash);

            var log = sim.ActionLog;
            Assert.That(log[2].Turn, Is.EqualTo(1));
            Assert.That(log[3].Turn, Is.EqualTo(2), "actions 4-6 should come from the fresh turn");
            int window = log[5].CombatTick - log[0].CombatTick;
            TestContext.WriteLine($"Double burst: 6 actions in {window} combat ticks ({window / 60f:F2} s) " +
                                  $"across the turn boundary; normal turn allows 3 in {t.TurnTicks}.");
            Assert.That(window, Is.EqualTo(5 * dashTotal));
        }
    }
}
