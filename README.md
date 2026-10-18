# RATS

Multiplayer PvP third-person game (repo: UnityThirdPersonGame). 1v1 gladiator arena. Unity 6.3 LTS (6000.3.26f1), C#.

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

Keys: WASD move · J melee · K ranged · Space dash · Tab lock-on · 1-3 pick an upgrade during a draft · Q/E orbit (unlocked) · Enter restart · F5 reload tunables · F9 save replay.

Tune numbers in `unity/Assets/StreamingAssets/tunables.txt`, save, press F5.

## Play online (1v1)

Host-authoritative: the host's machine runs the match; the other player sends inputs and sees the host's state.

1. Both install [Tailscale](https://tailscale.com/download) and sign in to the same tailnet (the host shares/invites the other).
2. Host: press **Esc** → **Host a game**. The menu lists your addresses; send the other player the **Tailscale** one (`100.x.y.z`).
   Windows Firewall asks once: allow it.
3. Other player: **Esc** → type the address → **Join**. The round starts for both. The host controls Enter/F5 restarts and tunables.

Built player shortcuts: `.\scripts\build.ps1` makes `unity/Builds/RATS/RATS.exe`; run `RATS.exe -host` or `RATS.exe -join 100.x.y.z`.
Both sides need the same code version (the game refuses mismatched versions). Port 7777.

## Replays (found an exploit? send the file)

Every round on the machine running the sim (practice or host) is recorded: inputs only, ~1 KB per second.
It saves automatically when a round ends, on restart and on quit. Press **F9** to save on the spot,
e.g. right after something broken happens.

- Files go to `unity/replays/` (editor) or `replays/` next to `RATS.exe`; the newest 100 are kept.
- To turn one into a regression test, copy it into `unity/SimTests/Replays/`. Every test run replays it
  and checks it still ends in exactly the same state. Assertions about *what* went wrong get added on top.
- A deliberate rule change can make an old replay diverge; re-record or delete it then.

## Tests

```
.\scripts\test.ps1          # sim tests, ~2 s
.\scripts\test.ps1 -Unity   # + play-mode smoke and headless freeze screenshots (close the editor first)
```

Screenshots and logs land in `captures/` (git-ignored).
