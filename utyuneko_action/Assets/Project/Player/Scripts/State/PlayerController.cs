using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class PlayerController : MonoBehaviour
{
    [Header("移動パラメータ")]
    public float moveSpeed = 5.0f;
    public float jumpForce = 7.0f;

    [Header("着地判定")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float castDistance = 0.2f;

    [Header("バースト・反射設定")]
    public float burstSpeed = 25.0f;     // 初速
    [Range(0f, 1f)]
    public float reflectEfficiency = 0.8f; // ★反射時のスピード維持率（0.8なら毎回20%減速）
    public int maxBurstCount = 3;       // 最大バースト回数（インスペクターから変更可能）
    [HideInInspector] public int currentBurstCount = 0; // 現在のバースト回数カウンター

    [Header("エイム設定")]
    public Transform aimPivot;
    public Color[] chargeColors = { Color.white, Color.yellow, Color.red };

    [Header("チャージ設定")]
    public float[] chargeForceLevels = { 15f, 25f, 40f };
    public float chargeTimePerLevel = 0.5f;               // 1段階溜まるのに必要な時間
    public float aimTimeScale = 0.05f;

    [Header("バースト演出設定")]
    public bool useTrail = true;       // 軌跡を使うかどうか
    public bool useAfterImage = true;  // 残像を使うかどうか

    // 隠しプロパティ（各ステートから楽にアクセスできるようにパブリックにします）
    [HideInInspector] public Rigidbody rb;
    [HideInInspector] public SphereCollider sphereCollider;
    [HideInInspector] public Vector2 moveInput;
    [HideInInspector] public PlayerInputActions inputActions;
    [HideInInspector] public Vector2 mousePositionInput;
    [HideInInspector] public TrailRenderer trailRenderer;
    [HideInInspector] public AfterImageEffect afterImageEffect;
    [HideInInspector] public int currentChargeLevel = 0;   // 0, 1, 2 段階
    [HideInInspector] public float currentChargeTimer = 0f;

    public IPlayerState CurrentState => currentState;

    public System.Action<Collision> OnCollisionEnterEvent;
    // ★現在アクティブな状態を記憶する箱（型がインターフェースなのがミソ！）
    private IPlayerState currentState;

    // ★あらかじめ各状態の実体を作って使い回す
    public PlayerState_Normal StateNormal { get; private set; }
    public PlayerState_Charge StateCharge { get; private set; }
    public PlayerState_Burst StateBurst { get; private set; }

    void Awake()
    {
        inputActions = new PlayerInputActions();

        // 各ステートの実体を生成
        StateNormal = new PlayerState_Normal();
        StateCharge = new PlayerState_Charge();
        StateBurst = new PlayerState_Burst();
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        sphereCollider = GetComponent<SphereCollider>();

        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        //エイム用のPivotがセットされていたら最初は非表示にしておく
        if (aimPivot != null)
        {
            aimPivot.gameObject.SetActive(false);
        }

        // TrailRenderer をプレイヤー自身から自動で取ってくる
        trailRenderer = GetComponent<TrailRenderer>();
        if (trailRenderer != null)
        {
            trailRenderer.enabled = false; // 最初は絶対にOFF
        }

        afterImageEffect = GetComponent<AfterImageEffect>();
        if (afterImageEffect != null)
        {
            afterImageEffect.enabled = false;
        }

        TransitionToState(StateNormal);

        // ★最初の状態を「通常状態」にセット
        TransitionToState(StateNormal);
    }

    void OnEnable() { inputActions.Player.Enable(); }
    void OnDisable() { inputActions.Player.Disable(); }

    void Update()
    {
        // 入力は常に本体で受け取って各ステートに配る
        moveInput = inputActions.Player.Move.ReadValue<Vector2>();

        //マウスの位置をスクリーン座標で受け取る
        if (inputActions.Player.MousePosition != null)
        {
            mousePositionInput = inputActions.Player.MousePosition.ReadValue<Vector2>();
        }

        // ★今のステートのUpdate処理を身代わりに実行してもらう
        currentState?.UpdateState();
    }

    void FixedUpdate()
    {
        // ★今のステートのFixedUpdate処理を身代わりに実行してもらう
        currentState?.FixedUpdateState();
    }

    // ★状態を「ガチャン」と切り替えるための超重要関数
    public void TransitionToState(IPlayerState newState)
    {
        if (currentState != null)
        {
            currentState.Exit(); // 今の状態に別れを告げる
        }

        currentState = newState; // 新しい状態を箱に入れる
        currentState.Enter(this); // 新しい状態の準備を始める
    }

    // 着地判定（前回作ったSphereCastをそのまま共通機能として持たせる）
    public bool IsGrounded()
    {
        float radius = sphereCollider.radius;
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        return Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, castDistance, groundLayer);
    }

    public LayerMask GetGroundLayerMask()
    {
        return groundLayer;
    }

    // Unity標準の衝突イベントを受け取ったら、現在アクティブなステートにそのまま丸投げする
    private void OnCollisionEnter(Collision collision)
    {
        // 今のステートが「反射して！」と待ち構えていたら、そっちの関数を実行する
        OnCollisionEnterEvent?.Invoke(collision);
    }
}