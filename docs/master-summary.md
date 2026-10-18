# RATS: master summary (October 17, 2026)

Self-contained snapshot for outside feedback. Written by Claude (the coding assistant) for Nick (designer).

---

## 1. The game in one paragraph

RATS is a **1v1 multiplayer PvP third-person gladiator arena game**. Combat runs in short real-time turns.
Each turn, each player gets **3 action points** to spend on **melee, dash, or ranged** attacks. Points are spent
the moment a press is accepted and expire at turn end. When a turn ends, **time freezes exactly mid-action**
(projectiles, dashes and swings hang in place) for a pause. In the full design that pause is where both players
draft 1 of 3 upgrades. Then time resumes and in-progress actions finish. Rounds will be best of 5 and end via a
downed/execute system plus a shrinking ring of fire. Aesthetic target: gritty, old-school PS2 / classic-WoW 3D.
Arena: oval stone-and-sand pit, 4 stone pillars, low walls.

**Core question the prototype must answer:** does limiting players to 3 actions per short turn, then freezing
time, create fun fights?

## 2. Team and how we work

- **Nick** designs, playtests and gives blunt notes. **Claude** writes the code and the tests. A friend of Nick's
  co-develops through a shared private GitHub repo.
- Rules: challenge weak ideas; never change a design rule without Nick's approval; keep it small (no frameworks,
  no speculative abstraction); **every exploit found becomes an automated regression test**; "tests pass" and
  "Nick playtested it" are always reported separately; no paid assets until the prototype is fun (capsules and
  free Mixamo placeholders only).

## 3. Where it stands

| Stage | Status |
|---|---|
| Task 1: engine choice | **Unity 6.3 LTS (6000.3.26f1)** chosen. Godot dropped. |
| P0: core turn loop | **Done. Nick playtested: "feels very great for a first implementation"**, fun relative to its stage. |
| Networked 1v1 (pulled forward from P3) | **Built and tested**: two game copies connect, stay in sync, render correctly. Not yet played over the internet between Nick and his friend. |
| Match recording / replays | **Done.** Every round can be replayed exactly; exploit replays become tests. |
| P1: drafting | **Next.** Waiting on Nick's upgrade-pool decisions (section 8). |
| P2: downed / execute / fire ring / best of 5 | Not started. |
| P3: hidden drafts, matchmaking | Not started. Matchmaking (Steam / Unity Lobby+Relay / Epic) to be chosen later. |

## 4. The game as it plays right now (P0)

You (red capsule) vs a simple bot or a second human (blue capsule) in the oval pit.

**Turn loop:** 8 s of real-time combat, then a 3 s freeze where everything stops mid-action (no drafting yet),
then the next turn. HP 100; the round ends at 0 (simultaneous knockouts = draw). No downed state yet.

**Controls:** WASD move (camera-relative) · J melee · K ranged · Space dash · Tab lock-on · Q/E orbit camera when
unlocked · Enter restart · F5 reload tunables · F9 save replay · Esc multiplayer menu.

**Current numbers** (all live in one editable file; 60 ticks = 1 second):

| Thing | Value |
|---|---|
| Turn / freeze | 480 ticks (8 s) / 180 ticks (3 s) |
| Action points per turn | 3 (unspent points expire at the freeze) |
| HP | 100 |
| Walk speed | 5 m/s (rooted during melee and ranged) |
| Melee | cost 1 · startup 10 / active 6 / recovery 20 ticks · 20 dmg · 1.6 m reach · 120° arc · one hit per swing |
| Dash | cost 1 · startup 3 / active 12 / recovery 8 ticks · 18 m/s → 3.6 m |
| Ranged | cost 1 · startup 12 / active 1 / recovery 18 ticks · 10 dmg · 14 m/s straight bolt · pillars and walls block it |
| Arena | oval 28 × 20 m, 4 pillars (radius 0.9 m) at (±5.5, ±4) |

**Visual readability:** capsule color shows the frame-data phase (yellow = startup, red = active, dark =
recovery). The opponent's remaining action points show as pips over their head. Over-the-shoulder lock-on camera.

**Defaults Claude chose that Nick hasn't ruled on yet** ("my default, your call"):
1. Rooted during melee and ranged; only dash moves you.
2. No cancels, no input buffering; a press while busy is dropped and costs nothing.
3. Presses during the freeze are ignored.
4. Same-tick presses: melee > ranged > dash.
5. Facing is locked when an action starts (toward the opponent when locked on).
6. Lock-on starts on; a pillar breaks it; Tab re-locks only with line of sight.
7. Melee: one hit per swing.
8. Ranged: straight line; pillars and walls stop it.
9. No best-of-5 yet; Enter restarts the round.

**Measured finding: turn-boundary "double burst".** Spending 3 actions right before the freeze and 3 right after
gives **6 actions in 115 combat ticks (1.9 s)**, versus 3 per 8 s normally. Frame data spaces actions by their
duration, so it's far less extreme than the feared "6 actions in 0.5 s", but it exists. Not yet judged in play.

## 5. Architecture (agreed rules, enforced by tests where possible)

1. A plain C# **simulation owns all gameplay state** and runs on a fixed 60 Hz tick. It has no Unity dependency
   and is tested from the terminal without Unity.
2. No engine physics for gameplay; movement and collisions are computed by the sim.
3. **Visuals only read sim state.** Animations, particles and timers never drive gameplay.
4. **Actions are frame data** (startup / active / recovery ticks, cost, hitbox). Hits come from frame data.
5. Every fighter is driven by an **input source** (keyboard, bot, script, network). The two fighters are symmetric.
6. **Inputs are the only way into the sim:** one small input frame (9 bytes) per fighter per tick.
7. All tunable numbers live in one text file the designer edits; F5 reloads it in-game.
8. Fixed per-tick order: inputs → action starts → movement → collisions → damage → down checks → timers.
9. Phases (combat / freeze / round over) are explicit sim state, not engine pause.
10. **Multiplayer is host-authoritative:** clients send inputs, the host runs the only sim and sends state
    snapshots. No rollback, no lockstep.
11. No gameplay state outside the sim; randomness, when needed, is a seeded generator stored in sim state.
12. **Sim math is double precision.** Unity's runtime does float math differently from .NET; with floats a
    recorded match diverged at tick 58. With doubles a 4,639-tick match is bit-identical in both.

**Multiplayer today:** direct connection by IP over Tailscale (a free private network), port 7777, one opponent.
The host's menu lists its addresses. No client-side prediction yet, so the joining player's own movement lags by
the network round trip; to be judged in play. Connection setup is kept separate from the game protocol so real
matchmaking can replace it later without touching gameplay.

**Replays:** every round on the host/practice machine records the tunables plus both players' inputs
(~1 KB/s). It auto-saves at round end, restart and quit; F9 saves on demand. A replay file dropped into the test
folder is replayed and checked on every test run, which is how exploits become regression tests.

**Tests:** 37 automated sim/network tests (about 2 s from the terminal), plus Unity batch checks: a play-mode smoke
test and headless screenshots proving frozen frames are pixel-identical. Notable tests: a frozen projectile and a
frozen dash resume bit-identical to an unfrozen run; snapshot round-trip; every state field is in the snapshot;
recorded inputs reproduce a full match; real host/client over localhost TCP.

## 6. Known risks (to test, not pre-solve)

- Turn-boundary double burst (measured above).
- Dumping all actions right before the freeze; drafting every turn feeling exhausting.
- Stacking "+1 action point" upgrades; dash invulnerability vs the fire ring = infinite survival.
- Ranged downs making melee pointless; simultaneous downs on the same tick.
- Network latency on the joining player (no prediction yet).

## 7. Roadmap

1. Nick and his friend play online over Tailscale; notes and replays come back as fixes and tests.
2. **P1:** drafting during the freeze from a fixed pool of ~8 upgrades (mirrored offers for now; may not stay
   mirrored in the final game). Picks travel as inputs, so drafting works online automatically.
3. **P2:** downed state, executions, shrinking fire ring, best of 5.
4. **P3:** hidden drafts, real matchmaking.
5. Side items when they start to hurt: gamepad support, smoothing the joining player's view, animations driven by
   frame data (Mixamo humanoid back in).

## 8. Upgrade pool (draft for Nick to cut)

**Assumed v0 rules:** mirrored offers (both see the same 3, pick blind); an upgrade applies to **future actions
only** (an action in progress keeps its old stats); upgrades last the round. **Size:** S = a number change
(under an hour) · M = a small new rule (a few hours) · L = a new mechanic (a day or more).

### Melee
| Name | Effect | Size | Notes |
|---|---|---|---|
| Heavy Blade | Melee damage +8 | S | Plain baseline. |
| Long Reach | Melee range +0.6 m | S | Strong vs dash-in play. |
| Wide Arc | Arc 120° → 200° | S | Kills strafing around melee. |
| Quick Draw | Melee startup −4 ticks | S | Hard to react to, especially online. |
| Lingering Edge | Active window +4 ticks | S | Easier to land. |
| Short Recovery | Recovery −8 ticks | S | Feeds the double burst. |
| Twin Strike | Second hit at 50% dmg, 6 ticks later | M | Multi-hit per action. |
| Shove | Melee hit knocks back 2 m | M | Must respect walls/pillars. |
| Wall Slam | Knockback into wall/pillar: +10 dmg | M | Needs Shove or another knockback. |
| Stagger | Hit cancels target's action during its startup | M | Very strong counter tool. |
| Bloodletter | Heal 30% of melee damage dealt | S | Can stall rounds. |
| Executioner | +50% vs targets under 30% HP | S | Sets up P2 downed play. |
| Lunge | Move 1.2 m forward during startup | M | Overlaps with dash. |
| Riposte Window | Double damage if hitting during opponent's melee startup | M | Reads; may be hard to see online. |

### Dash
| Name | Effect | Size | Notes |
|---|---|---|---|
| Long Dash | Distance +50% | S | |
| Snap Dash | Startup 3 → 0, recovery −4 | S | Instant escape. |
| Phase Step | Invulnerable during dash active ticks | S | Known risk vs fire ring. |
| Shoulder Charge | Dash contact: 12 dmg, stops dash | M | Dash becomes an attack. |
| Free Step | First dash each turn costs 0 | S | Safer than raw +1 AP. |
| Double Dash | Re-dash once during recovery for free | M | Strong mobility. |
| Afterimage | Decoy at start point breaks lock-on for 1 s | M | Mind games. |
| Steady Feet | Dash ends exactly at aim point | S | Quality of life. |
| Dash Reload | Ranged shortly after a dash is refunded | M | Combo enabler. |

### Ranged
| Name | Effect | Size | Notes |
|---|---|---|---|
| Heavy Bolt | Damage +6 | S | |
| Fast Bolt | Speed +50% | S | Strengthens ranged vs melee. |
| Big Bolt | Radius ×2 | S | |
| Quick Shot | Startup −6 ticks | S | |
| Spread Shot | 3 bolts, 20° spread, 60% dmg each | M | Maybe cap at 1 hit per volley. |
| Ricochet | Bounces once off walls/pillars | M | Pillars stop being safe. |
| Piercing | Passes through pillars | S | Probably too strong. |
| Homing | Turns up to 60°/s toward target | M | |
| Slowing Bolt | Hit target 40% slower for 1.5 s | M | Needs status timers. |
| Delayed Bolt | Pauses 0.5 s after spawning, then flies | M | Trap tool; pairs with the freeze. |
| Lob | Arcs over pillars | M | Needs vertical motion. |
| Point Blank | +100% damage within 3 m | S | Ranged brawler. |

### Action points and turn economy
| Name | Effect | Size | Notes |
|---|---|---|---|
| Fourth Action | +1 AP per turn | S | Stacking risk: max one copy or leave it out. |
| Saved Breath | 1 unspent AP carries over | S | Bigger double burst (4+3). |
| Momentum | Landing a hit refunds 1 AP (once/turn) | S | Snowball risk. |
| Opening Move | First action each turn: −50% startup | S | Rewards acting early. |
| Closer | Last action before the freeze costs 0 | M | Turn-boundary build. |
| Second Wind | At half-turn, +1 AP if you have 0 | M | Rewards early spending. |
| Efficiency | Every third action costs 0 | M | |
| Overclock | Actions 25% faster, but −1 AP per turn | S | Real trade-off. |

### Defense and HP
| Name | Effect | Size | Notes |
|---|---|---|---|
| Thick Hide | Max HP +25 (and heal 25) | S | |
| Regeneration | Heal 6 each turn start | S | Slows rounds. |
| Plating | −3 damage from every hit | S | Counters multi-hit. |
| Bulwark | First hit each turn deals 0 | S | Readable, strong. |
| Thorns | Melee attackers take 6 | S | Anti-melee. |
| Brace | Standing still 0.5 s: −50% damage until you move | M | Defensive stance. |
| Last Stand | Under 25% HP: actions cost 0 for a turn (once/round) | M | Comeback mechanic. |

### Movement and positioning
| Name | Effect | Size | Notes |
|---|---|---|---|
| Swift | Walk speed +30% | S | |
| Footwork | Walk at 50% during melee/ranged startup | S | Removes the rooted default for you. |
| Pillar Hugger | +20% damage for 1 s after breaking line of sight | M | Rewards pillar play. |
| Tracker | Lock-on survives 1 s behind pillars | S | Weakens pillars. |
| Smaller Target | Hitbox radius −25% | S | Invisible to the opponent; may feel unfair. |

### Freeze and turn-boundary specials (unique to RATS)
| Name | Effect | Size | Notes |
|---|---|---|---|
| Frozen Insight | See opponent's AP and action phase during the freeze | S | |
| Cold Start | Actions started in the last 1 s before the freeze: +30% dmg | S | Leans into the burst. |
| Thaw Shock | Your in-flight bolts +50% speed on resume | S | Freeze as setup. |
| Redirect | Re-aim one in-flight bolt during the freeze | L | Very on-theme; later. |
| Time Theft | Opponent's next turn is 1 s shorter | S | Attacks their economy. |

### Open questions on drafting
1. Can the same upgrade be taken twice? (Simplest: no duplicates; also handles the "+1 AP" risk.)
2. Do upgrades persist across best-of-5 rounds or reset each round?
3. Draw: pure random 3, or one from each category (offense / mobility / economy)?
4. Mirrored vs separate offers in the long run?
5. Draft every turn, every other turn, or only after a hit lands (fatigue risk)?

**Implementation note:** most S-sized upgrades are stat changes on a per-fighter stats block; the draw uses a
seeded generator stored in the sim, picks are network inputs, and "future actions only" means each action copies
the stats it needs when it starts.

---

## 9. What we'd like from you (prompt for an outside reviewer)

Please act as a combat/systems designer reviewing this prototype. Constraints for any suggestion:
- It must work with the existing mechanics (melee / dash / ranged as frame data, 3 action points, ~8 s turns,
  freeze, lock-on, 4 pillars, oval walls), and apply to **future actions only**.
- Give each suggestion a size (S / M / L as defined above) and the main abuse risk.
- Keep the game readable in a 1v1 at ~60–150 ms network latency.

Questions:
1. Pick the **8 upgrades** you'd ship for P1 and say why. Aim for distinct playstyles and meaningful draft choices.
2. Which upgrades are **degenerate or dominant**, alone or in combination? Which pairs should never appear together?
3. Which upgrades best support the core question (3 actions per turn + time freeze) and which ignore it?
4. Propose up to **10 new upgrades** that lean on the freeze, the turn boundary or the action-point economy.
5. Your answers to the 5 open drafting questions, with reasoning.
6. Anything in the current defaults (section 4) or risks (section 6) you would change before adding drafting.
