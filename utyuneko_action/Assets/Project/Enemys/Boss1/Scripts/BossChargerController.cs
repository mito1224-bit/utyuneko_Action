using UnityEngine;

/// <summary>
/// 突進×盾ボス（Boss1）のコントローラ。
///
/// コアループ（闘牛士型）:
///   正面は物理盾で守られている → 突進を誘って避ける → 壁に自滅スタン
///   → スタン中は盾が下がり全身が弱点（ダメージ倍率アップ）
///
/// 状態遷移は StageSecondBossController と同じ流儀（状態インスタンスを Awake で生成、
/// TransitionToState で Exit→Enter）。移動は EnemyCharger と同じく rb.MovePosition の手動制御。
/// 物理は 2D（Rigidbody2D / Collider2D）。
///
/// 技:
///   ①予兆つき突進（壁ヒットで自滅スタン）           … BossChargerChargeState
///   ②連続突進（フェーズ2で2〜3連、最後だけ自滅）     … 同上（multiChargeCount）
///   ③シールドバッシュ（密着対策の近接薙ぎ払い）      … BossChargerShieldBashState
///   ④壁ヒット衝撃波（自滅スタン時に地面を走る）      … BossChargerStunState + BossChargerShockwave
///   ⑤盾投げ（フェーズ2。投擲中は全身無防備。投擲前に軌道ラインを見せる） … BossChargerShieldThrowState
///   ⑥バースト反射カウンター（正面の盾にバースト→反撃突進） … BossChargerCounterState
///   ⑨必殺技「憤怒の乱舞突進」（HP低下で一度きり確定発動。壁から壁へ連続突進、最後だけ自滅スタン） … BossChargerRampageState
/// </summary>
public class BossChargerController : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("プレイヤーのタグ")]
    public string playerTag = "Player";

    [Tooltip("左右の向きを当てるモデル（Rotation層の子。コライダーと同居するルートには当てない）")]
    public Transform visualTransform;

    [Tooltip("物理盾（子オブジェクト）。正面ガード・投擲・カウンター検知を担当")]
    public BossChargerShield shield;

    [Tooltip("壁・床とみなすレイヤー（突進の停止＆スタンの原因。自分のレイヤーは含めない）")]
    public LayerMask wallLayers;

    [Header("向き（EnemyMovement と同じ流儀のY軸振り向き）")]
    [Tooltip("左向きのときの角度（度）")]
    public float leftAngle = 50f;
    [Tooltip("右向きのときの角度（度）")]
    public float rightAngle = -50f;
    [Tooltip("振り向く速さ（度/秒）。0以下なら即時")]
    public float turnSpeed = 360f;

    [Header("フェーズ2")]
    [Tooltip("HPがこの割合以下になったらフェーズ2へ移行する")]
    [Range(0f, 1f)] public float phase2HpThresholdRatio = 0.5f;
    [Tooltip("フェーズ2での行動速度倍率")]
    public float phase2SpeedMultiplier = 1.3f;

    [Header("待機（Idle）")]
    [Tooltip("次の行動を選ぶまでの待機時間（秒）")]
    public float idleTime = 1.2f;
    [Tooltip("待機中もプレイヤーへ寄って歩く（＝攻撃以外でも移動する）。" +
             "OFF なら待機中は動かず、移動は突進・地面叩き等の攻撃時のみになる")]
    public bool idleApproach = false;
    [Tooltip("待機中にプレイヤーへ寄る移動速度（idleApproach が ON のときのみ有効。0で移動しない）")]
    public float idleApproachSpeed = 2f;
    [Tooltip("プレイヤーとの距離がこれ未満ならシールドバッシュを選ぶ")]
    public float bashTriggerRange = 3.5f;

    [Header("突進（技①②）")]
    [Tooltip("突進前の予兆時間（この間に狙いを定める＝避ける猶予）")]
    public float chargeWindupTime = 0.8f;
    [Tooltip("突進速度")]
    public float chargeSpeed = 16f;
    [Tooltip("壁に当たらなかった場合に突進を打ち切る保険時間（秒）")]
    public float maxChargeTime = 3f;
    [Tooltip("突進方向を水平のみにする（ONなら左右へ一直線）")]
    public bool chargeHorizontalOnly = true;
    [Tooltip("フェーズ2での連続突進回数（最後の1回だけ壁で自滅スタン）")]
    public int multiChargeCount = 3;
    [Tooltip("連続突進の合間の狙い直し時間（秒）")]
    public float reAimTime = 0.35f;
    [Tooltip("壁の手前で止める余白（めり込み防止）")]
    public float wallSkin = 0.05f;

    [Header("突進の予兆演出")]
    [Tooltip("突進前に後ろへ少し下がる距離（タメの見せ）。0で下がらない")]
    public float chargeBackstepDistance = 10f;
    [Tooltip("後ろへ下がるのにかける時間（秒）。予兆時間の前半で使う")]
    public float chargeBackstepTime = 0.2f;
    [Tooltip("突進方向へモデルをX軸で傾ける（スナイパーと同じ上下の向き付け）。chargeHorizontalOnly=OFF時に縦成分が出て効く")]
    public bool tiltModelToChargeDir = true;
    [Tooltip("モデルのX軸ピッチの最大角（度）")]
    public float modelPitchMax = 60f;
    [Tooltip("ピッチの回転方向が逆になる場合はON")]
    public bool invertModelPitch = false;
    [Tooltip("予兆中に突進予定の視線ライン（LineRenderer）をプレイヤーへ見せる")]
    public bool showChargeTelegraph = true;
    [Tooltip("視線ラインの色：予兆の開始（まだ余裕がある＝黄）")]
    public Color chargeTelegraphColorStart = new Color(1f, 0.92f, 0.2f, 0.5f);
    [Tooltip("視線ラインの色：予兆の直前（もう来る＝赤）。予兆の進行に合わせて開始色からここへ変化する")]
    public Color chargeTelegraphColorEnd = new Color(1f, 0.15f, 0.15f, 0.95f);
    [Tooltip("視線ラインの太さ")]
    public float chargeTelegraphWidth = 0.08f;
    [Tooltip("視線ラインの最大長（壁があればそこで止まる）")]
    public float chargeTelegraphMaxLength = 20f;

    [Header("スタン（技④の衝撃波もここから出す）")]
    [Tooltip("壁に当たって自滅したときのスタン時間（＝攻撃チャンス）")]
    public float stunDuration = 3f;
    [Tooltip("スタン中にモデルを半透明フェードで明滅させる1往復の時間（秒）。0以下で明滅なし")]
    public float stunBlinkInterval = 0.12f;
    [Tooltip("スタン明滅で最も薄くなるときのアルファ（0=完全透明 / 1=不透明のまま）")]
    [Range(0f, 1f)] public float stunBlinkMinAlpha = 0.3f;
    [Tooltip("自滅スタン時に地面を走る衝撃波のプレハブ（BossChargerShockwave 付き）。未設定なら出さない")]
    public GameObject shockwavePrefab;
    [Tooltip("衝撃波を出す足元のオフセット（自分の位置からの相対）")]
    public Vector2 shockwaveSpawnOffset = new Vector2(0f, -0.5f);

    [Header("シールドバッシュ（技③）")]
    [Tooltip("バッシュの予兆時間（秒）")]
    public float bashWindupTime = 0.5f;
    [Tooltip("バッシュの攻撃判定が出ている時間（秒）")]
    public float bashActiveTime = 0.25f;
    [Tooltip("バッシュ後の隙（秒）")]
    public float bashRecoverTime = 0.6f;
    [Tooltip("バッシュが届く距離（正面方向）")]
    public float bashRange = 4f;
    [Tooltip("バッシュのダメージ量")]
    public int bashDamage = 1;
    [Tooltip("バッシュで弾く力")]
    public float bashKnockbackForce = 18f;

    [Header("盾投げ（技⑤・フェーズ2）")]
    [Tooltip("盾投げを選べるようになるクールダウン（秒）")]
    public float shieldThrowCooldown = 12f;
    [Tooltip("投擲中の移動速度（無防備な代わりに速く動く）")]
    public float throwMoveSpeed = 5f;
    [Tooltip("盾を投げる前に軌道ラインを見せるタメ時間（秒）。避ける猶予。0で即投げ")]
    public float throwTelegraphWindupTime = 0.4f;
    [Tooltip("投擲前に盾の飛ぶ軌道（LineRenderer）をプレイヤーへ見せる")]
    public bool showThrowTelegraph = true;
    [Tooltip("軌道ラインの色：予兆の開始（まだ余裕＝黄）")]
    public Color throwTelegraphColorStart = new Color(0.4f, 0.9f, 1f, 0.5f);
    [Tooltip("軌道ラインの色：投擲直前（もう来る＝赤）。予兆の進行に合わせて開始色からここへ変化する")]
    public Color throwTelegraphColorEnd = new Color(1f, 0.3f, 0.3f, 0.95f);

    [Header("カウンター（技⑥）")]
    [Tooltip("盾にバーストされたとき反撃するか")]
    public bool enableCounter = true;
    [Tooltip("カウンター突進の速度")]
    public float counterSpeed = 20f;
    [Tooltip("カウンター突進の継続時間（秒）。壁に当たってもスタンしない")]
    public float counterDuration = 0.5f;
    [Tooltip("カウンターのクールダウン（連発防止・秒）")]
    public float counterCooldown = 4f;

    [Header("必殺技・憤怒の乱舞突進（技⑨・HP低下で確定発動）")]
    [Tooltip("必殺技を有効にする")]
    public bool enableRampage = true;
    [Tooltip("HPがこの割合以下になったら（フェーズ2中に）一度だけ確定発動する")]
    [Range(0f, 1f)] public float rampageHpThresholdRatio = 0.25f;
    [Tooltip("突入時の咆哮タメ時間（秒。1回目の突進前だけの長めの予兆＝『大技が来る』の見せ）")]
    public float rampageRoarTime = 0.7f;
    [Tooltip("2回目以降の各突進の予兆時間（秒。短くして怒涛の乱舞感を出す）")]
    public float rampageWindupTime = 0.3f;
    [Tooltip("壁から壁へ突進する回数（最後の1回だけ壁で自滅スタン＝反撃チャンス）")]
    public int rampageChargeCount = 5;
    [Tooltip("乱舞突進の速度（通常突進より速く）")]
    public float rampageSpeed = 22f;
    [Tooltip("壁に当たらなかった場合に1回の突進を打ち切る保険時間（秒）")]
    public float rampageMaxChargeTime = 2.5f;

    [Header("地面叩き→隆起衝撃柱（技⑦）")]
    [Tooltip("この技を選択候補に入れる")]
    public bool enableGroundSlam = true;
    [Tooltip("叩く前のタメ時間（秒）")]
    public float groundSlamWindupTime = 0.7f;
    [Tooltip("柱を撒き終えてから待機へ戻るまでの追加硬直（秒）")]
    public float groundSlamRecoverTime = 0.6f;
    [Tooltip("この技のクールダウン（秒。連発防止）")]
    public float groundSlamCooldown = 8f;
    [Tooltip("並べる衝撃柱の本数")]
    public int groundSlamPillarCount = 5;
    [Tooltip("柱と柱の間隔")]
    public float groundSlamPillarSpacing = 1.6f;
    [Tooltip("足元から1本目までの距離")]
    public float groundSlamPillarStartOffset = 1.5f;
    [Tooltip("隣の柱が起きるまでの時間差（波及感。0で一斉）")]
    public float groundSlamPillarStagger = 0.1f;
    [Tooltip("各柱の予兆時間（足元に印を出す秒数）")]
    public float pillarTelegraphTime = 0.35f;
    [Tooltip("各柱が0→全高へ隆起する時間（秒）")]
    public float pillarRiseTime = 0.12f;
    [Tooltip("各柱が全高で当たり判定を残す時間（秒）")]
    public float pillarActiveTime = 0.2f;
    [Tooltip("各柱の幅")]
    public float pillarWidth = 0.9f;
    [Tooltip("各柱の高さ")]
    public float pillarHeight = 2.6f;
    [Tooltip("柱に当たったプレイヤーへのダメージ")]
    public int pillarDamage = 1;
    [Tooltip("柱の見た目に使うマテリアル（未指定なら従来どおり実行時生成の Sprites/Default 板を使う）。" +
             "隆起アニメ（下端固定で上へ伸びる）はこのマテリアルのまま効く")]
    public Material pillarMaterial;
    [Tooltip("柱マテリアルを予兆色（黄→赤）で色付けする。OFF ならマテリアルの見た目をそのまま出す")]
    public bool tintPillarMaterial = true;
    [Tooltip("柱予兆の色：出始め（まだ余裕＝黄）")]
    public Color pillarTelegraphColorStart = new Color(1f, 0.9f, 0.15f, 0.5f);
    [Tooltip("柱予兆の色：隆起直前（もう来る＝赤）")]
    public Color pillarTelegraphColorEnd = new Color(1f, 0.15f, 0.1f, 0.85f);
    [Tooltip("柱が隆起して当たり判定を出している間の色（危険＝赤）")]
    public Color pillarActiveColor = new Color(1f, 0.2f, 0.15f, 0.9f);

    [Header("飛び上がり叩きつけAoE（技⑧）")]
    [Tooltip("この技を選択候補に入れる")]
    public bool enableLeapSlam = true;
    [Tooltip("跳ぶ前のタメ時間（秒）")]
    public float leapWindupTime = 0.45f;
    [Tooltip("跳躍（放物線移動）にかける時間（秒）")]
    public float leapTime = 0.7f;
    [Tooltip("放物線の最高到達点の高さ")]
    public float leapArcHeight = 5f;
    [Tooltip("着地後の硬直（秒）")]
    public float leapRecoverTime = 0.6f;
    [Tooltip("着地AoEの半径")]
    public float leapLandRadius = 3.5f;
    [Tooltip("着地AoEに当たったプレイヤーへのダメージ")]
    public int leapDamage = 1;
    [Tooltip("着地時に左右へ衝撃波も出す（技④の SpawnShockwaves を流用。shockwavePrefab 未設定なら出ない）")]
    public bool leapLandShockwaves = true;
    [Tooltip("この技のクールダウン（秒。連発防止）")]
    public float leapSlamCooldown = 9f;

    [Header("範囲技共通")]
    [Tooltip("柱・着地AoE の Overlapで走査する対象レイヤー（プレイヤーのレイヤーを含めること）")]
    public LayerMask attackTargetLayers = ~0;

    [Header("登場・死亡")]
    [Tooltip("登場演出の時間（秒）")]
    public float appearTime = 1.5f;
    [Tooltip("フェーズ2移行の咆哮時間（秒）")]
    public float phaseTransitionTime = 1.5f;
    [Tooltip("死亡してから消滅するまでの時間（秒）")]
    public float deathDestroyDelay = 2f;
    [Tooltip("死亡中の半透明フェード明滅の1往復の時間（秒）。0以下で明滅なし")]
    public float deathBlinkInterval = 0.12f;
    [Tooltip("死亡明滅で最も薄くなるときのアルファ（0=完全透明 / 1=不透明のまま）")]
    [Range(0f, 1f)] public float deathBlinkMinAlpha = 0.3f;
    [Tooltip("死亡時に発火するイベント（扉を開ける・イベントトリガー起動など）")]
    public UnityEngine.Events.UnityEvent onDefeated;

    // ─── 状態インスタンス ───
    public BossChargerAppearState StateAppear { get; private set; }
    public BossChargerIdleState StateIdle { get; private set; }
    public BossChargerChargeState StateCharge { get; private set; }
    public BossChargerStunState StateStun { get; private set; }
    public BossChargerShieldBashState StateShieldBash { get; private set; }
    public BossChargerShieldThrowState StateShieldThrow { get; private set; }
    public BossChargerCounterState StateCounter { get; private set; }
    public BossChargerRampageState StateRampage { get; private set; }
   // public BossChargerGroundSlamState StateGroundSlam { get; private set; }
    public BossChargerLeapSlamState StateLeapSlam { get; private set; }
    public BossChargerPhaseTransitionState StatePhaseTransition { get; private set; }
    public BossChargerDeadState StateDead { get; private set; }

    public BossChargerBaseState CurrentState { get; private set; }
    public string currentDebugStateName; // Inspector で現在状態を確認する用

    public bool IsPhase2 { get; set; }
    public bool HasUsedRampage { get; set; } // 必殺技は一度きり（発動済みフラグ）
    public float SpeedMultiplier => IsPhase2 ? phase2SpeedMultiplier : 1f;

    public Rigidbody2D Rb { get; private set; }
    public BossChargerHealth Health { get; private set; }

    // 盾投げ・カウンター・範囲技のクールダウン管理（状態から参照）
    public float ShieldThrowTimer { get; set; }
    public float CounterTimer { get; set; }
    public float GroundSlamTimer { get; set; }
    public float LeapSlamTimer { get; set; }

    private Transform player;
    private Rigidbody2D playerRb;
    private float currentVisualAngle;
    private float currentModelPitch;
    private int facing = -1; // -1=左 / +1=右

    // 予兆の視線ライン（実行時生成、OnDestroy で破棄）
    private LineRenderer telegraphLine;
    private Material telegraphMaterial;

    void Awake()
    {
        Rb = GetComponent<Rigidbody2D>();
        Health = GetComponent<BossChargerHealth>();

        // 巡回敵と同じく回転だけ固定（位置は MovePosition のスイープ衝突に任せる）
        if (Rb != null) Rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        CreateTelegraphVisual();

        StateAppear = new BossChargerAppearState(this);
        StateIdle = new BossChargerIdleState(this);
        StateCharge = new BossChargerChargeState(this);
        StateStun = new BossChargerStunState(this);
        StateShieldBash = new BossChargerShieldBashState(this);
        StateShieldThrow = new BossChargerShieldThrowState(this);
        StateCounter = new BossChargerCounterState(this);
        StateRampage = new BossChargerRampageState(this);
        StateLeapSlam = new BossChargerLeapSlamState(this);
        StatePhaseTransition = new BossChargerPhaseTransitionState(this);
        StateDead = new BossChargerDeadState(this);
    }

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null)
        {
            player = p.transform;
            p.TryGetComponent(out playerRb);
        }

        ShieldThrowTimer = shieldThrowCooldown; // 開幕からは投げない
        TransitionToState(StateAppear);
    }

    void Update()
    {
        if (ShieldThrowTimer > 0f) ShieldThrowTimer -= Time.deltaTime;
        if (CounterTimer > 0f) CounterTimer -= Time.deltaTime;
        if (GroundSlamTimer > 0f) GroundSlamTimer -= Time.deltaTime;
        if (LeapSlamTimer > 0f) LeapSlamTimer -= Time.deltaTime;

        CurrentState?.Update();

        // 予兆ラインは突進系・盾投げ中のみ表示する。他の状態へ移っても線が残らないよう、
        // それ以外では毎フレーム確実に消す（消し漏れの保険）。
        if (CurrentState != StateCharge && CurrentState != StateRampage && CurrentState != StateShieldThrow)
            HideChargeTelegraph();

        // フェーズ2移行（死亡・登場・移行中は除く）
        if (!IsPhase2 && Health != null && Health.CurrentHpRatio <= phase2HpThresholdRatio &&
            CurrentState != StateDead && CurrentState != StateAppear && CurrentState != StatePhaseTransition)
        {
            IsPhase2 = true;
            TransitionToState(StatePhaseTransition);
        }

        // 必殺技「憤怒の乱舞突進」：フェーズ2中にHPが閾値以下へ落ちたら一度だけ確定発動。
        // 他の技を中断しないよう、待機（Idle）に戻ったタイミングで発動する（ボスは頻繁にIdleへ戻る）。
        if (enableRampage && !HasUsedRampage && IsPhase2 && Health != null &&
            Health.CurrentHpRatio <= rampageHpThresholdRatio && CurrentState == StateIdle)
        {
            HasUsedRampage = true;
            TransitionToState(StateRampage);
        }
    }

    void FixedUpdate()
    {
        CurrentState?.FixedUpdate();
    }

    public void TransitionToState(BossChargerBaseState newState)
    {
        if (CurrentState == newState) return;
        CurrentState?.Exit();
        CurrentState = newState;
        currentDebugStateName = newState?.GetType().Name;
        CurrentState?.Enter();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // 本体へのバースト体当たり＝ボスへのダメージ。
        // 盾は本体 Rigidbody2D の複合コライダーなので、盾側でのヒットもこのイベントに届く。
        // 当たったのが本体コライダー（collision.otherCollider）のときだけダメージ処理する
        // （盾ヒットのガード・カウンターは BossChargerShield 側が処理）。
        if (collision.gameObject.CompareTag(playerTag) && collision.otherCollider == BodyCollider())
        {
            PlayerController p = collision.gameObject.GetComponent<PlayerController>();
            bool isBursting = p != null && p.CurrentState == p.StateBurst;
            if (p != null && isBursting) p.OnEnemyKilledInBurst(); // バースト回数の回復（EnemyCollision と同じ流儀）
            Health?.HandleHit(collision.relativeVelocity.magnitude, isBursting);
        }

        CurrentState?.OnCollisionEnter(collision);
    }

    // ─── 状態から使う共通ユーティリティ ───

    public Transform GetPlayerTransform() => player;
    public Rigidbody2D GetPlayerRigidbody() => playerRb;

    /// <summary>プレイヤーへの方向（プレイヤー不在なら現在の向き）</summary>
    public Vector2 DirectionToPlayer(bool horizontalOnly)
    {
        if (player == null) return Vector2.right * facing;
        Vector2 d = (Vector2)player.position - (Vector2)transform.position;
        if (horizontalOnly) return new Vector2(Mathf.Sign(d.x) == 0 ? facing : Mathf.Sign(d.x), 0f);
        return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.right * facing;
    }

    /// <summary>向き（左右）を設定してモデルと盾を追従させる。毎フレーム Update から呼ばれる想定</summary>
    public void SetFacing(int dir)
    {
        if (dir != 0) facing = dir > 0 ? 1 : -1;
        shield?.SetFacing(facing);
    }

    public int Facing => facing;

    /// <summary>モデルのY軸振り向きだけを進める（縦の向き付けなし）。通常状態から呼ぶ</summary>
    public void UpdateModelFacing() => UpdateModelFacing(Vector2.zero);

    /// <summary>
    /// モデルを左右（Y軸）＋上下（X軸ピッチ）で向ける。スナイパーと同じ「先にY軸→その先にX軸」の順序。
    /// aimDir に縦成分があるとき（斜め突進など）だけピッチが効く。突進方向を見せたいとき chargeDir を渡す。
    /// </summary>
    public void UpdateModelFacing(Vector2 aimDir)
    {
        if (visualTransform == null) return;

        float targetY = facing < 0 ? leftAngle : rightAngle;
        if (turnSpeed > 0f) currentVisualAngle = Mathf.MoveTowards(currentVisualAngle, targetY, turnSpeed * Time.deltaTime);
        else currentVisualAngle = targetY;

        float targetPitch = 0f;
        if (tiltModelToChargeDir && aimDir.sqrMagnitude > 0.0001f)
        {
            // 仰角（水平からの上下角）。左右どちら向きでも上下量は同じなので x は絶対値
            float elevation = Mathf.Atan2(aimDir.y, Mathf.Abs(aimDir.x)) * Mathf.Rad2Deg;
            targetPitch = Mathf.Clamp(invertModelPitch ? elevation : -elevation, -modelPitchMax, modelPitchMax);
        }
        if (turnSpeed > 0f) currentModelPitch = Mathf.MoveTowards(currentModelPitch, targetPitch, turnSpeed * Time.deltaTime);
        else currentModelPitch = targetPitch;

        visualTransform.localRotation = Quaternion.Euler(0f, currentVisualAngle, 0f) * Quaternion.Euler(currentModelPitch, 0f, 0f);
    }

    // ─── 予兆の視線ライン（EnemySniper の射線と同じ実行時 LineRenderer 方式） ───

    private void CreateTelegraphVisual()
    {
        GameObject go = new GameObject("BossChargeTelegraph");
        go.transform.SetParent(transform, false);

        telegraphLine = go.AddComponent<LineRenderer>();
        telegraphLine.useWorldSpace = true;
        telegraphLine.positionCount = 2;
        telegraphLine.numCapVertices = 2;
        telegraphLine.textureMode = LineTextureMode.Stretch;
        telegraphLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        telegraphLine.receiveShadows = false;
        telegraphLine.sortingOrder = 10; // 敵・プレイヤーより前に描く

        telegraphMaterial = new Material(Shader.Find("Sprites/Default"));
        telegraphMaterial.renderQueue = 3000; // Transparent
        telegraphLine.material = telegraphMaterial;

        HideChargeTelegraph();
    }

    /// <summary>
    /// 予兆中に突進予定の視線を描く（壁があればそこで止まる）。ChargeState の Windup から毎フレーム呼ぶ。
    /// progress01 は予兆の進行度（0=始まったばかり / 1=もう突進する直前）。色を黄→赤へ変えて
    /// 「いつ来るか」を分かりやすく見せる。
    /// </summary>
    public void ShowChargeTelegraph(Vector2 dir, float progress01)
    {
        if (telegraphLine == null || !showChargeTelegraph) { HideChargeTelegraph(); return; }

        Vector2 origin = transform.position;
        float len = chargeTelegraphMaxLength;
        RaycastHit2D hit = Physics2D.CircleCast(origin, BodyRadius(), dir, chargeTelegraphMaxLength, wallLayers);
        if (hit.collider != null) len = hit.distance;

        // 予兆の進行に合わせて黄（余裕）→赤（直前）へ。直前ほど濃く・危険に見せる
        Color col = Color.Lerp(chargeTelegraphColorStart, chargeTelegraphColorEnd, Mathf.Clamp01(progress01));

        telegraphLine.enabled = true;
        telegraphLine.startWidth = chargeTelegraphWidth;
        telegraphLine.endWidth = chargeTelegraphWidth;
        telegraphLine.startColor = col;
        // 先端はフェードさせて「伸びていく矢印」感を出す
        telegraphLine.endColor = new Color(col.r, col.g, col.b, 0f);

        float z = transform.position.z;
        telegraphLine.SetPosition(0, new Vector3(origin.x, origin.y, z));
        Vector2 end = origin + dir * len;
        telegraphLine.SetPosition(1, new Vector3(end.x, end.y, z));
    }

    /// <summary>
    /// 盾投げの軌道ラインを描く（突進予兆と同じ LineRenderer を流用）。壁があればそこで止まる。
    /// origin=盾の現在位置 / dir=投げる方向 / maxLen=盾の投擲距離 / progress01=予兆の進行度（黄→赤）。
    /// 盾は直進投擲なので CircleCast ではなく Raycast（BossChargerShield.Throw の折り返し判定と揃える）。
    /// </summary>
    public void ShowThrowTelegraph(Vector2 origin, Vector2 dir, float maxLen, float progress01)
    {
        if (telegraphLine == null || !showThrowTelegraph) { HideChargeTelegraph(); return; }

        float len = maxLen;
        RaycastHit2D hit = Physics2D.Raycast(origin, dir, maxLen, wallLayers);
        if (hit.collider != null) len = hit.distance;

        Color col = Color.Lerp(throwTelegraphColorStart, throwTelegraphColorEnd, Mathf.Clamp01(progress01));

        telegraphLine.enabled = true;
        telegraphLine.startWidth = chargeTelegraphWidth;
        telegraphLine.endWidth = chargeTelegraphWidth;
        telegraphLine.startColor = col;
        telegraphLine.endColor = new Color(col.r, col.g, col.b, 0f);

        float z = transform.position.z;
        telegraphLine.SetPosition(0, new Vector3(origin.x, origin.y, z));
        Vector2 end = origin + dir * len;
        telegraphLine.SetPosition(1, new Vector3(end.x, end.y, z));
    }

    public void HideChargeTelegraph()
    {
        if (telegraphLine != null) telegraphLine.enabled = false;
    }

    void OnDestroy()
    {
        if (telegraphMaterial != null) Destroy(telegraphMaterial);
    }

    /// <summary>進行方向の壁を先読みしつつ MovePosition で1物理ステップ移動する。壁に当たったら true</summary>
    public bool MoveSweep(Vector2 dir, float speed)
    {
        if (Rb == null) return false;
        float step = speed * Time.fixedDeltaTime;
        RaycastHit2D hit = Physics2D.CircleCast(Rb.position, BodyRadius(), dir, step + wallSkin, wallLayers);
        if (hit.collider != null)
        {
            float travel = Mathf.Max(0f, hit.distance - wallSkin);
            Rb.MovePosition(Rb.position + dir * travel);
            return true;
        }
        Rb.MovePosition(Rb.position + dir * step);
        return false;
    }

    private Collider2D bodyCol;

    /// <summary>本体（ルート）のコライダー。盾の複合コライダーと区別するために使う</summary>
    public Collider2D BodyCollider()
    {
        if (bodyCol == null) bodyCol = GetComponent<Collider2D>();
        return bodyCol;
    }

    public float BodyRadius()
    {
        Collider2D c = BodyCollider();
        if (c == null) return 0.5f;
        Vector2 e = c.bounds.extents;
        return Mathf.Max(0.01f, Mathf.Min(e.x, e.y));
    }

    /// <summary>盾がバーストで叩かれたとき BossChargerShield から呼ばれる（技⑥）</summary>
    public void OnShieldBurstHit()
    {
        if (!enableCounter || CounterTimer > 0f) return;
        // 反撃できるのは通常行動中のみ（スタン・死亡・演出中は反応しない）
        if (CurrentState != StateIdle && CurrentState != StateCharge) return;
        CounterTimer = counterCooldown;
        TransitionToState(StateCounter);
    }

    /// <summary>自滅スタン時の衝撃波（技④）。左右両方向へ地面を走らせる</summary>
    public void SpawnShockwaves()
    {
        if (shockwavePrefab == null) return;
        Vector3 pos = transform.position + (Vector3)shockwaveSpawnOffset;
        SpawnShockwave(pos, Vector2.left);
        SpawnShockwave(pos, Vector2.right);
    }

    private void SpawnShockwave(Vector3 pos, Vector2 dir)
    {
        GameObject go = Instantiate(shockwavePrefab, pos, Quaternion.identity);
        if (go.TryGetComponent(out BossChargerShockwave wave)) wave.Init(dir, wallLayers);
    }

    /// <summary>接触ダメージ（DamageSource）を一括でON/OFFする（死亡時など）</summary>
    public void SetAllDamageSourcesEnabled(bool enabled)
    {
        foreach (var src in GetComponentsInChildren<DamageSource>(true))
            if (src != null) src.enabled = enabled;
    }
}
