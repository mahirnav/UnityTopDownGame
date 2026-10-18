using Rats.Sim;
using UnityEngine;

namespace Rats.Game
{
    /// <summary>
    /// Keyboard input source: WASD move (camera-relative), J melee, K ranged, Space dash, Tab lock-on, 1-3 draft pick.
    /// Converts camera-relative movement to world space here, so the sim only ever sees world input.
    /// Presses are latched until a sim tick reads them, so a press is never lost on a zero-tick frame.
    /// </summary>
    public sealed class KeyboardInput : IInputSource
    {
        readonly Transform camera;
        bool melee, ranged, dash, toggleLock;
        byte draftPick;

        public KeyboardInput(Transform camera) { this.camera = camera; }

        /// <summary>False while a menu has focus: no presses, no movement.</summary>
        public bool Enabled = true;

        /// <summary>Call once per render frame, before stepping the sim.</summary>
        public void Poll()
        {
            if (!Enabled) { melee = ranged = dash = toggleLock = false; draftPick = 0; return; }
            if (Input.GetKeyDown(KeyCode.J)) melee = true;
            if (Input.GetKeyDown(KeyCode.K)) ranged = true;
            if (Input.GetKeyDown(KeyCode.Space)) dash = true;
            if (Input.GetKeyDown(KeyCode.Tab)) toggleLock = true;
            if (Input.GetKeyDown(KeyCode.Alpha1)) draftPick = 1;
            if (Input.GetKeyDown(KeyCode.Alpha2)) draftPick = 2;
            if (Input.GetKeyDown(KeyCode.Alpha3)) draftPick = 3;
        }

        /// <summary>Draft choice from the on-screen buttons (1-3).</summary>
        public void ChooseDraft(int slot) => draftPick = (byte)slot;

        public InputFrame Read(int tick)
        {
            if (!Enabled) return default;
            float right = Axis(KeyCode.D, KeyCode.A), forward = Axis(KeyCode.W, KeyCode.S);
            var camForward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
            var camRight = Vector3.Cross(Vector3.up, camForward);
            var move = Vector3.ClampMagnitude(camForward * forward + camRight * right, 1f);

            var frame = new InputFrame
            {
                MoveX = move.x, MoveZ = move.z,
                Melee = melee, Ranged = ranged, Dash = dash, ToggleLock = toggleLock, DraftPick = draftPick,
            };
            melee = ranged = dash = toggleLock = false;
            draftPick = 0;
            return frame;
        }

        static float Axis(KeyCode positive, KeyCode negative) =>
            (Input.GetKey(positive) ? 1f : 0f) - (Input.GetKey(negative) ? 1f : 0f);
    }
}
