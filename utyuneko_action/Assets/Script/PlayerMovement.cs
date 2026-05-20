using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("基本移動")]
    [SerializeField] private float moveSpeed = 5.0f;
    [SerializeField] private float jumpForce = 12.0f;
    [SerializeField] private int maxJumpCount = 2;

    [Header("接地判定 (BoxCast)")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.8f, 0.1f);
    [SerializeField] private float groundCheckOffset = -0.1f;
    [SerializeField] private float maxSlopeAngle = 45f;

    [Header("操作感の調整")]
    [SerializeField] private float coyoteTime = 0.15f;

    [Header("バースト・エイム設定")]
    [SerializeField] private float aimTimeScale = 0.05f;
    [SerializeField] private LineRenderer aimIndicator;
    [SerializeField] private float bounceSpeedMultiplier = 1.0f;

    [Header("チャージ設定")]
    [SerializeField] private float[] chargeForceLevels = { 15f, 25f, 40f };
    [SerializeField] private float chargeTimePerLevel = 0.5f;
    private float currentChargeTimer = 0f;
    private int currentChargeLevel = 0;

    [Header("速度制限・演出設定")]
    [SerializeField] private float bulletModeExitThreshold = 3f;

    [Header("発射回数制限")]
    [SerializeField] private int maxBurstCount = 3;
    [SerializeField] private BurstCounterUI burstUI;
    private int currentBurstCount = 0;

    private Rigidbody2D rb;
    private BoxCollider2D col;
    private float horizontalInput;
    private bool isGrounded;
    private float colliderHalfHeight;

    private int jumpCount = 0;
    private float coyoteTimeCounter;
    private bool isAiming = false;
    private bool isBursting = false;
    private Vector2 aimDirection;
    private Rigidbody2D movingPlatformRb;
    private Vector2 lastVelocity;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<BoxCollider2D>();
        colliderHalfHeight = col.size.y / 2f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        if (aimIndicator) aimIndicator.enabled = false;

        if (AfterImageManager.Instance != null)
        {
            AfterImageManager.Instance.SetPlayer(transform, GetComponent<SpriteRenderer>());
        }
    }

    void Update()
    {
        HandleInput();
        CheckGround();
        HandleJump();
        HandleBurstInput();

        float currentSpeed = rb.linearVelocity.magnitude;
        if (currentSpeed <= bulletModeExitThreshold && isBursting)
        {
            ExitBurstMode();
        }
    }

    public void RestoreBurstCount(int amount)
    {
        // 現在のカウントを減らす（＝残弾を増やす） 0より小さくならないように制限
        currentBurstCount = Mathf.Max(0, currentBurstCount - amount);

        // UIを更新
        if (burstUI != null)
        {
            burstUI.UpdateDisplay(currentBurstCount, maxBurstCount);
        }
    }

    private void HandleInput()
    {
        if (Time.timeScale == 0f) { horizontalInput = 0f; return; }

        horizontalInput = 0f;
        if (Gamepad.current != null)
        {
            float stickInput = Gamepad.current.leftStick.x.ReadValue();
            if (Mathf.Abs(stickInput) > 0.1f) horizontalInput = stickInput;
        }
        if (Keyboard.current != null)
        {
            if (Keyboard.current.dKey.isPressed) horizontalInput = 1f;
            else if (Keyboard.current.aKey.isPressed) horizontalInput = -1f;
        }
    }

    private void HandleBurstInput()
    {
        bool aimHeld = (Gamepad.current != null && Gamepad.current.leftTrigger.isPressed) ||
                       (Mouse.current != null && Mouse.current.leftButton.isPressed);

        // エイムを開始できるのは、発射回数が上限に達していない時だけ
        if (aimHeld && (isGrounded || currentBurstCount < maxBurstCount))
        {
            if (!isAiming) StartAiming();
            UpdateCharge();
        }
        else if (isAiming)
        {
            ExecuteBurst();
        }
    }

    private void StartAiming()
    {
        isAiming = true;
        isBursting = false;
        currentChargeTimer = 0f;
        currentChargeLevel = 0;

        Time.timeScale = aimTimeScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
    }

    private void UpdateCharge()
    {
        currentChargeTimer += Time.unscaledDeltaTime;
        int lastLevel = currentChargeLevel;
        currentChargeLevel = Mathf.FloorToInt(currentChargeTimer / chargeTimePerLevel);
        currentChargeLevel = Mathf.Min(currentChargeLevel, chargeForceLevels.Length - 1);

        UpdateAimDirection();

        if (aimIndicator)
        {
            aimIndicator.enabled = true;
            aimIndicator.SetPosition(0, transform.position);
            float indicatorLength = 2f + currentChargeLevel * 1.5f;
            aimIndicator.SetPosition(1, (Vector2)transform.position + aimDirection * indicatorLength);

            Color lineColor = currentChargeLevel == 2 ? Color.red : (currentChargeLevel == 1 ? Color.yellow : Color.white);
            aimIndicator.startColor = lineColor;
            aimIndicator.endColor = lineColor;
        }
    }

    private void UpdateAimDirection()
    {
        if (Gamepad.current != null && Gamepad.current.leftStick.ReadValue().magnitude > 0.1f)
            aimDirection = Gamepad.current.leftStick.ReadValue().normalized;
        else if (Mouse.current != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
            Vector3 mousePos = Mouse.current.position.ReadValue();
            aimDirection = (mousePos - screenPos).normalized;
        }
    }

    private void ExecuteBurst()
    {
        isAiming = false;
        isBursting = true;

        // 発射回数をカウントアップ
        currentBurstCount++;
        Debug.Log($"Burst! Count: {currentBurstCount}/{maxBurstCount}");

        burstUI.UpdateDisplay(currentBurstCount, maxBurstCount);

        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;
        if (aimIndicator) aimIndicator.enabled = false;

        if (AfterImageManager.Instance != null)
        {
            AfterImageManager.Instance.StartEmitting();
        }

        float finalForce = chargeForceLevels[currentChargeLevel];
        rb.linearVelocity = aimDirection * finalForce;
    }

    private void CheckGround()
    {
        // 接地判定の開始地点を計算
        Vector2 origin = (Vector2)transform.position + new Vector2(0, -colliderHalfHeight + groundCheckOffset);
        RaycastHit2D hit = Physics2D.BoxCast(origin, groundCheckSize, 0f, Vector2.down, 0.1f, groundLayer);

        if (hit.collider != null)
        {
            float angle = Vector2.Angle(hit.normal, Vector2.up);
            if (angle <= maxSlopeAngle)
            {
                isGrounded = true;
                coyoteTimeCounter = coyoteTime;
                movingPlatformRb = hit.collider.GetComponent<Rigidbody2D>();

                // 「弾丸状態（isBursting）ではない」かつ「落下または静止している（y速度がほぼ0以下）」とき
                // 地面に触れていれば、発射回数とジャンプ回数をリセットする
                if (!isBursting && rb.linearVelocity.y <= 0.01f)
                {
                    jumpCount = 0;
                    currentBurstCount = 0;

                    burstUI.UpdateDisplay(currentBurstCount, maxBurstCount);
                }
            }
        }
        else
        {
            isGrounded = false;
            coyoteTimeCounter -= Time.deltaTime;
            movingPlatformRb = null;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isBursting)
        {
            // ヒットストップ
            if (HitStopManager.Instance != null) HitStopManager.Instance.Stop(0.05f);

            Vector2 incomingVector = lastVelocity;
            if (incomingVector.magnitude < 1f) return;

            Vector2 normal = collision.contacts[0].normal;
            Vector2 reflectDir = Vector2.Reflect(incomingVector.normalized, normal);

            rb.linearVelocity = reflectDir * incomingVector.magnitude * bounceSpeedMultiplier;
            aimDirection = reflectDir.normalized;
        }
    }

    private void HandleJump()
    {
        if (isAiming) return;
        bool jumpPressed = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
                           (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);

        if (jumpPressed)
        {
            // 弾丸状態なら強制キャンセルジャンプ
            if (isBursting)
            {
                JumpAction();
            }
            // 通常時のジャンプ
            else if (coyoteTimeCounter > 0f || jumpCount < maxJumpCount)
            {
                JumpAction();
            }
        }
    }

    private void JumpAction()
    {
        jumpCount++;
        coyoteTimeCounter = 0f;
        ExitBurstMode();
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
    }

    private void ExitBurstMode()
    {
        isBursting = false;
        if (AfterImageManager.Instance != null) AfterImageManager.Instance.StopEmitting();
    }

    void FixedUpdate()
    {
        lastVelocity = rb.linearVelocity;
        if (isAiming || isBursting) return;

        Vector2 currentVel = rb.linearVelocity;
        Vector2 platformVel = (movingPlatformRb != null) ? movingPlatformRb.linearVelocity : Vector2.zero;
        float targetX = horizontalInput * moveSpeed;
        float currentXRelative = currentVel.x - platformVel.x;
        float newXRelative = Mathf.MoveTowards(currentXRelative, targetX, moveSpeed * 20f * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newXRelative + platformVel.x, currentVel.y);
    }

    private void OnDrawGizmos()
    {
        if (col == null) return;
        Vector2 origin = (Vector2)transform.position + new Vector2(0, -colliderHalfHeight + groundCheckOffset);
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(origin, groundCheckSize);
    }
}