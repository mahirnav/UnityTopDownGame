using System.Collections.Generic;

namespace Rats.Sim
{
    /// <summary>One tick of input. Move is world-space on the floor plane (X, Z), length &lt;= 1.</summary>
    public struct InputFrame
    {
        public float MoveX, MoveZ;
        public bool Fire;
        public bool Melee;
    }

    /// <summary>Anything that can drive a fighter: keyboard, bot, script, later a network peer.</summary>
    public interface IInputSource
    {
        InputFrame Read(int tick);
    }

    /// <summary>Replays a fixed tick -> input script. Used by tests and the capture run.</summary>
    public sealed class ScriptedInput : IInputSource
    {
        readonly Dictionary<int, InputFrame> frames = new Dictionary<int, InputFrame>();

        public void Set(int tick, InputFrame frame) => frames[tick] = frame;

        public InputFrame Read(int tick) => frames.TryGetValue(tick, out var f) ? f : default;
    }
}
