using System;
using System.IO;
using NUnit.Framework;
using Rats.Sim;

namespace Rats.SimTests
{
    public class TunablesTests
    {
        static string ShippedFile => File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "tunables.txt"));

        [Test]
        public void ShippedFileParsesAndListsEveryTunable()
        {
            Tunables.Parse(ShippedFile);
            foreach (var key in Tunables.Keys)
                StringAssert.IsMatch($@"(?m)^\s*{key}\s*=", ShippedFile, $"tunables.txt is missing {key}");
        }

        [Test]
        public void ValuesOverrideDefaults()
        {
            var t = Tunables.Parse("TurnTicks = 300  # five seconds\nDashSpeed = 12.5\n");
            Assert.That(t.TurnTicks, Is.EqualTo(300));
            Assert.That(t.DashSpeed, Is.EqualTo(12.5));
            Assert.That(t.FreezeTicks, Is.EqualTo(new Tunables().FreezeTicks), "unlisted keys keep defaults");
        }

        [Test]
        public void TyposFailLoudlyWithLineNumber()
        {
            var unknown = Assert.Throws<FormatException>(() => Tunables.Parse("TurnTicks = 300\nTurnTics = 5"));
            StringAssert.Contains("line 2", unknown.Message);
            var badNumber = Assert.Throws<FormatException>(() => Tunables.Parse("MaxHp = lots"));
            StringAssert.Contains("MaxHp", badNumber.Message);
        }
    }

    public class SimSourceTests
    {
        /// <summary>
        /// Regression: Unity's Mono keeps float intermediates wider than .NET, so float math made replays
        /// recorded in the game diverge in these tests (first at tick 58). The sim uses double throughout;
        /// only InputFrame keeps floats (raw stick/keyboard values, converted exactly to double).
        /// </summary>
        [Test]
        public void SimDoesNotUseFloatMath()
        {
            var simDir = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../Assets/Sim"));
            foreach (var file in Directory.GetFiles(simDir, "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) == "Input.cs") continue;
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    var code = lines[i].Split("//")[0].Replace("(float)", ""); // casts into InputFrame are fine
                    Assert.That(System.Text.RegularExpressions.Regex.IsMatch(code, @"\bfloat\b|MathF\.|\d[fF]\b"), Is.False,
                        $"{Path.GetFileName(file)}:{i + 1} uses float math: {lines[i].Trim()}");
                }
            }
        }
    }

    public class ClockTests
    {
        [Test]
        public void OneTickPerFrameAt60Fps()
        {
            // Regression: float TickDt (1f/60 rounds up) made 1/60 s frames yield 0 ticks.
            var clock = new TickClock();
            for (int frame = 0; frame < 600; frame++)
                Assert.That(clock.Advance(1.0 / 60.0), Is.EqualTo(1), $"frame {frame}");
        }

        [Test]
        public void HitchIsCappedNotReplayed()
        {
            var clock = new TickClock();
            Assert.That(clock.Advance(2.0), Is.EqualTo(TickClock.MaxTicksPerFrame));
            Assert.That(clock.Advance(1.0 / 60.0), Is.EqualTo(1));
        }
    }
}
