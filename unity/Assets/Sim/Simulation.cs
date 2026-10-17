using System;
using System.Collections.Generic;
using System.IO;

namespace Rats.Sim
{
    public enum Phase { Combat, Freeze, RoundOver }

    public enum ActionKind { None, Melee, Dash, Ranged }

    public sealed class Fighter
    {
        public readonly int Index;
        public Vec3 Position;
        public Vec3 Facing;
        public int Hp;
        public int ActionPoints;
        public ActionKind Action;
        /// <summary>Ticks elapsed in the current action (0 on the tick it starts).</summary>
        public int ActionTick;
        public Vec3 ActionDir;
        public bool ActionHitLanded;
        public bool LockedOn;

        public Fighter(int index) { Index = index; }

        public bool IsBusy => Action != ActionKind.None;
    }

    public sealed class Projectile
    {
        public int Id;
        public int Owner;
        public Vec3 Position;
        public Vec3 Velocity;
        public int AgeTicks;
    }

    /// <summary>Diagnostic record of an accepted action. Not gameplay state, not in snapshots.</summary>
    public readonly struct ActionEvent
    {
        public readonly int Tick, CombatTick, Turn, Fighter;
        public readonly ActionKind Kind;

        public ActionEvent(int tick, int combatTick, int turn, int fighter, ActionKind kind)
        {
            Tick = tick; CombatTick = combatTick; Turn = turn; Fighter = fighter; Kind = kind;
        }
    }

    /// <summary>
    /// The combat simulation. Owns all gameplay state and only changes it in Step().
    /// Nothing here reads engine time, physics, animation, or local input.
    /// </summary>
    public sealed class Simulation
    {
        public const int NoWinner = -1;
        public const int Draw = 2;
        const int SnapshotVersion = 1;

        public readonly Tunables T;
        public readonly Arena Arena;
        public readonly Fighter[] Fighters = { new Fighter(0), new Fighter(1) };
        public readonly List<Projectile> Projectiles = new List<Projectile>();
        public readonly List<ActionEvent> ActionLog = new List<ActionEvent>();

        /// <summary>Every Step call, including frozen ones. Inputs are indexed by this.</summary>
        public int Tick { get; private set; }
        /// <summary>Gameplay time: only advances in Combat.</summary>
        public int CombatTick { get; private set; }
        public int Turn { get; private set; }
        public Phase Phase { get; private set; }
        public int PhaseTicksLeft { get; private set; }
        public int Winner { get; private set; } = NoWinner;
        int nextProjectileId = 1;

        public Simulation(Tunables tunables)
        {
            T = tunables;
            Arena = new Arena(tunables);
            for (int i = 0; i < 2; i++)
            {
                var f = Fighters[i];
                float side = i == 0 ? -1f : 1f;
                f.Position = new Vec3(side * T.StartOffsetX, 0f, 0f);
                f.Facing = new Vec3(-side, 0f, 0f);
                f.Hp = T.MaxHp;
                f.LockedOn = true;
            }
            StartTurn();
        }

        public bool IsFrozen => Phase == Phase.Freeze;
        public Fighter Opponent(Fighter f) => Fighters[1 - f.Index];

        /// <summary>
        /// Advances one tick. In Freeze only the freeze timer moves; gameplay state is untouched.
        /// Order: inputs -> action starts -> movement -> collisions -> damage -> down checks -> action progress / turn timer.
        /// </summary>
        public void Step(InputFrame input0, InputFrame input1)
        {
            if (Phase == Phase.RoundOver) return;
            Tick++;
            if (Phase == Phase.Freeze)
            {
                if (--PhaseTicksLeft <= 0) StartTurn();
                return;
            }

            // Inputs -> action starts
            HandleInput(Fighters[0], input0);
            HandleInput(Fighters[1], input1);
            foreach (var f in Fighters)
                if (f.Action == ActionKind.Ranged && f.ActionTick == T.RangedStartup) SpawnProjectile(f);

            // Movement
            Move(Fighters[0], input0);
            Move(Fighters[1], input1);
            foreach (var p in Projectiles)
            {
                p.Position += p.Velocity * Tunables.TickDt;
                p.AgeTicks++;
            }

            // Collisions
            SeparateFighters();
            Projectiles.RemoveAll(p => p.AgeTicks >= T.ProjectileLifetimeTicks
                                       || !Arena.Inside(p.Position, T.ProjectileRadius)
                                       || Arena.HitsPillar(p.Position, T.ProjectileRadius));
            foreach (var f in Fighters)
                if (f.LockedOn && !Arena.LineOfSight(f.Position, Opponent(f).Position)) f.LockedOn = false;

            // Damage
            Projectiles.RemoveAll(ProjectileHit);
            MeleeHit(Fighters[0]);
            MeleeHit(Fighters[1]);

            // Down checks
            bool down0 = Fighters[0].Hp <= 0, down1 = Fighters[1].Hp <= 0;
            if (down0 || down1)
            {
                Winner = down0 && down1 ? Draw : down0 ? 1 : 0;
                Phase = Phase.RoundOver;
                return;
            }

            // Action progress / turn timer
            foreach (var f in Fighters) AdvanceAction(f);
            CombatTick++;
            if (--PhaseTicksLeft <= 0) EndTurn();
        }

        void StartTurn()
        {
            Turn++;
            Phase = Phase.Combat;
            PhaseTicksLeft = T.TurnTicks;
            foreach (var f in Fighters) f.ActionPoints = T.ActionPointsPerTurn;
        }

        void EndTurn()
        {
            foreach (var f in Fighters) f.ActionPoints = 0; // unspent points expire
            if (T.FreezeTicks <= 0)
            {
                StartTurn();
                return;
            }
            Phase = Phase.Freeze;
            PhaseTicksLeft = T.FreezeTicks;
        }

        void HandleInput(Fighter f, InputFrame input)
        {
            var opponent = Opponent(f);
            if (input.ToggleLock)
                f.LockedOn = !f.LockedOn && Arena.LineOfSight(f.Position, opponent.Position);

            if (f.IsBusy) return; // no cancels, no buffering
            var kind = input.Melee ? ActionKind.Melee
                     : input.Ranged ? ActionKind.Ranged
                     : input.Dash ? ActionKind.Dash
                     : ActionKind.None;
            if (kind == ActionKind.None) return;
            var def = T.Get(kind);
            if (f.ActionPoints < def.Cost) return;

            f.ActionPoints -= def.Cost; // spent the moment the press is accepted
            if (f.LockedOn) f.Facing = FaceToward(f, opponent);
            var move = MoveVector(input);
            f.ActionDir = kind == ActionKind.Dash && move != Vec3.Zero ? move.Normalized : f.Facing;
            if (kind == ActionKind.Dash && !f.LockedOn) f.Facing = f.ActionDir;
            f.Action = kind;
            f.ActionTick = 0;
            f.ActionHitLanded = false;
            ActionLog.Add(new ActionEvent(Tick, CombatTick, Turn, f.Index, kind));
        }

        void Move(Fighter f, InputFrame input)
        {
            if (f.Action == ActionKind.Dash)
            {
                if (T.Dash.IsActive(f.ActionTick))
                    f.Position = Arena.Constrain(f.Position + f.ActionDir * (T.DashSpeed * Tunables.TickDt), T.FighterRadius);
                return;
            }
            if (f.IsBusy) return; // rooted during melee and ranged

            var move = MoveVector(input);
            if (move != Vec3.Zero)
            {
                f.Position = Arena.Constrain(f.Position + move * (T.MoveSpeed * Tunables.TickDt), T.FighterRadius);
                if (!f.LockedOn) f.Facing = move.Normalized;
            }
            if (f.LockedOn) f.Facing = FaceToward(f, Opponent(f));
        }

        static Vec3 MoveVector(InputFrame input)
        {
            var move = new Vec3(input.MoveX, 0f, input.MoveZ);
            return move.Length > 1f ? move.Normalized : move;
        }

        static Vec3 FaceToward(Fighter f, Fighter target)
        {
            var d = (target.Position - f.Position).Flat;
            return d.Length > 1e-4f ? d.Normalized : f.Facing;
        }

        void SeparateFighters()
        {
            Fighter a = Fighters[0], b = Fighters[1];
            var d = (b.Position - a.Position).Flat;
            float dist = d.Length, min = 2f * T.FighterRadius;
            if (dist >= min) return;
            var normal = dist > 1e-6f ? d / dist : new Vec3(1f, 0f, 0f);
            var push = normal * ((min - dist) * 0.5f);
            a.Position = Arena.Constrain(a.Position - push, T.FighterRadius);
            b.Position = Arena.Constrain(b.Position + push, T.FighterRadius);
        }

        void SpawnProjectile(Fighter f)
        {
            Projectiles.Add(new Projectile
            {
                Id = nextProjectileId++,
                Owner = f.Index,
                Position = f.Position + f.ActionDir * (T.FighterRadius + T.ProjectileRadius) + Vec3.Up * T.ProjectileSpawnHeight,
                Velocity = f.ActionDir * T.ProjectileSpeed,
            });
        }

        bool ProjectileHit(Projectile p)
        {
            var target = Fighters[1 - p.Owner];
            if (Vec3.DistanceXZ(p.Position, target.Position) > T.ProjectileRadius + T.FighterRadius) return false;
            target.Hp -= T.RangedDamage;
            return true;
        }

        void MeleeHit(Fighter f)
        {
            if (f.Action != ActionKind.Melee || f.ActionHitLanded || !T.Melee.IsActive(f.ActionTick)) return;
            var target = Opponent(f);
            var to = (target.Position - f.Position).Flat;
            float dist = to.Length;
            if (dist > T.MeleeRange + T.FighterRadius) return;
            float cosHalfArc = MathF.Cos(T.MeleeArcDegrees * 0.5f * MathF.PI / 180f);
            if (dist > 1e-4f && Vec3.Dot(f.Facing, to / dist) < cosHalfArc) return;
            target.Hp -= T.MeleeDamage;
            f.ActionHitLanded = true;
        }

        void AdvanceAction(Fighter f)
        {
            if (!f.IsBusy) return;
            f.ActionTick++;
            if (f.ActionTick >= T.Get(f.Action).Total) f.Action = ActionKind.None;
        }

        // --- Snapshots: everything a host would send a client. Tunables travel separately. ---

        public byte[] SaveSnapshot()
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(SnapshotVersion);
            w.Write(Tick); w.Write(CombatTick); w.Write(Turn); w.Write((int)Phase);
            w.Write(PhaseTicksLeft); w.Write(Winner); w.Write(nextProjectileId);
            foreach (var f in Fighters)
            {
                Write(w, f.Position); Write(w, f.Facing);
                w.Write(f.Hp); w.Write(f.ActionPoints); w.Write((int)f.Action); w.Write(f.ActionTick);
                Write(w, f.ActionDir); w.Write(f.ActionHitLanded); w.Write(f.LockedOn);
            }
            w.Write(Projectiles.Count);
            foreach (var p in Projectiles)
            {
                w.Write(p.Id); w.Write(p.Owner); Write(w, p.Position); Write(w, p.Velocity); w.Write(p.AgeTicks);
            }
            w.Flush();
            return ms.ToArray();
        }

        public static Simulation LoadSnapshot(byte[] data, Tunables tunables)
        {
            var sim = new Simulation(tunables);
            using var r = new BinaryReader(new MemoryStream(data));
            int version = r.ReadInt32();
            if (version != SnapshotVersion) throw new InvalidDataException($"snapshot version {version}, expected {SnapshotVersion}");
            sim.Tick = r.ReadInt32(); sim.CombatTick = r.ReadInt32(); sim.Turn = r.ReadInt32(); sim.Phase = (Phase)r.ReadInt32();
            sim.PhaseTicksLeft = r.ReadInt32(); sim.Winner = r.ReadInt32(); sim.nextProjectileId = r.ReadInt32();
            foreach (var f in sim.Fighters)
            {
                f.Position = ReadVec(r); f.Facing = ReadVec(r);
                f.Hp = r.ReadInt32(); f.ActionPoints = r.ReadInt32(); f.Action = (ActionKind)r.ReadInt32(); f.ActionTick = r.ReadInt32();
                f.ActionDir = ReadVec(r); f.ActionHitLanded = r.ReadBoolean(); f.LockedOn = r.ReadBoolean();
            }
            int count = r.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                sim.Projectiles.Add(new Projectile
                {
                    Id = r.ReadInt32(), Owner = r.ReadInt32(), Position = ReadVec(r), Velocity = ReadVec(r), AgeTicks = r.ReadInt32(),
                });
            }
            return sim;
        }

        static void Write(BinaryWriter w, Vec3 v) { w.Write(v.X); w.Write(v.Y); w.Write(v.Z); }
        static Vec3 ReadVec(BinaryReader r) => new Vec3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    }
}
