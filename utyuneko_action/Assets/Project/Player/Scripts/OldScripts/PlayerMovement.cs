using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class PlayerMovement : MonoBehaviour
{
    [Header("基本移動")]
    [SerializeField] private float moveSpeed = 5.0f;
    [SerializeField] private float jumpForce = 7.0f;

    [Header("着地判定の設定")]
    [SerializeField] private LayerMask groundLayer; // 床用のレイヤー
    [SerializeField] private float castDistance = 0.2f;

    private Rigidbody rb;
    private SphereCollider sphereCollider; // 自分の大きさを知るために取得
    private Vector2 moveInput;
    private PlayerInputActions inputActions;

    void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        sphereCollider = GetComponent<SphereCollider>(); // コンポーネントを記憶

        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void OnEnable() { inputActions.Player.Enable(); }
    void OnDisable() { inputActions.Player.Disable(); }

    void Update()
    {
        HandleInput();
    }

    void FixedUpdate()
    {
        Vector3 newVelocity = new Vector3(
            moveInput.x * moveSpeed,
            rb.linearVelocity.y,
            0.0f
        );
        rb.linearVelocity = newVelocity;
    }

    private void HandleInput()
    {
        moveInput = inputActions.Player.Move.ReadValue<Vector2>();

        if (inputActions.Player.Jump.triggered && IsGrounded())
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, 0.0f);
        }
    }

    // 接地判定関数(スフィアキャスト)
    private bool IsGrounded()
    {
        // 飛ばす球体の「半径」を、自分のSphereColliderと同じ大きさにする
        float radius = sphereCollider.radius;

        // 球体を飛ばし始める「スタート位置」を決める
        // プレイヤーの中心（transform.position）のままだと、最初から床にめり込んでバグる事があるので、少しだけ上にズラします
        Vector3 origin = transform.position + Vector3.up * 0.1f;

        // 飛ばす向きを指定
        Vector3 direction = Vector3.down;

        // 実際に球体を下に落として、床レイヤーに当たったかを判定する
        bool isHit = Physics.SphereCast(
            origin,      // スタート位置
            radius,      // 球体の半径
            direction,   // 飛ばす向き
            out RaycastHit hit, // 当たった情報を入れる箱（今回は空箱を渡すだけでOK）
            castDistance,       // 飛ばす「長さ（距離）」
            groundLayer        // 判定する「ターゲットのレイヤー」
        );

        return isHit;
    }
}