# StickManFight Client

Unity client project for StickManFight. This application connects to a separate
Dedicated Server; server runtime and authoritative game logic are maintained in
the server project.

## Environment

- Unity `6000.3.23f1`
- Universal Render Pipeline `17.3.0`
- Input System `1.20.0`

## Asset layout

```text
Assets/
├── Game/
│   ├── Characters/Bob/
│   │   ├── Animations/
│   │   ├── Materials/
│   │   └── Models/
│   ├── Input/
│   ├── Scenes/
│   └── Settings/Rendering/
└── ThirdParty/UnityTemplate/
```

The original resource scene is retained at `Assets/Game/Scenes/PrototypeArena.unity`.

## Client-only TPS prototype

`Assets/Game/Scenes/TPSPlayground.unity` is the active client gameplay scene.
The current sprint includes Bob locomotion, a shoulder camera, aim strafing,
procedural upper-body pitch, local hitscan shooting, recoil, tracer, impact,
ammo UI, and resettable target dummies.

Controls:

- `WASD`: move
- `Left Shift`: sprint
- `Left Alt`: walk
- `C`: crouch
- `Space`: jump
- `Right Mouse`: aim
- `Left Mouse`: fire
- `R`: reload
- `Q`: switch shoulder
- `Escape`: release cursor

Dedicated Server messages, reconciliation, remote players, and authoritative
damage are intentionally deferred to the next networking sprint.
