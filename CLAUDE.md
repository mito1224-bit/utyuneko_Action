# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

`utyuneko_action` is a 2.5D Unity action game where the player can transition between movement states and perform a high-speed burst attack that reflects off walls. This is a team project; each member works in their own feature branch and has a personal test scene (e.g., `TsujiScene.unity`, `Takagi_Scene.unity`).

The Unity project root is `utyuneko_action/`. All game scripts live under `utyuneko_action/Assets/Project/`.

## Development Commands

There are no CLI build scripts. All building and running is done through the Unity Editor:

- **Open project**: Open `utyuneko_action/` folder with Unity Hub
- **Play**: Enter Play Mode in the Editor (GameScene for full gameplay, personal scene for isolated testing)
- **Build**: File → Build Settings in the Unity Editor

## Architecture

### Player State Machine

The player is entirely driven by a state machine. `PlayerController.cs` holds the current `IPlayerState` and delegates `Update`/`FixedUpdate`/`OnCollision` to it.

```
State/
├── IPlayerState.cs               (interface: Enter, Exit, Update, FixedUpdate, OnCollision*)
├── PlayerController.cs           (orchestrator; holds references passed to each state)
└── pattern/
    ├── PlayerState_Normal.cs     (walk, jump, transition to Charge on hold)
    ├── PlayerState_Charge.cs     (slow-mo aiming at 0.2x timeScale, 3-tier charge)
    └── PlayerState_Burst.cs      (fast projectile movement, wall reflection via Vector3.Reflect)
```

State transitions: Normal → Charge (hold attack input) → Burst (release) → Normal (on land or burst exhausted).

Key `PlayerController` fields consumed by states:
- `maxBurstCount` – how many bursts allowed per jump (default 3)
- `reflectEfficiency` – speed multiplier per wall bounce (default 0.8)
- `chargeLevel` (0–3) – set by Charge state, read by Burst state for initial speed
- `rb` – shared `Rigidbody` reference

### Enemy System

Each enemy composes multiple scripts rather than one monolithic class:

| Script | Responsibility |
|---|---|
| `EnemyMovement.cs` | Simple left/right patrol with configurable turn interval |
| `EnemyHealth.cs` | HP pool, speed-scaled damage from player burst |
| `EnemyCollision.cs` | Determines **Reflect**, **Pierce**, or **Directional** reaction |
| `EnemyAttack.cs` | Fixed-pattern `StraightBullet` firing |
| `EnemyTargetAttack.cs` | Line-of-sight homing bullets |
| `PierceZoneTrigger.cs` | Child collider defining the angle range that allows pierce-through |

**Collision types** (set on `EnemyCollision`):
- `Reflect` – knocks player back unless player is in Burst state
- `Pierce` – always passthrough (isTrigger enabled at runtime)
- `Directional` – pierces from specific approach angles; reflects from others; uses `PierceZoneTrigger` child

### Layer-Based Burst Piercing

The physics collision matrix is the critical mechanism for Burst piercing:
- Normal player is on layer **Player**
- During Burst the player switches to layer **PlayerBurst** (`PlayerLayerSwitcher.cs`)
- **PlayerBurst** is excluded from colliding with **EnemyPierceable** in the Physics settings
- This means the same enemy prefab behaves differently depending on player layer — no per-object ignore lists

Changing which enemies a burst player passes through = edit `Physics2DSettings.asset` collision matrix, not individual scripts.

### Gimmicks

- `SpeedBoostPad.cs` – multiplies `rb.velocity` while preserving direction; clamps to `[minSpeed, maxSpeed]`
- `DirectionalLaunchPanel.cs` – sets player velocity to a configured direction, forces `PlayerState_Burst`, applies on the next `FixedUpdate` to avoid single-frame overwrite

### Scenes

- `GameScene.unity` – main gameplay
- `TitleScene.unity` / `LogoScene.unity` – title flow
- `TsujiScene.unity`, `Takagi_Scene.unity`, `YokozukaScene.unity`, `SampleScene.unity` – individual developer sandboxes; do not break other members' scenes

## Important Conventions

- **Comments are in Japanese** – the team uses Japanese for in-code documentation; keep that convention when adding comments.
- **Input** uses Unity's new Input System. `PlayerInputActions.cs` is auto-generated from the `.inputactions` asset — do not hand-edit it.
- **Physics is 3D Rigidbody** with Z frozen (continuous collision detection). The game is visually 2.5D but uses 3D physics.
- **Slow-motion** in Charge state uses `Time.timeScale` — any new timed effect must use `Time.unscaledDeltaTime` if it should be unaffected by charge.
- `Material` instances created in state `Enter()` must be destroyed in `Exit()` to avoid memory leaks (see `PlayerState_Burst.cs` for the pattern).
- `OldScripts/` contains legacy code (`PlayerMovement.cs`) kept for reference — do not use or modify.

##やってはいけないルール
-他メンバーのシーンを勝手に編集しない
-Physics設定を変える前に相談する
-layerを追加するときも相談する
-コードを勝手に編集しない

##作業メモ
-2026/06/03 エネミーのボスの仮実装（後で少しいじる）
-エネミーの吹き飛ばし処理実装中（テストはまだしていない）
