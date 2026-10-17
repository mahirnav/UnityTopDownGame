using Rats.Sim;
using UnityEngine;

namespace Rats.Game
{
    /// <summary>
    /// Player 1 keyboard: WASD move, Space fire, E melee.
    /// Presses are latched until a sim tick reads them, so a press is never lost
    /// on a render frame that runs zero ticks.
    /// </summary>
    public sealed class KeyboardInput : IInputSource
    {
        bool firePressed, meleePressed;

        /// <summary>Call once per render frame, before stepping the sim.</summary>
        public void Poll()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Space)) firePressed = true;
            if (UnityEngine.Input.GetKeyDown(KeyCode.E)) meleePressed = true;
        }

        public void ClearPresses() { firePressed = false; meleePressed = false; }

        public InputFrame Read(int tick)
        {
            var frame = new InputFrame
            {
                MoveX = Axis(KeyCode.D, KeyCode.A),
                MoveZ = Axis(KeyCode.W, KeyCode.S),
                Fire = firePressed,
                Melee = meleePressed,
            };
            ClearPresses();
            return frame;
        }

        static float Axis(KeyCode positive, KeyCode negative) =>
            (UnityEngine.Input.GetKey(positive) ? 1f : 0f) - (UnityEngine.Input.GetKey(negative) ? 1f : 0f);
    }
}
