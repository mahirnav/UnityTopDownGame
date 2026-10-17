# RATS

1v1 third-person gladiator arena. Unity 6.3 LTS (6000.3.26f1), C#.

Current state: **P0 core turn loop.** You (red) against a simple bot (blue) in an oval pit with 4 pillars.
8 s turns with 3 action points, then a 3 s freeze that stops everything mid-action. HP, round end, lock-on.
See CLAUDE.md for the architecture rules.

## Layout

- `unity/Assets/Sim/`: the combat simulation, tunables parser, bot. Plain C#, **no UnityEngine** (asmdef `noEngineReferences`).
- `unity/Assets/Game/`: Unity views and input. Visuals only follow sim state.
- `unity/Assets/Editor/`: scene generator, Mixamo import settings, headless capture, play-mode smoke test.
- `unity/SimTests/`: NUnit tests for the sim, run with `dotnet test` (Unity not needed).
- `scripts/test.ps1`: runs everything from the terminal.

## Getting started (new machine)

1. Install **Unity Hub**, then the exact editor **6000.3.26f1** (other versions will upgrade the project files).
2. Install **Git** and **Git LFS** (`git lfs install` once). Optional for tests: the **.NET 8 SDK**.
3. Clone, then run the one-time setup (LFS hooks + Unity SmartMerge for scene files):
   ```
   git clone https://github.com/mahirnav/UnityTopDownGame.git
   cd UnityTopDownGame
   .\scripts\setup-git.ps1
   ```
4. In Unity Hub: **Add → Add project from disk** → pick the `unity` folder.

Working together: branch per feature (`git switch -c feature/xyz`), keep `main` playable, run
`.\scripts\test.ps1` before pushing. Scenes are generated from code, so avoid hand-editing them.
Read `CLAUDE.md` for the architecture rules (also what Claude Code follows).

## Run it

Open `unity/` in Unity Hub (6000.3.26f1). The Bootstrap scene opens automatically. Press Play.

Keys: WASD move · J melee · K ranged · Space dash · Tab lock-on · Q/E orbit (unlocked) · Enter restart · F5 reload tunables.

Tune numbers in `unity/Assets/StreamingAssets/tunables.txt`, save, press F5.

## Tests

```
.\scripts\test.ps1          # sim tests, ~2 s
.\scripts\test.ps1 -Unity   # + play-mode smoke and headless freeze screenshots (close the editor first)
```

Screenshots and logs land in `captures/` (git-ignored).
