# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

`utyuneko_action` is a 2.5D Unity action game where the player can transition between movement states and perform a high-speed burst attack that reflects off walls. This is a team project; each member works in their own feature branch and has a personal test scene under `Assets/Scenes/TestScene/<Member>/`.

The Unity project root is `utyuneko_action/`. All game scripts live under `utyuneko_action/Assets/Project/`.

**Physics is mid-migration from 3D to 2D.** The player is now fully 2D (`Rigidbody2D` / `CircleCollider2D` / `Collision2D`), while the enemy and boss systems are still 3D `Rigidbody`. When touching collision/physics code, check which dimension the specific component uses — do not assume. Some enemy scripts (e.g. `EnemyCollision.cs`) already mix 2D callbacks (`OnCollisionEnter2D`) with 3D types (`Collision`, `Rigidbody`); treat such hybrids as in-progress, not as a pattern to copy.

## Development Commands

There are no CLI build scripts. All building and running is done through the Unity Editor:

- **Open project**: Open `utyuneko_action/` folder with Unity Hub
- **Play**: Enter Play Mode in the Editor (`Assets/Scenes/MainScene/Stage1.unity`…`Stage4.unity` for full gameplay, your personal `TestScene` for isolated testing)
- **Build**: File → Build Settings in the Unity Editor

## Architecture

### Player State Machine

The player is entirely driven by a state machine. `PlayerController.cs` holds the current `IPlayerState` and delegates `Update`/`FixedUpdate`/`OnCollision` to it.

```
State/
├── IPlayerState.cs               (interface: Enter, Exit, UpdateState, FixedUpdateState)
├── PlayerController.cs           (orchestrator; holds references passed to each state)
├── PlayerVisualManager.cs        (lean/squash/trail/after-image visuals, driven by state)
├── HoverSensor.cs                (ground detection — IsGrounded(); replaces raycasting)
└── pattern/
    ├── PlayerState_Normal.cs     (walk, jump, transition to Charge on hold)
    ├── PlayerState_Charge.cs     (slow-mo aiming via Time.timeScale = aimTimeScale, 3-tier charge)
    ├── PlayerState_Burst.cs      (fast projectile movement, wall reflection via Vector3.Reflect)
    └── PlayerState_Damage.cs     (knockback + temporary input lockout after taking a hit)
```

The four state instances are created once in `Awake()` and exposed as `StateNormal`/`StateCharge`/`StateBurst`/`StateDamage`. Transition with `TransitionToState(...)`; the orchestrator calls `UpdateState()`/`FixedUpdateState()` each frame and forwards `OnCollisionEnter2D` through the `OnCollisionEnterEvent` delegate.

State transitions: Normal → Charge (hold attack input) → Burst (release) → Normal (on land or burst exhausted); any state → Damage on taking a hit.

Key `PlayerController` fields consumed by states:
- `maxBurstCount` / `currentBurstCount` – bursts allowed per jump (default 3)
- `reflectEfficiency` – speed multiplier per wall bounce (default 0.8)
- `chargeForceLevels[]` (3 tiers) + `currentChargeLevel` – set by Charge, read by Burst for initial speed
- `aimTimeScale` – `Time.timeScale` used while aiming in Charge (default 0.05)
- `rb2D` – shared `Rigidbody2D` reference
- `useInertiaInCharge` / `useRotationInCharge` / `useSquashInCharge` / `useTrail` / `useAfterImage` – per-feature visual toggles

Related: `PlayerHealth.cs`, `DamageSource.cs` (objects that deal damage), `HealSource.cs`.

### Enemy System

Each enemy composes multiple scripts rather than one monolithic class:

| Script | Responsibility |
|---|---|
| `EnemyMovement.cs` | Simple left/right patrol with configurable turn interval |
| `EnemyHealth.cs` | HP pool, speed-scaled damage from player burst |
| `EnemyCollision.cs` | Determines **Reflect**, **Pierce**, or **PierceZone** reaction |
| `EnemyDirectionalReaction.cs` | Direction-dependent reflect/pierce reaction logic |
| `EnemyKnockback.cs` | Knockback-on-defeat: blink, ground-hit despawn, linger/fallback timers (see 作業メモ) |
| `EnemyAttack.cs` | Fixed-pattern `StraightBullet` firing |
| `EnemyTargetAttack.cs` | Line-of-sight homing bullets |
| `PierceZoneTrigger.cs` | Child trigger collider that allows pierce-through from a specific face |

**Collision types** (`EnemyCollision.CollisionType`):
- `Reflect` – knocks player back unless player is in Burst (then it reflects like a wall); always deals speed-scaled damage
- `Pierce` – always passthrough (`isTrigger` enabled at runtime); damage applied in `OnTriggerEnter2D`
- `PierceZone` – body always reflects; pierce-through is granted only when a burst player enters a child `PierceZoneTrigger`, which calls `Physics.IgnoreCollision` before the body's reflection resolves

### Boss System

The boss is a second, independent state machine (`Enemys/Scripts/Boss/`) modeled on `PlayerController` but **3D** (`Rigidbody`). `BossController.cs` holds `IBossState` instances and delegates `UpdateState`/`FixedUpdateState`.

```
Boss/
├── IBossState.cs / BossController.cs           (interface + orchestrator)
├── BossHealth.cs                               (HP, phase threshold → OnEnterPhase2 / OnDefeated UnityEvents)
├── BossBarrier.cs / BossCollision.cs / BossMovement.cs
├── BossState_Idle / _ChooseAttack             (idle pacing, then pick next attack)
├── BossState_HomingShot / _SpreadShot / _Charge / _AreaImpact   (attack patterns)
└── ReflectableBullet.cs                         (bullet the player can reflect back)
```

`BossHealth` flips `BossController.currentPhase` to 2 once HP drops below `phase2HpRatio`; states read `currentPhase` (e.g. `phase2IdleMultiplier` shortens idle time in phase 2). While `BossBarrier` is active, `HandleHit` deals 0 damage.

### Layer-Based Burst Piercing

The physics collision matrix is the critical mechanism for Burst piercing:
- Normal player is on layer **Player**
- During Burst the player switches to layer **PlayerBurst** (`PlayerLayerSwitcher.cs`)
- **PlayerBurst** is excluded from colliding with **EnemyPierceable** in the Physics settings
- This means the same enemy prefab behaves differently depending on player layer — no per-object ignore lists

Changing which enemies a burst player passes through = edit `Physics2DSettings.asset` collision matrix, not individual scripts.

### Gimmicks

Under `Gimmick/Scripts/`:
- `SpeedBoostPad.cs` – multiplies player velocity while preserving direction; clamps to `[minSpeed, maxSpeed]`
- `DirectionalLaunchPanel.cs` – sets player velocity to a configured direction, forces `PlayerState_Burst`, applies on the next `FixedUpdate` to avoid single-frame overwrite
- `KeyGimmick.cs` / `KeySocket.cs` – key pickup + matching socket to unlock
- `DoorController.cs`, `WarpPoint.cs`, `FloorMover.cs`, `FloorRotator.cs`, `ZLock.cs`, `Fiting.cs`, `FadeController.cs`

Scene flow / UI: `Systems/Scripts/SceneChanger.cs` (player-trigger scene load) & `ButtonSceneChanger.cs`; `Title/Script/` (title/logo); `Camera/Script/` (`CameraFollowWithZoom`, `CameraBoundsTrigger`); `Player/UI/` (charge gauge, bit UI).

### Scenes

- `Assets/Scenes/MainScene/Stage1.unity` … `Stage4.unity` – the real game stages
- `Assets/Scenes/TestScene/<Member>/` – per-developer sandboxes (Tuji, Takagi, Yokozuka, Amano, Hosino, Mituhasi, Satou, Takahasi, Furuta). **Do not break other members' scenes** (see やってはいけないルール).
- `Assets/Scenes/TestScene/Satou/` holds the Title/Logo flow scenes.

## Important Conventions

- **Comments are in Japanese** – the team uses Japanese for in-code documentation; keep that convention when adding comments.
- **Input** uses Unity's new Input System. `PlayerInputActions.cs` is auto-generated from the `.inputactions` asset — do not hand-edit it.
- **Physics is mid-migration to 2D.** The player and the regular enemies (`ReflectEnemy`/`PierceEnemy`, as of 2026/06/10) now use 2D physics (`Rigidbody2D`/`BoxCollider2D`); the **boss** is still 3D `Rigidbody`. Match whichever the component you're editing already uses. Burst piercing is *intended* to use the **2D** Layer Collision Matrix in `ProjectSettings/Physics2DSettings.asset`, but that matrix is currently all-on (no exclusions set) — see 作業メモ.
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
-2026/06/04 エネミーの吹き飛ばし処理（`EnemyKnockback.cs`）テスト済み・動作OK
  - 撃破時に Renderer を点滅させる（`blinkOnDeath` / `blinkInterval`、全 Renderer の enabled を切替。マテリアル複製なしでリークなし）
  - 撃破後、床・壁にぶつかったら消滅（`destroyOnGroundHit` / `groundLayers` で対象レイヤー指定、プレイヤータグは除外）
  - 着地から消滅までの猶予 `lingerAfterLanding`（既定0.5秒、この間も点滅継続）
  - 床に当たらず飛び続けた場合の保険として `deathDestroyDelay`（既定1.5秒）も残してある
  - 注意: 死亡時は速く飛ぶため、Rigidbody が Discrete だと薄い床をすり抜けて床ヒットが発火しないことがある（その場合は保険時間で消滅）
-2026/06/10 エネミーの当たり判定を2D化（★未テスト＝Unityでの動作確認まだ）
  - 背景: プレイヤーが2D（`Rigidbody2D`/`CircleCollider2D`）になったのにエネミーが3Dのままで、2D物理と3D物理は干渉しないためプレイヤーが敵に当たらなくなっていた
  - スクリプトを3D API→2D APIへ：`EnemyCollision.cs`（`OnCollisionEnter2D(Collision2D)`。※従来は引数が3D `Collision` 型で一度も呼ばれていなかった）、`EnemyKnockback.cs`、`PierceZoneTrigger.cs`（`Physics2D.IgnoreCollision` など）
  - プレハブの物理を2Dへ差し替え（YAML直接編集）：`ReflectEnemy` は `BoxCollider`→`BoxCollider2D` / `Rigidbody`→`Rigidbody2D`、`PierceEnemy` は `Rigidbody`→`Rigidbody2D` ＋ コライダーが無かったので追加
  - `Rigidbody2D` は Dynamic / gravityScale=0 / Z回転フリーズ（Constraints=4）/ Discrete（元の3D設定と等価。死亡時の床ヒットを発火させるため Dynamic）
  - エネミー本体のレイヤーは Default(0) のまま。`Physics2DSettings` の2D衝突マトリクスは現状**全ON**で、CLAUDE.md記載の「PlayerBurst×EnemyPierceable 除外」は**未設定**（EnemyPierceable レイヤー自体は定義済み）。レイヤー方式の貫通は未構築
  - 死亡時の床バウンドを追加（`EnemyKnockback.cs`）：`bounceOnDeath`/`maxBounceCount`/`bounceFactor`/`bounceHorizontalKeep`/`minBounceSpeed`/`bouncePushOut` をInspectorで調整可。接触法線で `Vector2.Reflect` して減衰、回数切れ or 失速で着地→消滅。`deathDestroyDelay` を超えると跳ねきる前に強制消滅する点に注意
  - Pierceタイプの「床・壁はすり抜けない／プレイヤーはすり抜ける」を IgnoreCollision方式で実装（物理設定は変更せず）：
    - `PierceEnemy` を **2コライダー構成**に（Body=ソリッド非トリガー＝床壁用 / もう1つ=トリガー＝プレイヤー検出＆ダメージ用）
    - `EnemyCollision` に `pierceBodyCollider` / `pierceDamageTrigger` 参照を追加。`Start` で `Physics2D.IgnoreCollision(Body, プレイヤーの全Collider2D)` を呼び、プレイヤーだけBodyをすり抜けさせる
    - Bodyソリッド化で床ヒットが衝突＋トリガーの二重発火になりうるため、`EnemyKnockback.HandleGroundHit` に同フレーム1回ガードを追加
  - 未着手/保留: `EnemyDirectionalReaction.cs` は3Dのまま（どのプレハブにも未アタッチなので対象外）。Boss系は仕様通り3Dのまま。`EnemyTargetAttack.cs` の `Physics.Raycast` も3Dのまま
  - 注意: プレハブをYAML直接編集しているので、Unityで開く前に対象プレハブを **Reimport**（or再起動）。Pierce本体は Dynamic ソリッドのため、巡回中に壁へ突っ込むと押し戻されて止まる（`EnemyMovement` は時間で反転するので壁際で少し詰まることがある）
-2026/06/11 2D化の動作確認OK（①〜④すべて確認済み）。続けて `EnemyMovement.cs` の巡回挙動を拡張（★Inspector調整＝Unityでの値合わせまだ）
  - 崖検知：進行方向の先へ**斜め下レイ**（`Physics2D.Raycast`）を飛ばし、床が無ければ反転（崖落ち防止）。`detectEdge`/`edgeCheckDistance`/`edgeRayDownBias`
  - 壁検知：進行方向へレイを飛ばし、壁に当たったら反転（Pierceソリッド化での壁際詰まり対策）。`detectWall`/`wallCheckDistance`
  - レイ原点はコライダー半幅の外から飛ばして自己ヒットを回避。`groundLayers` に**自分の敵レイヤーを含めない**こと。`OnDrawGizmosSelected` でレイを可視化（赤=壁/水色=崖）
  - 反転バタつき防止に `reverseCooldown`。時間反転は `useTimedReversal` で任意化（既存の `changeInterval` は残置）
  - 復帰：吹き飛ばされた後、収まったら `returnDelay` 待って `startPosition`（Startで記録）へ `MoveTowards` で戻る。`returnToStart`/`returnSpeed`/`returnThreshold`
  - 連携：`EnemyKnockback` に `IsActive` プロパティを追加。吹き飛び中（`IsActive` or `IsDying`）は巡回を止め、収束後に復帰
  - 注意: `groundLayers` の設定とレイ長は Inspector で要調整。崖検知レイは斜め下なので地形に合わせて `edgeRayDownBias` を詰める
  - 補足: 浮いている敵（gravityScale=0）は床が下に無いため `detectEdge` を OFF にすること（ONだと崖と誤判定して `reverseCooldown` ごとに小刻みに反転する）。地上を歩く敵のみ ON
-2026/06/11 盾を持つ敵を実装（ダメージ判定＝Unityで確認OK。盾見た目の追従も実装済み。プレハブ化は各シーンで要対応）
  - 新規 `EnemyShield.cs`：向いている方向（前方）からの攻撃を盾で防ぎ**ダメージ無効化**。背後（盾の反対側）からのみ倒せる
  - 前方は `EnemyMovement.moveDirection` に自動追従（`followMoveDirection`）。巡回反転で盾の向きも反転。EnemyMovement が無ければ `facingOverride` 固定
  - 防御範囲は `shieldHalfAngle`（既定90＝前方半円すべて防御）。`Blocks(hitFromPos)` で前方となす角を判定
  - `EnemyHealth.HandleHit` にゲートを追加：`shield.Blocks()` が true ならダメージだけ無効化（反射・ノックバックは EnemyCollision の Reflect ソリッドが担当＝疎結合）。`TakeDamage` 直接呼び出しは方向が曖昧なので非ゲート
  - 盾の見た目は `shieldPivot`（子オブジェクト）に割当てると、前方へ**自動回転＋自動配置**（`shieldDistance` で中心からの距離を調整）。敵が反転すると盾も反対側へ移動。位置はワールド座標で毎フレーム上書きするため子側の手動オフセットは無効（Zは敵と同じ平面）。`OnDrawGizmosSelected` で防御範囲を可視化（青）
  - セットアップ想定：EnemyCollision の collisionType=**Reflect** ＋ `EnemyShield` を付与。前から当てると弾かれるだけ／背後から当てるとダメージ
  - 確認済み：盾の判定（前=無効/背後=ダメージ）OK、`shieldPivot` の回転・位置追従OK
-別種類のEnemy(上から優先順位が高い順) ※盾敵=実装済（次は「範囲攻撃を出す敵」から）
-仕様
-向いている方向に盾を持っている敵（盾の反対側から倒せるようにする）
-一定の時間で自分を中心とした設定範囲に攻撃を出す敵
-プレイヤーが射程に入ったら射線が出て一定時間経つと射線が止まりその場所にレーザーを出す敵（つまりスナイパー）
-突進のみのEnemy、壁に当たった時に自らスタンを食らう
-射程に入るとカウントダウンが始まり時間が経つと爆発する敵
-敵を中心にブラックホールがあり入ったら吸い込まれて敵に当たるとダメージを喰らう
-倒すと分裂して2体に増える敵