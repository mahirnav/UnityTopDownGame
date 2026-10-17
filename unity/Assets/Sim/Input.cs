using System.Collections.Generic;
using System.IO;

namespace Rats.Sim
{
    /// <summary>
    /// One fighter's input for one tick: the only way anything gets into the sim.
    /// Move is world-space on the floor plane (X, Z), length &lt;= 1. Buttons are presses.
    /// Small and serializable on purpose: this is what a client will send to the host.
    /// </summary>
    public struct InputFrame
    {
        public float MoveX, MoveZ;
        public bool Melee, Dash, Ranged, ToggleLock;

        public void Write(BinaryWriter w)
        {
            w.Write(MoveX);
            w.Write(MoveZ);
            w.Write((byte)((Melee ? 1 : 0) | (Dash ? 2 : 0) | (Ranged ? 4 : 0) | (ToggleLock ? 8 : 0)));
        }

        public static InputFrame Read(BinaryReader r)
        {
            var f = new InputFrame { MoveX = r.ReadSingle(), MoveZ = r.ReadSingle() };
            byte b = r.ReadByte();
            f.Melee = (b & 1) != 0;
            f.Dash = (b & 2) != 0;
            f.Ranged = (b & 4) != 0;
            f.ToggleLock = (b & 8) != 0;
            return f;
        }
    }

    /// <summary>Anything that can drive a fighter: keyboard, bot, script, later a network peer.</summary>
    public interface IInputSource
    {
        InputFrame Read(int tick);
    }

    /// <summary>Replays a fixed tick -> input script. Used by tests, captures, and replays.</summary>
    public sealed class ScriptedInput : IInputSource
    {
        readonly Dictionary<int, InputFrame> frames = new Dictionary<int, InputFrame>();

        public void Set(int tick, InputFrame frame) => frames[tick] = frame;

        public InputFrame Read(int tick) => frames.TryGetValue(tick, out var f) ? f : default;
    }

    /// <summary>Passes another source through and records every frame it returns.</summary>
    public sealed class RecordingInput : IInputSource
    {
        readonly IInputSource inner;
        public readonly ScriptedInput Recorded = new ScriptedInput();

        public RecordingInput(IInputSource inner) { this.inner = inner; }

        public InputFrame Read(int tick)
        {
            var f = inner.Read(tick);
            Recorded.Set(tick, f);
            return f;
        }
    }
}
