using System.Collections.Generic;
using NUnit.Framework;
using Rats.Sim;
using static Rats.SimTests.TestSim;

namespace Rats.SimTests
{
    /// <summary>P1: draft timing, offers, pick rules, and each upgrade's "read at hit" vs "locked at start" rule.</summary>
    public class DraftTests
    {
        const int Turn = 60;

        static void Drafting(Tunables t)
        {
            t.TurnTicks = Turn;
            t.FreezeTicks = 30;
            t.DraftFreezeTicks = 100;
            t.DraftsPerRound = 3;
            t.DraftInterval = 1;
        }

        static void Grant(Fighter f, UpgradeId u) => f.Upgrades |= 1UL << (int)u;

        /// <summary>A seed whose first draft offers the upgrade, and the slot (1-3) it appears in.</summary>
        static (ulong seed, byte slot) SeedOffering(UpgradeId wanted)
        {
            for (ulong seed = 1; seed < 500; seed++)
            {
                var sim = Create(Drafting, seed);
                RunUntil(sim, s => s.DraftActive);
                for (int slot = 0; slot < 3; slot++)
                    if (Simulation.Offer(sim.Fighters[0], slot) == (int)wanted) return (seed, (byte)(slot + 1));
            }
            Assert.Fail($"no seed offers {wanted}");
            return default;
        }

        static ScriptedInput PickDuringFirstDraft(Simulation probe, byte slot)
        {
            var input = new ScriptedInput();
            input.Set(Turn, new InputFrame { DraftPick = slot }); // first tick of the first freeze
            return input;
        }

        // --- Drafting ---------------------------------------------------------------------

        [Test]
        public void DraftsHappenOnTheFirstThreeFreezes()
        {
            var sim = Create(Drafting);
            var drafts = new List<bool>();
            var lengths = new List<int>();
            for (int freeze = 1; freeze <= 7; freeze++)
            {
                RunUntil(sim, s => s.IsFrozen);
                drafts.Add(sim.DraftActive);
                lengths.Add(sim.PhaseTicksLeft);
                RunUntil(sim, s => !s.IsFrozen);
            }
            Assert.That(drafts, Is.EqualTo(new[] { true, true, true, false, false, false, false }));
            Assert.That(lengths, Is.EqualTo(new[] { 100, 100, 100, 30, 30, 30, 30 }));
            Assert.That(sim.DraftsThisRound, Is.EqualTo(3));
        }

        [Test]
        public void EachPlayerGetsTheirOwnThreeOffersReproducibleFromTheSeed()
        {
            var seen = new HashSet<string>();
            bool playersDiffered = false;
            for (ulong seed = 1; seed <= 10; seed++)
            {
                var a = Create(Drafting, seed);
                var b = Create(Drafting, seed);
                RunUntil(a, s => s.DraftActive);
                RunUntil(b, s => s.DraftActive);
                for (int i = 0; i < 2; i++)
                {
                    var offers = Offers(a.Fighters[i]);
                    Assert.That(Offers(b.Fighters[i]), Is.EqualTo(offers), "same seed, same offers");
                    Assert.That(offers, Is.Unique);
                    Assert.That(offers, Is.All.InRange(0, Upgrades.Pool.Length - 1));
                    seen.Add(string.Join(",", offers));
                }
                if (string.Join(",", Offers(a.Fighters[0])) != string.Join(",", Offers(a.Fighters[1]))) playersDiffered = true;
            }
            Assert.That(seen.Count, Is.GreaterThan(1), "different seeds should give different offers");
            Assert.That(playersDiffered, Is.True, "players should usually get different offers");
        }

        [Test]
        public void OffersNeverIncludeUpgradesYouAlreadyOwn()
        {
            var sim = Create(Drafting, seed: 7);
            var f = sim.Fighters[0];
            var owned = new[] { UpgradeId.HeavyBlade, UpgradeId.LongReach, UpgradeId.BigBolt, UpgradeId.FreeStep, UpgradeId.OpeningMove };
            foreach (var u in owned) Grant(f, u);
            RunUntil(sim, s => s.DraftActive);
            Assert.That(Offers(f), Is.EquivalentTo(new[] { (int)UpgradeId.Fortification, (int)UpgradeId.ExhaustionPunish, (int)UpgradeId.CarryTheFlame }),
                "only the 3 unowned upgrades remain");
        }

        [Test]
        public void PicksApplyOnlyWhenTheFreezeEndsAndNoPickGetsTheFirstOffer()
        {
            var sim = Create(Drafting);
            RunUntil(sim, s => s.DraftActive);
            int mine = Simulation.Offer(sim.Fighters[0], 1), theirs = Simulation.Offer(sim.Fighters[1], 0);
            sim.Step(new InputFrame { DraftPick = 2 }, default);
            Assert.That(sim.Fighters[0].Upgrades, Is.Zero, "picks must not apply during the freeze");
            Assert.That(sim.Fighters[0].DraftPick, Is.EqualTo(mine + 1));

            RunUntil(sim, s => !s.IsFrozen);
            Assert.That(sim.Fighters[0].Has((UpgradeId)mine), Is.True);
            Assert.That(sim.Fighters[1].Has((UpgradeId)theirs), Is.True, "no pick in time: first offer");
            Assert.That(Simulation.Offer(sim.Fighters[0], 0), Is.EqualTo(Simulation.NoOffer), "offers cleared after the draft");
        }

        [Test]
        public void FirstPickLocksIn()
        {
            var sim = Create(Drafting);
            RunUntil(sim, s => s.DraftActive);
            var f = sim.Fighters[0];
            sim.Step(new InputFrame { DraftPick = 2 }, default);
            sim.Step(new InputFrame { DraftPick = 3 }, default);
            Assert.That(f.DraftPick, Is.EqualTo(Simulation.Offer(f, 1) + 1));
        }

        static int[] Offers(Fighter f) => new[] { Simulation.Offer(f, 0), Simulation.Offer(f, 1), Simulation.Offer(f, 2) };

        [Test]
        public void BotsDraftAndBotMatchesReplayExactly()
        {
            var sim = Create(Drafting, seed: 99);
            var b0 = new RecordingInput(new SimpleBot(sim, 0, 1));
            var b1 = new RecordingInput(new SimpleBot(sim, 1, 2));
            Run(sim, 1000, b0, b1);
            Assert.That(sim.DraftsThisRound, Is.GreaterThan(0));
            Assert.That(sim.Fighters[0].Upgrades, Is.Not.Zero);
            Assert.That(sim.Fighters[1].Upgrades, Is.Not.Zero);

            var replay = Create(Drafting, seed: 99);
            Run(replay, 1000, b0.Recorded, b1.Recorded);
            Assert.That(replay.SaveSnapshot(), Is.EqualTo(sim.SaveSnapshot()));
        }

        // --- Upgrades ---------------------------------------------------------------------

        [Test]
        public void HeavyBladeDraftedDuringTheFreezeLandsOnTheFrozenSwing()
        {
            var (seed, slot) = SeedOffering(UpgradeId.HeavyBlade);
            var sim = Create(Drafting, seed);
            Place(sim, new Vec3(0, 0, 0), new Vec3(1.2, 0, 0));
            var input = PickDuringFirstDraft(sim, slot);
            input.Set(Turn - 5, new InputFrame { Melee = true }); // swing is still in startup when time freezes

            RunUntil(sim, s => s.IsFrozen, in0: input);
            Assert.That(sim.Fighters[0].Action, Is.EqualTo(ActionKind.Melee), "frozen mid-swing");
            Assert.That(sim.Fighters[1].Hp, Is.EqualTo(sim.T.MaxHp));
            RunUntil(sim, s => !s.IsFrozen, in0: input);
            RunUntil(sim, s => s.Fighters[1].Hp < s.T.MaxHp, 60, input);
            Assert.That(sim.Fighters[1].Hp, Is.EqualTo(sim.T.MaxHp - (sim.T.MeleeDamage + sim.T.HeavyBladeDamage)));
        }

        [Test]
        public void LongReachOnlyAffectsSwingsStartedAfterThePick()
        {
            var sim = Create();
            double justOutOfReach = sim.T.MeleeRange + sim.T.FighterRadius + 0.3;
            Place(sim, new Vec3(0, 0, 0), new Vec3(justOutOfReach, 0, 0));
            sim.Step(new InputFrame { Melee = true }, default);
            Grant(sim.Fighters[0], UpgradeId.LongReach);          // picked mid-swing
            Run(sim, sim.T.Melee.Total);
            Assert.That(sim.Fighters[1].Hp, Is.EqualTo(sim.T.MaxHp), "reach is locked when the swing starts");

            sim.Step(new InputFrame { Melee = true }, default);
            Run(sim, sim.T.Melee.Total);
            Assert.That(sim.Fighters[1].Hp, Is.EqualTo(sim.T.MaxHp - sim.T.MeleeDamage), "the next swing has the reach");
        }

        [Test]
        public void BigBoltOnlyAffectsBoltsFiredAfterThePick()
        {
            var sim = Create();
            sim.Fighters[0].Position = new Vec3(-9, 0, -3);       // keep bolts away from the opponent
            sim.Fighters[0].LockedOn = false;
            sim.Fighters[0].Facing = new Vec3(0, 0, 1);
            sim.Step(new InputFrame { Ranged = true }, default);
            Run(sim, sim.T.RangedStartup + 1);
            Grant(sim.Fighters[0], UpgradeId.BigBolt);
            Assert.That(sim.Projectiles[0].Radius, Is.EqualTo(sim.T.ProjectileRadius), "a bolt in flight keeps its size");

            Run(sim, sim.T.Ranged.Total);
            sim.Step(new InputFrame { Ranged = true }, default);
            Run(sim, sim.T.RangedStartup + 1);
            var newest = sim.Projectiles[sim.Projectiles.Count - 1];
            Assert.That(newest.Radius, Is.EqualTo(sim.T.ProjectileRadius * sim.T.BigBoltRadiusMultiplier));
        }

        [Test]
        public void FortificationRaisesMaxHpAndHealsPartially()
        {
            var (seed, slot) = SeedOffering(UpgradeId.Fortification);
            var sim = Create(Drafting, seed);
            sim.Fighters[0].Hp = 50;
            var input = PickDuringFirstDraft(sim, slot);
            RunUntil(sim, s => s.DraftActive, in0: input);
            RunUntil(sim, s => !s.IsFrozen, in0: input);
            var t = sim.T;
            Assert.That(sim.Fighters[0].MaxHp, Is.EqualTo(t.MaxHp + t.FortificationMaxHp));
            Assert.That(sim.Fighters[0].Hp, Is.EqualTo(50 + t.FortificationHeal));
        }

        [Test]
        public void FreeStepMakesTheFirstDashEachTurnFree()
        {
            var sim = Create(t => { t.TurnTicks = 200; t.FreezeTicks = 10; });
            Grant(sim.Fighters[0], UpgradeId.FreeStep);
            int full = sim.T.ActionPointsPerTurn;
            sim.Step(new InputFrame { Dash = true }, default);
            Assert.That(sim.Fighters[0].ActionPoints, Is.EqualTo(full), "first dash is free");
            Run(sim, sim.T.Dash.Total);
            sim.Step(new InputFrame { Dash = true }, default);
            Assert.That(sim.Fighters[0].ActionPoints, Is.EqualTo(full - 1), "second dash costs a point");

            RunUntil(sim, s => s.Turn == 2);
            sim.Step(new InputFrame { Dash = true }, default);
            Assert.That(sim.Fighters[0].ActionPoints, Is.EqualTo(full), "free again next turn");
        }

        [Test]
        public void OpeningMoveShortensOnlyTheFirstActionOfEachTurn()
        {
            var sim = Create(t => { t.TurnTicks = 200; t.FreezeTicks = 10; });
            Grant(sim.Fighters[0], UpgradeId.OpeningMove);
            int normal = sim.T.MeleeStartup, opened = normal * sim.T.OpeningMoveStartupPercent / 100;
            sim.Step(new InputFrame { Melee = true }, default);
            Assert.That(sim.Fighters[0].Frames.Startup, Is.EqualTo(opened));
            RunUntil(sim, s => !s.Fighters[0].IsBusy);
            sim.Step(new InputFrame { Melee = true }, default);
            Assert.That(sim.Fighters[0].Frames.Startup, Is.EqualTo(normal));

            RunUntil(sim, s => s.Turn == 2);
            sim.Step(new InputFrame { Melee = true }, default);
            Assert.That(sim.Fighters[0].Frames.Startup, Is.EqualTo(opened), "first action of the new turn");
        }

        [Test]
        public void ExhaustionPunishHitsHarderAgainstATargetWithNoPoints()
        {
            var sim = Create();
            var me = sim.Fighters[0];
            var them = sim.Fighters[1];
            Grant(me, UpgradeId.ExhaustionPunish);
            int baseline = sim.T.MeleeDamage;
            Assert.That(sim.HitDamage(me, ActionKind.Melee, them), Is.EqualTo(baseline), "target still has points");
            them.ActionPoints = 0;
            Assert.That(sim.HitDamage(me, ActionKind.Melee, them), Is.EqualTo(baseline * (100 + sim.T.ExhaustionPunishPercent) / 100));
        }

        [Test]
        public void CarryTheFlameTurnsUnspentPointsIntoNextTurnDamage()
        {
            var sim = Create(t => { t.TurnTicks = 100; t.FreezeTicks = 10; });
            var me = sim.Fighters[0];
            Grant(me, UpgradeId.CarryTheFlame);
            sim.Step(new InputFrame { Dash = true }, default);     // spend 1 of 3
            RunUntil(sim, s => s.Turn == 2);
            Assert.That(me.CarriedPoints, Is.EqualTo(sim.T.ActionPointsPerTurn - 1));
            Assert.That(sim.HitDamage(me, ActionKind.Ranged, sim.Fighters[1]),
                Is.EqualTo(sim.T.RangedDamage + 2 * sim.T.CarryTheFlameDamagePerPoint));

            var mash = HoldFrom(sim.Tick, sim.Tick + 100, new InputFrame { Dash = true });
            RunUntil(sim, s => s.Turn == 3, in0: mash);             // spend everything
            Assert.That(me.CarriedPoints, Is.Zero);
            Assert.That(sim.Fighters[1].CarriedPoints, Is.Zero, "no upgrade, no carry");
        }
    }
}
