using UnityEngine;

public class StageSecondBossStunState : StageSecondBossBaseState
{
    private float stunTimer = 0f;
    private Vector3 airPosition;
    private Rigidbody2D rb;
    private float deadRotationAngle = 90f;
    private bool isRecoveryStarted = false;

    public StageSecondBossStunState(StageSecondBossController boss) : base(boss) { }

    public override void Enter()
    {
        stunTimer = 0f;
        isRecoveryStarted = false;
        airPosition = boss.transform.position;

        SoundManager.Instance.FadeBGMVolume(0.5f, 0.5f);

        Debug.Log("<color=yellow>💫 ボス：気絶ダウン。画面上の全ての攻撃予兆を完全クリーンアップします。</color>");

        if (boss.TryGetComponent<StageSecondBossHealth>(out var health))
        {
            health.StopFlashAndReset();
            health.hasBarrier = false;
            health.UpdateBarrierVisual();
        }
        boss.ForceResetAllMaterials();

        // ===================================================================
        // 🛠️【バグ修正：予測範囲の完全消滅】
        // ① ウルト中にスタンさせた際、ボス本体のウルト用予測円が残るのを防ぐため、確実にOFF！
        // ===================================================================
        if (boss.ultIndicatorRoot != null)
        {
            boss.ultIndicatorRoot.SetActive(false);
        }

        // ===================================================================
        // ② 通常攻撃の予兆中にスタンさせた際、空中置き去りになるのを防ぐため、
        // 画面上のすべての「時限爆弾」と「地雷式爆弾」を根こそぎ完全消去！
        // 爆弾が消えれば、連動してそれぞれの予測範囲（インジケーター）も綺麗に消え去ります。
        // ===================================================================
        var timedBombs = Object.FindObjectsByType<StageSecondBossTimedBomb>(FindObjectsSortMode.None);
        foreach (var bomb in timedBombs)
        {
            if (bomb != null) Object.Destroy(bomb.gameObject);
        }

        var mineBombs = Object.FindObjectsByType<StageSecondBossMineBomb>(FindObjectsSortMode.None);
        foreach (var bomb in mineBombs)
        {
            if (bomb != null) Object.Destroy(bomb.gameObject);
        }

        // 最初からパッと横倒し（90度）の美しい軸補正にして落とす
        Transform bossVisual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;
        bossVisual.localRotation = Quaternion.Euler(0f, 0f, deadRotationAngle);

        Vector3 baseOffset = new Vector3(0f, boss.stunPivotOffsetY, 0f);
        Vector3 finalRotatedOffset = Quaternion.Euler(0f, 0f, deadRotationAngle) * baseOffset;
        bossVisual.localPosition = boss.originalVisualLocalPosition + (baseOffset - finalRotatedOffset);

        rb = boss.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = boss.stunGravityAmount;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.mass = 1f;
            rb.linearVelocity = Vector2.zero; // 初速リセット
        }

        boss.SetAllDamageSourcesEnabled(false);

        if (boss.bossAnimator != null) boss.bossAnimator.speed = 0f;
        SoundManager.Instance.PlaySE(SeType.EnemyConfusion);

        if (boss.specialDisappearEffect) Object.Instantiate(boss.specialDisappearEffect, boss.transform.position, Quaternion.identity);

    }

    public override void Update()
    {
        stunTimer += Time.deltaTime;
        Transform bossVisual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;

        if (stunTimer >= boss.stunDuration - boss.stunRecoveryDuration)
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

            float recoveryProgress = (stunTimer - (boss.stunDuration - boss.stunRecoveryDuration)) / boss.stunRecoveryDuration;
            float smoothT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(recoveryProgress));

            boss.transform.position = Vector3.Lerp(boss.transform.position, airPosition, smoothT);

            float currentAngle = Mathf.Lerp(deadRotationAngle, 0f, smoothT);
            bossVisual.localRotation = Quaternion.Euler(0f, 0f, currentAngle);

            Vector3 baseOffset = new Vector3(0f, boss.stunPivotOffsetY, 0f);
            Vector3 rotatedOffset = Quaternion.Euler(0f, 0f, currentAngle) * baseOffset;
            bossVisual.localPosition = boss.originalVisualLocalPosition + (baseOffset - rotatedOffset);

            if (stunTimer >= boss.stunDuration)
            {
                boss.TransitionToState(boss.StateIdle);
            }
            return;
        }
    }

    public override void Exit()
    {
        SoundManager.Instance.FadeBGMVolume(0.5f, 1.0f);
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

        Transform visual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;
        visual.localRotation = boss.originalVisualLocalRotation;
        visual.localPosition = boss.originalVisualLocalPosition;

        boss.ForceResetAllMaterials();
    }
}