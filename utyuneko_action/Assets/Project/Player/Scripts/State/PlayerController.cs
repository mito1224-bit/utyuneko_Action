using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("移動パラメータ")]
    public float moveSpeed = 5.0f;
    public float jumpForce = 7.0f;

    [Header("着地判定")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float castDistance = 0.2f;

    [Header("バースト・反射設定")]
    public float burstSpeed = 25.0f;
    [Range(0f, 1f)]
    public float reflectEfficiency = 0.8f;
    public int maxBurstCount = 3;
    [HideInInspector] public int currentBurstCount = 0;
    public bool canCancelBurstWithJump = true;

    [Header("エイム設定")]
    public Transform aimPivot;
    public Color[] chargeColors = { Color.white, Color.yellow, Color.red };

    [Header("ホバーセンサー")]
    public HoverSensor hoverSensor;

    [Header("チャージ設定")]
    public float[] chargeForceLevels = { 15f, 25f, 40f };
    public float chargeTimePerLevel = 0.5f;
    public float aimTimeScale = 0.05f;

    [Header("チャージ中演出設定")]
    [Tooltip("True: チャージ中もバースト時の慣性を残してスロー移動する\nFalse: チャージに入った瞬間に速度を0にしてその場に完全停止する")]
    public bool useInertiaInCharge = true; // 慣性移動のON/OFF
    [Tooltip("True: チャージ中もドリル回転やしなり移動を行う\nFalse: 回転などを止め、純粋にエイム方向を向くだけにする")]
    public bool useRotationInCharge = true; // 回転演出のON/OFF
    [Tooltip("True: チャージ中の壁衝突時にもモチッと伸縮・反射演出を行う\nFalse: チャージ中は一切伸縮しなくなる")]
    public bool useSquashInCharge = true; // 伸縮演出のON/OFF

    [Header("ダメージ設定")]
    public float knockbackForceX = 10f; // 横に吹っ飛ぶ強さ
    public float knockbackForceY = 8f; // 上に跳ね上がる強さ
    public float damageDuration = 1.0f; // 操作不能になる時間(秒)

    [Header("バースト演出設定")]
    public bool useTrail = true;       // トレイル演出のオンオフ
    public bool useAfterImage = true;  // 残像演出のオンオフ

    [Header("Visual Manager Reference")]
    [Tooltip("演出管理コンポーネントの参照")]
    public PlayerVisualManager visualManager;

    [Header("Visual Settings")]
    [Tooltip("どれくらい前のめりにするか(最大角度)")]
    public float leanAngle = 20.0f;

    // 2D物理用の隠しプロパティ
    [HideInInspector] public Rigidbody2D rb2D;
    [HideInInspector] public CircleCollider2D circleCollider2D;
    [HideInInspector] public Vector2 moveInput;
    [HideInInspector] public PlayerInputActions inputActions;
    [HideInInspector] public Vector2 mousePositionInput;
    [HideInInspector] public TrailRenderer trailRenderer;
    [HideInInspector] public AfterImageEffect afterImageEffect;
    [HideInInspector] public int currentChargeLevel = 0;
    [HideInInspector] public float currentChargeTimer = 0f;

    [HideInInspector] public Animator anim;

    public IPlayerState CurrentState => currentState;

    public System.Action<Collision2D> OnCollisionEnterEvent;
    private IPlayerState currentState;

    public PlayerState_Normal StateNormal { get; private set; }
    public PlayerState_Charge StateCharge { get; private set; }
    public PlayerState_Burst StateBurst { get; private set; }
    public PlayerState_Damage StateDamage { get; private set; }

    void Awake()
    {
        inputActions = new PlayerInputActions();

        StateNormal = new PlayerState_Normal();
        StateCharge = new PlayerState_Charge();
        StateBurst = new PlayerState_Burst();
        StateDamage = new PlayerState_Damage();
    }

    void Start()
    {
        rb2D = GetComponent<Rigidbody2D>();
        circleCollider2D = GetComponent<CircleCollider2D>();
        anim = GetComponent<Animator>();

        rb2D.constraints = RigidbodyConstraints2D.FreezeRotation;

        rb2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb2D.sleepMode = RigidbodySleepMode2D.NeverSleep;

        if (aimPivot != null)
        {
            aimPivot.gameObject.SetActive(false);
        }

        trailRenderer = GetComponentInChildren<TrailRenderer>();
        if (trailRenderer != null)
        {
            trailRenderer.enabled = false;
        }

        afterImageEffect = GetComponent<AfterImageEffect>();
        if (afterImageEffect != null)
        {
            afterImageEffect.enabled = false;
        }

        if (visualManager == null) visualManager = GetComponent<PlayerVisualManager>();
        if (visualManager != null) visualManager.Initialize(this);

        TransitionToState(StateNormal);
    }

    void OnEnable() { inputActions.Player.Enable(); }
    void OnDisable() { inputActions.Player.Disable(); }

    void Update()
    {
        moveInput = inputActions.Player.Move.ReadValue<Vector2>();

        if (inputActions.Player.MousePosition != null)
        {
            mousePositionInput = inputActions.Player.MousePosition.ReadValue<Vector2>();
        }

        currentState?.UpdateState();
    }

    void FixedUpdate()
    {
        currentState?.FixedUpdateState();
    }

    public void TransitionToState(IPlayerState newState)
    {
        if (currentState != null)
        {
            currentState.Exit();
        }

        currentState = newState;
        currentState.Enter(this);
    }

    public bool IsGrounded()
    {
        if (hoverSensor != null)
        {
            return hoverSensor.IsGrounded();
        }
        return false;
    }

    public LayerMask GetGroundLayerMask()
    {
        return groundLayer;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        OnCollisionEnterEvent?.Invoke(collision);
    }
}