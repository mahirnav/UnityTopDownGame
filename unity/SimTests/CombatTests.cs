using NUnit.Framework;
using Rats.Sim;
using static Rats.SimTests.TestSim;

namespace Rats.SimTests
{
    /// <summary>Hits, arena collisions, lock-on, and round end.</summary>
    public class CombatTests
    {
        [Test]
        public void ProjectileHitsOpponentOnce()
        {
            var sim = Create();
            RunUntil(sim, s => s.Fighters[1].Hp < s.T.MaxHp || s.Tick > 300, in0: PressAt(0, new InputFrame { Ranged = true }));
            Assert.That(sim.Fighters[1].Hp, Is.EqualTo(sim.T.MaxHp - sim.T.RangedDamage));
            Assert.That(sim.Projectiles, Is.Empty, "projectile should be consumed by the hit");
        }

        [Test]
        public void PillarBlocksProjectile()
        {
            var sim = Create();
            var pillar = sim.Arena.Pillars[0];
            Place(sim, pillar + new Vec3(-4f, 0, 0), pillar + new Vec3(4f, 0, 0));
            sim.Fighters[0].LockedOn = sim.Fighters[1].LockedOn = false;
            Run(sim, 120, PressAt(0, new InputFrame { Ranged = true }));
            Assert.That(sim.Fighters[1].Hp, Is.EqualTo(sim.T.MaxHp));
            Assert.That(sim.Projectiles, Is.Empty);
        }

        [Test]
        public void MeleeHitsOncePerSwing()
        {
            var sim = Create();
            Place(sim, new Vec3(0, 0, 0), new Vec3(1.2f, 0, 0));
            Run(sim, sim.T.Melee.Total, PressAt(0, new InputFrame { Melee = true }));
            Assert.That(sim.Fighters[1].Hp, Is.EqualTo(sim.T.MaxHp - sim.T.MeleeDamage));
        }

        [Test]
        public void MeleeMissesOutsideArcAndRange()
        {
            var behind = Create();
            Place(behind, new Vec3(0, 0, 0), new Vec3(1.2f, 0, 0));
            behind.Fighters[0].LockedOn = false;
            behind.Fighters[0].Facing = new Vec3(-1f, 0, 0); // facing away
            Run(behind, behind.T.Melee.Total, PressAt(0, new InputFrame { Melee = true }));
            Assert.That(behind.Fighters[1].Hp, Is.EqualTo(behind.T.MaxHp), "hit from behind the arc");

            var far = Create();
            Place(far, new Vec3(0, 0, 0), new Vec3(far.T.MeleeRange + far.T.FighterRadius + 0.3f, 0, 0));
            Run(far, far.T.Melee.Total, PressAt(0, new InputFrame { Melee = true }));
            Assert.That(far.Fighters[1].Hp, Is.EqualTo(far.T.MaxHp), "hit out of range");
        }

        [Test]
        public void WallsAndPillarsContainFighters()
        {
            var sim = Create();
            var hold = HoldFrom(0, 600, new InputFrame { MoveX = -1f });
            Run(sim, 600, hold);
            Assert.That(sim.Arena.Inside(sim.Fighters[0].Position, sim.T.FighterRadius * 0.999f), Is.True, "walked through the wall");

            var pillar = sim.Arena.Pillars[0];
            Place(sim, pillar + new Vec3(-3f, 0, 0), new Vec3(-9f, 0, -6f));
            sim.Fighters[0].LockedOn = false;
            Run(sim, 120, HoldFrom(sim.Tick, sim.Tick + 120, new InputFrame { MoveX = 1f }));
            Assert.That(sim.Arena.HitsPillar(sim.Fighters[0].Position, sim.T.FighterRadius * 0.999f), Is.False, "walked into a pillar");
        }

        [Test]
        public void PillarBreaksLockOn()
        {
            var sim = Create();
            var pillar = sim.Arena.Pillars[0];
            Place(sim, pillar + new Vec3(-3f, 0, 0), pillar + new Vec3(3f, 0, 0));
            Assert.That(sim.Fighters[0].LockedOn, Is.True);
            sim.Step(default, default);
            Assert.That(sim.Fighters[0].LockedOn, Is.False, "lock should break with a pillar in the way");

            sim.Step(new InputFrame { ToggleLock = true }, default);
            Assert.That(sim.Fighters[0].LockedOn, Is.False, "can't re-lock without line of sight");
        }

        [Test]
        public void KnockoutEndsRoundAndFreezesEverything()
        {
            var sim = Create();
            sim.Fighters[1].Hp = sim.T.RangedDamage;
            RunUntil(sim, s => s.Phase == Phase.RoundOver, 600, PressAt(0, new InputFrame { Ranged = true }));
            Assert.That(sim.Winner, Is.EqualTo(0));
            int tick = sim.Tick;
            Run(sim, 60, HoldFrom(0, 100000, new InputFrame { Dash = true }));
            Assert.That(sim.Tick, Is.EqualTo(tick), "sim should not advance after the round ends");
        }

        [Test]
        public void SimultaneousKnockoutIsADraw()
        {
            var sim = Create();
            sim.Fighters[0].Hp = sim.Fighters[1].Hp = sim.T.RangedDamage;
            var fire = PressAt(0, new InputFrame { Ranged = true });
            RunUntil(sim, s => s.Phase == Phase.RoundOver, 600, fire, fire);
            Assert.That(sim.Winner, Is.EqualTo(Simulation.Draw));
        }
    }
}
