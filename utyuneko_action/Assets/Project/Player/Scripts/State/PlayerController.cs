using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class PlayerController : MonoBehaviour, IEventActor
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
    public int maxReflect = 3;
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

    [Header("リロード速度")]
    public float reloadSpeed = 8f; // 地面に着いた時にゲージが溜まる速度

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
    [HideInInspector] public GameInputActions inputActions;
    [HideInInspector] public Vector2 mousePositionInput;
    [HideInInspector] public TrailRenderer trailRenderer;
    [HideInInspector] public AfterImageEffect afterImageEffect;
    [HideInInspector] public int currentChargeLevel = 0;
    [HideInInspector] public float currentChargeTimer = 0f;

    [HideInInspector] public Animator anim;

    public AimTrajectoryLine trajectoryLine;

    public IPlayerState CurrentState => currentState;

    public System.Action<Collision2D> OnCollisionEnterEvent;
    private IPlayerState currentState;

    private ImageBubble imageBubble;

    public PlayerDamageEffect damageEffect;

    public PlayerState_None StateNone { get; private set; }
    public PlayerState_Normal StateNormal { get; private set; }
    public PlayerState_Charge StateCharge { get; private set; }
    public PlayerState_Burst StateBurst { get; private set; }
    public PlayerState_Damage StateDamage { get; private set; }

    void Awake()
    {
        inputActions = InputManager.Instance;

        StateNone = new PlayerState_None();
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

        damageEffect = GetComponent<PlayerDamageEffect>();

        rb2D.constraints = RigidbodyConstraints2D.FreezeRotation;

        rb2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb2D.sleepMode = RigidbodySleepMode2D.NeverSleep;

        trajectoryLine = GetComponentInChildren<AimTrajectoryLine>();

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

        imageBubble = GetComponentInChildren<ImageBubble>();

        if (visualManager == null) visualManager = GetComponent<PlayerVisualManager>();
        if (visualManager != null) visualManager.Initialize(this);

        TransitionToState(StateNormal);
    }

    void OnEnable() {
        if (SceneManager.GetActiveScene().name != "TitleScene")
        {
            inputActions.Player.Enable();
        }
    }
    void OnDisable() { inputActions.Player.Disable(); }

    void Update()
    {
        if (Time.timeScale == 0f) return;

        moveInput = inputActions.Player.Move.ReadValue<Vector2>();

        if (inputActions.Player.MousePosition != null)
        {
            mousePositionInput = inputActions.Player.MousePosition.ReadValue<Vector2>();
        }

        currentState?.UpdateState();
    }

    void FixedUpdate()
    {
        if (Time.timeScale == 0f) return;

        //デバック機能
        if (Input.GetKeyDown(KeyCode.U))
        {
            OnEnemyKilledInBurst();
        }

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

    /// <summary>
    /// バースト中に敵を撃破した際、バースト回数を回復する関数
    /// </summary>
    public void OnEnemyKilledInBurst()
    {
            currentBurstCount = Mathf.Max(0, currentBurstCount - 1);
            // currentBurstCount = 0f;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        OnCollisionEnterEvent?.Invoke(collision);
    }

    /// <summary>
    /// 外部（イベントマネージャー）から呼ばれるリアクション窓口
    /// </summary>
    public void PlayReaction(ImageBubble.StampType type, float duration = 2.0f)
    {
        // 種類に応じて「体（アニメーションや物理）」のリアクションだけを自分が担当する
        switch (type)
        {
            case ImageBubble.StampType.OK:
                break;
            case ImageBubble.StampType.Question:
                break;
            case ImageBubble.StampType.Surprise:
                rb2D.linearVelocity = new Vector2(rb2D.linearVelocity.x, 4f); // ぴょこっと上に跳ねる物理リアクション
                break;
            case ImageBubble.StampType.Doya:
                break;
            case ImageBubble.StampType.Sweat:
                break;
            case ImageBubble.StampType.Hatena:
                break;
            case ImageBubble.StampType.Joy:
                rb2D.linearVelocity = new Vector2(rb2D.linearVelocity.x, 3f);
                break;
            case ImageBubble.StampType.Gift:
                break;
            case ImageBubble.StampType.Star:
                break;
            case ImageBubble.StampType.Go:
                break;
            case ImageBubble.StampType.Enemy:
                break;
        }
    }

    /// <summary>
    /// プレイヤーが現在向いている方向を返します（右向き: 1, 左向き: -1）
    /// </summary>
    public float GetFacingDirection()
    {
        if (visualManager != null && visualManager.playerVisual != null)
        {
            // VisualのY軸回転が180度より大きければ右向き(310f)、小さければ左向き(50f)
            float yAngle = visualManager.playerVisual.localRotation.eulerAngles.y;
            return (yAngle > 180f) ? 1f : -1f;
        }
        return 1f; // 取れなかった時の安全策（デフォルト右向き）
    }
}