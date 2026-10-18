using NUnit.Framework;
using Rats.Sim;
using static Rats.SimTests.TestSim;

namespace Rats.SimTests
{
    /// <summary>The placeholder bot's habits, so solo playtests behave as intended.</summary>
    public class BotTests
    {
        [Test]
        public void BotUsuallyDodgesASwingItSeesComing()
        {
            int dodges = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var sim = Create();
                Place(sim, new Vec3(0, 0, 0), new Vec3(1.5, 0, 0));
                var bot = new SimpleBot(sim, 1, seed);
                sim.Step(new InputFrame { Melee = true }, bot.Read(sim.Tick));
                for (int i = 0; i < sim.T.MeleeStartup; i++) sim.Step(default, bot.Read(sim.Tick));
                if (sim.ActionLog.Exists(e => e.Fighter == 1 && e.Kind == ActionKind.Dash)) dodges++;
            }
            Assert.That(dodges, Is.InRange(8, 20), $"dodged {dodges}/20");
        }

        [Test]
        public void BotKeepsItsLastPointUntilThereIsAnOpening()
        {
            var sim = Create(t => t.TurnTicks = 2000);
            Place(sim, new Vec3(0, 0, 0), new Vec3(1.5, 0, 0));
            var bot = new SimpleBot(sim, 1, 3);
            sim.Fighters[1].ActionPoints = 1;
            for (int i = 0; i < 200; i++)
            {
                sim.Fighters[1].Position = new Vec3(1.5, 0, 0); // hold the bot in melee range
                sim.Step(default, bot.Read(sim.Tick));
            }
            Assert.That(sim.ActionLog.Exists(e => e.Fighter == 1), Is.False, "held its last point with no opening");

            sim.Fighters[0].ActionPoints = 0;                 // opponent is exhausted: punish
            for (int i = 0; i < 200 && !sim.ActionLog.Exists(e => e.Fighter == 1); i++)
            {
                sim.Fighters[1].Position = new Vec3(1.5, 0, 0);
                sim.Step(default, bot.Read(sim.Tick));
            }
            Assert.That(sim.ActionLog.Exists(e => e.Fighter == 1 && e.Kind == ActionKind.Melee), Is.True);
        }

        [Test]
        public void BotFinishesADownedOpponent()
        {
            var sim = Create(t => t.TurnTicks = 10000);
            Place(sim, new Vec3(0, 0, 0), new Vec3(4, 0, 0));
            var bot = new SimpleBot(sim, 1, 4);
            sim.Fighters[0].Hp = 0;
            for (int i = 0; i < 400 && sim.Phase == Phase.Combat; i++) sim.Step(default, bot.Read(sim.Tick));
            Assert.That(sim.ActionLog.Exists(e => e.Fighter == 1 && e.Kind == ActionKind.Execute), Is.True);
            Assert.That(sim.Winner, Is.EqualTo(1));
        }

        [Test]
        public void BotStepsOutOfTheFire()
        {
            var sim = Create(t => { t.FireStartTicks = 0; t.RoundMaxTicks = 6000; t.TurnTicks = 10000; });
            Run(sim, 600);                                       // ring at 90%
            sim.Fighters[1].Position = new Vec3(13.2, 0, 0);     // standing in the fire
            sim.Fighters[0].Position = new Vec3(13.2, 0, 3);     // opponent far from the center too
            var bot = new SimpleBot(sim, 1, 5);
            for (int i = 0; i < 120; i++) sim.Step(default, bot.Read(sim.Tick));
            Assert.That(sim.InFire(sim.Fighters[1]), Is.False, "walked back inside the ring");
        }
    }
}