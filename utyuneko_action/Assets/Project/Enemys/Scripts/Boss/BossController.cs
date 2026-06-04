using UnityEngine;

/// <summary>
/// ボスのオーケストレータ。
/// 各ステートのインスタンスを保持し、現在のステートに Update/FixedUpdate を委譲する。
/// PlayerController と同じ作り。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BossController : MonoBehaviour
{
    [Header("プレイヤー参照")]
    public string playerTag = "Player";
    [HideInInspector] public Transform player;

    [Header("攻撃用プレハブ")]
    [Tooltip("反射可能な弾のプレハブ。ReflectableBullet を持つこと")]
    public GameObject bulletPrefab;

    [Tooltip("着弾型範囲攻撃の弾。同じく ReflectableBullet 推奨")]
    public GameObject areaImpactPrefab;

    [Header("発射位置")]
    public Transform firePoint;

    [Header("攻撃パラメータ（全ステート共通の初期値）")]
    [Tooltip("散弾の本数")]
    public int spreadCount = 5;
    [Tooltip("散弾の広がり角度（度）")]
    public float spreadAngleDeg = 60f;
    [Tooltip("追尾弾の連射回数")]
    public int homingShotCount = 3;
    [Tooltip("追尾弾の連射間隔（秒）")]
    public float homingShotInterval = 0.25f;
    [Tooltip("突進の速度")]
    public float chargeSpeed = 18f;
    [Tooltip("突進の持続時間（秒）")]
    public float chargeDuration = 1.0f;
    [Tooltip("攻撃と攻撃の間（Idle時間 / 秒）")]
    public float idleBetweenAttacks = 1.0f;
    [Tooltip("範囲攻撃の生成高さ（プレイヤー位置からの上方向オフセット）")]
    public float areaImpactSpawnHeight = 8f;

    [Header("フェーズ2での補正")]
    [Tooltip("フェーズ2でのIdle短縮倍率（0.5 なら半分の時間で次の攻撃へ）")]
    [Range(0.1f, 1f)] public float phase2IdleMultiplier = 0.5f;

    // 内部参照
    [HideInInspector] public Rigidbody rb;
    [HideInInspector] public BossHealth health;
    [HideInInspector] public BossBarrier barrier;
    [HideInInspector] public BossMovement movement;

    // ステート
    public BossState_Idle StateIdle { get; private set; }
    public BossState_ChooseAttack StateChooseAttack { get; private set; }
    public BossState_HomingShot StateHomingShot { get; private set; }
    public BossState_SpreadShot StateSpreadShot { get; private set; }
    public BossState_Charge StateCharge { get; private set; }
    public BossState_AreaImpact StateAreaImpact { get; private set; }

    public IBossState CurrentState { get; private set; }

    // 現在のフェーズ番号（1始まり）。BossHealth から書き換えられる
    [HideInInspector] public int currentPhase = 1;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<BossHealth>();
        barrier = GetComponent<BossBarrier>();
        movement = GetComponent<BossMovement>();

        StateIdle = new BossState_Idle();
        StateChooseAttack = new BossState_ChooseAttack();
        StateHomingShot = new BossState_HomingShot();
        StateSpreadShot = new BossState_SpreadShot();
        StateCharge = new BossState_Charge();
        StateAreaImpact = new BossState_AreaImpact();
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null) player = p.transform;
        else Debug.LogWarning($"BossController: タグ '{playerTag}' のプレイヤーが見つかりません");

        TransitionToState(StateIdle);
    }

    void Update()
    {
        CurrentState?.UpdateState();
    }

    void FixedUpdate()
    {
        CurrentState?.FixedUpdateState();
    }

    public void TransitionToState(IBossState next)
    {
        CurrentState?.Exit();
        CurrentState = next;
        CurrentState?.Enter(this);
    }
}
