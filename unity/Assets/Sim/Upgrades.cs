using System;

namespace Rats.Sim
{
    /// <summary>The P1 upgrade pool. Numbers live in Tunables; see docs/upgrades.md for the design notes.</summary>
    public enum UpgradeId
    {
        HeavyBlade,        // melee damage, read when the hit lands
        LongReach,         // melee reach, locked when the swing starts
        BigBolt,           // projectile radius, locked when fired
        Fortification,     // max HP up, partial heal, applied when the draft ends
        FreeStep,          // first dash each turn costs nothing
        OpeningMove,       // first action each turn has shorter startup, locked when it starts
        ExhaustionPunish,  // bonus damage against a target with 0 points, read when the hit lands
        CarryTheFlame,     // unspent points become bonus damage next turn
    }

    public static class Upgrades
    {
        /// <summary>Everything a draft can offer, in a fixed order (offers are drawn from it with the sim's seeded RNG).</summary>
        public static readonly UpgradeId[] Pool =
        {
            UpgradeId.HeavyBlade, UpgradeId.LongReach, UpgradeId.BigBolt, UpgradeId.Fortification,
            UpgradeId.FreeStep, UpgradeId.OpeningMove, UpgradeId.ExhaustionPunish, UpgradeId.CarryTheFlame,
        };

        public static string Name(UpgradeId u)
        {
            switch (u)
            {
                case UpgradeId.HeavyBlade: return "Heavy Blade";
                case UpgradeId.LongReach: return "Long Reach";
                case UpgradeId.BigBolt: return "Big Bolt";
                case UpgradeId.Fortification: return "Fortification";
                case UpgradeId.FreeStep: return "Free Step";
                case UpgradeId.OpeningMove: return "Opening Move";
                case UpgradeId.ExhaustionPunish: return "Exhaustion Punish";
                case UpgradeId.CarryTheFlame: return "Carry the Flame";
                default: return u.ToString();
            }
        }

        public static string Describe(UpgradeId u, Tunables t)
        {
            switch (u)
            {
                case UpgradeId.HeavyBlade: return $"Melee hits deal +{t.HeavyBladeDamage} damage (frozen swings too).";
                case UpgradeId.LongReach: return FormattableString.Invariant($"Melee reach +{t.LongReachBonus} m (swings started after the pick).");
                case UpgradeId.BigBolt: return FormattableString.Invariant($"Bolts are {t.BigBoltRadiusMultiplier}x bigger (bolts fired after the pick).");
                case UpgradeId.Fortification: return $"+{t.FortificationMaxHp} max HP and heal {t.FortificationHeal} now.";
                case UpgradeId.FreeStep: return "Your first dash each turn costs no action point.";
                case UpgradeId.OpeningMove: return $"Your first action each turn has {100 - t.OpeningMoveStartupPercent}% less startup.";
                case UpgradeId.ExhaustionPunish: return $"+{t.ExhaustionPunishPercent}% damage to a target with 0 action points.";
                case UpgradeId.CarryTheFlame: return $"Each point left unspent at turn end: +{t.CarryTheFlameDamagePerPoint} damage per hit next turn.";
                default: return "";
            }
        }
    }
}
