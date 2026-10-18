# RATS: master summary (October 18, 2026)

Self-contained snapshot for outside feedback. Written by Claude (the coding assistant) for Nick (designer).
The companion file `future-features.md` lists everything not built yet and asks for help choosing what's next.

---

## 1. The game in one paragraph

RATS is a **1v1 multiplayer PvP third-person gladiator arena game**. Combat runs in short real-time turns
(currently 13 s). Each turn, each player gets **3 action points** to spend on **melee, dash, or ranged**
attacks; points are spent when a press is accepted and expire at turn end. When a turn ends, **time freezes
exactly mid-action** (bolts, dashes and swings hang in place). The first three freezes of a round are **drafts**:
each player picks 1 of 3 upgrades. Then time resumes and in-progress actions finish. At 0 HP you're **downed**
and can be **executed**; a **ring of fire** closes in to force an ending. Matches are **best of 5**.
Look: gritty gladiator / western / ninja in a **classic-WoW style**, lit by a **realistic photographic sunset**.

**Core question the prototype must answer:** does limiting players to 3 actions per short turn, then freezing
time, create fun fights?

## 2. Team and how we work

- **Nick** designs, playtests and gives blunt notes. **Claude** writes the code and tests. Nick's friend
  co-develops through a shared private GitHub repo (Nick works on his own branch, `bosen`).
- Part-time project, mostly for fun, with an eye on a possible Steam release if it turns out good.
- Rules: challenge weak ideas; never change a design rule without Nick's approval; keep it small; **every exploit
  found becomes an automated test**; "tests pass" and "Nick playtested it" are always reported separately.
- Assets: free placeholders by default; small purchases are fine once Nick approves them. Never ripped or
  copyrighted game assets.

## 3. Where it stands

| Stage | Status |
|---|---|
| Engine | **Unity 6.3 LTS (6000.3.26f1)**, Built-in render pipeline. |
| P0: core turn loop | **Done, Nick playtested** ("feels very great for a first implementation"). |
| Online 1v1 | **Built and tested on one machine** (host-authoritative over Tailscale). **Not yet played between two people online**; the friend still needs to install Tailscale. |
| Replays | **Done.** Every round can be replayed exactly; exploit replays become tests. |
| P1: drafting (8 placeholder upgrades) | **Done, Nick playtested**: fun even against the bot. |
| Facing, backstabs, slower backpedal | **Done, Nick playtested.** |
| P2: downed, executions, fire ring, best of 5 | **Built**, played by Nick against the bot. |
| Visual pass 1 + 2 (arena, effects, HUD) | **Done, Nick playtested** ("getting better and better"). |
| WoW-scale arena, 13 s turns, camera back | **Done, Nick playtested**: movement speed now feels right; 13 s turns feel a few seconds long but Nick wants to judge that against a human first. |
| Realistic sunset lighting + scanned textures | **Built, tests pass, not yet playtested.** |
| Character model | **In progress:** Nick is buying *Ninja Otoko* (Asset Store, $25, 108 animations). |
| P3: hidden drafts, matchmaking | Not started. |

Automated checks: **81 sim/network tests** (~2 s), plus Unity batch checks (play-mode smoke test, headless
screenshots proving frozen frames are pixel-identical).

## 4. How it plays right now

You vs a bot or a second human in a 48 × 34 m oval pit with 4 wooden posts on spiked iron bases.

**Round flow:** 13 s combat → 3 s freeze (5 s on draft freezes) → repeat. Drafts on the first 3 freezes.
The ring of fire starts closing at 30 s of combat time and is fully closed at 90 s; fire damage ramps the longer
you stand in it. At 0 HP you're downed: you bleed out after 5 s (1.5 s in fire) unless the opponent walks up and
presses melee to execute you (free, always completes). If time runs out, higher HP% wins. First to 3 rounds.

**Controls:** WASD move (camera-relative) · J melee · K ranged · Space dash · Tab lock-on · Q/E orbit camera ·
1-3 draft pick · Enter restart · F5 reload settings · F9 save replay · F1 debug · Esc menu (host / join).

**Current numbers** (one editable text file; 60 ticks = 1 s):

| Thing | Value |
|---|---|
| Turn / freeze / draft freeze | 13 s / 3 s / 5 s |
| Action points | 3 per turn, unspent ones expire |
| HP | 100 |
| Walk speed | 5 m/s; backpedal 60%; rooted during melee/ranged startup |
| Turning | 720°/s idle; 180°/s tracking a locked target during attack startup; locked during active/recovery |
| Melee | 1 pt · startup 10 / active 6 / recovery 20 ticks · 20 dmg · 1.6 m reach · 120° arc |
| Backstab | melee from the target's 90° rear cone: +50% |
| Dash | 1 pt · startup 3 / active 12 / recovery 8 · 3.6 m |
| Ranged | 1 pt · startup 12 / recovery 18 · 10 dmg · 14 m/s bolt · posts and walls block it |
| Fire ring | starts 30 s, closed 90 s · 2 dmg per pulse (4/s) +1 per second inside |

**The 8 placeholder upgrades:** Heavy Blade (melee +8), Long Reach (+0.6 m), Big Bolt (bolt size ×2),
Fortification (+25 max HP, heal 10), Free Step (first dash each turn free), Opening Move (first action each
turn −50% startup), Exhaustion Punish (+20% vs a target with 0 points), Carry the Flame (unspent points → bonus
damage next turn). Each player gets their own 3 random offers, never something they already own. Upgrades reset
each round. Damage-type upgrades apply to attacks already frozen mid-swing (damage is read when the hit lands).

**Bot:** circles, dodges swings it sees coming, saves its last point to punish, takes backstabs, avoids fire,
executes.

**Known balance notes:** ranged is weak (melee is being balanced first). The "double burst" exists: 3 actions
right before a freeze plus 3 right after.

## 5. Look and assets

- **Arena:** tall rock walls with stone trim and buttresses, arched gate, wooden posts with spikes and chains,
  stands with a crowd, banners, flickering torches, sand floor.
- **Lighting (new):** a photographic HDRI sunset sky (Poly Haven, free/CC0) lights everything: sky light,
  reflections, a sun matching the photo, soft glow (bloom) and film-style tone mapping. Scanned sand and rock
  textures. Every visual knob lives in a separate `visuals.txt`, with switches back to the older painted look.
- **Effects:** hit sparks, damage numbers (BACKSTAB!), flinch, camera shake, dash dust, bolt trails, swing-arc
  flash, icy tint + countdown before each freeze, "FROZEN" flash, fire ring with sparks.
- **HUD:** HP bars, action-point pips, turn timer, draft cards, round and match banners.
- **Characters:** still capsules. Next: **Ninja Otoko** (2015/2018 Asset Store pack, Unity 5 PBR, works in
  Unity 6 Built-in, ~24k triangles, modular parts and weapons, 108 animations including slashes, rolls, hits,
  death, drinking, throwing). Nick estimates 75%+ of the animations are usable. Also a free Poly Haven katana.
- **No audio yet** beyond a freeze tone.

## 6. Architecture (agreed rules, enforced by tests where possible)

1. A plain C# **simulation owns all gameplay state** at a fixed 60 Hz tick, with no Unity dependency; it's tested
   from the terminal.
2. No engine physics for gameplay. 3. **Visuals only read sim state** (animations never drive gameplay).
4. **Actions are frame data** (startup / active / recovery, cost, hitbox). 5. Both fighters are symmetric and
   driven by an input source (keyboard, bot, network). 6. **Inputs are the only way into the sim** (10 bytes per
   fighter per tick). 7. All gameplay numbers live in one text file; F5 reloads it. 8. Fixed per-tick order.
9. Phases (combat / freeze / round over) are explicit sim state. 10. **Host-authoritative multiplayer:** clients
   send inputs, the host runs the only sim and sends snapshots; no rollback. 11. Randomness is a seeded generator
   stored in the sim. 12. Sim math is double precision (float math differs between Unity and .NET and broke replays).

**Multiplayer today:** direct connection over Tailscale (free private network), port 7777. No client-side
prediction yet, so the joining player's own movement lags by the round trip; the host plays lag-free.
Connection setup is separate from the game protocol so matchmaking can replace it later.

## 7. Known risks (to test, not pre-solve)

- Turn-boundary double burst; dumping all actions right before the freeze.
- 13 s turns may be too long against a human, or right once abilities exist.
- Joining player's lag (no prediction) and host advantage.
- Ranged irrelevance; melee buffs (tracking, backstabs) may need ranged retuning.
- Old animation pack may look stiff next to the realistic lighting; photoreal sky vs stylized characters.
