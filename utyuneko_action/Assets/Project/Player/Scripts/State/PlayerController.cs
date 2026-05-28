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

    [Header("エイム設定")]
    public Transform aimPivot;
    public Color[] chargeColors = { Color.white, Color.yellow, Color.red };

    [Header("チャージ設定")]
    public float[] chargeForceLevels = { 15f, 25f, 40f };
    public float chargeTimePerLevel = 0.5f;
    public float aimTimeScale = 0.05f;

    [Header("バースト演出設定")]
    public bool useTrail = true;
    public bool useAfterImage = true;

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

    public IPlayerState CurrentState => currentState;

    // イベントも Collision2D 用に変更
    public System.Action<Collision2D> OnCollisionEnterEvent;
    private IPlayerState currentState;

    public PlayerState_Normal StateNormal { get; private set; }
    public PlayerState_Charge StateCharge { get; private set; }
    public PlayerState_Burst StateBurst { get; private set; }

    void Awake()
    {
        inputActions = new PlayerInputActions();

        StateNormal = new PlayerState_Normal();
        StateCharge = new PlayerState_Charge();
        StateBurst = new PlayerState_Burst();
    }

    void Start()
    {
        rb2D = GetComponent<Rigidbody2D>();
        circleCollider2D = GetComponent<CircleCollider2D>();

        // 2D用の Constraints 設定（Z軸回転のみ固定。2DなのでZ移動固定の概念はありません）
        rb2D.constraints = RigidbodyConstraints2D.FreezeRotation;

        // 高速移動の隙間すり抜け・挟まり防止（最強設定）
        rb2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb2D.sleepMode = RigidbodySleepMode2D.NeverSleep;

        if (aimPivot != null)
        {
            aimPivot.gameObject.SetActive(false);
        }

        trailRenderer = GetComponent<TrailRenderer>();
        if (trailRenderer != null)
        {
            trailRenderer.enabled = false;
        }

        afterImageEffect = GetComponent<AfterImageEffect>();
        if (afterImageEffect != null)
        {
            afterImageEffect.enabled = false;
        }

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

    // 2D版の着地判定（CircleCast2D を使用）
    public bool IsGrounded()
    {
        float radius = circleCollider2D.radius;
        // プレイヤーの中心から少し下に向けて球をキャスト
        Vector2 origin = (Vector2)transform.position + Vector2.up * 0.1f;
        RaycastHit2D hit = Physics2D.CircleCast(origin, radius, Vector2.down, castDistance, groundLayer);
        return hit.collider != null;
    }

    public LayerMask GetGroundLayerMask()
    {
        return groundLayer;
    }

    // 2Dの衝突イベントを受け取ってステートに丸投げ
    private void OnCollisionEnter2D(Collision2D collision)
    {
        OnCollisionEnterEvent?.Invoke(collision);
    }
}