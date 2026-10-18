using NUnit.Framework;
using Rats.Sim;
using static Rats.SimTests.TestSim;

namespace Rats.SimTests
{
    /// <summary>The turn-end freeze stops gameplay exactly mid-action; resuming finishes it as if nothing happened.</summary>
    public class FreezeTests
    {
        const int Turn = 60; // short turns keep scenarios small

        static Simulation WithFreeze(int freezeTicks) => Create(t => { t.TurnTicks = Turn; t.FreezeTicks = freezeTicks; });

        [Test]
        public void ProjectileResumesExactlyAfterFreeze()
        {
            // Fire so the projectile is mid-flight when the turn ends. Reference has no freeze at all.
            var fire = PressAt(40, new InputFrame { Ranged = true });
            var reference = WithFreeze(0);
            var sim = WithFreeze(180);

            RunUntil(sim, s => s.IsFrozen, in0: fire);
            Assert.That(sim.Projectiles.Count, Is.EqualTo(1), "projectile should be in flight at the freeze");
            var p = sim.Projectiles[0];
            var frozenPos = p.Position;
            var frozenVel = p.Velocity;

            RunUntil(sim, s => !s.IsFrozen, in0: fire);
            Assert.That(p.Position, Is.EqualTo(frozenPos), "projectile moved during the freeze");
            Assert.That(p.Velocity, Is.EqualTo(frozenVel), "projectile speed changed during the freeze");

            int combatTicks = sim.CombatTick + 10;
            RunUntil(sim, s => s.CombatTick == combatTicks, in0: fire);
            RunUntil(reference, s => s.CombatTick == combatTicks, in0: fire);
            var refP = reference.Projectiles[0];
            Assert.That(p.Position, Is.EqualTo(refP.Position), "position not bit-exact with the unfrozen run");
            Assert.That(p.Velocity, Is.EqualTo(refP.Velocity), "velocity not bit-exact with the unfrozen run");
        }

        [Test]
        public void FrozenDashFinishesWithCorrectDistance()
        {
            var t = new Tunables();
            // Start the dash so the turn ends in the middle of its active ticks.
            int start = Turn - t.DashStartup - t.DashActive / 2;
            var dash = PressAt(start, new InputFrame { Dash = true, MoveZ = 1f });

            var sim = WithFreeze(180);
            var origin = sim.Fighters[0].Position;
            RunUntil(sim, s => s.IsFrozen, in0: dash);
            var mid = sim.Fighters[0].Position;
            Assert.That(sim.Fighters[0].Action, Is.EqualTo(ActionKind.Dash), "freeze should land mid-dash");
            Assert.That(mid.Z - origin.Z, Is.GreaterThan(0).And.LessThan(t.DashSpeed * t.DashActive / 60.0));

            RunUntil(sim, s => !s.IsFrozen, in0: dash);
            Assert.That(sim.Fighters[0].Position, Is.EqualTo(mid), "fighter moved during the freeze");
            RunUntil(sim, s => s.Fighters[0].Action == ActionKind.None, in0: dash);

            var reference = WithFreeze(0);
            RunUntil(reference, s => s.CombatTick == sim.CombatTick, in0: dash);
            Assert.That(sim.Fighters[0].Position, Is.EqualTo(reference.Fighters[0].Position), "not bit-exact with the unfrozen dash");
            double distance = sim.Fighters[0].Position.Z - origin.Z;
            Assert.That(distance, Is.EqualTo(t.DashSpeed * t.DashActive / 60.0).Within(1e-9), "dash distance");
        }

        [Test]
        public void MeleeStartedBeforeFreezeLandsAfterResume()
        {
            var t = new Tunables();
            var swing = PressAt(Turn - t.MeleeStartup / 2, new InputFrame { Melee = true });
            var sim = WithFreeze(180);
            Place(sim, new Vec3(0, 0, 0), new Vec3(1.2f, 0, 0));
            sim.Fighters[0].LockedOn = sim.Fighters[1].LockedOn = false;

            RunUntil(sim, s => s.IsFrozen, in0: swing);
            Assert.That(sim.Fighters[1].Hp, Is.EqualTo(t.MaxHp), "hit landed before the freeze");
            RunUntil(sim, s => !s.IsFrozen, in0: swing);
            Assert.That(sim.Fighters[1].Hp, Is.EqualTo(t.MaxHp), "hit landed during the freeze");
            Run(sim, t.MeleeStartup + t.MeleeActive, in0: swing);
            Assert.That(sim.Fighters[1].Hp, Is.EqualTo(t.MaxHp - t.MeleeDamage), "in-progress swing should finish and hit");
        }

        [Test]
        public void GameplayStateUntouchedDuringFreeze()
        {
            var sim = WithFreeze(180);
            var b0 = new SimpleBot(sim, 0, 1);
            var b1 = new SimpleBot(sim, 1, 2);
            RunUntil(sim, s => s.IsFrozen, in0: b0, in1: b1);
            var atFreeze = GameplayBytes(sim);
            int combatTick = sim.CombatTick;
            Run(sim, 179, b0, b1);
            Assert.That(sim.IsFrozen, Is.True);
            Assert.That(sim.CombatTick, Is.EqualTo(combatTick), "combat time advanced during the freeze");
            Assert.That(GameplayBytes(sim), Is.EqualTo(atFreeze), "fighter or projectile state changed during the freeze");
        }

        /// <summary>Snapshot minus its fixed header (ticks, phase timer), which may move while frozen.</summary>
        static byte[] GameplayBytes(Simulation sim)
        {
            var bytes = sim.SaveSnapshot();
            const int header = 4 * 8; // version, tick, combatTick, turn, phase, phaseTicksLeft, winner, nextProjectileId
            var tail = new byte[bytes.Length - header];
            System.Array.Copy(bytes, header, tail, 0, tail.Length);
            return tail;
        }
    }
}
