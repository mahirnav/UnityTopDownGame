# RATS

1v1 third-person gladiator arena. Unity 6.3 LTS (6000.3.26f1), C#.

Current state: **Task 1 engine spike**, a capsule on a floor with a follow camera, WASD, a projectile,
and a sim-driven freeze. No game rules yet.

## Layout

- `unity/Assets/Sim/`: the combat simulation. Plain C#, **no UnityEngine** (asmdef `noEngineReferences`).
- `unity/Assets/Game/`: Unity views and input. Visuals only follow sim state.
- `unity/Assets/Editor/`: scene generator, Mixamo import settings, headless capture, play-mode smoke test.
- `unity/SimTests/`: NUnit tests for the sim, run with `dotnet test` (Unity not needed).
- `scripts/test.ps1`: runs everything from the terminal.

## Run it

Open `unity/` in Unity Hub (6000.3.26f1). The Bootstrap scene opens automatically. Press Play.

Keys: WASD move · Space fire · E melee · F freeze · R resume.

## Tests

```
.\scripts\test.ps1          # sim tests, ~2 s
.\scripts\test.ps1 -Unity   # + play-mode smoke and headless freeze screenshots (close the editor first)
```

Screenshots and logs land in `captures/` (git-ignored).
