using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections;

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
    [SerializeField] private LayerMask ReflectionLayer;
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
    public Animator anim;

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

    public AimTrajectoryLine trajectoryLine;

    public IPlayerState CurrentState => currentState;

    public System.Action<Collision2D> OnCollisionEnterEvent;
    private IPlayerState currentState;

    private ImageBubble imageBubble;

    public PlayerDamageEffect damageEffect;

    public PlayerChargeGauge chargeGauge;

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
        chargeGauge = GetComponentInChildren<PlayerChargeGauge>();

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

        if (Input.GetKeyDown(KeyCode.F1)) SoundManager.Instance.PlayBGM(BgmType.Stage2);

        currentState?.UpdateState();
    }

    void FixedUpdate()
    {
        if (Time.timeScale == 0f) return;

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

    public LayerMask GetReflectionLayerMask()
    {
        return ReflectionLayer;
    }

    /// <summary>
    /// バースト中に敵を撃破した際、バースト回数を回復する関数
    /// </summary>
    public void OnEnemyKilledInBurst(int value = 1)
    {
        if (chargeGauge) chargeGauge.RecoveryGauge(value);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        OnCollisionEnterEvent?.Invoke(collision);
    }

    private Coroutine playerReactionCoroutine;

    private IEnumerator PlayerTwitchRoutine(float duration, float magnitude)
    {
        if (visualManager == null || visualManager.playerVisual == null) yield break;
        Transform visual = visualManager.playerVisual;
        Vector3 origPos = visual.localPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // 左右にガタガタ震える計算
            float offsetX = Random.Range(-magnitude, magnitude);
            visual.localPosition = origPos + new Vector3(offsetX, 0f, 0f);
            yield return null;
        }
        visual.localPosition = origPos;
    }

    private IEnumerator PlayerNodRoutine(int nodCount, float height)
    {
        if (visualManager == null || visualManager.playerVisual == null) yield break;
        Transform visual = visualManager.playerVisual;
        Vector3 origPos = visual.localPosition;

        for (int i = 0; i < nodCount; i++)
        {
            // コクッと下がる
            float t = 0f;
            while (t < 0.06f) { t += Time.deltaTime; visual.localPosition = origPos + Vector3.down * height; yield return null; }
            // スッと戻る
            t = 0f;
            while (t < 0.06f) { t += Time.deltaTime; visual.localPosition = origPos; yield return null; }
        }
        visual.localPosition = origPos;
    }

    private IEnumerator PlayerTiltRoutine(float angle, float duration)
    {
        if (visualManager == null || visualManager.playerVisual == null) yield break;
        Transform visual = visualManager.playerVisual;
        Quaternion origRot = visual.localRotation;

        float t = 0f;
        // 首をかしげる（Z軸回転）
        while (t < duration * 0.3f) { t += Time.deltaTime; visual.localRotation = origRot * Quaternion.Euler(0f, 0f, angle); yield return null; }
        yield return new WaitForSeconds(duration * 0.4f);
        t = 0f;
        while (t < duration * 0.3f) { t += Time.deltaTime; visual.localRotation = Quaternion.Slerp(visual.localRotation, origRot, t / (duration * 0.3f)); yield return null; }
        visual.localRotation = origRot;
    }

    /// <summary>
    /// 💡【完全版】スタンプと連動してプレイヤーの体が全自動で演技する窓口
    /// </summary>
    public void PlayReaction(ImageBubble.StampType type, float duration = 2.0f)
    {
        if (playerReactionCoroutine != null) StopCoroutine(playerReactionCoroutine);

        // 念のためビジュアルのローカル位置を綺麗に戻す安全策
        if (visualManager != null && visualManager.playerVisual != null) visualManager.playerVisual.localPosition = Vector3.zero;

        switch (type)
        {
            // ⭕️ OK / 丸：嬉しそうに「コクコクッ！」と可愛く2回うなずく
            case ImageBubble.StampType.OK:
            case ImageBubble.StampType.Maru:
                playerReactionCoroutine = StartCoroutine(PlayerNodRoutine(2, 0.15f));
                break;

            // ⭕️ 疑問 / はてな：不思議そうに首を「きょとん」と傾げる（Z軸回転チルト）
            case ImageBubble.StampType.Question:
            case ImageBubble.StampType.Hatena:
                playerReactionCoroutine = StartCoroutine(PlayerTiltRoutine(15f, 0.6f));
                break;

            // ⭕️ 驚き / 危険 / 敵：ビクッ！！っと上に高く飛び跳ねて硬直する（脳汁ポイント！）
            case ImageBubble.StampType.Surprise:
            case ImageBubble.StampType.Denger:
            case ImageBubble.StampType.Enemy:
                if (rb2D != null) rb2D.linearVelocity = new Vector2(rb2D.linearVelocity.x, 5.5f); // 勢いよく跳ねる
                playerReactionCoroutine = StartCoroutine(PlayerTwitchRoutine(0.25f, 0.08f)); // 体をビクビク震わせる
                break;

            // ⭕️ どや顔：フンッ！と顎を突き出すように一瞬だけ少し浮き上がってポーズを決める
            case ImageBubble.StampType.Doya:
                if (rb2D != null) rb2D.linearVelocity = new Vector2(rb2D.linearVelocity.x, 2.0f);
                break;

            // ⭕️ 悲しい / 混乱 / どくろ：ガクガクガク…と青ざめたように小刻みに激しく震え出す
            case ImageBubble.StampType.Sweat:
            case ImageBubble.StampType.Confusion:
            case ImageBubble.StampType.Dokuro:
                playerReactionCoroutine = StartCoroutine(PlayerTwitchRoutine(0.8f, 0.06f));
                break;

            // ⭕️ 喜ぶ / 星：やったー！と小気味よく「ぴょん！ぴょん！ぴょん！」と3回跳ね踊る！
            case ImageBubble.StampType.Joy:
            case ImageBubble.StampType.Star:
                StartCoroutine(PlayerNodRoutine(3, 0.08f)); // 縦揺れもブレンド
                if (rb2D != null)
                {
                    // 連続小ジャンプを物理で再現
                    StartCoroutine(FuncJoyJumps());
                    IEnumerator FuncJoyJumps()
                    {
                        rb2D.linearVelocity = new Vector2(rb2D.linearVelocity.x, 3.2f); yield return new WaitForSeconds(0.18f);
                        rb2D.linearVelocity = new Vector2(rb2D.linearVelocity.x, 3.2f); yield return new WaitForSeconds(0.18f);
                        rb2D.linearVelocity = new Vector2(rb2D.linearVelocity.x, 3.2f);
                    }
                }
                break;

            // ⭕️ 行こう / 右 / 左：進む方向の地面へ「グッ」と一瞬低く身構える（ダッシュのタメ）
            case ImageBubble.StampType.Go:
            case ImageBubble.StampType.Right:
            case ImageBubble.StampType.Left:
                playerReactionCoroutine = StartCoroutine(PlayerNodRoutine(1, 0.25f));
                break;

            // ⭕️ バツ：ガクッ…と膝から崩れ落ちるように一瞬だけ下に沈み込む
            case ImageBubble.StampType.Batu:
                playerReactionCoroutine = StartCoroutine(PlayerNodRoutine(1, 0.4f));
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