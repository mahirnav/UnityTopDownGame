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
        public int MaxHp;
        public int ActionPoints;
        public ActionKind Action;
        /// <summary>Ticks elapsed in the current action (0 on the tick it starts).</summary>
        public int ActionTick;
        /// <summary>Frame data locked when the current action started (upgrades picked later don't change it).</summary>
        public ActionDef Frames;
        /// <summary>Melee reach locked when the swing started.</summary>
        public double ActionReach;
        public Vec3 ActionDir;
        public bool ActionHitLanded;
        public bool LockedOn;
        /// <summary>Ticks in a row the opponent has been out of sight while locked (grace period).</summary>
        public int LockBlockedTicks;
        /// <summary>Ticks left in which lock comes back on its own if the opponent reappears (0 = none).</summary>
        public int RelockWindowTicks;

        /// <summary>One bit per UpgradeId owned this round.</summary>
        public ulong Upgrades;
        public int ActionsThisTurn;
        public bool FreeDashUsed;
        /// <summary>Carry the Flame: points left unspent at the end of the previous turn.</summary>
        public int CarriedPoints;
        /// <summary>This fighter's own draft offers (UpgradeId, or Simulation.NoOffer). Never something already owned.</summary>
        public int Offer0 = Simulation.NoOffer, Offer1 = Simulation.NoOffer, Offer2 = Simulation.NoOffer;
        /// <summary>Pick locked in during the current draft: UpgradeId + 1, or 0 for none yet.</summary>
        public int DraftPick;

        public Fighter(int index) { Index = index; }

        public bool IsBusy => Action != ActionKind.None;
        public bool Has(UpgradeId u) => (Upgrades & (1UL << (int)u)) != 0;
    }

    public sealed class Projectile
    {
        public int Id;
        public int Owner;
        public Vec3 Position;
        public Vec3 Velocity;
        /// <summary>Locked when fired (Big Bolt).</summary>
        public double Radius;
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
    ///
    /// Upgrade rule: values are either read when a hit lands (damage, so a frozen swing benefits from a
    /// pick made during the freeze) or locked when an action starts (frame data, reach, bolt size).
    /// </summary>
    public sealed class Simulation
    {
        public const int NoWinner = -1;
        public const int Draw = 2;
        public const int NoOffer = -1;
        const int SnapshotVersion = 5; // 2: doubles, 3: lock-on timers, 4: upgrades and drafting, 5: per-player offers

        public readonly Tunables T;
        public readonly Arena Arena;
        /// <summary>Seed for the draft draws; replays and tests reproduce a match from tunables + seed + inputs.</summary>
        public readonly ulong Seed;
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
        /// <summary>True during a freeze that offers upgrades.</summary>
        public bool DraftActive { get; private set; }
        public int DraftsThisRound { get; private set; }
        int freezesThisRound;
        ulong rng;
        int nextProjectileId = 1;

        public Simulation(Tunables tunables, ulong seed = 1)
        {
            T = tunables;
            Arena = new Arena(tunables);
            Seed = seed;
            rng = seed != 0 ? seed : 0x9E3779B97F4A7C15UL; // xorshift state must not be zero
            for (int i = 0; i < 2; i++)
            {
                var f = Fighters[i];
                double side = i == 0 ? -1 : 1;
                f.Position = new Vec3(side * T.StartOffsetX, 0, 0);
                f.Facing = new Vec3(-side, 0, 0);
                f.MaxHp = T.MaxHp;
                f.Hp = T.MaxHp;
                f.LockedOn = true;
            }
            StartTurn();
        }

        public bool IsFrozen => Phase == Phase.Freeze;
        public Fighter Opponent(Fighter f) => Fighters[1 - f.Index];

        /// <summary>A fighter's offer slot 0-2 in the current draft as an UpgradeId, or NoOffer.</summary>
        public static int Offer(Fighter f, int slot) => slot == 0 ? f.Offer0 : slot == 1 ? f.Offer1 : slot == 2 ? f.Offer2 : NoOffer;

        /// <summary>
        /// Advances one tick. In Freeze only the freeze timer (and draft picks) move; gameplay state is untouched.
        /// Order: inputs -> action starts -> movement -> collisions -> damage -> down checks -> action progress / turn timer.
        /// </summary>
        public void Step(InputFrame input0, InputFrame input1)
        {
            if (Phase == Phase.RoundOver) return;
            Tick++;
            if (Phase == Phase.Freeze)
            {
                if (DraftActive)
                {
                    TakeDraftPick(Fighters[0], input0);
                    TakeDraftPick(Fighters[1], input1);
                }
                if (--PhaseTicksLeft <= 0)
                {
                    if (DraftActive) ApplyDraftPicks(); // both at once, never at pick time
                    StartTurn();
                }
                return;
            }

            // Inputs -> action starts
            HandleInput(Fighters[0], input0);
            HandleInput(Fighters[1], input1);
            foreach (var f in Fighters)
                if (f.Action == ActionKind.Ranged && f.ActionTick == f.Frames.Startup) SpawnProjectile(f);

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
                                       || !Arena.Inside(p.Position, p.Radius)
                                       || Arena.HitsPillar(p.Position, p.Radius));
            foreach (var f in Fighters) UpdateLockOn(f);

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

        /// <summary>Damage a hit from this attacker deals right now. Read at the moment of impact.</summary>
        public int HitDamage(Fighter attacker, ActionKind kind, Fighter target)
        {
            int damage = kind == ActionKind.Melee
                ? T.MeleeDamage + (attacker.Has(UpgradeId.HeavyBlade) ? T.HeavyBladeDamage : 0)
                : T.RangedDamage;
            damage += attacker.CarriedPoints * T.CarryTheFlameDamagePerPoint;
            if (attacker.Has(UpgradeId.ExhaustionPunish) && target.ActionPoints == 0)
                damage = damage * (100 + T.ExhaustionPunishPercent) / 100;
            return damage;
        }

        void StartTurn()
        {
            Turn++;
            Phase = Phase.Combat;
            PhaseTicksLeft = T.TurnTicks;
            DraftActive = false;
            foreach (var f in Fighters)
            {
                f.ActionPoints = T.ActionPointsPerTurn;
                f.ActionsThisTurn = 0;
                f.FreeDashUsed = false;
            }
        }

        void EndTurn()
        {
            foreach (var f in Fighters)
            {
                f.CarriedPoints = f.Has(UpgradeId.CarryTheFlame) ? f.ActionPoints : 0;
                f.ActionPoints = 0; // unspent points expire
            }
            freezesThisRound++;

            bool draft = DraftsThisRound < T.DraftsPerRound && T.DraftInterval > 0
                         && (freezesThisRound - 1) % T.DraftInterval == 0;
            if (draft)
            {
                RollOffers();
                DraftActive = true;
                DraftsThisRound++;
                Phase = Phase.Freeze;
                PhaseTicksLeft = Math.Max(1, T.DraftFreezeTicks);
                return;
            }
            if (T.FreezeTicks <= 0)
            {
                StartTurn();
                return;
            }
            Phase = Phase.Freeze;
            PhaseTicksLeft = T.FreezeTicks;
        }

        // --- Drafting -------------------------------------------------------------------

        /// <summary>Each fighter gets their own 3 random upgrades from the ones they don't own yet.</summary>
        void RollOffers()
        {
            foreach (var f in Fighters)
            {
                var candidates = new List<int>();
                foreach (var u in Upgrades.Pool)
                    if (!f.Has(u)) candidates.Add((int)u);
                var picked = new int[3];
                for (int slot = 0; slot < 3; slot++)
                {
                    if (candidates.Count == 0) { picked[slot] = NoOffer; continue; }
                    int i = NextInt(candidates.Count);
                    picked[slot] = candidates[i];
                    candidates.RemoveAt(i);
                }
                f.Offer0 = picked[0]; f.Offer1 = picked[1]; f.Offer2 = picked[2];
                f.DraftPick = 0;
            }
        }

        /// <summary>First valid pick locks in; picks of an upgrade you already own are ignored.</summary>
        void TakeDraftPick(Fighter f, InputFrame input)
        {
            if (f.DraftPick != 0 || input.DraftPick < 1 || input.DraftPick > 3) return;
            int offer = Offer(f, input.DraftPick - 1);
            if (offer == NoOffer || f.Has((UpgradeId)offer)) return;
            f.DraftPick = offer + 1;
        }

        void ApplyDraftPicks()
        {
            foreach (var f in Fighters)
            {
                int chosen = f.DraftPick > 0 ? f.DraftPick - 1 : FirstValidOffer(f); // no pick in time: first valid offer
                if (chosen != NoOffer) Grant(f, (UpgradeId)chosen);
                f.DraftPick = 0;
                f.Offer0 = f.Offer1 = f.Offer2 = NoOffer;
            }
        }

        int FirstValidOffer(Fighter f)
        {
            for (int slot = 0; slot < 3; slot++)
            {
                int offer = Offer(f, slot);
                if (offer != NoOffer && !f.Has((UpgradeId)offer)) return offer;
            }
            return NoOffer;
        }

        void Grant(Fighter f, UpgradeId u)
        {
            f.Upgrades |= 1UL << (int)u;
            if (u == UpgradeId.Fortification)
            {
                f.MaxHp += T.FortificationMaxHp;
                f.Hp = Math.Min(f.Hp + T.FortificationHeal, f.MaxHp);
            }
        }

        /// <summary>Seeded xorshift64*; the state is part of the snapshot, so draws replay exactly.</summary>
        int NextInt(int n)
        {
            rng ^= rng >> 12;
            rng ^= rng << 25;
            rng ^= rng >> 27;
            return (int)((rng * 2685821657736338717UL) % (ulong)n);
        }

        // --- Actions --------------------------------------------------------------------

        void HandleInput(Fighter f, InputFrame input)
        {
            var opponent = Opponent(f);
            if (input.ToggleLock)
            {
                if (f.LockedOn)
                {
                    f.LockedOn = false;
                    f.LockBlockedTicks = 0;
                    f.RelockWindowTicks = 0; // a deliberate unlock stays unlocked
                }
                else if (Arena.LineOfSight(f.Position, opponent.Position))
                {
                    f.LockedOn = true;
                    f.RelockWindowTicks = 0;
                }
            }

            if (f.IsBusy) return; // no cancels, no buffering
            var kind = input.Melee ? ActionKind.Melee
                     : input.Ranged ? ActionKind.Ranged
                     : input.Dash ? ActionKind.Dash
                     : ActionKind.None;
            if (kind == ActionKind.None) return;

            var frames = T.Get(kind);
            bool freeDash = kind == ActionKind.Dash && f.Has(UpgradeId.FreeStep) && !f.FreeDashUsed;
            int cost = freeDash ? 0 : frames.Cost;
            if (f.ActionPoints < cost) return;

            f.ActionPoints -= cost; // spent the moment the press is accepted
            if (freeDash) f.FreeDashUsed = true;
            if (f.ActionsThisTurn == 0 && f.Has(UpgradeId.OpeningMove))
                frames = frames.WithStartup(frames.Startup * T.OpeningMoveStartupPercent / 100);
            f.Frames = frames;
            f.ActionReach = T.MeleeRange + (f.Has(UpgradeId.LongReach) ? T.LongReachBonus : 0);
            f.ActionsThisTurn++;

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
                if (f.Frames.IsActive(f.ActionTick))
                    f.Position = Arena.Constrain(f.Position + f.ActionDir * (T.DashSpeed * Tunables.TickDt), T.FighterRadius);
                return;
            }
            if (f.IsBusy)
            {
                // Melee/ranged startup: walk at a fraction of speed (0 = rooted). Facing stays where the action started.
                if (T.StartupWalkMultiplier > 0 && f.ActionTick < f.Frames.Startup)
                {
                    var step = MoveVector(input);
                    if (step != Vec3.Zero)
                    {
                        double speed = T.MoveSpeed * T.StartupWalkMultiplier * BackpedalFactor(step, f.Facing);
                        f.Position = Arena.Constrain(f.Position + step * (speed * Tunables.TickDt), T.FighterRadius);
                    }
                }
                return;
            }

            var move = MoveVector(input);
            if (move != Vec3.Zero)
            {
                // Locked on you face the opponent, so moving away is a slow backpedal; unlocked you face where you run.
                double factor = f.LockedOn ? BackpedalFactor(move, FaceToward(f, Opponent(f))) : 1;
                f.Position = Arena.Constrain(f.Position + move * (T.MoveSpeed * factor * Tunables.TickDt), T.FighterRadius);
                if (!f.LockedOn) f.Facing = move.Normalized;
            }
            if (f.LockedOn) f.Facing = FaceToward(f, Opponent(f));
        }

        /// <summary>
        /// Pillars break lock-on after LockOnGraceTicks of lost sight (0 = immediately). Within AutoRelockTicks
        /// after a break, lock returns by itself if the opponent reappears; otherwise Tab re-locks.
        /// </summary>
        void UpdateLockOn(Fighter f)
        {
            bool visible = Arena.LineOfSight(f.Position, Opponent(f).Position);
            if (f.LockedOn)
            {
                f.LockBlockedTicks = visible ? 0 : f.LockBlockedTicks + 1;
                if (f.LockBlockedTicks > T.LockOnGraceTicks)
                {
                    f.LockedOn = false;
                    f.LockBlockedTicks = 0;
                    f.RelockWindowTicks = T.AutoRelockTicks;
                }
            }
            else if (f.RelockWindowTicks > 0)
            {
                if (visible) { f.LockedOn = true; f.RelockWindowTicks = 0; }
                else f.RelockWindowTicks--;
            }
        }

        /// <summary>1 when moving forward or sideways, down to BackpedalMultiplier when moving straight back.</summary>
        double BackpedalFactor(Vec3 move, Vec3 facing)
        {
            double back = -Vec3.Dot(move.Normalized, facing);
            return back > 0 ? 1 - (1 - T.BackpedalMultiplier) * back : 1;
        }

        static Vec3 MoveVector(InputFrame input)
        {
            var move = new Vec3(input.MoveX, 0, input.MoveZ);
            return move.Length > 1 ? move.Normalized : move;
        }

        static Vec3 FaceToward(Fighter f, Fighter target)
        {
            var d = (target.Position - f.Position).Flat;
            return d.Length > 1e-4 ? d.Normalized : f.Facing;
        }

        void SeparateFighters()
        {
            Fighter a = Fighters[0], b = Fighters[1];
            var d = (b.Position - a.Position).Flat;
            double dist = d.Length, min = 2 * T.FighterRadius;
            if (dist >= min) return;
            var normal = dist > 1e-6 ? d / dist : new Vec3(1, 0, 0);
            var push = normal * ((min - dist) * 0.5);
            a.Position = Arena.Constrain(a.Position - push, T.FighterRadius);
            b.Position = Arena.Constrain(b.Position + push, T.FighterRadius);
        }

        void SpawnProjectile(Fighter f)
        {
            double radius = T.ProjectileRadius * (f.Has(UpgradeId.BigBolt) ? T.BigBoltRadiusMultiplier : 1);
            Projectiles.Add(new Projectile
            {
                Id = nextProjectileId++,
                Owner = f.Index,
                Position = f.Position + f.ActionDir * (T.FighterRadius + radius) + Vec3.Up * T.ProjectileSpawnHeight,
                Velocity = f.ActionDir * T.ProjectileSpeed,
                Radius = radius,
            });
        }

        bool ProjectileHit(Projectile p)
        {
            var target = Fighters[1 - p.Owner];
            if (Vec3.DistanceXZ(p.Position, target.Position) > p.Radius + T.FighterRadius) return false;
            target.Hp -= HitDamage(Fighters[p.Owner], ActionKind.Ranged, target);
            return true;
        }

        void MeleeHit(Fighter f)
        {
            if (f.Action != ActionKind.Melee || f.ActionHitLanded || !f.Frames.IsActive(f.ActionTick)) return;
            var target = Opponent(f);
            var to = (target.Position - f.Position).Flat;
            double dist = to.Length;
            if (dist > f.ActionReach + T.FighterRadius) return;
            double cosHalfArc = Math.Cos(T.MeleeArcDegrees * 0.5 * Math.PI / 180);
            if (dist > 1e-4 && Vec3.Dot(f.Facing, to / dist) < cosHalfArc) return;
            target.Hp -= HitDamage(f, ActionKind.Melee, target);
            f.ActionHitLanded = true;
        }

        void AdvanceAction(Fighter f)
        {
            if (!f.IsBusy) return;
            f.ActionTick++;
            if (f.ActionTick >= f.Frames.Total) f.Action = ActionKind.None;
        }

        // --- Snapshots: everything a host would send a client. Tunables travel separately. ---

        public byte[] SaveSnapshot()
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(SnapshotVersion);
            w.Write(Tick); w.Write(CombatTick); w.Write(Turn); w.Write((int)Phase);
            w.Write(PhaseTicksLeft); w.Write(Winner); w.Write(nextProjectileId);
            w.Write(DraftActive); w.Write(DraftsThisRound); w.Write(freezesThisRound);
            w.Write(rng);
            foreach (var f in Fighters)
            {
                Write(w, f.Position); Write(w, f.Facing);
                w.Write(f.Hp); w.Write(f.MaxHp); w.Write(f.ActionPoints); w.Write((int)f.Action); w.Write(f.ActionTick);
                w.Write(f.Frames.Cost); w.Write(f.Frames.Startup); w.Write(f.Frames.Active); w.Write(f.Frames.Recovery);
                w.Write(f.ActionReach);
                Write(w, f.ActionDir); w.Write(f.ActionHitLanded); w.Write(f.LockedOn);
                w.Write(f.LockBlockedTicks); w.Write(f.RelockWindowTicks);
                w.Write(f.Upgrades); w.Write(f.ActionsThisTurn); w.Write(f.FreeDashUsed); w.Write(f.CarriedPoints); w.Write(f.DraftPick);
                w.Write(f.Offer0); w.Write(f.Offer1); w.Write(f.Offer2);
            }
            w.Write(Projectiles.Count);
            foreach (var p in Projectiles)
            {
                w.Write(p.Id); w.Write(p.Owner); Write(w, p.Position); Write(w, p.Velocity); w.Write(p.Radius); w.Write(p.AgeTicks);
            }
            w.Flush();
            return ms.ToArray();
        }

        public static Simulation LoadSnapshot(byte[] data, Tunables tunables)
        {
            var sim = new Simulation(tunables);
            sim.ApplySnapshot(data);
            return sim;
        }

        /// <summary>Overwrites this sim's state with a snapshot (a network client does this every update).</summary>
        public void ApplySnapshot(byte[] data)
        {
            using var r = new BinaryReader(new MemoryStream(data));
            int version = r.ReadInt32();
            if (version != SnapshotVersion) throw new InvalidDataException($"snapshot version {version}, expected {SnapshotVersion}");
            Tick = r.ReadInt32(); CombatTick = r.ReadInt32(); Turn = r.ReadInt32(); Phase = (Phase)r.ReadInt32();
            PhaseTicksLeft = r.ReadInt32(); Winner = r.ReadInt32(); nextProjectileId = r.ReadInt32();
            DraftActive = r.ReadBoolean(); DraftsThisRound = r.ReadInt32(); freezesThisRound = r.ReadInt32();
            rng = r.ReadUInt64();
            foreach (var f in Fighters)
            {
                f.Position = ReadVec(r); f.Facing = ReadVec(r);
                f.Hp = r.ReadInt32(); f.MaxHp = r.ReadInt32(); f.ActionPoints = r.ReadInt32(); f.Action = (ActionKind)r.ReadInt32(); f.ActionTick = r.ReadInt32();
                f.Frames = new ActionDef(r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
                f.ActionReach = r.ReadDouble();
                f.ActionDir = ReadVec(r); f.ActionHitLanded = r.ReadBoolean(); f.LockedOn = r.ReadBoolean();
                f.LockBlockedTicks = r.ReadInt32(); f.RelockWindowTicks = r.ReadInt32();
                f.Upgrades = r.ReadUInt64(); f.ActionsThisTurn = r.ReadInt32(); f.FreeDashUsed = r.ReadBoolean(); f.CarriedPoints = r.ReadInt32(); f.DraftPick = r.ReadInt32();
                f.Offer0 = r.ReadInt32(); f.Offer1 = r.ReadInt32(); f.Offer2 = r.ReadInt32();
            }
            int count = r.ReadInt32();
            Projectiles.Clear();
            for (int i = 0; i < count; i++)
            {
                Projectiles.Add(new Projectile
                {
                    Id = r.ReadInt32(), Owner = r.ReadInt32(), Position = ReadVec(r), Velocity = ReadVec(r), Radius = r.ReadDouble(), AgeTicks = r.ReadInt32(),
                });
            }
        }

        static void Write(BinaryWriter w, Vec3 v) { w.Write(v.X); w.Write(v.Y); w.Write(v.Z); }
        static Vec3 ReadVec(BinaryReader r) => new Vec3(r.ReadDouble(), r.ReadDouble(), r.ReadDouble());
    }
}
