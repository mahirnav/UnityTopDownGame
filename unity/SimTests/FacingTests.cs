using System;
using NUnit.Framework;
using Rats.Sim;
using static Rats.SimTests.TestSim;

namespace Rats.SimTests
{
    /// <summary>Facing model (turn rates per state) and backstabs.</summary>
    public class FacingTests
    {
        static double AngleDeg(Vec3 a, Vec3 b) => Math.Acos(Math.Clamp(Vec3.Dot(a, b), -1, 1)) * 180 / Math.PI;

        [Test]
        public void IdleTurnRateLimitsRotationPerTick()
        {
            var sim = Create(t => t.IdleTurnRateDeg = 180); // 3 degrees per tick
            Place(sim, new Vec3(0, 0, 0), new Vec3(5, 0, 0));
            sim.Fighters[0].Facing = new Vec3(0, 0, 1);      // 90 degrees off the target
            sim.Step(default, default);
            Assert.That(AngleDeg(sim.Fighters[0].Facing, new Vec3(0, 0, 1)), Is.EqualTo(3).Within(1e-6));
            Run(sim, 29);
            Assert.That(sim.Fighters[0].Facing.X, Is.EqualTo(1).Within(1e-9), "faces the target after 0.5 s");
        }

        [Test]
        public void StartupTracksTheTargetThenActiveFramesAreLocked()
        {
            var sim = Create(t => { t.StartupTurnRateDeg = 180; t.IdleTurnRateDeg = 720; });
            Place(sim, new Vec3(0, 0, 0), new Vec3(1.2, 0, 0));
            sim.Fighters[0].Facing = new Vec3(Math.Cos(0.3), 0, Math.Sin(0.3)); // ~17 degrees off
            sim.Step(new InputFrame { Melee = true }, default);
            Assert.That(AngleDeg(sim.Fighters[0].Facing, new Vec3(1, 0, 0)), Is.GreaterThan(13).And.LessThan(15),
                "starting an attack doesn't snap; startup turns at the limited rate");
            Run(sim, sim.T.MeleeStartup - 1);
            var locked = sim.Fighters[0].Facing;
            Assert.That(locked.X, Is.EqualTo(1).Within(1e-9), "tracked onto the target during startup");

            sim.Fighters[1].Position = new Vec3(0, 0, 1.2);  // target moves during active frames
            Run(sim, 3);
            Assert.That(sim.Fighters[0].Facing, Is.EqualTo(locked), "no tracking once the strike is out");
        }

        [Test]
        public void ZeroStartupTurnRateKeepsTheAttackWhereItStarted()
        {
            var sim = Create(t => { t.StartupTurnRateDeg = 0; t.IdleTurnRateDeg = 720; });
            Place(sim, new Vec3(0, 0, 0), new Vec3(1.2, 0, 0));
            var start = new Vec3(Math.Cos(0.3), 0, Math.Sin(0.3));
            sim.Fighters[0].Facing = start;
            sim.Step(new InputFrame { Melee = true }, default);
            Run(sim, sim.T.MeleeStartup);
            Assert.That(sim.Fighters[0].Facing, Is.EqualTo(start));
        }

        [Test]
        public void RangedAimLocksWhenTheBoltIsReleased()
        {
            var sim = Create(t => { t.StartupTurnRateDeg = 180; t.IdleTurnRateDeg = 720; });
            Place(sim, new Vec3(0, 0, 0), new Vec3(8, 0, 0));
            sim.Fighters[0].Facing = new Vec3(Math.Cos(0.4), 0, Math.Sin(0.4));
            sim.Step(new InputFrame { Ranged = true }, default);
            RunUntil(sim, s => s.Projectiles.Count > 0, 60);
            var dir = sim.Projectiles[0].Velocity.Normalized;
            Assert.That(dir, Is.EqualTo(sim.Fighters[0].Facing), "bolt flies where the fighter faced at release");
            Assert.That(dir.X, Is.EqualTo(1).Within(1e-9), "startup tracking turned it onto the target");
        }

        [Test]
        public void BackstabsHitHarderFromBehindOnly()
        {
            var sim = Create(t => { t.BackstabPercent = 50; t.BackstabArcDegrees = 90; });
            var attacker = sim.Fighters[0];
            var target = sim.Fighters[1];
            attacker.Position = new Vec3(0, 0, 0);
            target.Position = new Vec3(1.2, 0, 0);

            target.Facing = new Vec3(1, 0, 0);               // looking away: attacker is straight behind
            Assert.That(sim.IsBackstab(attacker, target), Is.True);
            Assert.That(sim.HitDamage(attacker, ActionKind.Melee, target), Is.EqualTo(sim.T.MeleeDamage * 150 / 100));
            Assert.That(sim.HitDamage(attacker, ActionKind.Ranged, target), Is.EqualTo(sim.T.RangedDamage), "melee only");

            target.Facing = new Vec3(0, 0, 1);               // side-on: outside a 90 degree rear cone
            Assert.That(sim.IsBackstab(attacker, target), Is.False);
            target.Facing = new Vec3(-1, 0, 0);              // facing the attacker
            Assert.That(sim.IsBackstab(attacker, target), Is.False);
        }

        [Test]
        public void ABackstabIsRecordedAsTheLastHit()
        {
            var sim = Create(t => { t.BackstabPercent = 50; t.IdleTurnRateDeg = 90; });
            Place(sim, new Vec3(0, 0, 0), new Vec3(1.2, 0, 0));
            var target = sim.Fighters[1];
            target.LockedOn = false;
            target.Facing = new Vec3(1, 0, 0);               // back turned
            sim.Fighters[0].LockedOn = false;
            sim.Fighters[0].Facing = new Vec3(1, 0, 0);
            Run(sim, sim.T.Melee.Total, PressAt(0, new InputFrame { Melee = true }));
            Assert.That(target.LastHitBackstab, Is.True);
            Assert.That(target.LastHitDamage, Is.EqualTo(sim.T.MeleeDamage * 150 / 100));
            Assert.That(target.Hp, Is.EqualTo(sim.T.MaxHp - target.LastHitDamage));
            Assert.That(target.LastHitTick, Is.GreaterThan(0));
        }

        [Test]
        public void InstantTurningMakesBackstabsImpossibleAgainstALockedTarget()
        {
            var sim = Create(t => { t.BackstabPercent = 50; t.IdleTurnRateDeg = 0; });
            Place(sim, new Vec3(0, 0, 0), new Vec3(1.2, 0, 0));
            sim.Fighters[1].Facing = new Vec3(1, 0, 0);      // back turned, but locked on and idle
            sim.Step(default, default);
            Assert.That(sim.IsBackstab(sim.Fighters[0], sim.Fighters[1]), Is.False, "snaps to face the attacker");
        }
    }
}
