using System;
using System.Collections.Generic;

namespace Rats.Sim
{
    public enum Phase { Combat, Frozen }

    public sealed class Projectile
    {
        public int Id;
        public Vec3 Position;
        public Vec3 Velocity;
        public int AgeTicks;
    }

    public sealed class Fighter
    {
        public Vec3 Position = Vec3.Zero;
        public Vec3 Facing = Vec3.Forward;
        /// <summary>Ticks elapsed in the current swing, or -1 when not swinging.</summary>
        public int SwingTick = -1;

        public bool IsSwinging => SwingTick >= 0;

        /// <summary>0..1 through the swing. Visuals use this to pick the animation pose.</summary>
        public float SwingProgress => SwingTick < 0 ? 0f : (float)SwingTick / Tunables.SwingTotalTicks;
    }

    /// <summary>
    /// Spike combat simulation. Owns all gameplay state and only changes it in Step().
    /// Nothing here reads engine time, physics, or animation.
    /// </summary>
    public sealed class Simulation
    {
        public int Tick { get; private set; }
        public Phase Phase { get; private set; } = Phase.Combat;
        public readonly Fighter Player = new Fighter();
        public readonly List<Projectile> Projectiles = new List<Projectile>();
        int nextProjectileId = 1;

        public bool IsFrozen => Phase == Phase.Frozen;
        public void Freeze() => Phase = Phase.Frozen;
        public void Resume() => Phase = Phase.Combat;

        /// <summary>
        /// Advances exactly one tick. Does nothing while frozen.
        /// Order: inputs -> action starts -> movement -> action progress -> tick.
        /// </summary>
        public void Step(InputFrame input)
        {
            if (Phase == Phase.Frozen) return;

            // Action starts
            if (input.Fire) SpawnProjectile();
            if (input.Melee && !Player.IsSwinging) Player.SwingTick = 0;

            // Movement
            var move = new Vec3(input.MoveX, 0f, input.MoveZ);
            if (move.Length > 1f) move = move.Normalized;
            if (move != Vec3.Zero)
            {
                Player.Position += move * (Tunables.MoveSpeed * Tunables.TickDt);
                Player.Facing = move.Normalized;
            }

            for (int i = Projectiles.Count - 1; i >= 0; i--)
            {
                var p = Projectiles[i];
                p.Position += p.Velocity * Tunables.TickDt;
                p.AgeTicks++;
                if (p.AgeTicks >= Tunables.ProjectileLifetimeTicks) Projectiles.RemoveAt(i);
            }

            // Action progress
            if (Player.IsSwinging)
            {
                Player.SwingTick++;
                if (Player.SwingTick >= Tunables.SwingTotalTicks) Player.SwingTick = -1;
            }

            Tick++;
        }

        void SpawnProjectile()
        {
            Projectiles.Add(new Projectile
            {
                Id = nextProjectileId++,
                Position = Player.Position + Player.Facing * Tunables.ProjectileSpawnForward
                           + Vec3.Up * Tunables.ProjectileSpawnHeight,
                Velocity = Player.Facing * Tunables.ProjectileSpeed,
            });
        }
    }
}
