# RATS — working notes for Claude

1v1 third-person gladiator arena, **multiplayer** (future host-authoritative). Short real-time turns
(~8 s) with 3 action points, then time freezes mid-action for a draft/pause, then resumes.
Core question for the prototype: does 3 actions per short turn + freeze make fun fights?

Engine: **Unity 6000.3.26f1** (exact version; anyone opening the project must use it). Godot is not used.

## How we work
- Nick designs and playtests; Claude writes the code and the tests. Nick's friend (mahirnav, owns the GitHub repo) collaborates.
- Challenge weak ideas and explain trade-offs. **Never change a game design rule without Nick's approval.**
  When a rule isn't specified, pick the simplest default, make it a tunable if cheap, and list it as "my default, your call".
- Keep it small: no frameworks, no speculative abstraction, no unrelated refactors.
- **Every exploit Nick finds becomes an automated regression test.**
- Commit working checkpoints. Commit messages and summaries say "tests pass" and "Nick playtested" separately.
- No paid assets until the prototype is fun. Capsules / free placeholders (Mixamo) only.

## Architecture rules (enforced by tests where possible)
1. The sim (`unity/Assets/Sim`, plain C#, `noEngineReferences`) owns **all** gameplay state and runs on a fixed 60 Hz tick.
2. No engine physics for gameplay. Movement and collisions are computed by the sim.
3. Visuals only read sim state. Animations, particles, timers never drive gameplay.
4. Actions are frame data: startup / active / recovery ticks, cost, hitbox. Hits come from frame data.
5. Every fighter is driven by an `IInputSource` (keyboard, bot, script, later network). Fighters are symmetric:
   `Fighters[0]` / `Fighters[1]`, never "the player".
6. **Inputs are the only way into the sim.** One `InputFrame` per fighter per tick. No sim changes from key handlers.
   Camera-relative movement is converted to world space in the input source, not the sim.
7. All tunable numbers live in `unity/Assets/StreamingAssets/tunables.txt`. Nick edits that file; F5 reloads in play mode.
8. Per-tick order: inputs → action starts → movement → collisions → damage → down checks → action progress / turn timer.
9. Phases are explicit sim state (Combat / Freeze / RoundOver). Never use Unity's pause or timeScale for the freeze.
10. No cross-machine determinism or rollback. Multiplayer will be host-authoritative: clients send inputs, host sends snapshots.
11. No gameplay state in MonoBehaviours, statics, `Time.*`, Unity physics, or unseeded randomness.
    Randomness, when needed, is a seeded generator stored in sim state.
12. **Sim math is `double`, never `float`/`MathF`** (only `InputFrame` holds floats). Unity's Mono keeps float
    intermediates wider than .NET, so float sims diverge between the game and `dotnet test` (replays broke at tick 58).
    `SimDoesNotUseFloatMath` enforces this. Convert to float only at the Unity boundary (`SimConvert`).

Replays: every round on the sim-owning machine is recorded (`MatchRecording`: tunables + inputs), F9 saves now.
An exploit replay Nick sends goes into `unity/SimTests/Replays/` and becomes a regression test automatically.

Multiplayer-readiness tests that must stay green: **snapshot round-trip** (save → load → both sims stay identical)
and **replay** (recorded inputs → bit-identical end state). If you add sim state, add it to the snapshot.

## Networking (current)
- `Sim/Net/`: plain C# TCP, polled from the game loop (no threads). Host runs the only sim and sends a snapshot
  every tick; the client sends one `InputFrame` per tick and applies snapshots. No client prediction yet.
- Connection setup (host on port 7777 / join by address, used with Tailscale) is separate from the protocol so
  matchmaking can replace it later. Bump `NetProtocol.Version` when messages or snapshots change.
- `NetTests` run real localhost TCP; keep them green. Builds need `PlayerBuild.IncludeCodeOnlyShaders`
  (code-built scenes reference no shaders, so builds would strip Standard and fog).

## Layout
- `unity/Assets/Sim/`: simulation, tunables parser, bot. No UnityEngine.
- `unity/Assets/Game/`: MonoBehaviours, views, keyboard input, camera. Only reads sim state.
- `unity/Assets/Editor/`: scene generator, Mixamo import settings, headless capture, play-mode smoke test.
- `unity/SimTests/`: NUnit sim tests via `dotnet test` (compiles `Assets/Sim/**/*.cs` with C# 9, Unity's limit).
- `unity/Assets/Art/Mixamo/`: placeholder humanoid + melee clip (not used in P0; capsules).
- The Bootstrap scene is generated from code (`RATS/Rebuild Bootstrap Scene`); don't hand-edit scenes.

## Commands (from repo root)
```
.\scripts\test.ps1          # sim tests, ~2 s
.\scripts\test.ps1 -Unity   # + batch-mode play smoke and headless freeze screenshots (editor must be closed)
```
Screenshots/logs go to `captures/` (git-ignored). Look at the screenshots after visual changes.

## Gotchas
- `1f/60` rounds up; tick clock math stays in double.
- Play mode reloads the script domain; editor automation must keep state in `SessionState`.
- PlayableGraph manual Evaluate doesn't pose models outside play mode; use `AnimationClip.SampleAnimation`.
- Sim code must compile as C# 9 (no newer syntax); `dotnet test` enforces this.

## Roadmap
P0 core turn loop (done, playtested) → networked 1v1 over Tailscale (current; pulled forward from P3) → P1 mirrored drafting (~8 upgrades) → P2 downed/execute/fire ring → P3 hidden drafts, matchmaking.
