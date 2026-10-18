using System;

namespace Rats.Sim
{
    /// <summary>
    /// Placeholder opponent so Nick can play alone. Just another input source: it reads the sim like a
    /// player looks at the screen and returns presses. Seeded so runs repeat.
    /// Approaches from far and circles otherwise (backpedaling is slow). Dodges swings it sees coming,
    /// keeps its last action point to punish, loves a backstab, stays out of the fire and finishes downed opponents.
    /// </summary>
    public sealed class SimpleBot : IInputSource
    {
        const int ReserveSpendTicksLeft = 60;  // spend the reserved point anyway in the last second of a turn
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
                // Think for a moment, then take a random offer.
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
            if (sim.Phase != Phase.Combat || me.Downed) return frame;

            var them = sim.Opponent(me);
            var to = (them.Position - me.Position).Flat;
            double dist = to.Length;
            var dir = dist > 1e-4 ? to / dist : Vec3.Zero;
            bool lineOfSight = sim.Arena.LineOfSight(me.Position, them.Position);
            double reach = sim.T.MeleeRange + sim.T.FighterRadius;

            if (!me.LockedOn && lineOfSight) frame.ToggleLock = true;

            if (tick >= nextStrafeFlip)
            {
                strafeSign = rng.Next(2) == 0 ? -1 : 1;
                nextStrafeFlip = tick + rng.Next(40, 120);
            }
            var side = new Vec3(-dir.Z, 0, dir.X) * strafeSign;
            var move = dist > 7 ? dir : side; // circle rather than backpedal

            // A downed opponent: walk up and finish them (executions are free).
            if (them.Downed)
            {
                frame.MoveX = (float)dir.X;
                frame.MoveZ = (float)dir.Z;
                if (!me.IsBusy && dist <= sim.T.ExecuteRange) frame.Melee = true;
                return frame;
            }

            // Keep clear of the closing ring: head for the center when the fire is near.
            if (sim.FireScale < 1 && !sim.Arena.InsideScaled(me.Position, sim.FireScale * 0.85))
            {
                var home = (Vec3.Zero - me.Position).Flat;
                if (home.Length > 1e-4) move = home.Normalized;
            }
            frame.MoveX = (float)move.X;
            frame.MoveZ = (float)move.Z;
            if (me.IsBusy || me.ActionPoints == 0) return frame;

            // Dodge a swing that's winding up in range: dash sideways, usually.
            bool swingIncoming = them.Action == ActionKind.Melee && them.ActionTick < them.Frames.Startup && dist < reach + 1;
            if (swingIncoming && rng.Next(100) < 65)
            {
                frame.Dash = true;
                frame.MoveX = (float)side.X;
                frame.MoveZ = (float)side.Z;
                return frame;
            }
            if (tick < nextActionTick) return frame;

            // Keep the last point for a punish unless the opening is there or the turn is ending.
            bool opening = them.ActionPoints == 0 || (them.IsBusy && them.ActionTick >= them.Frames.Startup + them.Frames.Active);
            bool lastPoint = me.ActionPoints == 1 && !me.Has(UpgradeId.CarryTheFlame);
            if (lastPoint && !opening && sim.PhaseTicksLeft > ReserveSpendTicksLeft) return frame;

            if (dist < reach) frame.Melee = true;                       // includes backstabs when behind
            else if (dist < 10 && lineOfSight) frame.Ranged = true;
            else if (dist >= 10) frame.Dash = true;
            if (frame.Melee || frame.Ranged || frame.Dash) nextActionTick = tick + rng.Next(20, 90);
            if (frame.Dash) { frame.MoveX = (float)dir.X; frame.MoveZ = (float)dir.Z; }
            return frame;
        }
    }
}
