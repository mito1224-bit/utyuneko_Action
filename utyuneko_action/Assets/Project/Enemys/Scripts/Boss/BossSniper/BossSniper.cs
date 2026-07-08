using System.Collections.Generic;

using UnityEngine;

using UnityEngine.Events;



/// <summary>

/// 空中スナイパーボス（ステートパターン版・コントローラ）。

///

/// 状態遷移（各ステートは BossState_*.cs に分割。プレイヤーの IPlayerState と同じ流儀）:

///

///   行動ループ：ランダム行動（巡回／全体攻撃／横一斉射・重み付き抽選）を actionsBeforeSplit 回

///   挟むごとに、本命の分身攻撃（Split）を行う。各行動の終わりは Return（帰還テレポ）を経て

///   次の行動選択（ChooseNextAction）へつながる。

///

///   Patrol（巡回：多角形エリア内をランダムに瞬間移動）

///     ↔ PatrolShot（出現撃ち：瞬間移動が確率で攻撃に化ける。出現直後に狙いを固定して短いレーザー。

///                   偏差撃ち／直撃狙いが混ざる牽制。撃ち終えたら Patrol へ戻る）

///   RadialAttack（全体攻撃：ステージ中央＝巡回ポイントの重心へテレポートし、

///                 回転しながら四方八方へ1本ずつ連射）

///   SideBarrage（横一斉射：左右の壁沿いに互い違いの高さで分身を順次配置し、

///                全員そろったら中央向きの横レーザーを一斉発射。本体は上空から狙い撃ちを重ねる）

///   Split（分身展開）→ Aim（照準）→ Lock（固定・最終警告）→ Fire（レーザー発射）→ Return …

///

///   Aim / Lock 中に本物へバースト体当たり

///     → StunFall（無敵のまま落下）→ 着地 → StunGrace（着地猶予・まだ無敵）

///     → Stunned（スタン倍率の一撃が1回だけ通るボーナス帯）→ StunRecover → Return

///

/// HP・難易度（フェーズ制は廃止）:

///   - HPは BossSniperHealth の単一プール（maxHP）。削り切ったら撃破（Defeated）。

///   - 難易度パラメータは normalSettings（通常）／ enragedSettings（強化）の2セットで、

///     HPがしきい値（既定：半分）以下になると自動で強化セットへ切り替わる（Difficulty プロパティ）。

///   - 強化時は巡回中に「お供分身」1体が常駐し、巡回テレポートしながらプレイヤーを狙撃する。

///     お供分身はバーストで破壊でき（バースト回数を回復）、その巡回中は再出現しない。

///     巡回（Patrol / PatrolShot）以外の状態では退場する。

///

/// 移動はすべて瞬間移動（XZスケール収縮 → 座標切替 → 復元。消えている間は当たり判定なし）。

/// 例外はスタンの落下のみ（Dynamic な Rigidbody2D の重力に任せる）。

///

/// 分身の配置は、分裂した瞬間のプレイヤー位置を中心としたリングを試し、

/// 1体でも地形（obstacleLayer）に接するなら 横一列／縦一列／弧状のいずれか（ランダム）の

/// 「埋まらない整列」に切り替える。整列も全員が埋まらない置き方を探してから確定する。

///

/// セットアップ:

///   ボスルート: Rigidbody2D(Kinematic, FreezeRotation) + Collider2D(非Trigger) + BossSniper + BossBeamUnit

///   分身プレハブ: Collider2D(IsTrigger) + BossBeamUnit + 見た目の子（ビーム設定は本体から自動コピー）

///   巡回ポイント: 空中に3つ以上置くと、その多角形の内側へランダムに瞬間移動する

/// </summary>

[RequireComponent(typeof(Rigidbody2D))]

[RequireComponent(typeof(BossSniperBeamUnit))]

[RequireComponent(typeof(BossSniperHealth))]

public class BossSniper : MonoBehaviour

{

    // ─── 難易度設定（通常／強化の2セット。HP比率で自動切替） ───



    [System.Serializable]

    public class DifficultySettings

    {

        [Header("分身攻撃")]

        [Tooltip("ユニット総数（本物1を含む）。4なら 本物1＋偽物3")]

        public int totalUnits = 4;



        [Tooltip("照準（射線がプレイヤーを追う）時間")]

        public float aimTime = 1.5f;



        [Tooltip("射線固定後、レーザー発射までの最終警告時間")]

        public float lockTime = 0.35f;



        [Tooltip("レーザー（攻撃判定）が出ている時間")]

        public float fireDuration = 0.25f;



        [Header("スタン")]

        [Tooltip("着地して猶予が明けてから、ダメージを受け付けている時間")]

        public float stunDuration = 4f;



        [Tooltip("スタン中に通る一撃の倍率。通常の速度依存ダメージにこれを掛ける（見破りスタンのボーナス）")]

        public float stunDamageMultiplier = 4f;



        [Header("巡回ショット（出現撃ち）")]

        [Tooltip("巡回中の瞬間移動が「出現撃ち」になる確率（0〜1）")]

        [Range(0f, 1f)] public float patrolShotChance = 0.5f;



        [Tooltip("出現撃ちの、出現〜レーザー発射までのロック時間（短いほど難しい）")]

        public float patrolShotLockTime = 0.5f;



        [Tooltip("1回の出現撃ちで連続して撃つ回数。2発目以降は毎回狙いを付け直す（偏差／直撃も毎回抽選）")]

        public int patrolShotCount = 1;



        [Header("全体攻撃")]

        [Tooltip("全体攻撃で撃つ総数。角度ステップ×回数ぶんだけ回転しながら連射する")]

        public int radialShotCount = 10;



        [Tooltip("全体攻撃の1発ごとのロック時間（短いほど回転が速く難しい）")]

        public float radialLockTime = 0.3f;



        [Header("横一斉射（左右の壁から中央へ）")]

        [Tooltip("横一斉射で配置する分身の数。左右交互・互い違いの高さに置かれる")]

        public int sidebarCloneCount = 4;



        [Tooltip("全員配置完了後、一斉発射までのロック時間（赤い射線での最終警告）")]

        public float sidebarLockTime = 0.6f;

    }



    [Header("難易度（通常時：HPが強化しきい値より上）")]

    public DifficultySettings normalSettings = new DifficultySettings

    {

        totalUnits = 4,

        aimTime = 1.5f,

        patrolShotChance = 0.35f,

        patrolShotLockTime = 0.6f,

        patrolShotCount = 1,

        radialShotCount = 8,

        radialLockTime = 0.35f,

        stunDuration = 4f,

        stunDamageMultiplier = 4f,

        sidebarCloneCount = 4,

        sidebarLockTime = 0.65f,

    };



    [Header("難易度（強化時：HPが半分以下）")]

    [Tooltip("BossSniperHealth の enragedThresholdRatio（既定0.5＝半分）を下回ると、こちらのセットに切り替わる")]

    public DifficultySettings enragedSettings = new DifficultySettings

    {

        totalUnits = 6,

        aimTime = 1.2f,

        patrolShotChance = 0.75f,

        patrolShotLockTime = 0.4f,

        patrolShotCount = 3,

        radialShotCount = 16,

        radialLockTime = 0.25f,

        stunDuration = 3f,

        stunDamageMultiplier = 5f,

        sidebarCloneCount = 6,

        sidebarLockTime = 0.45f,

    };



    // ─── 巡回（多角形エリア内の瞬間移動） ─────────────



    [Header("巡回（通常状態）")]

    [Tooltip("プレイヤーのタグ")]

    public string playerTag = "Player";



    [Tooltip("巡回エリアを形作る頂点（3つ以上推奨・外周の順に並べる）。その多角形の内側へランダムに瞬間移動する。2つなら線分上、1つならその点、未設定なら初期位置の左右幅内")]

    public Transform[] patrolPoints;



    [Tooltip("巡回ポイント未設定時の、初期位置からの左右のランダム幅")]

    public float patrolHalfWidth = 5f;



    [Tooltip("巡回中に瞬間移動する間隔（秒）")]

    public float teleportInterval = 1.8f;



    [Tooltip("巡回してから次の分身攻撃を始めるまでの時間（＝攻撃の間隔）")]

    public float timeBetweenAttacks = 3.5f;



    // ─── 瞬間移動の演出 ────────────────────────────



    [Header("瞬間移動")]

    [Tooltip("XZスケールが収縮しきるまでの時間")]

    public float teleportShrinkTime = 0.15f;



    [Tooltip("XZスケールが元に戻るまでの時間")]

    public float teleportExpandTime = 0.15f;



    // ─── 巡回ショット（テレポ出現撃ち） ─────────────────



    [Header("巡回ショット（テレポ出現撃ち）")]

    [Tooltip("出現撃ちのレーザーが出ている時間")]

    public float patrolShotFireDuration = 0.15f;



    [Tooltip("偏差撃ちのとき、プレイヤーの何秒先の位置を狙うか（現在速度×この秒数だけ先）。ロック時間と同じくらいにすると、走り続けるプレイヤーに刺さる")]

    public float patrolShotLeadTime = 0.5f;



    [Tooltip("出現撃ちが偏差撃ちになる確率（0〜1）。外れは現在位置への直撃狙い。両方混ぜることで「止まれば安全」も「走り続ければ安全」も成立しなくなる")]

    [Range(0f, 1f)] public float patrolShotLeadChance = 0.6f;



    // ─── 全体攻撃（ステージ中央での回転連射） ─────────────



    [Header("全体攻撃（ステージ中央での回転連射）")]

    [Tooltip("全体攻撃のレーザーが出ている時間（1発ぶん）")]

    public float radialFireDuration = 0.12f;



    [Tooltip("1発ごとに回転する角度（度）。回転方向と開始角度は毎回ランダム")]

    public float radialAngleStep = 30f;



    [Tooltip("中央に到着してから初弾までの溜め時間。この間に初弾のロック射線を出してプレイヤーに避ける準備をさせる")]

    public float radialWindupTime = 1.2f;



    // ─── 行動ループ（ランダム行動→分身攻撃） ─────────────



    [Header("行動ループ（ランダム行動→分身攻撃）")]

    [Tooltip("分身攻撃までに挟むランダム行動（巡回／全体攻撃／横一斉射）の回数")]

    public int actionsBeforeSplit = 3;



    [Tooltip("ランダム行動の重み：巡回（テレポ待機＋出現撃ち）。重みが大きいほど選ばれやすい")]

    public float actionWeightPatrol = 1f;



    [Tooltip("ランダム行動の重み：全体攻撃（中央での回転連射）")]

    public float actionWeightRadial = 1f;



    [Tooltip("ランダム行動の重み：横一斉射（左右の壁から中央へ）")]

    public float actionWeightSidebar = 1f;



    // ─── 横一斉射（左右の壁から中央へ一斉に撃つ） ─────────



    [Header("横一斉射")]

    [Tooltip("横一斉射のエリアを決める対角の2点（矩形の角）。未設定なら巡回ポイントの外接矩形を使う")]

    public Transform sidebarAreaA;

    public Transform sidebarAreaB;



    [Tooltip("左右に配置する設置ユニットのプレハブ（分身とは別モデル）。壊せず、バースト含め触れたプレイヤーが接触ダメージを受ける。未設定なら分身プレハブを流用")]

    public GameObject sidebarUnitPrefab;



    [Tooltip("設置ユニットを1体置くごとの間隔（秒）。左右交互・互い違いの高さに順次出現する")]

    public float sidebarPlaceInterval = 0.3f;



    [Tooltip("一斉発射のレーザーが出ている時間")]

    public float sidebarFireDuration = 0.2f;



    // ─── お供分身（強化時・巡回中のみ常駐する1体） ─────────



    [System.Serializable]

    public class CompanionSettings

    {

        [Tooltip("お供分身が瞬間移動する間隔（秒）。テレポ後に1発撃ってから次のテレポまで待つ")]

        public float teleportInterval = 3f;



        [Tooltip("出現〜レーザー発射までのロック時間。本体の出現撃ちより長め（弱め）にするのがおすすめ")]

        public float lockTime = 0.8f;



        [Tooltip("レーザーが出ている時間")]

        public float fireDuration = 0.12f;



        [Tooltip("レーザーのダメージ量（本体より弱めに設定できる）")]

        public int beamDamage = 1;



        [Tooltip("偏差撃ちになる確率（0〜1）。外れは直撃狙い")]

        [Range(0f, 1f)] public float leadChance = 0.4f;



        [Tooltip("偏差撃ちのとき、プレイヤーの何秒先を狙うか")]

        public float leadTime = 0.5f;

    }



    [Header("お供分身（強化時・巡回中のみ）")]

    [Tooltip("お供分身を使うか。オフなら強化時でも出現しない")]

    public bool companionEnabled = true;



    [Tooltip("お供分身の攻撃パラメータ（本体より弱めに調整する用）")]

    public CompanionSettings companionSettings = new CompanionSettings();



    [Tooltip("偽物（分身攻撃の偽物・お供分身）をバーストで破壊したとき、プレイヤーのバースト回数を回復させるか")]

    public bool refundPlayerBurstOnCloneDestroyed = true;



    // ─── 分身の展開 ────────────────────────────────



    [Header("分身の展開")]

    [Tooltip("偽物の分身プレハブ（Collider2D(IsTrigger)＋BossBeamUnit＋見た目）")]

    public GameObject clonePrefab;



    [Tooltip("円形配置の余白。巡回エリアに収まる最大半径からこの分だけ内側に寄せる")]

    public float splitRingMargin = 1.5f;



    [Tooltip("巡回テレポート先の埋まり判定の半径。ボスのコライダーより少し大きめにすると地面スレスレ配置を防げる")]

    public float slotCheckRadius = 1.2f;



    // ─── スタン・被弾 ──────────────────────────────



    [Header("スタン・被弾")]

    [Tooltip("スタン落下時の重力スケール")]

    public float stunGravityScale = 2.5f;



    [Tooltip("撃破後のRigidbody2Dの質量。大きいほどプレイヤーに押されにくくなる")]

    public float defeatedMass = 1000f;



    [Tooltip("着地してからダメージを受け付け始めるまでの猶予時間。落下中〜この間は無敵")]

    public float postLandingGrace = 0.5f;



    [Tooltip("スタン落下が長引いた場合（着地を検知できない場合）に諦めて復帰する保険時間")]

    public float stunFallTimeout = 3f;



    [Tooltip("スタン中に倍率付きの一撃を受けたあと、復帰までに置く間（秒）")]

    public float stunRecoverDelay = 0.6f;



    [Tooltip("撃破後に本体を Destroy するまでの秒数。0以下なら残す（演出を外部で行う場合など）")]

    public float destroyDelayOnDefeat = 0f;



    // ─── イベント（SE・エフェクト・UI 接続用） ─────────



    [Header("イベント")]

    public UnityEvent onSplit;          // 分身展開の開始

    public UnityEvent onStunned;        // スタン開始（本物を見破られた）

    public UnityEvent onEnraged;        // 強化モードに入った（HPが半分以下になった瞬間・1回だけ）

    public UnityEvent onDefeated;       // 撃破された

    public UnityEvent onCloneDestroyed; // 偽物（お供分身含む）が破壊された



    // ─── ステート（プレイヤーと同じ流儀で公開プロパティにする） ───



    public BossSniperState_Patrol StatePatrol { get; private set; }

    public BossSniperState_PatrolShot StatePatrolShot { get; private set; }

    public BossSniperState_RadialAttack StateRadialAttack { get; private set; }

    public BossSniperState_SideBarrage StateSideBarrage { get; private set; }

    public BossSniperState_Split StateSplit { get; private set; }

    public BossSniperState_Aim StateAim { get; private set; }

    public BossSniperState_Lock StateLock { get; private set; }

    public BossSniperState_Fire StateFire { get; private set; }

    public BossSniperState_Return StateReturn { get; private set; }

    public BossSniperState_StunFall StateStunFall { get; private set; }

    public BossSniperState_StunGrace StateStunGrace { get; private set; }

    public BossSniperState_Stunned StateStunned { get; private set; }

    public BossSniperState_StunRecover StateStunRecover { get; private set; }

    public BossSniperState_Defeated StateDefeated { get; private set; }



    public IBossSniperState CurrentState => currentState;

    private IBossSniperState currentState;



    /// <summary>

    /// ボスだけのヒットストップ中か（BossSniperHitStop が制御）。

    /// true の間は Update/FixedUpdate でステート更新をスキップし、ボスの内部時間を止める。

    /// プレイヤー・UI は影響を受けない。

    /// </summary>

    public bool HitStopActive { get; set; }



    // ─── ステートから使う共有参照 ─────────────────────



    public Transform Player { get; private set; }



    /// <summary>プレイヤーの Rigidbody2D（偏差撃ちの速度取得に使う。無ければ null）。</summary>

    public Rigidbody2D PlayerRb { get; private set; }



    public Rigidbody2D Rb { get; private set; }

    public BossSniperBeamUnit SelfUnit { get; private set; }



    /// <summary>HP・ダメージ・無敵時間を管理するコンポーネント（同じ GameObject 上）。</summary>

    public BossSniperHealth Health { get; private set; }



    /// <summary>強化モード（HPが半分以下）か。難易度セットとお供分身の出現条件に使う。</summary>

    public bool IsEnraged => Health != null && Health.IsEnraged;



    /// <summary>現在のHP状態に応じた難易度セット（通常／強化）。</summary>

    public DifficultySettings Difficulty => IsEnraged ? enragedSettings : normalSettings;



    /// <summary>

    /// 次の分身攻撃までの残り時間。ボス側が持つことで、

    /// 出現撃ち（PatrolShot）を挟んでもリセットされず、分身攻撃の間隔が一定に保たれる。

    /// Patrol / PatrolShot が減算し、Return（攻撃サイクルの区切り）で補充される。

    /// </summary>

    public float AttackTimer { get; set; }



    /// <summary>展開中のユニット一覧（本物を含む）。分身展開中以外は空。</summary>

    public List<BossSniperBeamUnit> Units { get; } = new List<BossSniperBeamUnit>();



    // お供分身（強化時・巡回中のみ常駐する1体）

    private BossSniperCompanion companion;

    private bool companionKilledThisPatrol; // 撃破されたら、その巡回中は再出現しない



    private Vector3 patrolOrigin;



    // ─── Unity ライフサイクル ─────────────────────────



    void Awake()

    {

        Rb = GetComponent<Rigidbody2D>();

        SelfUnit = GetComponent<BossSniperBeamUnit>();

        Health = GetComponent<BossSniperHealth>();



        Rb.bodyType = RigidbodyType2D.Kinematic;

        Rb.freezeRotation = true;

        Rb.gravityScale = 0f;



        StatePatrol = new BossSniperState_Patrol();

        StatePatrolShot = new BossSniperState_PatrolShot();

        StateRadialAttack = new BossSniperState_RadialAttack();

        StateSideBarrage = new BossSniperState_SideBarrage();

        StateSplit = new BossSniperState_Split();

        StateAim = new BossSniperState_Aim();

        StateLock = new BossSniperState_Lock();

        StateFire = new BossSniperState_Fire();

        StateReturn = new BossSniperState_Return();

        StateStunFall = new BossSniperState_StunFall();

        StateStunGrace = new BossSniperState_StunGrace();

        StateStunned = new BossSniperState_Stunned();

        StateStunRecover = new BossSniperState_StunRecover();

        StateDefeated = new BossSniperState_Defeated();

    }



    void Start()

    {

        GameObject p = GameObject.FindGameObjectWithTag(playerTag);

        if (p != null)

        {

            Player = p.transform;

            PlayerRb = p.GetComponent<Rigidbody2D>();

        }



        SelfUnit.Init(Player);

        SelfUnit.IsReal = true;

        SelfUnit.OnBurstHit = RouteBurstHit;



        patrolOrigin = transform.position;

        AttackTimer = timeBetweenAttacks;



        // HPの初期化は BossSniperHealth が自分で行う（単一HP・フェーズ廃止）



        TransitionToState(StatePatrol);

    }



    void Update()

    {

        UpdateCompanionPresence(); // お供分身の出現管理（強化時・巡回中のみ）



        if (HitStopActive) return; // ボスだけフリーズ中は内部時間を止める

        currentState?.UpdateState();

    }



    void FixedUpdate()

    {

        if (HitStopActive) return;

        currentState?.FixedUpdateState();

    }



    public void TransitionToState(IBossSniperState newState)

    {

        // 撃破後は他の状態へ戻らない（撃破と同フレームの被弾・スタン処理などによる「復活」を防ぐ最終防壁）

        if (currentState == StateDefeated) return;



        if (currentState != null)

        {

            currentState.Exit();

        }



        currentState = newState;

        currentState.Enter(this);



        // 巡回（Patrol / PatrolShot）以外へ移ったら、お供分身は退場。

        // 撃破フラグもここでリセットするので、次に巡回へ戻ったときは再出現できる

        if (!IsPatrolLikeState(currentState))

        {

            DespawnCompanion();

            companionKilledThisPatrol = false;

        }

    }



    /// <summary>巡回系のステート（お供分身が居られる場面）か。出現撃ちも巡回の一部として扱う。</summary>

    public bool IsPatrolLikeState(IBossSniperState s)

    {

        return s == StatePatrol || s == StatePatrolShot;

    }



    // ─── ステートへのイベント中継 ─────────────────────



    // 各ユニットの OnBurstHit はここに集約し、現在のステートに反応を委ねる

    private void RouteBurstHit(BossSniperBeamUnit unit, PlayerController pc)

    {

        currentState?.OnBurstHit(unit, pc);

    }



    /// <summary>

    /// 「常時ダメージ可能」なステート（巡回・出現撃ち・全体攻撃）から呼ぶ共通の被弾処理。

    /// 本物なら通常ダメージ（無敵時間つき）、偽物なら何もしない。

    /// 分身展開中の照準・ロックは別扱い（スタンも起こす）なので各ステートで個別処理する。

    /// </summary>

    public void HandleNormalBurstHit(BossSniperBeamUnit unit, PlayerController pc)

    {

        if (unit == null || !unit.IsReal) return;

        if (Health != null) Health.TryApplyBurstDamage(unit, pc, isStunned: false);

    }



    // 地形（obstacleLayer）との接触を現在のステートへ渡す（スタン落下の着地検知用）

    void OnCollisionEnter2D(Collision2D collision)

    {

        if (((1 << collision.gameObject.layer) & SelfUnit.obstacleLayer) == 0) return;

        currentState?.OnGroundHit();

    }



    // ─── ユニット（分身）管理 ─────────────────────────



    /// <summary>

    /// 配置ポイントへ本体を移動し、残りの枠に偽物を「収縮しきった状態」で生成する。

    /// 本体が縮んで消えている間に呼ぶこと（Split ステートが使う）。出現（Expand）は呼び出し側が行う。

    /// </summary>

    public void DeployFormation(List<Vector2> slots, int realIndex)

    {

        Units.Clear();

        Units.Add(SelfUnit);

        transform.position = slots[realIndex];



        for (int i = 0; i < slots.Count; i++)

        {

            if (i == realIndex) continue;



            GameObject go = Instantiate(clonePrefab, slots[i], Quaternion.identity);

            BossSniperBeamUnit u = go.GetComponent<BossSniperBeamUnit>();

            if (u == null) u = go.AddComponent<BossSniperBeamUnit>();



            u.CopySettingsFrom(SelfUnit);

            u.Init(Player);

            u.IsReal = false;

            u.OnBurstHit = RouteBurstHit;

            u.SetShrunkenImmediate(); // 縮んだ状態で待機し、全員同時に出現させる

            Units.Add(u);

        }

    }



    /// <summary>

    /// 横一斉射のエリア矩形（min, max）を返す。

    /// sidebarAreaA/B（対角の2点）が設定されていればそれを、無ければ巡回ポイントの外接矩形を使う。

    /// </summary>

    public void GetSidebarArea(out Vector2 min, out Vector2 max)

    {

        if (sidebarAreaA != null && sidebarAreaB != null)

        {

            Vector2 a = sidebarAreaA.position;

            Vector2 b = sidebarAreaB.position;

            min = Vector2.Min(a, b);

            max = Vector2.Max(a, b);

            return;

        }



        // フォールバック：巡回ポイントの外接矩形

        GetPatrolBounds(out min, out max);

    }



    /// <summary>巡回ポイントの外接矩形（巡回エリアのおおまかな広さ）を求める。</summary>

    public void GetPatrolBounds(out Vector2 min, out Vector2 max)

    {

        min = max = (Vector2)transform.position;

        bool first = true;

        if (patrolPoints != null)

        {

            foreach (Transform t in patrolPoints)

            {

                if (t == null) continue;

                Vector2 p = t.position;

                if (first) { min = max = p; first = false; }

                else { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }

            }

        }

    }



    /// <summary>

    /// 偽物の分身を1体、収縮状態で生成して Units に登録する（横一斉射などの配置攻撃用）。

    /// 被弾はボスのステートへ通知され、バーストで壊せる（DestroyClone 扱い）。出現（Expand）は呼び出し側が行う。

    /// </summary>

    public BossSniperBeamUnit SpawnCloneAt(Vector2 pos)

    {

        GameObject go = Instantiate(clonePrefab, pos, Quaternion.identity);

        BossSniperBeamUnit u = go.GetComponent<BossSniperBeamUnit>();

        if (u == null) u = go.AddComponent<BossSniperBeamUnit>();



        u.CopySettingsFrom(SelfUnit);

        u.Init(Player);

        u.IsReal = false;

        u.OnBurstHit = RouteBurstHit;

        u.SetShrunkenImmediate();

        Units.Add(u);

        return u;

    }



    /// <summary>

    /// 横一斉射用の「設置ユニット」を収縮状態で1体生成する（分身とは別モデル・sidebarUnitPrefab）。

    /// 破壊不可（IsHazardUnit）：バーストで壊せず、バースト含め触れたプレイヤーが接触ダメージを受ける。

    /// Units に登録するので、後片付けは分身と同じく Return の DespawnClones() が行う。

    /// </summary>

    public BossSniperBeamUnit SpawnSidebarUnitAt(Vector2 pos)

    {

        GameObject prefab = sidebarUnitPrefab != null ? sidebarUnitPrefab : clonePrefab;

        GameObject go = Instantiate(prefab, pos, Quaternion.identity);

        BossSniperBeamUnit u = go.GetComponent<BossSniperBeamUnit>();

        if (u == null) u = go.AddComponent<BossSniperBeamUnit>();



        u.CopySettingsFrom(SelfUnit); // ビーム見た目・接触ダメージ量なども本体から引き継ぐ

        u.Init(Player);

        u.IsReal = false;

        u.IsHazardUnit = true; // 壊せない設置物。OnBurstHit は通知されないので配線不要

        u.SetShrunkenImmediate();

        Units.Add(u);

        return u;

    }



    /// <summary>偽物を1体破壊する（見破りの外れ）。破壊者が居ればバースト回数を回復させる。</summary>

    public void DestroyClone(BossSniperBeamUnit unit, PlayerController pc = null)

    {

        if (unit == null || unit.IsReal) return;

        Units.Remove(unit);

        Destroy(unit.gameObject);



        if (refundPlayerBurstOnCloneDestroyed && pc != null) pc.OnEnemyKilledInBurst();

        onCloneDestroyed?.Invoke();

    }



    /// <summary>偽物を全て破壊してユニット一覧を空にする。</summary>

    public void DespawnClones()

    {

        foreach (BossSniperBeamUnit u in Units)

        {

            if (u != null && !u.IsReal) Destroy(u.gameObject);

        }

        Units.Clear();

    }



    // ─── お供分身の管理 ─────────────────────────────



    // 毎フレーム：出現条件が揃っていて不在なら生成する（強化時・巡回中・未撃破・プレハブあり）

    private void UpdateCompanionPresence()

    {

        if (companion != null) return;

        if (!companionEnabled || !IsEnraged) return;

        if (!IsPatrolLikeState(currentState)) return;

        if (companionKilledThisPatrol) return;

        if (clonePrefab == null || Player == null) return;



        SpawnCompanion();

    }



    private void SpawnCompanion()

    {

        GameObject go = Instantiate(clonePrefab, RandomPatrolPoint(), Quaternion.identity);

        BossSniperBeamUnit u = go.GetComponent<BossSniperBeamUnit>();

        if (u == null) u = go.AddComponent<BossSniperBeamUnit>();



        u.CopySettingsFrom(SelfUnit);

        u.Init(Player);

        u.IsReal = false;

        u.beamDamage = Mathf.Max(0, companionSettings.beamDamage); // 本体より弱く設定できる



        companion = go.AddComponent<BossSniperCompanion>();

        companion.Init(this, u);

    }



    /// <summary>お供分身がバーストで撃破された（BossSniperCompanion から呼ばれる）。</summary>

    public void OnCompanionKilled(PlayerController pc)

    {

        companionKilledThisPatrol = true; // この巡回中は再出現しない

        companion = null;



        if (refundPlayerBurstOnCloneDestroyed && pc != null) pc.OnEnemyKilledInBurst();

        onCloneDestroyed?.Invoke();

    }



    private void DespawnCompanion()

    {

        if (companion != null)

        {

            Destroy(companion.gameObject);

            companion = null;

        }

    }



    /// <summary>

    /// お供分身と分身攻撃の複製をまとめて即消去する（強化カットインの開始時などに使う）。

    /// EventPaused 中は UpdateCompanionPresence が止まるので、演出中に呼べば再出現しない。

    /// 演出後に巡回へ戻れば、強化中である限り通常どおりまた出現する。

    /// </summary>

    public void DespawnAllMinions()

    {

        DespawnCompanion();

        DespawnClones();

    }



    // ─── 被ダメージ・物理切り替え ─────────────────────



    /// <summary>強化モードに入った瞬間に Health から呼ばれる（HPがしきい値を下回った・1回だけ）。</summary>

    public void NotifyEnraged()

    {

        onEnraged?.Invoke();

    }



    /// <summary>HPを削り切ったときに Health から呼ばれる。撃破へ。</summary>

    public void DefeatByHP()

    {

        TransitionToState(StateDefeated);

    }



    /// <summary>スタン落下用：Dynamic に切り替えて重力で落とす。</summary>

    public void BeginFallBody()

    {

        Rb.bodyType = RigidbodyType2D.Dynamic;

        Rb.gravityScale = stunGravityScale;

        Rb.linearVelocity = Vector2.zero;

        SelfUnit.SetHitboxEnabled(true); // 落下・着地にはコライダーが必要

    }



    /// <summary>

    /// 撃破後用：重力で落としつつ、質量を大きくしてプレイヤーに押されにくくする。

    /// </summary>

    public void BeginDefeatedBody()

    {

        BeginFallBody();



        if (Rb != null)

        {

            Rb.mass = Mathf.Max(1f, defeatedMass);

            Rb.linearVelocity = Vector2.zero;

            Rb.angularVelocity = 0f;

        }

    }



    /// <summary>飛行用：Kinematic に戻す。</summary>

    public void RestoreFlightBody()

    {

        Rb.bodyType = RigidbodyType2D.Kinematic;

        Rb.gravityScale = 0f;

        Rb.linearVelocity = Vector2.zero;

    }



    // ─── 行動ループ ─────────────────────────────────



    private int actionCount; // 分身攻撃後に実行したランダム行動の回数



    /// <summary>

    /// 次の行動を決める（巡回の時間切れ・帰還完了のたびに呼ばれる）。

    /// 巡回／全体攻撃／横一斉射 を重み付きランダムで選び、

    /// actionsBeforeSplit 回の行動を挟んだら分身攻撃（Split）へ。

    /// 巡回が選ばれた場合は AttackTimer（＝その巡回行動の長さ）を補充する。

    /// </summary>

    public IBossSniperState ChooseNextAction()

    {

        actionCount++;

        if (actionCount > actionsBeforeSplit)

        {

            actionCount = 0; // カウントを仕切り直して本命の分身攻撃へ

            return StateSplit;

        }



        float wPatrol = Mathf.Max(0f, actionWeightPatrol);

        float wRadial = Mathf.Max(0f, actionWeightRadial);

        float wSidebar = Mathf.Max(0f, actionWeightSidebar);

        float total = wPatrol + wRadial + wSidebar;

        if (total <= 0f) { AttackTimer = timeBetweenAttacks; return StatePatrol; } // 全部0なら巡回



        float r = Random.value * total;

        if (r < wPatrol)

        {

            AttackTimer = timeBetweenAttacks; // 巡回行動1回ぶんの時間を補充

            return StatePatrol;

        }

        r -= wPatrol;

        if (r < wRadial) return StateRadialAttack;

        return StateSideBarrage;

    }



    // ─── 巡回エリア（多角形）内のランダム位置 ─────────────



    /// <summary>ステージ中央＝巡回ポイントの重心（未設定なら初期位置）。全体攻撃の立ち位置。</summary>

    public Vector2 StageCenter()

    {

        Vector2 sum = Vector2.zero;

        int n = 0;

        if (patrolPoints != null)

        {

            foreach (Transform t in patrolPoints)

            {

                if (t == null) continue;

                sum += (Vector2)t.position;

                n++;

            }

        }

        return n > 0 ? sum / n : (Vector2)patrolOrigin;

    }



    /// <summary>

    /// 巡回ポイントが作る多角形の内側からランダムな1点を返す（地形に埋まらない位置を優先）。

    /// 頂点が2つなら線分上、1つならその点、未設定なら初期位置の左右幅内。

    /// </summary>

    public Vector2 RandomPatrolPoint()

    {

        var verts = new List<Vector2>();

        if (patrolPoints != null)

        {

            foreach (Transform t in patrolPoints)

            {

                if (t != null) verts.Add(t.position);

            }

        }



        if (verts.Count == 0)

        {

            return (Vector2)patrolOrigin + new Vector2(Random.Range(-patrolHalfWidth, patrolHalfWidth), 0f);

        }

        if (verts.Count == 1) return verts[0];

        if (verts.Count == 2)

        {

            for (int i = 0; i < 10; i++)

            {

                Vector2 p = Vector2.Lerp(verts[0], verts[1], Random.value);

                if (IsSlotClear(p)) return p;

            }

            return Vector2.Lerp(verts[0], verts[1], Random.value);

        }



        // 3点以上：バウンディングボックス内でサンプリングし、多角形の内側かつ地形に埋まらない点を探す

        Vector2 min = verts[0], max = verts[0];

        foreach (Vector2 v in verts)

        {

            min = Vector2.Min(min, v);

            max = Vector2.Max(max, v);

        }



        for (int i = 0; i < 40; i++)

        {

            Vector2 p = new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));

            if (PointInPolygon(p, verts) && IsSlotClear(p)) return p;

        }



        // 見つからなければ頂点のどれか（設置ポイント自体は空中にある前提）

        return verts[Random.Range(0, verts.Count)];

    }



    // 点が多角形の内側にあるか（レイ交差法）

    private static bool PointInPolygon(Vector2 p, List<Vector2> poly)

    {

        bool inside = false;

        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)

        {

            Vector2 a = poly[i];

            Vector2 b = poly[j];

            if ((a.y > p.y) != (b.y > p.y) &&

                p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)

            {

                inside = !inside;

            }

        }

        return inside;

    }



    // ─── 配置ポイントの生成（巡回エリア中央を囲む円形） ───



    /// <summary>

    /// 分身の配置ポイント一覧を作る。巡回エリアの中央（StageCenter＝巡回ポイントの重心）を囲む、

    /// 等間隔のきれいな円形配置。半径はエリア（巡回ポイントの外接矩形）に収まる最大から

    /// splitRingMargin を引いて自動で決める。円の向き（開始角度）は毎回ランダムに回転させるので、

    /// 等間隔は保ちつつ、毎回の配置座標から本物の位置を読むことはできない。

    /// </summary>

    public List<Vector2> BuildFormationSlots(int count)

    {

        Vector2 center = StageCenter();

        float radius = ComputeSplitRingRadius(center);



        float baseAngle = Random.Range(0f, Mathf.PI * 2f); // 毎回ランダムに回転

        var slots = new List<Vector2>(count);

        for (int i = 0; i < count; i++)

        {

            float a = baseAngle + (Mathf.PI * 2f / count) * i;

            slots.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);

        }

        return slots;

    }



    // 巡回エリア（外接矩形）に収まる最大半径 − 余白。中央から四辺までの最短距離を基準にする

    private float ComputeSplitRingRadius(Vector2 center)

    {

        GetPatrolBounds(out Vector2 min, out Vector2 max);

        float half = Mathf.Min(

            Mathf.Min(center.x - min.x, max.x - center.x),

            Mathf.Min(center.y - min.y, max.y - center.y));

        return Mathf.Max(1f, half - splitRingMargin);

    }



    // その位置にボスを置いたとき地形（obstacleLayer）に接しないか（巡回テレポート先の判定に使用）。

    // TilemapCollider2D / CompositeCollider2D も物理的にはただの Collider2D なので、そのまま拾える

    public bool IsSlotClear(Vector2 pos)

    {

        return Physics2D.OverlapCircle(pos, slotCheckRadius, SelfUnit.obstacleLayer) == null;

    }



    // ─── ギズモ ───────────────────────────────────



    private void OnDrawGizmosSelected()

    {

        // 巡回エリアの多角形

        Gizmos.color = Color.cyan;

        if (patrolPoints != null && patrolPoints.Length > 1)

        {

            for (int i = 0; i < patrolPoints.Length; i++)

            {

                Transform a = patrolPoints[i];

                Transform b = patrolPoints[(i + 1) % patrolPoints.Length];

                if (a != null && b != null) Gizmos.DrawLine(a.position, b.position);

            }

        }

        else

        {

            Vector3 origin = Application.isPlaying ? patrolOrigin : transform.position;

            Gizmos.DrawLine(origin + Vector3.left * patrolHalfWidth, origin + Vector3.right * patrolHalfWidth);

        }



        // 分身の円形配置（巡回エリア中央＋自動半径）のプレビュー

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);

        Vector2 ringCenter = StageCenter();

        Gizmos.DrawWireSphere(ringCenter, ComputeSplitRingRadius(ringCenter));

    }

}