# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

`utyuneko_action` is a 2.5D Unity action game where the player can transition between movement states and perform a high-speed burst attack that reflects off walls. This is a team project; each member works in their own feature branch and has a personal test scene under `Assets/Scenes/TestScene/<Member>/`.

The Unity project root is `utyuneko_action/`. All game scripts live under `utyuneko_action/Assets/Project/`.

**Physics is mid-migration from 3D to 2D.** The player and the regular enemies are now fully 2D (`Rigidbody2D` / `Collider2D` / `Collision2D`); the **boss** system is still 3D `Rigidbody`. When touching collision/physics code, check which dimension the specific component uses — do not assume. The remaining 3D holdouts are the Boss system and `EnemyTargetAttack.cs` (`Physics.Raycast`).

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
| `EnemyCollision.cs` | Determines **Reflect** or **Pierce** reaction; gates damage to Burst via `PlayerController.CurrentState` |
| `EnemyDirectionalReaction.cs` | Direction-dependent reflect/pierce reaction logic |
| `EnemyKnockback.cs` | Knockback-on-defeat: blink, ground-hit despawn, linger/fallback timers (see 作業メモ) |
| `EnemyAttack.cs` | Fixed-pattern `StraightBullet` firing |
| `EnemyTargetAttack.cs` | Line-of-sight homing bullets (`Physics.Raycast` — still 3D) |

**Optional behavior/attack modules** — each is a self-contained `MonoBehaviour` (no `RequireComponent`) you drop onto a base enemy to give it an attack pattern. They share two conventions: contact damage/reflection is left to `EnemyCollision` (typically `Reflect`) and player i-frames to `PlayerHealth` (decoupled — they don't check invincibility themselves), and they **suspend themselves while `EnemyKnockback.IsActive || IsDying`** (so hitting the enemy interrupts its attack). Several render a runtime-generated translucent disc mesh (`Shader.Find("Sprites/Default")`, freed in `OnDestroy`) to show their range in the Game view. All are 2D (`Physics2D`). See 作業メモ for per-field tuning notes.

| Script | Behavior |
|---|---|
| `EnemyShield.cs` | Blocks (nullifies) damage from its facing arc (`shieldHalfAngle`); only killable from behind. Front auto-follows `EnemyMovement.moveDirection` |
| `EnemyAreaAttack.cs` | Periodic self-centered AoE: cooldown → telegraph → active window (`OverlapCircleAll`) |
| `EnemySniper.cs` | Aim (sight line tracks) → lock → fire laser (`CircleCast`); `LineRenderer` beam, muzzle `aimPivot` follows |
| `EnemyCharger.cs` | Ray-tripwire detect (player crosses forward ray) → windup → charge along ray → self-stun on wall hit; model faces ray dir. `useRayDetection=OFF` falls back to circular detect |
| `EnemyBomber.cs` | Proximity countdown (accelerating blink) → explosion (`OverlapCircleAll`), self-destruct or reset |
| `EnemyBlackHole.cs` | Pulls player `Rigidbody2D` toward center via `FixedUpdate` `AddForce`; damage is from the body's `EnemyCollision(Reflect)` |

**Collision types** (`EnemyCollision.CollisionType`):
- `Reflect` – knocks player back unless player is in Burst (then it reflects like a wall). Deals speed-scaled damage **only when the player is Bursting** (gated by `EnemyHealth.requireBurstToDamage`); a non-burst bump is knocked back but deals 0 damage
- `Pierce` – **2-collider setup**: a solid Body collider (blocks floor/wall; `IgnoreCollision` vs the player so the player passes through) + a Trigger collider that detects the player and deals damage in `OnTriggerEnter2D` (also Burst-gated). Both must be assigned (`pierceBodyCollider` / `pierceDamageTrigger`) — with only one collider the enemy takes no damage and **never dies** (`Awake` logs a warning)

Burst detection reads the state machine directly (`PlayerController.CurrentState == StateBurst`), **not** a layer/tag — see 作業メモ 2026/07/01. (`PierceZone` and the `PlayerBurst` layer were removed.)

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

### Burst detection & piercing

**Burst state is read from the state machine directly** — `PlayerController.CurrentState == StateBurst`. This is the single source of truth. Tags identify object identity; layers are for the physics matrix; neither is used to detect Burst in gameplay scripts (`EnemyCollision.IsBursting`, `PlayerLayerSwitcher`).

The **PlayerBurst** layer (a layer-matrix approach for burst piercing) and its driver `PlayerLayerSwitcher.cs` were **removed from the project** — the layer was only ever a signal derived from the state, and the `PlayerBurst × EnemyPierceable` matrix exclusion was never configured (matrix all-on). See 作業メモ 2026/07/01.

Pierce-type enemies pass the player through via `Physics2D.IgnoreCollision` (the 2-collider setup in `EnemyCollision`), independent of any layer.

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
-2026/06/10 エネミーの当たり判定を2D化（★未テスト＝Unityでの動作確認まだ）
-2026/07/02 ボムエネミーの追尾機能のオンオフの追加・突進エネミーを例を飛ばした方向にプレイヤーがいるとはつどうするようになる・プレイヤーのステートによってダメージが入るか決める
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
-2026/06/12 範囲攻撃を出す敵を実装（★未テスト＝Unityでの動作確認まだ）
  - 新規 `EnemyAreaAttack.cs`：一定間隔で自分中心の円範囲へ攻撃を出す。クールダウン→予兆→発動を繰り返す
  - サイクル：`startDelay`（初回待ち）→`attackInterval`（攻撃間隔）→`telegraphTime`（予兆＝回避猶予）→発動。`Time.deltaTime` 駆動（チャージ中のスローモーの影響を受ける＝他のエネミーと同じ）
  - 発動時に `Physics2D.OverlapCircleAll(中心, attackRadius, targetLayers)` で範囲走査し、`PlayerHealth.TakeDamage(attackDamage)` を呼ぶ。バースト中無敵・被弾後の無敵は `PlayerHealth` 側が処理（疎結合）。`GetComponentInParent<PlayerHealth>` ＋ 最初の1体でbreak（多重ヒット防止）
  - 演出（任意）：`telegraphEffectPrefab`（予兆・敵の子として追従）／`strikeEffectPrefab`（発動の瞬間）。`autoScaleEffect` ONで直径 `attackRadius*2` に自動スケール（1x1ユニット＝直径1のプレハブ想定）
  - 協調：`EnemyKnockback` の `IsActive`／`IsDying` 中は攻撃を中断（予兆も消す）。`EnemyMovement` と同じ協調パターン
  - 単体で付与できる攻撃モジュール（`RequireComponent` なし）。`OnDrawGizmosSelected` で攻撃範囲を可視化（予兆中=橙／通常=赤）
  - 注意: `targetLayers` にプレイヤーのレイヤーを含めること（既定は全レイヤー）。`attackRadius`／`attackInterval`／`telegraphTime` は Inspector で要調整。プレハブ化は各シーンで要対応
-2026/06/17 範囲攻撃エネミーを拡張（★Unity動作確認まだ）
  - 実行時可視化（仮）を追加：`EnemyAreaAttack` が半径1の塗りつぶし円メッシュを子として実行時生成し、Gameビューでも攻撃範囲が見える（プレハブ未用意でも可視）。`showRuntimeRange`/`idleColor`/`telegraphColor`/`strikeColor`。ビルトインRP前提で `Shader.Find("Sprites/Default")`、生成Material/Meshは `OnDestroy` で破棄
  - フェーズに「発動中（Active）」を追加：サイクルが クールダウン→予兆→**発動中(`activeTime`)**→クールダウン に変更。発動中は毎フレーム `OverlapCircleAll` で判定が残る（無敵切れで再ヒット）。`activeTime=0` で従来の瞬間判定と同じ。中断処理は `CancelAttack`（予兆中・発動中の両方を打ち切り）
-2026/06/17 スナイパー敵を実装（★未テスト＝Unityでの動作確認まだ）
  - 新規 `EnemySniper.cs`：2D前提（`Physics2D`）。フェーズ機械 待機(Idle)→照準(Aim:射線がプレイヤー追従)→ロック(Lock:射線固定＝最終警告)→発射(Fire:レーザー判定)→クールダウン→待機
  - 索敵：`detectionRange` 内＋`obstacleLayer` で射線が壁に遮られないと照準開始（`Physics2D.Raycast`）。一度照準に入るとプレイヤーが逃げても固定方向に撃つ（避けるゲーム性）。見失っても最後の方向を保持
  - レーザー：`obstacleLayer` で壁に当たると止まる（`ComputeBeamLength`）。判定は太さ考慮で `Physics2D.CircleCast`（半径=`beamWidth/2`）→ `PlayerHealth.TakeDamage(beamDamage)`。無敵は `PlayerHealth` 側（疎結合）
  - 可視化：`LineRenderer` を実行時生成（射線=細い`sightWidth`／レーザー=太い`beamWidth`、色は `aimColor`/`lockColor`/`fireColor`）。ビルトインRP前提 `Sprites/Default`、Materialは `OnDestroy` で破棄。`sortingOrder=10`
  - 協調：`EnemyKnockback` の `IsActive`／`IsDying` 中は中断してクールダウンへ。`firePoint` 未指定なら自分の位置が原点
  - 銃口追従：`aimPivot`（銃口オブジェクト）を毎フレーム照準方向 `lockedDir` へ回転（`aimAngleOffset` で絵の基準向き補正）。位置は `aimPivotDistance=0` のとき**最初に置いた配置を基準に照準方向へオービット**（Startで敵中心からのオフセットと角度を記録し、照準方向との差分だけ回転）＝反対方向を狙うと銃口も反対側へ回り込む。`aimPivotDistance>0` なら敵中心からその距離の純粋な放射状配置で上書き。Zは敵と同平面。`firePoint` を `aimPivot` の子（銃口先端）にすると射線原点も自動追従
  - 注意: `targetLayers` にプレイヤー、`obstacleLayer` に壁を設定すること。`aimTime`/`lockTime`/`fireDuration`/`cooldown`/`maxBeamLength` は Inspector で要調整。プレハブ化は各シーンで要対応
-2026/06/17 突進のみの敵を実装（★未テスト＝Unityでの動作確認まだ）
  - 新規 `EnemyCharger.cs`：2D前提。フェーズ機械 待機(Idle)→予兆(Windup:突進方向を固定)→突進(Charge)→壁ヒットで自滅スタン(Stun:停止＋点滅)→クールダウン→待機
  - 移動は EnemyMovement/EnemyKnockback と同じく `transform` で手動制御（物理解決に頼らない）。壁検知は衝突コールバックではなく進行方向への `Physics2D.Raycast`（高速突進のトンネリング回避、今フレームの移動量＋`wallSkin` の範囲で検知し壁手前で停止）
  - 索敵：`detectionRange` 内＋`requireLineOfSight` で `wallLayers` に射線を遮られないと突進開始。`horizontalOnly` で左右のみ/全方向
  - 2026/06/17 手直し：プレイヤー発見方向への突進を強化。`horizontalOnly` の既定を **false（斜め含む全方向にプレイヤーの実位置へ突進）** に変更。突進方向の固定タイミングを `trackDuringWindup` で切替可能に（OFF＝予兆開始時に固定＝避けゲー寄り／ON＝予兆中も追従し突進開始の瞬間に確定＝当たりやすい）。Inspectorでどちらも選べる
  - スタン：壁ヒットで `stunDuration` 停止＝攻撃チャンス。`stunBlink`/`blinkInterval` で点滅。壁に当たらず `maxChargeTime` を過ぎたらスタンせず終了。`IsStunned` プロパティあり
  - 接触ダメージ・反射は `EnemyCollision(Reflect)` が担当（疎結合）。吹き飛び中は中断してクールダウンへ
  - 注意: `wallLayers` に壁を設定すること（未設定だと壁を貫通し永遠に止まらない→`maxChargeTime` で終了）。`chargeSpeed`/`windupTime`/`stunDuration` は Inspector で要調整。プレハブ化は各シーンで要対応
-2026/06/17 自爆（カウントダウン爆発）の敵を実装（★未テスト＝Unityでの動作確認まだ）
  - 新規 `EnemyBomber.cs`：2D前提。フェーズ機械 待機(Idle)→カウントダウン(Countdown:導火線)→爆発(Exploding)→自滅 or 待機へリセット
  - 索敵：`detectionRange` 内に入ると導火線スタート。残り時間が減るほど Renderer の点滅が加速（`blinkIntervalStart`→`blinkIntervalEnd`）＋爆発範囲の円が濃くなる
  - 爆発：`fuseTime` 経過で `Physics2D.OverlapCircleAll(explosionRadius, targetLayers)` → `PlayerHealth.TakeDamage(explosionDamage)`（最初の1体でbreak）。`explosionEffectPrefab`（任意・半径に自動スケール）。`destroyOnExplode` で自滅、false なら待機へリセット
  - `resetIfPlayerLeaves`：カウントダウン中に射程外へ出たらリセット（既定false＝一度始まったら止まらない）。吹き飛び中／死亡中はカウントダウン中断＝殴って爆発を止められる（爆発中は止めない）
  - 可視化：EnemyAreaAttack と同じ実行時生成の塗りつぶし円メッシュ（`showRuntimeRange`/`idleColor`/`dangerColor`/`explodeColor`、Sprites/Default、`OnDestroy` で破棄）
  - 注意: `targetLayers` にプレイヤーを含めること。点滅対象 Renderer は可視化メッシュ生成前に取得＝自分の見た目のみ。`detectionRange`/`fuseTime`/`explosionRadius` は Inspector で要調整。プレハブ化は各シーンで要対応
-2026/06/17 ブラックホール（吸い込み）の敵を実装（★未テスト＝Unityでの動作確認まだ）
  - 新規 `EnemyBlackHole.cs`：2D前提。`pullRadius` 内のプレイヤー `Rigidbody2D` を中心へ `AddForce`（`FixedUpdate` で物理に乗せる）。吸引のみ担当
  - 「敵に当たるとダメージ／反射」は本体の `EnemyCollision(Reflect)` ＋ `EnemyHealth.HandleHit` が担当（疎結合）。バースト中無敵・被弾後無敵は `PlayerHealth` 側。仕様の「吸い込まれて敵に当たるとダメージ」はこの組み合わせで成立
  - 吸引力：`pullForce` 基準。`strongerNearCenter` ON で中心に近いほど強める（外周1倍→中心 `centerForceMultiplier` 倍）。`horizontalOnly` で水平限定（既定OFF＝平面全方向）。`limitApproachSpeed`/`maxApproachSpeed` で中心方向の速度に上限（暴走防止）
  - 協調：`EnemyKnockback` の `IsActive`／`IsDying` 中は吸引停止（殴って怯ませれば止まる）。プレイヤーは Start でタグ検索→`Rigidbody2D` を取得（ルート→子の順）
  - 可視化：EnemyBomber と同じ実行時生成の塗りつぶし円メッシュ。ただし**頂点カラーで中心濃→外周透明**のグラデ＋`swirlSpeed` で渦回転（`showRuntimeRange`/`edgeColor`/`coreColor`、Sprites/Default、`OnDestroy` で破棄）
  - セットアップ想定：`EnemyCollision.collisionType=Reflect` ＋本体コライダー ＋ `EnemyBlackHole`。`pullRadius`/`pullForce` は Inspector で要調整。プレハブ化は各シーンで要対応
  - 注意: 吸引は物理（`FixedUpdate`/`AddForce`）なのでチャージ中のスロー（`Time.timeScale`）の影響を受ける＝他エネミーと同じ。バースト中も吸引自体は効く（無敵なので当たってもダメージは無し）
-2026/07/01 エネミーをプレイヤー型の入れ子構成へ寄せる下準備＋不具合修正（★プレハブ組み替えはEditor作業として残）
  - 目的: プレイヤー（`Player_main`）に倣い「物理ルート＋回転は子」に統一。回転を Rigidbody2D/Collider2D 同居のルートへ当てると2Dの当たり判定がつぶれて壁すり抜けの一因になる（2026/06/25 すり抜け原因②）ため、回転を子（Rotation層）へ逃がす
  - 目標階層: `Root`(物理: RB2D+Collider+Enemyスクリプト。回さない/伸縮しない) → `Scale`(伸縮※squash無ければ省略可) → `Rotation`(向き回転＝`visualTransform`) → `Model`(見た目)。`shieldPivot`/`aimPivot` はルート直下のまま
  - `EnemyMovement.cs`: `public Transform visualTransform;` を追加。`ApplyRotation()` を**ルートではなく `visualTransform`** へ当てるよう変更（未割り当て時はルートにフォールバック＝後方互換）。Editorで `visualTransform` に Rotation層を割り当てる
  - `EnemyCollision.cs`: 実行時の Rigidbody2D 拘束を `FreezeAll`→**`FreezeRotation`** に緩和（位置は固定しない）。理由: 2026/06/25 で移動を `rb.MovePosition` 化したため、位置固定だとスイープ移動・壁衝突と干渉する。副作用: プレイヤーに押されて敵が少し動きうる→ Mass 等で調整
  - `EnemyHealth.cs`: **`isDeadFlg`** を追加（`[SerializeField] private`／外部は `IsDeadFlg` で読み取り専用）。HPが0の `Die()` 先頭で true（ノックバックより先に立て、同フレーム参照でも拾える）。`EnemyCollision` の衝突/トリガーは `IsDeadFlg` で早期リターン（死亡後の多重処理防止）
  - `EnemySniper.cs`: **モデルを弾を打つ方向の左右へ向ける**処理を追加（EnemyMovement を付けない前提）。`visualTransform`/`visualRotationAxis`(既定Y)/`leftAngle`(50)/`rightAngle`(-50)/`visualRotationSpeed`。`UpdateModelFacing()` が `lockedDir.x` の左右でY角を補間。銃口 `aimPivot` は従来どおり弾方向へZ回転（変更なし）。※真上/真下ではモデルは寝ない（左右のみ）
  - バグ修正: **Pierceにすると敵が死なない** → 原因は現行プレハブがコライダー1個。Pierceにすると Body がプレイヤーと IgnoreCollision ですり抜け（`OnCollisionEnter2D` 出ない）＋ダメージ用トリガーが無い（`OnTriggerEnter2D` 出ない）＝ `HandleHit` が呼ばれずダメージ0で死なない。対策は**2コライダー方式**（Body=ソリッド＋別途トリガーを追加し `pierceBodyCollider`/`pierceDamageTrigger` を割当）。`EnemyCollision.Awake` にトリガー未割り当て時の**警告ログ**を追加（無言の死なないバグを可視化）
  - ★Editor残作業: (1) 各エネミープレハブを上記の入れ子階層へ組み替え＋`visualTransform` 割当（YAML手編集は壊れやすいのでUnity上で）。(2) Pierce運用する敵にトリガーColliderを追加し2参照を割当、`EnemyHealth.damageSpeedThreshold` はバースト最低速(15)以下に
  - 補足: 追加したい仕様のうち「EnemyMovement の進行方向回転（左50/右310）」は `leftAngle`/`rightAngle`/`turnViaAngle` で実装済み。「スナイパーの向きを弾方向へ」も上記で対応済み
-2026/07/01 自爆敵に追尾機能を追加（★未テスト＝Unityでの動作確認まだ）
  - `EnemyBomber.cs`：`chasePlayer`（オン/オフ）を追加。ON なら射程に入ってからカウントダウン（`fuseTime`）のあいだプレイヤーを追尾してから爆発する（＝追う秒数は fuseTime に一致）。OFF＝従来どおりその場で爆発
  - 追尾は `FixedUpdate` + `rb.MovePosition`（`EnemyMovement` と同じ流儀＝壁すり抜け防止）。`chaseSpeed`/`chaseHorizontalOnly`（地上敵は水平のみ）/`chaseStopDistance`（めり込み防止）で調整
  - 追尾中は巡回（`EnemyMovement`）を `enabled=false` で一時停止（両者が `rb.MovePosition` を書くと実行順で競合するため）。カウントダウン中断・爆発後リセットで巡回を復帰（`SuspendPatrol`/`ResumePatrol`）
  - 協調は従来どおり：吹き飛び中／死亡中（`EnemyKnockback`）はカウントダウン中断＋追尾停止。`resetIfPlayerLeaves` ON なら追尾中に射程外へ出るとリセット
  - 注意: `chaseSpeed` を上げすぎるとプレイヤーが避けにくくなる。浮遊敵は `chaseHorizontalOnly` OFF、地上敵は ON 推奨。プレハブ化・値合わせは Editor で要対応
  - 倒された時の壁ヒット即爆発：`explodeOnDeathWallHit`（オン/オフ）を追加。ON なら**プレイヤーに倒されて吹き飛んだあと、壁・床にぶつかった瞬間に即爆発**（カウントダウン中でなくても爆発する）
    - 仕組み：`EnemyKnockback` に `public event Action<Vector2> OnDeathGroundHit` を追加（`isDying` 中の `HandleGroundHit` で発火＝バウンド/消滅とは独立）。`EnemyBomber` が購読して `DoExplosionDamage()`（範囲ダメージ＋演出）を1回だけ実行（`hasExplodedOnDeath` でガード）
    - 購読は `OnEnable` ではなく **`Awake`**（`OnDestroy` で解除）。死亡時 `disableOnDeath` で本スクリプトが `enabled=false` にされても購読を維持するため
    - 消滅そのものは従来どおり `EnemyKnockback`（バウンド→着地→消滅 or 保険 `deathDestroyDelay`）が担当。**即消えさせたいなら `EnemyKnockback.bounceOnDeath` を OFF**（着地即消滅）。ダメージ自体は最初の壁接触で即発生する
-2026/07/01 敵をバースト時のみ倒せるように変更（★未テスト＝Unityでの動作確認まだ）
  - 目的: 今までは接触速度が `damageSpeedThreshold` を超えれば非バースト（ジャンプ接触等）でも敵にダメージが入り倒せてしまっていた。バースト攻撃でのみ倒せるようにする
  - `EnemyHealth.HandleHit` の**シグネチャに `bool isBursting` を追加**し、`requireBurstToDamage`（既定ON）を追加。`requireBurstToDamage && !isBursting` のときはダメージ無効（弾き・ノックバックは従来どおり `EnemyCollision` が担当＝疎結合。非バーストは弾かれるがダメージ0）
  - `EnemyCollision`：Reflect・Pierce の両経路で `isBursting` を算出して `HandleHit` へ渡す。**バースト判定は状態機械を直接参照**（`IsBursting(p)` = `p.CurrentState == p.StateBurst`）。※後述の通りレイヤー判定は廃止
  - `OnEnemyKilledInBurst()`（バースト回数回復）も**バースト時のみ**呼ぶよう修正（従来は全接触で呼ばれ、ジャンプ接触でも回数が変動していた）
  - Boss 系（`BossHealth.HandleHit`）は別クラス・別シグネチャなので対象外（未変更）
  - 補足: 速度閾値 `damageSpeedThreshold` は従来どおり併用（バースト中でも遅すぎる当たりは弾く）。特定の敵だけ非バーストでも倒したいときは `requireBurstToDamage` を OFF
-2026/07/01 PierceZone 廃止＋バースト判定を状態ベースへ統一（★未テスト＝Unityでの動作確認まだ）
  - **PierceZone は不要になったので削除**。`PierceZoneTrigger.cs`（＋meta）を削除、`EnemyCollision.CollisionType` から `PierceZone` を除去（`Reflect`/`Pierce` の2種のみ。enum末尾の削除なので `Reflect=0`/`Pierce=1` の保存値は不変）。参照プレハブ・シーンが無いことを GUID と`collisionType: 2`で確認済み
  - **PlayerBurst レイヤーは（プロジェクトから削除された）**。これに伴い `EnemyCollision` のバースト判定 `LayerMask.NameToLayer("PlayerBurst")` は `-1` を返し常に false ＝敵が絶対ダメージを受けない状態になっていたため、**状態機械を直接見る方式（`CurrentState == StateBurst`）に変更**して修正
  - 方針: バーストは「状態」なので `PlayerController` の状態を直接見るのが最も正確。タグ＝オブジェクト識別、レイヤー＝物理マトリクス用であって状態判定には使わない（レイヤーは元々 `PlayerLayerSwitcher` が状態から派生させた二次情報）
  - 注意: Pierce タイプのすり抜けは IgnoreCollision 方式なのでレイヤー削除の影響なし
-2026/07/01 `PlayerLayerSwitcher.cs` を削除（＋meta）。PlayerBurst レイヤー廃止で不要になったため。参照プレハブ・シーンが無いことを GUID 確認済み。バースト貫通のレイヤー方式は完全に撤去（Pierce は IgnoreCollision 方式で継続）
-2026/07/01 敵の死亡吹き飛びをカメラ内に留める＋減速オフを追加（★未テスト＝Unityでの動作確認まだ）
  - `EnemyKnockback.cs`：`keepInCameraOnDeath`（既定ON）を追加。死亡吹き飛び中、`Camera.main` のビューポート範囲でクランプし、端で速度を反射（`cameraMargin`＝端の余白、`cameraBounceFactor`＝端反射の速度保持率）。画面外へ飛んでいかず画面内で吹っ飛んでから消滅する
  - `decelerateOnDeath`（既定OFF）を追加。OFFなら死亡吹き飛び中は減速しない（`decayPerSecond` を死亡時はスキップ＝一定速度で飛び続ける）。通常被弾（非死亡）の吹き飛びは従来どおり減速する
  - クランプは Update の移動後に `ClampToCamera()` で実施（`WorldToViewportPoint`→クランプ→`ViewportToWorldPoint`、Z平面維持、カメラ後方 vp.z<=0 はスキップ）。`Camera.main` はキャッシュ
  - 注意: `decelerateOnDeath` OFF＋`deathGravity` ありだと重力で加速→カメラ端で反射を繰り返して画面内で跳ね回る。`deathDestroyDelay`（既定1.5s）or 床ヒットで消滅。跳ね回りが激しすぎるときは `cameraBounceFactor` を下げるか `decelerateOnDeath` を ON に
-2026/07/01 突進敵をレイ索敵化＋モデルをレイ方向へ向ける（★未テスト＝Unityでの動作確認まだ）
  - `EnemyCharger.cs`：`useRayDetection`（既定ON）を追加。正面へ飛ばしたレイ（`sightRange`×`sightThickness`）にプレイヤーが入ったら突進する罠タイプに。突進方向＝レイ（正面）の方向で固定。OFFで従来の円形索敵（`detectionRange`＋視線）にフォールバック
  - レイの向き＝`SightDirection()`：`followMoveDirection` かつ `EnemyMovement` があれば `moveDirection` 追従、無ければ `facingOverride`。待機中も毎フレーム `chargeDir` を更新してモデル・レイ・突進方向を一致させる
  - レイ判定 `PlayerInSightRay()`：`Physics2D.CircleCast`（半径=`sightThickness/2`）を `wallLayers | (1<<プレイヤーのlayer)` で撃ち、最初に当たったのがプレイヤータグなら検知（壁が手前なら遮断）。原点はコライダー半径＋`wallSkin` だけ外へ出して自己ヒット回避
  - モデルの向き：`EnemySniper` と同方式で `visualTransform`（Rotation層の子）を左右角（`leftAngle`=50/`rightAngle`=-50）へ Y軸回転（`visualRotationAxis`/`visualRotationSpeed`）。`chargeDir.x` の左右で補間。`UpdateModelFacing()` を毎フレーム呼ぶ
  - Gizmo：レイ方式のとき索敵レイ（黄・太さ表示）、円形のとき従来の球。突進方向はマゼンタ線
  - 注意: `EnemyMovement` の進行方向回転も `visualTransform` を回すので、**同じ `visualTransform` を EnemyMovement と EnemyCharger の両方に割り当てると競合**する。どちらか一方に任せること（Sniper と同じく Charger 単体運用が素直）。`wallLayers` に壁を設定（未設定だとレイが壁で遮られず＝常に貫通検知）
-2026/07/02 突進敵のレイ索敵をテスト→不具合修正→**円形索敵へ戻した**（レイ方式はオプションとして残置）
  - 不具合①: レイ索敵が一切検知しない → 原因は**自己ヒット**。敵とプレイヤーが同じ Default レイヤーのため、マスクに自分も含まれ、開始時に重なる自分のコライダーが距離0で最初にヒット→常に false だった。`PlayerInSightRay` を `CircleCastAll` 化し**自分の Rigidbody2D 配下のコライダーをスキップ**して修正（プレイヤー判定は子トリガーも拾えるよう `attachedRigidbody` のタグでも確認）
  - 不具合②: 突進が1フレームで即スタン → 原因は**足元の床の誤検知**。壁先読みの CircleCast（本体と同径）が接地中の床（壁と同じ Ground レイヤー）に距離0でヒットしていた。`FindWallAhead` を新設し「自分／距離0（開始時重なり）／**法線が進行方向に正対しない面**（dot > -0.5）」を壁とみなさないよう修正。`OnCollisionEnter2D` 側も接触法線で同フィルタ
  - デバッグ機能を追加: `debugDraw`（既定ON。実行中に索敵レイを常時 Debug.DrawRay、緑=検知/赤=不検知。選択不要）／`debugLog`（既定OFF。索敵ヒット内容・フェーズ遷移・wallLayers外の接触を Console へ）。黄色Gizmoは選択中のみなので調査はこちらを使う
  - テスト所感: 壁スタンには**壁が Ground レイヤー（wallLayers）に載っている必要**があり、テストシーンの壁が未設定でうまく機能しなかったため、`Bv(Charger)_main.prefab` の `useRayDetection` を **0（円形索敵）に戻した**。レイ方式のコードは残っているので、壁レイヤーを整えれば ON にするだけで再挑戦できる
-2026/07/02 範囲系エネミーの可視化メッシュがモデルと一緒に傾く不具合を修正
  - 原因: `EnemyAreaAttack`/`EnemyBomber`/`EnemyBlackHole` の範囲円メッシュはルートの子として生成されるが、`EnemyMovement.visualTransform` 未割り当てのプレハブでは進行方向回転がルートに当たり（フォールバック）、平らな円がY軸50°回転を継承して斜めに寝ていた
  - 修正: 3種の `UpdateRangeVisual` で `rangeVisual.rotation = Quaternion.identity`（BlackHoleは渦角を累積して `Quaternion.Euler(0,0,swirlAngle)`）＝親の回転を継承せず常にカメラ正面（XY平面）を向く。当たり判定は物理クエリなので見た目とは無関係（メッシュにコライダー無し）
-2026/07/02 エネミーの範囲/爆発/レーザー攻撃がプレイヤーに当たるか検証 → **Bomber/Sniper のプレハブ設定バグを発見**
  - コードは3種とも `GetComponentInParent<PlayerHealth>()`→`TakeDamage()` で正しい。AreaAttack は `targetLayers=全` で当たる
  - **Bomber/Sniper は `targetLayers=128（Playerレイヤー7）のみ`。しかしプレイヤー `Player_main` は実際レイヤー0(Default)に置かれている**（Playerレイヤーは定義だけで未使用）＝爆発・レーザーがプレイヤーを検出しない
  - 直し方2案: (A)プレイヤーをPlayerレイヤー(7)へ移す＝1箇所で全部直る (B)敵の targetLayers を Default に。★ユーザー判断待ち（プレイヤーのレイヤー変更は保留中）
-2026/07/02 敵本体への接触ダメージは**未実装**（`EnemyCollision` Reflect はノックバックのみ、`PlayerHealth.TakeDamage` を呼ばない）。付けるなら敵本体に `DamageSource` をアタッチ（PlayerHealth側が Enemyタグ＋バーストで無効化を処理）。接触ダメージが要るとき対応
-2026/07/02 エネミーにアニメ付与（未完）。方式＝モデルのルート節(Rot直下)に Animator＋型別 .controller（`Assets/Models/Enemys/<型>/<型>.controller`）。FBXは Generic なのでネスト型は Animator 自動生成済み＝コントローラ割当のみ
  - YAML済(展開2体): `Bv(Charger)_main`(Bv(Charger)Core節)／`Rf(Reflect)_main`(Rf節)。要 Reimport。★動作未確認（.controllerの中身も未確認）
  - エディタ残(ネスト5体): Bt/Pp/Sh/Oc/Sl。Inspectorで Controller に .controller をドラッグするだけ
-別種類のEnemy ※盾敵・範囲攻撃敵・スナイパー・突進敵・自爆敵・ブラックホール=実装済。「倒すと分裂して2体に増える敵」は**実装不要になった（中止）**。突進敵は2026/06/17にプレイヤー方向突進を手直し（上記参照）
追加したい仕様内容
・~~突進する敵はレイを飛ばしたところにプレイヤーが入ったら攻撃するようにしてほしい~~←2026/07/02テスト済み。円形索敵に戻した（上記メモ参照）
・DamageSource.csがあると思うので、プレイヤーを攻撃したときに、ダメージを与えるようにするのにDamagesource.csをアタッチするだけで大丈夫か教えてほしい
・ボスの判定が以前３Dでやっていたので、2D版で改めて作成したいので、現在の敵のプレハブの構成を参考に、YAML編集をしてよいので、プレハブに作っておいてほしい