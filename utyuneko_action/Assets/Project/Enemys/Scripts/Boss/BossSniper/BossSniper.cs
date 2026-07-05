using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 空中スナイパーボス（ステートパターン版・コントローラ）。
///
/// 状態遷移（各ステートは BossState_*.cs に分割。プレイヤーの IPlayerState と同じ流儀）:
///
///   Patrol（巡回：多角形エリア内をランダムに瞬間移動）
///     ↔ PatrolShot（出現撃ち：瞬間移動が確率で攻撃に化ける。出現直後に狙いを固定して短いレーザー。
///                   偏差撃ち／直撃狙いが混ざる牽制。撃ち終えたら Patrol へ戻る）
///     → RadialAttack（全体攻撃：ステージ中央＝巡回ポイントの重心へテレポートし、
///                     回転しながら四方八方へ1本ずつ連射。撃ち切ったら分身攻撃へ。
///                     通常フェーズは毎サイクル必ず挟む。最終フェーズのみ挟むかどうかがランダム）
///     → Split（分身展開：本体が収縮して消え、全ユニットが配置ポイントに同時出現）
///     → Aim（照準：射線がプレイヤーを追う）→ Lock（固定・最終警告）→ Fire（レーザー発射）
///     → Return（瞬間移動で巡回エリアへ帰還）→ Patrol …
///
///   Aim / Lock 中に本物へバースト体当たり
///     → StunFall（無敵のまま落下）→ 着地 → StunGrace（着地猶予・まだ無敵）
///     → Stunned（ここだけダメージを受け付ける）
///        - 被弾: フェーズが進んで Return（最終フェーズなら Defeated）
///        - 時間切れ: Return（同じフェーズで攻撃を繰り返す）
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
    // ─── フェーズ（難易度）設定 ─────────────────────

    [System.Serializable]
    public class PhaseSettings
    {
        [Tooltip("ユニット総数（本物1を含む）。4なら 本物1＋偽物3")]
        public int totalUnits = 4;

        [Header("フェーズHP・被弾")]
        [Tooltip("このフェーズのHP。満タンから始まり、削り切ったら次フェーズへ。最終フェーズで削り切ると撃破")]
        public float phaseMaxHP = 100f;

        [Tooltip("スタン中に通る一撃の倍率。通常の速度依存ダメージにこれを掛ける（見破りスタンのボーナス）")]
        public float stunDamageMultiplier = 3f;

        [Tooltip("照準（射線がプレイヤーを追う）時間")]
        public float aimTime = 1.5f;

        [Tooltip("射線固定後、レーザー発射までの最終警告時間")]
        public float lockTime = 0.35f;

        [Tooltip("レーザー（攻撃判定）が出ている時間")]
        public float fireDuration = 0.25f;

        [Tooltip("着地して猶予が明けてから、ダメージを受け付けている時間")]
        public float stunDuration = 4f;

        [Header("巡回ショット（このフェーズでの難易度）")]
        [Tooltip("巡回中の瞬間移動が「出現撃ち」になる確率（0〜1）")]
        [Range(0f, 1f)] public float patrolShotChance = 0.5f;

        [Tooltip("出現撃ちの、出現〜レーザー発射までのロック時間（短いほど難しい）")]
        public float patrolShotLockTime = 0.5f;

        [Tooltip("1回の出現撃ちで連続して撃つ回数。2発目以降は毎回狙いを付け直す（偏差／直撃も毎回抽選）")]
        public int patrolShotCount = 1;

        [Header("全体攻撃（このフェーズでの難易度）")]
        [Tooltip("全体攻撃で撃つ総数。角度ステップ×回数ぶんだけ回転しながら連射する")]
        public int radialShotCount = 10;

        [Tooltip("全体攻撃の1発ごとのロック時間（短いほど回転が速く難しい）")]
        public float radialLockTime = 0.3f;
    }

    [Header("フェーズ設定（配列の長さ＝撃破に必要なヒット数）")]
    [Tooltip("スタン中に攻撃を受けるたびに次の要素へ進む。既定は3フェーズ＝3回で撃破")]
    public PhaseSettings[] phases = new PhaseSettings[]
    {
        new PhaseSettings { totalUnits = 4, phaseMaxHP = 120f, stunDamageMultiplier = 4f, aimTime = 1.5f,  patrolShotChance = 0.35f, patrolShotLockTime = 0.6f, patrolShotCount = 1, radialShotCount = 8,  radialLockTime = 0.35f },
        new PhaseSettings { totalUnits = 5, phaseMaxHP = 150f, stunDamageMultiplier = 4f, aimTime = 1.35f, patrolShotChance = 0.55f, patrolShotLockTime = 0.5f, patrolShotCount = 2, radialShotCount = 12, radialLockTime = 0.3f },
        new PhaseSettings { totalUnits = 6, phaseMaxHP = 180f, stunDamageMultiplier = 5f, aimTime = 1.2f,  patrolShotChance = 0.75f, patrolShotLockTime = 0.4f, patrolShotCount = 3, radialShotCount = 16, radialLockTime = 0.25f },
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

    [Tooltip("中央に到着してから初弾までの溜め時間。この間に初弾のロック射線を出してプレイヤーに避ける準備をさせる（全フェーズ共通）")]
    public float radialWindupTime = 1.2f;

    [Tooltip("最終フェーズで、巡回のあとに全体攻撃を挟む確率（0〜1）。挟まない場合はそのまま分身攻撃へ。最終フェーズ以外は必ず挟む")]
    [Range(0f, 1f)] public float finalPhaseRadialChance = 0.5f;

    // ─── 分身の展開 ────────────────────────────────

    [Header("分身の展開")]
    [Tooltip("偽物の分身プレハブ（Collider2D(IsTrigger)＋BossBeamUnit＋見た目）")]
    public GameObject clonePrefab;

    [Tooltip("分裂した瞬間のプレイヤー位置を中心に、この半径のリング状へ展開する（弧状整列の半径も兼ねる）")]
    public float formationRadius = 6f;

    [Tooltip("リング配置ポイントの最低Y座標。地面へのめり込み防止（十分低い値なら実質無効）")]
    public float formationMinY = -999f;

    [Header("整列フォールバック（リングが地形に埋まる場合）")]
    [Tooltip("埋まり判定の半径。ボスのコライダーより少し大きめにすると地面スレスレ配置を防げる")]
    public float slotCheckRadius = 1.2f;

    [Tooltip("整列時の隣同士の間隔")]
    public float lineSpacing = 2.5f;

    [Tooltip("横一列・弧状の基準高さ（プレイヤーからどれだけ上に置くか）")]
    public float lineHeight = 5f;

    [Tooltip("縦一列のとき、プレイヤーからどれだけ横に離すか")]
    public float columnDistance = 5f;

    [Tooltip("弧状整列の全体角度（度）。プレイヤー上空に弧を描く")]
    public float arcAngleRange = 110f;

    // ─── スタン・被弾 ──────────────────────────────

    [Header("スタン・被弾")]
    [Tooltip("スタン落下時の重力スケール")]
    public float stunGravityScale = 2.5f;

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
    public UnityEvent<int> onDamaged;   // ダメージを受けた（引数＝新しいフェーズ番号 0始まり）
    public UnityEvent onDefeated;       // 撃破された
    public UnityEvent onCloneDestroyed; // 偽物が破壊された

    // ─── ステート（プレイヤーと同じ流儀で公開プロパティにする） ───

    public BossSniperState_Patrol StatePatrol { get; private set; }
    public BossSniperState_PatrolShot StatePatrolShot { get; private set; }
    public BossSniperState_RadialAttack StateRadialAttack { get; private set; }
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

    /// <summary>現在が最終フェーズか。</summary>
    public bool IsFinalPhase => CurrentPhaseIndex >= phases.Length - 1;

    /// <summary>
    /// 次の分身攻撃までの残り時間。ボス側が持つことで、
    /// 出現撃ち（PatrolShot）を挟んでもリセットされず、分身攻撃の間隔が一定に保たれる。
    /// Patrol / PatrolShot が減算し、Return（攻撃サイクルの区切り）で補充される。
    /// </summary>
    public float AttackTimer { get; set; }

    /// <summary>展開中のユニット一覧（本物を含む）。分身展開中以外は空。</summary>
    public List<BossSniperBeamUnit> Units { get; } = new List<BossSniperBeamUnit>();

    public int CurrentPhaseIndex { get; private set; }
    public PhaseSettings Phase => phases[Mathf.Clamp(CurrentPhaseIndex, 0, phases.Length - 1)];

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

        if (Health != null) Health.InitPhase(Phase.phaseMaxHP); // 最初のフェーズHPを満タンに

        TransitionToState(StatePatrol);
    }

    void Update()
    {
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
        if (currentState != null)
        {
            currentState.Exit();
        }

        currentState = newState;
        currentState.Enter(this);
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

    /// <summary>偽物を1体破壊する（見破りの外れ）。</summary>
    public void DestroyClone(BossSniperBeamUnit unit)
    {
        if (unit == null || unit.IsReal) return;
        Units.Remove(unit);
        Destroy(unit.gameObject);
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

    // ─── 被ダメージ・物理切り替え ─────────────────────

    /// <summary>
    /// 現フェーズのHPを削り切ったときに Health から呼ばれる。次フェーズへ進み、HPを満タンに。
    /// フェーズ進行のトリガーはこれ（HPゼロ）だけ。見破りスタンは進行トリガーではなくボーナス帯。
    /// </summary>
    public void AdvancePhaseByHP()
    {
        CurrentPhaseIndex++;
        onDamaged?.Invoke(CurrentPhaseIndex); // 互換：フェーズ番号を通知

        if (Health != null)
        {
            Health.InitPhase(Phase.phaseMaxHP);           // 次フェーズHPを満タンに
            Health.onPhaseChanged?.Invoke(CurrentPhaseIndex, Phase.phaseMaxHP);
        }

        TransitionToState(StateReturn); // 瞬間移動で復帰し、難易度アップした攻撃を再開
    }

    /// <summary>最終フェーズのHPを削り切ったときに Health から呼ばれる。撃破へ。</summary>
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

    /// <summary>飛行用：Kinematic に戻す。</summary>
    public void RestoreFlightBody()
    {
        Rb.bodyType = RigidbodyType2D.Kinematic;
        Rb.gravityScale = 0f;
        Rb.linearVelocity = Vector2.zero;
    }

    // ─── 巡回エリア（多角形）内のランダム位置 ─────────────

    /// <summary>
    /// 巡回が終わった（AttackTimer が満ちた）ときに向かう先を決める。
    /// 通常フェーズ: 必ず 全体攻撃 → 分身攻撃 の順。
    /// 最終フェーズ: 確率（finalPhaseRadialChance）で全体攻撃を挟むか、そのまま分身攻撃かをランダムに（行動を読めなくする）。
    /// </summary>
    public IBossSniperState NextAttackAfterPatrol()
    {
        bool finalPhase = CurrentPhaseIndex >= phases.Length - 1;
        if (finalPhase && Random.value >= finalPhaseRadialChance)
        {
            return StateSplit;
        }
        return StateRadialAttack;
    }

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

    // ─── 配置ポイントの生成（リング → 整列フォールバック） ───

    private enum FormationType { HorizontalLine, VerticalLine, Arc }

    /// <summary>
    /// 分身の配置ポイント一覧を作る。まずプレイヤーを取り囲むリングを試し、
    /// 1体でも地形（obstacleLayer）に接するならリング全体を諦め、
    /// 横一列・縦一列・弧状のいずれか（ランダム）の整列に切り替える。
    /// 整列も全員が埋まらない置き方を探してから確定する。
    /// </summary>
    public List<Vector2> BuildFormationSlots(int count, Vector2 center)
    {
        // 1) まずリング配置を試す
        List<Vector2> ring = GenerateRing(count, center);
        if (AllSlotsClear(ring)) return ring;

        // 2) 誰かが地形に接する → 整列へ。種類はランダム（その種類で置けなければ他の種類も順に試す）
        var types = new List<FormationType> { FormationType.HorizontalLine, FormationType.VerticalLine, FormationType.Arc };
        Shuffle(types);

        // 基準位置を少しずつずらしながら、全員が埋まらない置き方を探す
        Vector2[] anchorOffsets =
        {
            Vector2.zero,
            Vector2.up * 2f,
            Vector2.up * 4f,
            Vector2.right * 3f,
            Vector2.left * 3f,
            Vector2.up * 2f + Vector2.right * 3f,
            Vector2.up * 2f + Vector2.left * 3f,
        };

        foreach (FormationType type in types)
        {
            // 縦一列は左右どちら側に立てるかもランダム（片側がダメならもう片側）
            float firstSide = Random.value < 0.5f ? 1f : -1f;
            float[] sides = type == FormationType.VerticalLine ? new[] { firstSide, -firstSide } : new[] { 1f };

            foreach (float side in sides)
            {
                foreach (Vector2 offset in anchorOffsets)
                {
                    List<Vector2> slots = GenerateFormation(type, center + offset, count, side);
                    if (AllSlotsClear(slots)) return slots;
                }
            }
        }

        // 3) 最終手段：横一列を作り、埋まるスロットだけ個別に上へ逃がす
        List<Vector2> fallback = GenerateFormation(FormationType.HorizontalLine, center, count, 1f);
        for (int i = 0; i < fallback.Count; i++) fallback[i] = FindClearAbove(fallback[i]);
        return fallback;
    }

    private List<Vector2> GenerateRing(int count, Vector2 center)
    {
        float baseAngle = Random.Range(0f, Mathf.PI * 2f);
        var slots = new List<Vector2>(count);
        for (int i = 0; i < count; i++)
        {
            float a = baseAngle + (Mathf.PI * 2f / count) * i;
            Vector2 pos = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * formationRadius;
            pos.y = Mathf.Max(pos.y, formationMinY);
            slots.Add(pos);
        }
        return slots;
    }

    private List<Vector2> GenerateFormation(FormationType type, Vector2 center, int count, float side)
    {
        var slots = new List<Vector2>(count);
        float half = (count - 1) * 0.5f;

        switch (type)
        {
            case FormationType.HorizontalLine:
                // プレイヤー上空に横一列
                for (int i = 0; i < count; i++)
                {
                    slots.Add(center + Vector2.up * lineHeight + Vector2.right * ((i - half) * lineSpacing));
                }
                break;

            case FormationType.VerticalLine:
                // プレイヤーの横に縦一列（side=±1 で左右）。列の中心は少し上げて下端が地面に近づきにくくする
                for (int i = 0; i < count; i++)
                {
                    slots.Add(center
                        + Vector2.right * (side * columnDistance)
                        + Vector2.up * ((i - half) * lineSpacing + lineHeight * 0.5f));
                }
                break;

            case FormationType.Arc:
                // プレイヤー上空に弧を描く（真上を中心に arcAngleRange 度の扇）
                for (int i = 0; i < count; i++)
                {
                    float t = count == 1 ? 0.5f : (float)i / (count - 1);
                    float deg = 90f - arcAngleRange * 0.5f + arcAngleRange * t;
                    float rad = deg * Mathf.Deg2Rad;
                    slots.Add(center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * formationRadius);
                }
                break;
        }
        return slots;
    }

    // その位置にボスを置いたとき地形（obstacleLayer）に接しないか。
    // TilemapCollider2D / CompositeCollider2D も物理的にはただの Collider2D なので、そのまま拾える
    public bool IsSlotClear(Vector2 pos)
    {
        return Physics2D.OverlapCircle(pos, slotCheckRadius, SelfUnit.obstacleLayer) == null;
    }

    private bool AllSlotsClear(List<Vector2> slots)
    {
        foreach (Vector2 s in slots)
        {
            if (!IsSlotClear(s)) return false;
        }
        return true;
    }

    // 埋まっている位置を上方向へ少しずつ逃がす（最終手段用）
    private Vector2 FindClearAbove(Vector2 pos)
    {
        for (int i = 0; i < 40; i++)
        {
            if (IsSlotClear(pos)) return pos;
            pos += Vector2.up * 0.5f;
        }
        return pos; // どうしても空きが無ければ諦めてそのまま
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
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

        // 分身リングの半径（プレイヤー中心だが、参考として自分の周りに表示）
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, formationRadius);
    }
}