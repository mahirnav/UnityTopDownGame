using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rats.Sim;

namespace Rats.SimTests
{
    /// <summary>Match recordings: the file format, and every saved replay in SimTests/Replays.</summary>
    public class ReplayTests
    {
        [Test]
        public void TunablesToTextRoundTripsEveryValue()
        {
            var t = new Tunables();
            int i = 0;
            foreach (var f in typeof(Tunables).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                i++;
                f.SetValue(t, f.FieldType == typeof(double) ? (object)(0.1 * i + 1.0 / 3.0) : i * 7);
            }
            var back = Tunables.Parse(t.ToText());
            foreach (var f in typeof(Tunables).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Assert.That(f.GetValue(back), Is.EqualTo(f.GetValue(t)), f.Name);
        }

        [Test]
        public void RecordingSurvivesTheFileAndReplaysExactly()
        {
            var sim = new Simulation(new Tunables { TurnTicks = 240, FreezeTicks = 60 });
            var bot0 = new SimpleBot(sim, 0, 7);
            var bot1 = new SimpleBot(sim, 1, 8);
            var rec = new MatchRecording(sim.T) { Note = "test" };
            for (int i = 0; i < 2500 && sim.Phase != Phase.RoundOver; i++)
            {
                var a = bot0.Read(sim.Tick);
                var b = bot1.Read(sim.Tick);
                rec.Record(a, b);
                sim.Step(a, b);
            }
            rec.FinalSnapshot = sim.SaveSnapshot();

            using var file = new MemoryStream();
            rec.Save(file);
            file.Position = 0;
            var loaded = MatchRecording.Load(file);

            Assert.That(loaded.Note, Is.EqualTo("test"));
            Assert.That(loaded.Ticks, Is.EqualTo(rec.Ticks));
            Assert.That(loaded.Replay().SaveSnapshot(), Is.EqualTo(rec.FinalSnapshot), "replay diverged from the recorded match");
            TestContext.WriteLine($"{rec.Ticks} ticks -> {file.Length} bytes");
        }

        /// <summary>
        /// Regression net: every .ratsreplay in SimTests/Replays must still reproduce its recorded end state.
        /// When a rule changes on purpose, a replay can legitimately diverge; re-record or delete it then.
        /// </summary>
        [Test]
        public void SavedReplaysStillReproduce()
        {
            var dir = Path.Combine(TestContext.CurrentContext.TestDirectory, "Replays");
            var files = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.ratsreplay") : new string[0];
            if (files.Length == 0) Assert.Pass("no saved replays yet");
            foreach (var path in files)
            {
                using var stream = File.OpenRead(path);
                var rec = MatchRecording.Load(stream);
                Assert.That(rec.Replay().SaveSnapshot(), Is.EqualTo(rec.FinalSnapshot),
                    $"{Path.GetFileName(path)} ({rec.Note}) no longer reproduces");
            }
            TestContext.WriteLine($"{files.Length} replay(s) reproduce");
        }
    }
}
