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

The current build scene is `Assets/Game/Scenes/PrototypeArena.unity`.
