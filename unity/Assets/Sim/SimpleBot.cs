using System;

namespace Rats.Sim
{
    /// <summary>
    /// Placeholder opponent so Nick can play alone. Just another input source: it reads the
    /// sim like a player looks at the screen and returns presses. Seeded so runs repeat.
    /// Approach from far, circle otherwise; melee up close, shoot at mid range.
    /// </summary>
    public sealed class SimpleBot : IInputSource
    {
        readonly Simulation sim;
        readonly int index;
        readonly Random rng;
        int nextActionTick;
        int strafeSign = 1;
        int nextStrafeFlip;
        int draftDecideTick = -1;

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
            var me = sim.Fighters[index];
            if (sim.DraftActive && me.DraftPick == 0)
            {
                // Think for a moment, then take a random offer it doesn't own yet.
                if (draftDecideTick < 0) draftDecideTick = tick + rng.Next(20, 120);
                if (tick >= draftDecideTick)
                {
                    int slot = rng.Next(3);
                    for (int k = 0; k < 3; k++, slot = (slot + 1) % 3)
                    {
                        int offer = Simulation.Offer(me, slot);
                        if (offer != Simulation.NoOffer && !me.Has((UpgradeId)offer)) { frame.DraftPick = (byte)(slot + 1); break; }
                    }
                    draftDecideTick = -1;
                }
                return frame;
            }
            if (sim.Phase != Phase.Combat) return frame;

            var them = sim.Opponent(me);
            var to = (them.Position - me.Position).Flat;
            double dist = to.Length;
            var dir = dist > 1e-4 ? to / dist : Vec3.Zero;
            bool lineOfSight = sim.Arena.LineOfSight(me.Position, them.Position);

            if (!me.LockedOn && lineOfSight) frame.ToggleLock = true;

            if (tick >= nextStrafeFlip)
            {
                strafeSign = rng.Next(2) == 0 ? -1 : 1;
                nextStrafeFlip = tick + rng.Next(40, 120);
            }
            var side = new Vec3(-dir.Z, 0, dir.X) * strafeSign;
            var move = dist > 7 ? dir : side; // circle rather than backpedal (backpedaling is slow)
            frame.MoveX = (float)move.X;
            frame.MoveZ = (float)move.Z;

            if (!me.IsBusy && me.ActionPoints > 0 && tick >= nextActionTick)
            {
                if (dist < sim.T.MeleeRange + sim.T.FighterRadius) frame.Melee = true;
                else if (dist < 10 && lineOfSight) frame.Ranged = true;
                else if (dist >= 10) frame.Dash = true;
                if (frame.Melee || frame.Ranged || frame.Dash) nextActionTick = tick + rng.Next(25, 110);
            }
            return frame;
        }
    }
}
