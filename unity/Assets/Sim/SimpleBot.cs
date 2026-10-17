using System;

namespace Rats.Sim
{
    /// <summary>
    /// Placeholder opponent so Nick can play alone. Just another input source: it reads the
    /// sim like a player looks at the screen and returns presses. Seeded so runs repeat.
    /// Approach from far, back off when crowded, strafe otherwise; melee up close, shoot at mid range.
    /// </summary>
    public sealed class SimpleBot : IInputSource
    {
        readonly Simulation sim;
        readonly int index;
        readonly Random rng;
        int nextActionTick;
        int strafeSign = 1;
        int nextStrafeFlip;

        public SimpleBot(Simulation sim, int index, int seed)
        {
            this.sim = sim;
            this.index = index;
            rng = new Random(seed);
            nextActionTick = rng.Next(30, 90);
        }

        public InputFrame Read(int tick)
        {
            var frame = new InputFrame();
            if (sim.Phase != Phase.Combat) return frame;

            var me = sim.Fighters[index];
            var them = sim.Opponent(me);
            var to = (them.Position - me.Position).Flat;
            float dist = to.Length;
            var dir = dist > 1e-4f ? to / dist : Vec3.Zero;
            bool lineOfSight = sim.Arena.LineOfSight(me.Position, them.Position);

            if (!me.LockedOn && lineOfSight) frame.ToggleLock = true;

            if (tick >= nextStrafeFlip)
            {
                strafeSign = rng.Next(2) == 0 ? -1 : 1;
                nextStrafeFlip = tick + rng.Next(40, 120);
            }
            var side = new Vec3(-dir.Z, 0f, dir.X) * strafeSign;
            var move = dist > 7f ? dir : dist < 2.5f ? dir * -1f : side;
            frame.MoveX = move.X;
            frame.MoveZ = move.Z;

            if (!me.IsBusy && me.ActionPoints > 0 && tick >= nextActionTick)
            {
                if (dist < sim.T.MeleeRange + sim.T.FighterRadius) frame.Melee = true;
                else if (dist < 10f && lineOfSight) frame.Ranged = true;
                else if (dist >= 10f) frame.Dash = true;
                if (frame.Melee || frame.Ranged || frame.Dash) nextActionTick = tick + rng.Next(25, 110);
            }
            return frame;
        }
    }
}
