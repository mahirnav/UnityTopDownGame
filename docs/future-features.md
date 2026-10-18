# RATS: future features and next-step options (October 18, 2026)

Companion to `master-summary.md` (read that first). Everything here is **not built yet**. Status labels:
**Approved** = Nick decided it, **Leaning** = likely direction to discuss, **Idea** = candidate only.
Nothing here changes a design rule without Nick's approval.

**Size:** S = under a day · M = a few days · L = a week or more (part-time pace, Claude writing the code).

---

## A. Already in motion

| Item | Status | Size | Notes |
|---|---|---|---|
| **Character model: Ninja Otoko** on both fighters (tinted per player) | Approved, Nick buying | M | Humanoid rig; separate parts (hood, mask, weapons) can be shown or hidden. |
| **Animations driven by frame data** | Approved | M | Idle / run / strafe / backpedal from movement; slash, roll (dash), throw (ranged), hit, downed, death, execution mapped to sim states. Clips are time-stretched to match startup/active/recovery; gameplay timing never changes. |
| Poly Haven katana in hand (or the pack's short swords) | Approved | S | Waiting on an FBX download (or Blender install). |
| **First online playtest** (Nick vs friend over Tailscale) | Approved | S | Friend installs Tailscale; host adds a firewall rule. Main unknowns: joining player's lag, whether 13 s turns drag vs a human. |

## B. Combat depth (melee first, then ranged)

| Item | Status | Size | Notes / main risk |
|---|---|---|---|
| **Hold-to-charge melee** (tap = light, hold = stronger, capped; 1 point at start) | Under evaluation | M | Open: what happens to a charge held across the freeze (bank it? release? lose it?). Risk: fresh points + banked charge at resume. |
| **Hit-stop** (a few frames of impact pause on hits) | Idea | S–M | Visual-only version is easy; a sim-level version changes timing and needs care online. |
| **Startup walking** (~50% speed during attack startup) | Leaning, tunable exists (off) | S | A/B in playtests. |
| **Lock-on grace / auto re-lock** behind posts | Leaning, tunables exist (off) | S | A/B in playtests. |
| **Ranged rebalance** | Approved to do later | S | Known weak; retune after melee settles. |
| **Turn length** (13 s now) | Open | S | Feels a few seconds long; judge vs a human first. |
| **Final-stage shorter turns** when the ring closes | Leaning, tunable exists (off) | S | First fix for endgame stalemates before any new mechanic. |
| Double-burst handling (6 actions around a freeze) | Open | S–M | Measure in human play before changing anything. |

## C. Progression (three layers)

| Item | Status | Size | Notes |
|---|---|---|---|
| **Real mid-round upgrade pool** (replace the 8 placeholders) | Approved direction | M | Small upgrades; mix of "helps now" (read when a hit lands, affects frozen attacks) and "helps later" (locked when an action starts). ~50 candidates are already written up. |
| **Starting ability** (one per match, own cooldown ~25–30 s, no point cost) | Leaning | M each | Physical gladiator tools, not magic: throwing axe, shield bash, net, grapple. Needs a shortlist from Nick. |
| **Between-round augments** (big, build-defining, risk/reward) | Leaning | L | Probably separate offers per player. Open: do they carry across rounds? |
| **Reroll** your 3 draft offers | Parked | S | As an ability/augment, not a default. |
| Hidden drafts (opponent can't see your picks) | Roadmap P3 | S–M | Currently both players can see picks. |
| Reveal offers at turn start, pick at the freeze | Idea | S | Creates "plan now, commit later" tension. |

## D. Multiplayer and release plumbing

| Item | Status | Size | Notes |
|---|---|---|---|
| **Client-side prediction / smoothing** for the joining player | Idea, do if lag hurts | M–L | The host is lag-free today; the joiner's own movement lags one round trip. |
| Host advantage fix (dedicated server or deliberate host input delay) | Open | M–L | Only matters for competitive play. |
| **Matchmaking** (Steam / Unity Lobby + Relay / Epic) | Approved for later | L | Replaces direct connect by IP; the protocol is already separated from connection setup. |
| Reconnect after a drop, rematch button | Idea | S–M | |
| Gamepad support | Idea | S–M | |
| Main menu, settings (volume, sensitivity, graphics), key rebinding | Idea | M | |
| Builds friends can download (itch.io or Steam playtest) | Idea | S–M | A Windows build script exists. |

## E. Look, sound and feel

| Item | Status | Size | Notes |
|---|---|---|---|
| **Audio pass** (swings, hits, footsteps, dash, fire, crowd, freeze/thaw, music) | Idea, big gap | M | No audio yet beyond the freeze tone. Free/cheap packs exist. Large effect on feel per hour spent. |
| Blood / impact VFX, weapon trails | Idea | S–M | |
| Character variety / cosmetics (second model, colors, hood/mask toggles) | Idea | S–L | Ninja Otoko's modular parts give cheap variety. |
| More arenas or arena variants (time of day via other HDRIs) | Idea | S per sky, L per arena | Swapping the HDRI is nearly free. |
| Camera polish (kill cam on execution, round-end orbit) | Idea | S–M | |
| Tutorial / onboarding for new players | Idea | M | The turn + freeze loop needs explaining. |

## F. Claude's recommendation for the next three steps

1. **Online playtest with the friend** (S). It's the cheapest way to answer the core question with a real
   opponent, and it decides several open items at once: turn length, lag, double burst, ranged weakness.
2. **Ninja Otoko + frame-data animations** (M). Already in motion. Beyond looks, it's a gameplay readability
   upgrade: reading the opponent's startup (wind-up) is central to this game, and capsules hide it.
3. **Then choose between an audio pass and melee depth** (charge + hit-stop), based on playtest notes. Audio is
   the bigger feel gain per hour; melee depth is the bigger design question.

Hold the real upgrade pool, starting abilities and augments until humans have played P1 + P2 a few times:
their design depends on what fights actually feel like.

---

## Questions for an outside reviewer

Act as a game producer and combat designer. Constraints: part-time solo designer + AI coder, small budget,
1v1 online, ~60–150 ms latency, Unity Built-in pipeline.

1. Given the status above, what are the **best next 3–5 steps**, in order, and why? Do you agree with section F?
2. What's the **minimum** that must exist before inviting a friend group to play regularly
   (menus, audio, builds, matchmaking)?
3. Which items in B–E are **traps** (high effort, low payoff at this stage)?
4. For hold-to-charge melee: what should happen to a charge held across the freeze?
5. Starting abilities: propose a shortlist of 6 physical gladiator tools that create counterplay with the
   3-point economy, with the main abuse risk of each.
6. How should the animation set be used for **readability** (telegraphing startup, showing recovery windows,
   showing downed / executable states)?
