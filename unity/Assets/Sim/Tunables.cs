using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Rats.Sim
{
    /// <summary>
    /// Every tunable number. Defaults here; Nick overrides them in
    /// Assets/StreamingAssets/tunables.txt ("Name = value  # comment").
    /// Each public field is a key, so adding a tunable = adding a field + a line in the file.
    /// </summary>
    public sealed class Tunables
    {
        public const int TickRate = 60;
        public const double TickDt = 1.0 / TickRate;

        // Turn structure
        public int TurnTicks = 480;            // 8 s of combat
        public int FreezeTicks = 180;          // 3 s frozen
        public int ActionPointsPerTurn = 3;

        // Fighters
        public int MaxHp = 100;
        public double FighterRadius = 0.4;
        public double MoveSpeed = 5;           // m/s, walking (not during actions)
        public double StartupWalkMultiplier = 0; // walk speed fraction during melee/ranged startup (0 = rooted)
        public double BackpedalMultiplier = 0.6; // speed moving straight back from where you face (sideways = full)

        // Facing and backstabs
        public double IdleTurnRateDeg = 720;    // deg/s turning toward your target or run direction (0 = instant)
        public double StartupTurnRateDeg = 180; // deg/s tracking a locked target during attack startup (0 = none)
        public double BackstabArcDegrees = 90;  // rear cone (total width) where melee counts as a backstab
        public int BackstabPercent = 50;        // bonus melee damage from behind (0 = off)

        // Rounds
        public int RoundsToWin = 3;            // best of 5
        public int RoundBreakTicks = 180;      // pause between rounds

        // Fire ring, downed, executions (P2). All timers run on combat time (they pause during freezes).
        public int RoundMaxTicks = 5400;       // 90 s: the ring is fully closed; higher HP% wins if nobody was finished
        public int FireStartTicks = 1800;      // the ring starts closing after 30 s
        public int FirePulseTicks = 15;        // fire hurts 4 times a second
        public int FireBaseDamage = 2;         // per pulse on first contact
        public int FireRampTicks = 60;         // +1 damage per pulse for every second spent in the fire
        public int DownedBleedTicks = 300;     // downed outside the fire: out after 5 s unless executed sooner
        public int FireBleedTicks = 90;        // downed in the fire: out after 1.5 s
        public double ExecuteRange = 1.8;      // m from center
        public int ExecuteTicks = 45;          // execution length; once started it always completes; free (no point cost)
        public int FinalStageTurnTicks = 0;    // turn length once the ring is closing (0 = same as TurnTicks)

        // Lock-on
        public int LockOnGraceTicks = 0;       // ticks of lost sight before lock breaks (0 = immediately)
        public int AutoRelockTicks = 0;        // after a break, lock returns if the target reappears within this (0 = off)

        // Melee
        public int MeleeCost = 1;
        public int MeleeStartup = 10;
        public int MeleeActive = 6;
        public int MeleeRecovery = 20;
        public int MeleeDamage = 20;
        public double MeleeRange = 1.6;        // m from center, hitbox reach
        public double MeleeArcDegrees = 120;   // total arc in front of the fighter

        // Dash
        public int DashCost = 1;
        public int DashStartup = 3;
        public int DashActive = 12;
        public int DashRecovery = 8;
        public double DashSpeed = 18;          // m/s during active ticks

        // Ranged
        public int RangedCost = 1;
        public int RangedStartup = 12;
        public int RangedActive = 1;
        public int RangedRecovery = 18;
        public int RangedDamage = 10;
        public double ProjectileSpeed = 14;    // m/s
        public double ProjectileRadius = 0.15;
        public int ProjectileLifetimeTicks = 180;
        public double ProjectileSpawnHeight = 1;

        // Arena (oval pit, origin at center)
        public double ArenaRadiusX = 14;
        public double ArenaRadiusZ = 10;
        public double PillarRadius = 0.9;
        public double PillarOffsetX = 5.5;     // 4 pillars at (+-X, +-Z)
        public double PillarOffsetZ = 4;
        public double StartOffsetX = 9;        // fighters start at (-X, 0) and (+X, 0)

        // Drafting (P1): offers appear during some freezes; picks apply when the freeze ends
        public int DraftsPerRound = 3;
        public int DraftInterval = 1;          // draft on every Nth freeze, starting with the first (1 = first freezes in a row)
        public int DraftFreezeTicks = 300;     // draft freezes last longer (5 s) than plain ones

        // Upgrades
        public int HeavyBladeDamage = 8;
        public double LongReachBonus = 0.6;    // m
        public double BigBoltRadiusMultiplier = 2;
        public int FortificationMaxHp = 25;
        public int FortificationHeal = 10;
        public int OpeningMoveStartupPercent = 50;   // first action's startup becomes this % of normal
        public int ExhaustionPunishPercent = 20;     // bonus damage vs a target with 0 action points
        public int CarryTheFlameDamagePerPoint = 2;  // per unspent point, per hit, for the next turn

        public ActionDef Melee => new ActionDef(MeleeCost, MeleeStartup, MeleeActive, MeleeRecovery);
        public ActionDef Dash => new ActionDef(DashCost, DashStartup, DashActive, DashRecovery);
        public ActionDef Ranged => new ActionDef(RangedCost, RangedStartup, RangedActive, RangedRecovery);

        public ActionDef Get(ActionKind kind)
        {
            switch (kind)
            {
                case ActionKind.Melee: return Melee;
                case ActionKind.Dash: return Dash;
                case ActionKind.Ranged: return Ranged;
                case ActionKind.Execute: return new ActionDef(0, ExecuteTicks, 1, 0);
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static IEnumerable<string> Keys
        {
            get { foreach (var f in Fields()) yield return f.Name; }
        }

        /// <summary>
        /// Parses "Name = value" lines over the defaults. Blank lines and # comments are ignored.
        /// Unknown names or bad numbers throw with the line number, so typos never pass silently.
        /// </summary>
        public static Tunables Parse(string text)
        {
            var t = new Tunables();
            var lines = text.Replace("\r", "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                int hash = line.IndexOf('#');
                if (hash >= 0) line = line.Substring(0, hash);
                line = line.Trim();
                if (line.Length == 0) continue;

                int eq = line.IndexOf('=');
                if (eq < 0) throw new FormatException($"tunables line {i + 1}: expected 'Name = value'");
                var name = line.Substring(0, eq).Trim();
                var value = line.Substring(eq + 1).Trim();
                var field = typeof(Tunables).GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (field == null) throw new FormatException($"tunables line {i + 1}: unknown name '{name}'");
                try
                {
                    field.SetValue(t, System.Convert.ChangeType(value, field.FieldType, CultureInfo.InvariantCulture));
                }
                catch (Exception e) when (e is FormatException || e is OverflowException)
                {
                    throw new FormatException($"tunables line {i + 1}: '{value}' is not a valid {field.FieldType.Name} for {name}");
                }
            }
            return t;
        }

        /// <summary>Every value as "Name = value" lines that Parse reads back exactly (floats round-trip).</summary>
        public string ToText()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var f in Fields())
            {
                var v = f.GetValue(this);
                string text = v is double fl ? fl.ToString("R", CultureInfo.InvariantCulture)
                            : System.Convert.ToString(v, CultureInfo.InvariantCulture);
                sb.Append(f.Name).Append(" = ").Append(text).Append('\n');
            }
            return sb.ToString();
        }

        static IEnumerable<FieldInfo> Fields() =>
            typeof(Tunables).GetFields(BindingFlags.Public | BindingFlags.Instance);
    }

    public readonly struct ActionDef
    {
        public readonly int Cost, Startup, Active, Recovery;

        public ActionDef(int cost, int startup, int active, int recovery)
        {
            Cost = cost; Startup = startup; Active = active; Recovery = recovery;
        }

        public int Total => Startup + Active + Recovery;
        public ActionDef WithStartup(int startup) => new ActionDef(Cost, startup, Active, Recovery);
        public bool IsActive(int actionTick) => actionTick >= Startup && actionTick < Startup + Active;
    }
}
