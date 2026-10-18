# RATS design direction (as of October 17, 2026)

Where the game is heading, from Nick's design discussions. Three levels, and they mean different things:
**Approved** = decided, build to it. **Leaning** = likely direction, but test or discuss before it becomes a rule.
**Open** = undecided. Nothing here overrides "never change a design rule without Nick's approval".

## Approved
- **Engine:** Unity 6.3 LTS. Godot is not used.
- **P1 scope:** mid-round drafting of upgrades to the **core actions only** (melee, dash, ranged). Starting
  abilities come after P1 is played.
- **P1 pool (8):** Heavy Blade, Long Reach, Big Bolt, Fortification, Free Step, Opening Move, Exhaustion Punish,
  Carry the Flame. Overclock was cut. Details in `docs/upgrades.md`.
- **P1 is built:** drafting on the 1st/3rd/5th freeze (5 s), same 3 offers for both, keys 1-3, picks apply when the freeze ends, no pick = first offer you don't own. Damage is read when a hit lands, so frozen attacks get upgrades picked during the freeze.
- **Facing:** an action's direction stays where it started (for now; see the facing model under Leaning).
- **Startup walking and lock-on grace** exist as tunables, default off, to A/B in playtests.
- **Online:** host-authoritative direct connection over Tailscale now; real matchmaking later.

## Leaning (likely, not yet rules)

**Freeze and upgrades**
- Upgrades can affect actions already frozen in progress. The rule is *when a value is read*:
  read when a hit lands (damage, damage taken, HP, lifesteal) vs locked when an action starts (timing, range,
  arc, dash distance, projectile size/speed). Structural swaps (dash → teleport) apply to new actions only.
- HP upgrades give partial healing at most (e.g. +25 max, +10 now), to retest once we see how often lethal hits
  get frozen.
- Draft choices should create tension between "helps right now" and "helps for the rest of the round"
  (mix of read-at-hit and future-only upgrades). Idea to try: reveal offers at turn start, pick at the freeze.

**Rounds and progression**
- Rounds about 90–120 s, ending through an escalating hazard (shrinking fire ring) rather than a visible round
  clock. The turn timer stays visible. A deterministic hard cap exists internally.
- About 3 mid-round drafts per round, early (roughly alternate freezes), then none; mid-round upgrades reset
  each round in the first experiment. No duplicates, mirrored offers, seeded draw.
- Three progression layers to explore later: **starting ability** (one, match-long, physical gladiator tools such
  as throwing axe, shield bash, net, grapple; not magic), **between-round augments** (big, risk/reward,
  build-defining, probably separate offers), **mid-round upgrades** (small). Mechanical replacements probably
  belong to abilities/augments, not mid-round picks.
- Starting ability: independent cooldown, no action-point cost, only one. Cooldown should be long relative to
  the turn cycle (~25–30 s combat time) so it isn't effectively a 4th action every turn.

**Action economy**
- Running out of action points must stay a real vulnerability. No free fallback attack, shove or guard approved.
- Endgame stalemates: try shorter turns in the final hazard stage before any new mechanic. Handoff rule stands:
  ring fully closes at max time, higher HP% wins if no execution.
- Cooldowns, the fire ring and the round cap all run on combat time (they pause during freezes).

**Movement, lock-on, facing**
- About 50% walk speed during attack startup as the first value to test; active/recovery may differ.
- Pillars should break lock-on quickly; at most a tiny grace period plus optional auto re-lock.
- A facing model with turn rates per state (fast when idle and locked on, slow during melee startup, zero in
  active/recovery) would support limited-rotation tracking and backstabs.

**Feel**
- Turn-boundary cue: try an audio/visual warning before the freeze first; subtle slow-motion only if the cue is
  not enough (slow-mo would extend real time at the boundary and feed action dumping).

## Under evaluation (theory only, not approved)
- **Limited-rotation melee tracking** during startup, direction locked on release.
- **Hold-to-charge melee** on the same button: tap = fast/light, hold = stronger up to a cap, 1 point at start.
  Main risk: banking a charge across the freeze (fresh points plus a charged swing).
- **Deterministic backstabs**: rear-cone damage multiplier read at hit; works best with the facing model.
- All three buff melee; expect to retune ranged afterwards.

## Open questions
- Do upgrades or augments carry across rounds (full, one chosen, none)?
- Do some freezes stay plain pauses, and how long are draft freezes?
- Fire ring: forcing proximity vs killing directly; stacking damage; dash vs fire; does fire down or kill?
- Do executions cost action points (an exhausted player might be unable to finish a downed opponent)?
- What happens to a charge held across a freeze (if charging is approved)?
- Host advantage online: the host plays with zero lag; dedicated server or deliberate host input delay later?

## Design priorities (in order)
1. Preserve the importance of action-point management.
2. Make frozen-time interactions distinctive.
3. Keep combat responsive and readable.
4. Avoid excessive upgrade accumulation.
5. Make rounds increasingly tense without arbitrary mechanics.
6. Preserve meaningful counterplay.
7. Don't add systems before testing whether they're needed.
