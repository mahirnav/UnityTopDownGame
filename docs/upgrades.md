# Upgrade pool: draft for Nick to cut

## P1 decision (Nick, 2026-10-17)

P1 tests **mid-round drafting of core-action upgrades only** (no starting ability yet). **Built** (tests pass; not yet playtested). Numbers are in `tunables.txt`.

| Upgrade | Effect | When it applies |
|---|---|---|
| Heavy Blade | Melee damage +8 | Read when the hit lands (affects frozen swings) |
| Long Reach | Melee range +0.6 m | Locked when the swing starts |
| Big Bolt | Projectile radius ×2 | Locked when the bolt is fired |
| Fortification | +25 max HP, +10 HP immediately (amount to retest) | Immediately |
| Free Step | First dash each turn costs 0 | Future turns |
| Opening Move | First action each turn has −50% startup | Locked when the action starts |
| Exhaustion Punish | +20% damage against a target with 0 action points | Read when the hit lands |
| Carry the Flame | Each unspent point at turn end: small damage bonus next turn (points don't carry) | Next turn |

Working rules for P1 (leaning, revisit after play): values are either **read when a hit lands** (damage,
damage taken, HP, lifesteal) or **locked when an action starts** (timing, range, arc, dash distance, projectile
size/speed). 3 drafts per round on the first 3 freezes, upgrades reset each round, each player gets their own 3
offers from upgrades they don't own, seeded draw. **The current 8 are placeholders** to be rebalanced or replaced.

---

Tick `[x]` to **keep**, delete the line (or write `CUT`) to drop. Add notes inline. Numbers are starting
values; every one becomes a tunable. P1 needs ~8; the rest can rotate in later.

**Rules assumed for v0** (from the handoff, change any of them):
- ~~Drafts are mirrored~~ Decided 2026-10-18: each player gets their own 3 offers (never owned ones).
- An upgrade applies to **future actions only**; an action already in progress keeps its old stats.
- Upgrades last the whole round. Whether they reset between rounds is open (see questions at the end).

**Size** = how much work: **S** = number change on your stats only (an hour or less) ·
**M** = small new sim rule (a few hours with tests) · **L** = new mechanic or new state (a day or more).

---

## Melee

| Keep | Name | Effect | Size | Notes / risk |
|---|---|---|---|---|
| [ ] | Heavy Blade | Melee damage +8 | S | Baseline pick; may be too plain to be exciting. |
| [ ] | Long Reach | Melee range +0.6 m | S | Strong vs dash-in play. |
| [ ] | Wide Arc | Melee arc 120° → 200° | S | Makes strafing around melee useless. |
| [ ] | Quick Draw | Melee startup −4 ticks | S | Faster swings are hard to react to; watch the 166 ms latency budget online. |
| [ ] | Lingering Edge | Melee active window +4 ticks | S | Easier to land; good "noob" pick. |
| [ ] | Short Recovery | Melee recovery −8 ticks | S | Shorter recovery means more actions fit in the double burst. |
| [ ] | Twin Strike | Melee hits twice (second hit 50% dmg, 6 ticks later) | M | Needs per-action multi-hit; the frame data supports it. |
| [ ] | Shove | Melee hit knocks the target back 2 m | M | Knockback must respect walls and pillars. |
| [ ] | Wall Slam | Knockback into a wall/pillar adds +10 damage | M | Needs Shove (or knockback from another source). |
| [ ] | Stagger | Melee hit cancels the target's action in its startup | M | Very strong; turns melee into a counter. Interacts with "actions finish after the freeze". |
| [ ] | Bloodletter | Melee hits heal you 30% of damage dealt | S | Lifesteal; can stall rounds. |
| [ ] | Executioner | Melee does +50% to targets below 30% HP | S | Good setup for the P2 downed state. |
| [ ] | Lunge | Melee moves you forward 1.2 m during startup | M | Gap-closer; partly overlaps with Dash. |
| [ ] | Riposte Window | Melee during the opponent's melee startup does double damage | M | Rewards reads, may be too hard to see online. |

## Dash

| Keep | Name | Effect | Size | Notes / risk |
|---|---|---|---|---|
| [ ] | Long Dash | Dash distance +50% | S | |
| [ ] | Snap Dash | Dash startup 3 → 0, recovery −4 | S | Instant escape; very strong. |
| [ ] | Phase Step | Invulnerable during dash active ticks | S | **Known risk:** i-frames vs fire ring = infinite survival (P2). Needs a cap or fire exception. |
| [ ] | Shoulder Charge | Dash contact deals 12 damage and stops the dash | M | Turns dash into an attack; collides with Shove/Wall Slam nicely. |
| [ ] | Free Step | First dash each turn costs 0 AP | S | Effectively +1 AP, but only for movement; safer than raw +1 AP. |
| [ ] | Double Dash | Dash can be pressed again during recovery for 0 AP (once) | M | Strong mobility. |
| [ ] | Afterimage | Leaves a decoy at your start point for 1 s that breaks lock-on | M | Mind-game pick; needs the lock-on target to be a position, not a fighter. |
| [ ] | Steady Feet | Dash ends exactly at your aim point (no sliding along walls) | S | Quality-of-life; low impact. |
| [ ] | Dash Reload | Dashing refunds a ranged shot's cost if used within 0.5 s after | M | Combo enabler. |

## Ranged

| Keep | Name | Effect | Size | Notes / risk |
|---|---|---|---|---|
| [ ] | Heavy Bolt | Ranged damage +6 | S | |
| [ ] | Fast Bolt | Projectile speed +50% | S | Harder to dodge; strengthens ranged vs melee (known risk). |
| [ ] | Big Bolt | Projectile radius ×2 | S | Easier to hit. |
| [ ] | Quick Shot | Ranged startup −6 ticks | S | |
| [ ] | Spread Shot | Fires 3 bolts in a 20° spread, 60% damage each | M | Dangerous up close if all 3 hit; consider "max 1 hit per volley". |
| [ ] | Ricochet | Bolts bounce once off walls and pillars | M | Pillars stop being safe cover; changes the arena a lot. |
| [ ] | Piercing | Bolts pass through pillars | S | Same as above but simpler; probably too strong. |
| [ ] | Homing | Bolts turn up to 60°/s toward the target | M | Dodging becomes about timing, not position. |
| [ ] | Slowing Bolt | Hit target moves 40% slower for 1.5 s | M | Needs a status-effect timer in fighter state. |
| [ ] | Delayed Bolt | Bolt pauses 0.5 s after spawning, then flies | M | Pairs interestingly with the freeze; a trap tool. |
| [ ] | Lob | Bolt arcs over pillars (ignores pillar blocking) | M | Needs vertical motion; gravity in the sim. |
| [ ] | Point Blank | Ranged does +100% within 3 m | S | Lets ranged builds brawl. |

## Action points and turn economy

| Keep | Name | Effect | Size | Notes / risk |
|---|---|---|---|---|
| [ ] | Fourth Action | +1 action point per turn | S | **Known risk:** stacking. Suggest max one copy, or not in the pool at all. |
| [ ] | Saved Breath | 1 unspent point carries into the next turn | S | Counters "dump before the freeze"; makes the double burst bigger (4+3). |
| [ ] | Momentum | Landing a hit refunds 1 point (once per turn) | S | Rewards accuracy; snowball risk. |
| [ ] | Opening Move | Your first action each turn has −50% startup | S | Encourages acting early instead of dumping at the end. |
| [ ] | Closer | Your last action before the freeze costs 0 | M | Makes the turn-boundary burst a deliberate build. |
| [ ] | Second Wind | At half the turn timer, gain +1 point if you have 0 | M | Rewards spending early. |
| [ ] | Efficiency | Every third action you take costs 0 | M | Counter across turns. |
| [ ] | Overclock | All actions 25% faster, but −1 point per turn | S | A real trade-off; good for drafting decisions. |

## Defense and HP

| Keep | Name | Effect | Size | Notes / risk |
|---|---|---|---|---|
| [ ] | Thick Hide | Max HP +25 (and heal 25) | S | |
| [ ] | Regeneration | Heal 6 at the start of each turn | S | Slows rounds; pairs badly with no-execute endings until P2. |
| [ ] | Plating | Take −3 damage from every hit | S | Hard-counters Spread Shot / Twin Strike. |
| [ ] | Bulwark | First hit each turn deals 0 | S | Simple, readable, strong. |
| [ ] | Thorns | Melee attackers take 6 damage when they hit you | S | Anti-melee. |
| [ ] | Brace | Standing still for 0.5 s gives −50% damage until you move | M | New state; encourages a defensive stance. |
| [ ] | Last Stand | Below 25% HP, your actions cost 0 for one turn (once per round) | M | Comeback mechanic. |

## Movement and positioning

| Keep | Name | Effect | Size | Notes / risk |
|---|---|---|---|---|
| [ ] | Swift | Walk speed +30% | S | |
| [ ] | Footwork | You can walk (50% speed) during melee and ranged startup | S | Removes the "rooted" default for you. |
| [ ] | Pillar Hugger | +20% damage for 1 s after breaking line of sight | M | Rewards pillar play. |
| [ ] | Tracker | Lock-on survives 1 s behind pillars | S | Weakens pillars as defense. |
| [ ] | Smaller Target | Your hitbox radius −25% | S | Invisible to the opponent; might feel unfair. |

## Freeze and turn-boundary specials (unique to RATS)

| Keep | Name | Effect | Size | Notes / risk |
|---|---|---|---|---|
| [ ] | Frozen Insight | During the freeze, see the opponent's action points and current action phase | S | Info is already partly visible; tweak with the pips leaning. |
| [ ] | Cold Start | Actions started in the last 1 s before the freeze deal +30% damage | S | Leans into the turn-boundary burst on purpose. |
| [ ] | Thaw Shock | When time resumes, your in-flight bolts get +50% speed | S | Freeze becomes a setup tool. |
| [ ] | Redirect | During the freeze, re-aim one bolt you have in flight | L | Needs freeze-time input; very on-theme. Probably later. |
| [ ] | Time Theft | The opponent's next turn is 1 s shorter | S | Directly attacks their action economy. |

---

## Open questions for you (and your friend)
1. **Stacking:** can you take the same upgrade twice? (Simplest: no duplicates; it also handles the "+1 AP" risk.)
2. **Round reset:** do upgrades persist across the best-of-5 rounds or reset each round?
3. **Pool draw:** pure random 3 from the remaining pool, or one from each category (offense / mobility / economy)?
4. **Mirrored vs separate offers:** decided: separate offers per player.
5. **Draft cadence:** decided: the first 3 freezes of each round, then none.

## How it would be built (for reference)
Most S-sized upgrades are just changes to a per-fighter stats block computed from tunables plus picked
upgrades, so the sim reads `fighter.Stats.MeleeDamage` instead of `T.MeleeDamage`. The draw uses a seeded
random generator stored in the sim, picks travel as inputs (so they work online), and the "future actions
only" rule means each action copies the stats it needs when it starts.
