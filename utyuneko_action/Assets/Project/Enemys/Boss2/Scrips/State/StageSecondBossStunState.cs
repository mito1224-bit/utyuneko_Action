using UnityEngine;

public class StageSecondBossStunState : StageSecondBossBaseState
{
    private float stunTimer = 0f;
    private Vector3 airPosition;
    private Vector3 originalLocalPosition;
    private bool isGrounded = false;
    private Rigidbody2D rb;
    private float currentAngle = 0f;

    private DamageSource cachedDamageSource;

    public StageSecondBossStunState(StageSecondBossController boss) : base(boss) { }

    public override void Enter()
    {
        stunTimer = 0f;
        isGrounded = false;
        currentAngle = 0f;

        airPosition = boss.transform.position;

        Transform bossVisual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;
        originalLocalPosition = bossVisual.localPosition;

        rb = boss.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = boss.stunGravityAmount;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        cachedDamageSource = boss.GetComponent<DamageSource>();
        if (cachedDamageSource == null) cachedDamageSource = boss.GetComponentInChildren<DamageSource>();

        if (cachedDamageSource != null) cachedDamageSource.enabled = false;

        Debug.Log("<color=yellow>💫 ボス：気絶ダウン中！プレイヤーの最大コンボチャンス！</color>");

        if (boss.bossAnimator != null) boss.bossAnimator.speed = 0f;
        SoundManager.Instance.PlaySE(SeType.EnemyConfusion);
    }

    public override void Update()
    {
        stunTimer += Time.deltaTime;
        Transform bossVisual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;
        float groundY = boss.stageMinY + boss.stunGroundYOffset;

        // --- 演出フェーズ①：スタン終了直前の「空中へのフワッと自動復帰」 ---
        if (stunTimer >= boss.stunDuration - boss.stunRecoveryDuration)
        {
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.linearVelocity = Vector2.zero;
            }

            float recoveryProgress = (stunTimer - (boss.stunDuration - boss.stunRecoveryDuration)) / boss.stunRecoveryDuration;
            float smoothT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(recoveryProgress));

            boss.transform.position = Vector3.Lerp(boss.transform.position, airPosition, smoothT);

            currentAngle = Mathf.Lerp(90f, 0f, smoothT);
            bossVisual.localRotation = Quaternion.Euler(0f, 0f, currentAngle);

            Vector3 baseOffset = new Vector3(0f, boss.stunPivotOffsetY, 0f);
            Vector3 rotatedOffset = Quaternion.Euler(0f, 0f, currentAngle) * baseOffset;
            bossVisual.localPosition = originalLocalPosition + (baseOffset - rotatedOffset);

            if (stunTimer >= boss.stunDuration)
            {
                // 🔥 時間満了！ Exit() を経由して Idle 状態へ戻ります。
                boss.TransitionToState(boss.StateIdle);
            }
            return;
        }

        // --- 演出フェーズ②：物理落下 ＆ 地面での気絶ダウン ---
        if (!isGrounded)
        {
            currentAngle += boss.stunRotateSpeed * Time.deltaTime;
            bossVisual.localRotation = Quaternion.Euler(0f, 0f, currentAngle);

            Vector3 baseOffset = new Vector3(0f, boss.stunPivotOffsetY, 0f);
            Vector3 rotatedOffset = Quaternion.Euler(0f, 0f, currentAngle) * baseOffset;
            bossVisual.localPosition = originalLocalPosition + (baseOffset - rotatedOffset);

            bool isVelocityStopped = rb != null && rb.linearVelocity.y >= -0.05f && stunTimer > 0.1f;

            if (boss.transform.position.y <= groundY || isVelocityStopped)
            {
                isGrounded = true;
                if (rb != null)
                {
                    rb.bodyType = RigidbodyType2D.Kinematic;
                    rb.linearVelocity = Vector2.zero;
                }

                Vector3 correctedPos = boss.transform.position;
                correctedPos.y = groundY;
                boss.transform.position = correctedPos;

                currentAngle = 90f;
                bossVisual.localRotation = Quaternion.Euler(0f, 0f, currentAngle);

                Vector3 finalRotatedOffset = Quaternion.Euler(0f, 0f, currentAngle) * baseOffset;
                bossVisual.localPosition = originalLocalPosition + (baseOffset - finalRotatedOffset);
            }
        }
        else
        {
            float shakeX = Mathf.Sin(Time.time * 40f) * 0.06f;
            boss.transform.position = new Vector3(airPosition.x + shakeX, groundY, boss.transform.position.z);
        }
    }

    public override void Exit()
    {
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }

        // ===================================================================
        // 🛠️【要望の追加】スタン状態が完全になくなったらバリアを新しく生成（復活）する！
        // 元の空中位置に戻り、Idleステートを始める直前にバリアがパリィンと再展開されます。
        // ===================================================================
        boss.ResetBarrier();

        if (cachedDamageSource != null) cachedDamageSource.enabled = true;
        if (boss.bossAnimator != null) boss.bossAnimator.speed = 1f;

        Transform visual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;
        visual.localRotation = Quaternion.identity;
        visual.localPosition = originalLocalPosition;
    }
}