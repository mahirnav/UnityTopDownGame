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
        public const float TickDt = 1f / TickRate;

        // Turn structure
        public int TurnTicks = 480;            // 8 s of combat
        public int FreezeTicks = 180;          // 3 s frozen
        public int ActionPointsPerTurn = 3;

        // Fighters
        public int MaxHp = 100;
        public float FighterRadius = 0.4f;
        public float MoveSpeed = 5f;           // m/s, walking (not during actions)

        // Melee
        public int MeleeCost = 1;
        public int MeleeStartup = 10;
        public int MeleeActive = 6;
        public int MeleeRecovery = 20;
        public int MeleeDamage = 20;
        public float MeleeRange = 1.6f;        // m from center, hitbox reach
        public float MeleeArcDegrees = 120f;   // total arc in front of the fighter

        // Dash
        public int DashCost = 1;
        public int DashStartup = 3;
        public int DashActive = 12;
        public int DashRecovery = 8;
        public float DashSpeed = 18f;          // m/s during active ticks

        // Ranged
        public int RangedCost = 1;
        public int RangedStartup = 12;
        public int RangedActive = 1;
        public int RangedRecovery = 18;
        public int RangedDamage = 10;
        public float ProjectileSpeed = 14f;    // m/s
        public float ProjectileRadius = 0.15f;
        public int ProjectileLifetimeTicks = 180;
        public float ProjectileSpawnHeight = 1f;

        // Arena (oval pit, origin at center)
        public float ArenaRadiusX = 14f;
        public float ArenaRadiusZ = 10f;
        public float PillarRadius = 0.9f;
        public float PillarOffsetX = 5.5f;     // 4 pillars at (+-X, +-Z)
        public float PillarOffsetZ = 4f;
        public float StartOffsetX = 9f;        // fighters start at (-X, 0) and (+X, 0)

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
        public bool IsActive(int actionTick) => actionTick >= Startup && actionTick < Startup + Active;
    }
}
