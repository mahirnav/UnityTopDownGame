using System;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using Rats.Sim;
using Rats.Sim.Net;
using static Rats.SimTests.TestSim;

namespace Rats.SimTests
{
    /// <summary>Host/client protocol over real TCP on localhost.</summary>
    public class NetTests
    {
        static void PumpUntil(Func<bool> done, Action pump, int timeoutMs = 5000)
        {
            var sw = Stopwatch.StartNew();
            while (!done())
            {
                if (sw.ElapsedMilliseconds > timeoutMs) Assert.Fail("timed out");
                pump();
                Thread.Sleep(1);
            }
        }

        [Test]
        public void FrameReaderHandlesByteByByteDelivery()
        {
            var a = FrameReader.Encode(MessageType.Input, new byte[] { 1, 2, 3 });
            var b = FrameReader.Encode(MessageType.Snapshot, new byte[300]);
            var stream = new byte[a.Length + b.Length];
            a.CopyTo(stream, 0);
            b.CopyTo(stream, a.Length);

            var reader = new FrameReader();
            int got = 0;
            for (int i = 0; i < stream.Length; i++)
            {
                reader.Append(stream, i, 1);
                while (reader.TryRead(out var m))
                {
                    got++;
                    Assert.That(m.Type, Is.EqualTo(got == 1 ? MessageType.Input : MessageType.Snapshot));
                    Assert.That(m.Payload.Length, Is.EqualTo(got == 1 ? 3 : 300));
                }
            }
            Assert.That(got, Is.EqualTo(2));
        }

        [Test]
        public void RemoteInputKeepsPressesUntilATickReadsThem()
        {
            var remote = new RemoteInput();
            remote.Push(new InputFrame { Ranged = true, MoveX = 1f });
            remote.Push(new InputFrame { MoveX = 0.5f }); // newer frame without the press
            var f = remote.Read(0);
            Assert.That(f.Ranged, Is.True, "press lost when a newer frame arrived before the tick");
            Assert.That(f.MoveX, Is.EqualTo(0.5f), "movement should be the latest frame");
            Assert.That(remote.Read(1).Ranged, Is.False, "press should be consumed once");
        }

        [Test]
        public void RemoteDraftPickSurvivesUntilATickReadsIt()
        {
            var remote = new RemoteInput();
            remote.Push(new InputFrame { DraftPick = 2 });
            remote.Push(new InputFrame());
            Assert.That(remote.Read(0).DraftPick, Is.EqualTo(2));
            Assert.That(remote.Read(1).DraftPick, Is.EqualTo(0), "consumed once");
        }

        [Test]
        public void ClientSeesExactlyWhatTheHostSimulates()
        {
            using var host = new HostSession(0); // any free port
            using var client = new ClientSession("127.0.0.1", host.Port);
            PumpUntil(() => host.HasPlayer, () => { host.Poll(); client.Poll(); });

            var tunablesText = "TurnTicks = 200\nFreezeTicks = 30\n";
            host.SendWelcome(tunablesText);
            ClientSession.Event ev = ClientSession.Event.None;
            PumpUntil(() => ev == ClientSession.Event.RoundStarted, () => ev = client.Poll());
            Assert.That(client.RoundTunables, Is.EqualTo(tunablesText));

            var sim = new Simulation(Tunables.Parse(tunablesText));
            var view = new Simulation(Tunables.Parse(client.RoundTunables));
            var clientScript = new ScriptedInput();
            for (int t = 0; t < 40; t++) clientScript.Set(t, new InputFrame { Ranged = t == 5, MoveZ = 1f });

            // Lockstep pump so the test is exact: client sends its frame, host waits for it, steps, sends a snapshot.
            for (int t = 0; t < 300; t++)
            {
                client.SendInput(clientScript.Read(t));
                int expected = t + 1;
                PumpUntil(() => host.RemoteInput.FramesReceived >= expected, () => host.Poll());
                sim.Step(default, host.RemoteInput.Read(sim.Tick));
                host.SendSnapshot(sim.SaveSnapshot());
                client.Poll();
                if (client.TakeSnapshot(out var snap)) view.ApplySnapshot(snap);
            }
            PumpUntil(() => view.Tick == sim.Tick, () =>
            {
                client.Poll();
                if (client.TakeSnapshot(out var snap)) view.ApplySnapshot(snap);
            });

            Assert.That(view.SaveSnapshot(), Is.EqualTo(sim.SaveSnapshot()), "client view differs from host sim");
            Assert.That(sim.ActionLog.Exists(e => e.Fighter == 1 && e.Kind == ActionKind.Ranged), Is.True,
                "the client's ranged press never reached the host sim");
            Assert.That(sim.Fighters[1].Position.Z, Is.Not.EqualTo(0.0), "the client's movement never reached the host sim");
        }

        [Test]
        public void HostNoticesWhenThePlayerLeaves()
        {
            using var host = new HostSession(0);
            var client = new ClientSession("127.0.0.1", host.Port);
            PumpUntil(() => host.HasPlayer, () => { host.Poll(); client.Poll(); });

            client.Dispose();
            var ev = HostSession.Event.None;
            PumpUntil(() => ev == HostSession.Event.PlayerLeft, () => ev = host.Poll());
            Assert.That(host.HasPlayer, Is.False);

            // A new player can join afterwards.
            using var second = new ClientSession("127.0.0.1", host.Port);
            PumpUntil(() => host.HasPlayer, () => { host.Poll(); second.Poll(); });
        }

        [Test]
        public void SecondPlayerIsTurnedAway()
        {
            using var host = new HostSession(0);
            using var first = new ClientSession("127.0.0.1", host.Port);
            PumpUntil(() => host.HasPlayer, () => { host.Poll(); first.Poll(); });

            using var second = new ClientSession("127.0.0.1", host.Port);
            PumpUntil(() => second.Failed, () => { host.Poll(); first.Poll(); second.Poll(); });
            StringAssert.Contains("full", second.Status);
            Assert.That(host.HasPlayer, Is.True, "the first player should stay connected");
        }

        [Test]
        public void ClientReportsWhenNobodyIsHosting()
        {
            int port;
            using (var probe = new HostSession(0)) port = probe.Port; // a port that was free, now closed
            using var client = new ClientSession("127.0.0.1", port);
            PumpUntil(() => client.Failed, () => client.Poll(), 10000);
            StringAssert.StartsWith("Could not connect", client.Status);
        }
    }
}
