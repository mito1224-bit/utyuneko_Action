using System.Collections;
using UnityEngine;

public class GlitchHosaStunState : GlitchHosaBaseState
{
    private float stunTimer = 0f;
    private Vector3 airPosition;
    private Vector3 groundPosition;
    private Rigidbody2D rb;
    private bool isRecoveryStarted = false;
    private bool hasHitGround = false;

    public bool startAsGrounded { get; set; } = false;

    public GlitchHosaStunState(GlitchHosaController boss) : base(boss) { }

    public override void Enter()
    {
        stunTimer = 0f;
        isRecoveryStarted = false;

        airPosition = boss.transform.position;
        airPosition.z = 0f;
        groundPosition = airPosition;

        boss.isLineStolen = false;
        boss.isReflectionStolen = false;
        boss.isSlowStolen = false;

        // 👑【ボス2完全準拠】フラッシュを安全に止めてからマテリアルを強制リセット[cite: 9]
        GlitchHosaHealth health = null;
        if (boss.TryGetComponent<GlitchHosaHealth>(out health))
        {
            health.StopFlashAndReset(false);
            health.hasBarrier = false;
            health.UpdateBarrierVisual();
        }
        boss.ForceResetAllMaterials();

        // 深く前傾して項垂れ(X-40)、力なく少し傾く(Z10)アングルストレージへロック！
        boss.targetXRotation = -40f;
        boss.targetYRotation = boss.defaultYRotation;
        boss.targetZRotation = 10f;

        boss.targetVisualOffset = Vector3.zero;

        rb = boss.GetComponent<Rigidbody2D>();

        if(boss.StunParticle) Object.Instantiate(boss.StunParticle, boss.transform.position, Quaternion.identity);

        if (startAsGrounded)
        {
            hasHitGround = true;
            groundPosition = boss.transform.position;
            groundPosition.z = 0f;
            boss.transform.position = groundPosition;

            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
            Debug.Log("<color=yellow>💤 補佐：接地スタンを開始！</color>");
        }
        else
        {
            hasHitGround = false;
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = boss.stunGravityAmount;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                rb.mass = 1f;
                rb.linearVelocity = Vector2.zero;
            }
        }

        boss.SetAllDamageSourcesEnabled(false);
        if (boss.bossAnimator != null) boss.bossAnimator.speed = 0f;
        SoundManager.Instance.PlaySE(SeType.EnemyConfusion);
    }

    public override void Update()
    {
        if (!hasHitGround)
        {
            int groundLayer = LayerMask.GetMask("Ground");
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(groundLayer);
            filter.useLayerMask = true;

            Collider2D col = boss.GetComponent<Collider2D>();
            if (col == null) col = boss.GetComponentInChildren<Collider2D>();

            bool isTouchingGround = false;
            if (col != null) isTouchingGround = col.IsTouching(filter);

            if (isTouchingGround && rb != null && rb.linearVelocity.y <= 0.1f)
            {
                hasHitGround = true;
                stunTimer = 0f;
                groundPosition = boss.transform.position;
                groundPosition.z = 0f;

                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
            return;
        }

        // ⚡【超肉厚デジタルグリッチ演出】
        Transform squashTarget = boss.GetSquashTarget();
        if (squashTarget != null && !isRecoveryStarted)
        {
            float shakeX = Random.Range(-0.25f, 0.25f);
            float shakeY = Random.Range(-0.08f, 0.08f);

            boss.targetVisualOffset = new Vector3(shakeX, shakeY, 0f);

            if (Random.value < 0.12f)
            {
                boss.targetScale = new Vector3(boss.originalVisualLocalScale.x * 1.3f, boss.originalVisualLocalScale.y * 0.7f, boss.originalVisualLocalScale.z);
            }
            else
            {
                boss.targetScale = boss.originalVisualLocalScale;
            }
        }

        stunTimer += Time.deltaTime;
        float totalStunDuration = boss.GetHosaStunDuration();
        float recoveryDuration = boss.stunRecoveryDuration;

        if (stunTimer >= totalStunDuration - recoveryDuration)
        {
            if (!isRecoveryStarted)
            {
                isRecoveryStarted = true;
                if (rb != null)
                {
                    rb.bodyType = RigidbodyType2D.Kinematic;
                    rb.linearVelocity = Vector2.zero;
                }
            }

            float recoveryProgress = (stunTimer - (totalStunDuration - recoveryDuration)) / recoveryDuration;
            float smoothT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(recoveryProgress));

            boss.transform.position = Vector3.Lerp(boss.transform.position, airPosition, smoothT);

            boss.targetXRotation = Mathf.Lerp(-40f, boss.defaultXRotation, smoothT);
            boss.targetYRotation = boss.defaultYRotation;
            boss.targetZRotation = Mathf.Lerp(10f, 0f, smoothT);
            boss.targetVisualOffset = Vector3.zero;
            boss.targetScale = boss.originalVisualLocalScale;

            if (stunTimer >= totalStunDuration)
            {
                boss.TransitionToState(boss.StateIdle);
            }
            return;
        }
    }

    public override void Exit()
    {
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            rb.mass = 1000f;
        }

        if (boss.currentHP > 0f)
        {
            boss.ResetBarrier();
            boss.SetAllDamageSourcesEnabled(true);
        }

        if (boss.bossAnimator != null) boss.bossAnimator.speed = 1f;

        Vector3 finalPos = boss.transform.position;
        finalPos.z = 0f;
        boss.transform.position = finalPos;

        boss.targetXRotation = boss.defaultXRotation;
        boss.targetYRotation = boss.defaultYRotation;
        boss.targetZRotation = 0f;
        boss.targetScale = boss.originalVisualLocalScale;
        boss.targetVisualOffset = Vector3.zero;

        Transform squashTarget = boss.GetSquashTarget();
        if (squashTarget != null)
        {
            squashTarget.localScale = boss.originalVisualLocalScale;
            squashTarget.localPosition = boss.originalVisualLocalPosition;
        }

        // 👑【ボス2完全準拠】Exit時にも綺麗に初期化[cite: 9]
        boss.ForceResetAllMaterials();
        startAsGrounded = false;
    }
}