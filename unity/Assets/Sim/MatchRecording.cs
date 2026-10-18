using System;
using System.Collections.Generic;
using System.IO;

namespace Rats.Sim
{
    /// <summary>
    /// Everything needed to replay a round exactly: the tunables plus both fighters' input for every
    /// tick (~18 bytes per tick). Replays are how exploits Nick finds become regression tests:
    /// drop the file in SimTests/Replays and it is replayed and checked on every test run.
    /// </summary>
    public sealed class MatchRecording
    {
        const string Magic = "RATSREPLAY";
        const int FormatVersion = 1;

        public readonly string TunablesText;
        public readonly List<InputFrame> Inputs0 = new List<InputFrame>();
        public readonly List<InputFrame> Inputs1 = new List<InputFrame>();
        /// <summary>Sim state when saved; a replay must reproduce it bit for bit.</summary>
        public byte[] FinalSnapshot = Array.Empty<byte>();
        public string Note = "";

        public MatchRecording(Tunables tunables) : this(tunables.ToText()) { }
        MatchRecording(string tunablesText) { TunablesText = tunablesText; }

        public int Ticks => Inputs0.Count;

        public void Record(InputFrame input0, InputFrame input1)
        {
            Inputs0.Add(input0);
            Inputs1.Add(input1);
        }

        /// <summary>Runs a fresh sim through every recorded tick.</summary>
        public Simulation Replay()
        {
            var sim = new Simulation(Tunables.Parse(TunablesText));
            for (int i = 0; i < Inputs0.Count; i++) sim.Step(Inputs0[i], Inputs1[i]);
            return sim;
        }

        public void Save(Stream stream)
        {
            using var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            w.Write(Magic);
            w.Write(FormatVersion);
            w.Write(Note);
            w.Write(TunablesText);
            w.Write(Inputs0.Count);
            for (int i = 0; i < Inputs0.Count; i++)
            {
                Inputs0[i].Write(w);
                Inputs1[i].Write(w);
            }
            w.Write(FinalSnapshot.Length);
            w.Write(FinalSnapshot);
        }

        public static MatchRecording Load(Stream stream)
        {
            using var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            if (r.ReadString() != Magic) throw new InvalidDataException("not a RATS replay");
            int version = r.ReadInt32();
            if (version != FormatVersion) throw new InvalidDataException($"replay format {version}, expected {FormatVersion}");
            var note = r.ReadString();
            var rec = new MatchRecording(r.ReadString()) { Note = note };
            int ticks = r.ReadInt32();
            for (int i = 0; i < ticks; i++)
            {
                rec.Inputs0.Add(InputFrame.Read(r));
                rec.Inputs1.Add(InputFrame.Read(r));
            }
            rec.FinalSnapshot = r.ReadBytes(r.ReadInt32());
            return rec;
        }
    }
}
