using UnityEngine;

public class StageSecondBossDeadState : StageSecondBossBaseState
{
    private float twitchTimer = 0f;
    private float deadRotationAngle = 90f;

    public StageSecondBossDeadState(StageSecondBossController boss) : base(boss) { }

    public override void Enter()
    {
        twitchTimer = 0f;

        TimeManager.Instance.TriggerGlobalSlowMotion(1.0f, 0.2f);

        Debug.Log("<color=red>💀 ボス：戦闘不能。ノックバック落下を開始します。</color>");

        SoundManager.Instance.StopLoopSE(boss.gameObject);
        if (boss.bossAnimator != null) boss.bossAnimator.speed = 0f;

        if (boss.TryGetComponent<StageSecondBossHealth>(out var health))
        {
            health.StopFlashAndReset();
            health.hasBarrier = false;
            health.UpdateBarrierVisual();
        }
        boss.ForceResetAllMaterials();

        // パッと横倒しにして落とす
        Transform bossVisual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;
        bossVisual.localRotation = Quaternion.Euler(0f, 0f, deadRotationAngle);

        Vector3 baseOffset = new Vector3(0f, boss.stunPivotOffsetY, 0f);
        Vector3 finalRotatedOffset = Quaternion.Euler(0f, 0f, deadRotationAngle) * baseOffset;
        bossVisual.localPosition = boss.originalVisualLocalPosition + (baseOffset - finalRotatedOffset);

        boss.SetAllDamageSourcesEnabled(false);

        // 必殺技用インジケーターを消す
        if (boss.ultIndicatorRoot != null)
        {
            boss.ultIndicatorRoot.SetActive(false);
        }

        // ステージ上の全爆弾の大掃除
        StageSecondBossTimedBomb[] timedBombs = Object.FindObjectsByType<StageSecondBossTimedBomb>(FindObjectsSortMode.None);
        foreach (var bomb in timedBombs)
        {
            if (bomb != null) Object.Destroy(bomb.gameObject);
        }

        StageSecondBossMineBomb[] mineBombs = Object.FindObjectsByType<StageSecondBossMineBomb>(FindObjectsSortMode.None);
        foreach (var bomb in mineBombs)
        {
            if (bomb != null) Object.Destroy(bomb.gameObject);
        }

        // 物理落下の開始
        Rigidbody2D rb = boss.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = boss.stunGravityAmount;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.mass = 1f;

            Transform player = boss.GetPlayerTransform();
            float pushPowerX = 3.5f;
            float pushPowerY = 2.0f;
            float finalKnockbackX = 0f;

            if (player != null)
            {
                finalKnockbackX = boss.transform.position.x > player.position.x ? pushPowerX : -pushPowerX;
            }

            rb.linearVelocity = new Vector2(finalKnockbackX, pushPowerY);
        }
    }

    public override void Update()
    {
        Transform bossVisual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;

        // 痙攣演出
        twitchTimer += Time.unscaledDeltaTime * 12f;
        if (Random.value > 0.85f)
        {
            float twitchX = Mathf.Sin(twitchTimer) * 0.012f;
            float twitchY = Mathf.Cos(twitchTimer * 2.0f) * 0.008f;

            Vector3 baseOffset = new Vector3(0f, boss.stunPivotOffsetY, 0f);
            Vector3 finalRotatedOffset = Quaternion.Euler(0f, 0f, deadRotationAngle) * baseOffset;
            Vector3 correctedCenter = boss.originalVisualLocalPosition + (baseOffset - finalRotatedOffset);

            Vector3 rotatedTwitch = Quaternion.Euler(0f, 0f, deadRotationAngle) * new Vector3(twitchX, twitchY, 0f);
            bossVisual.localPosition = correctedCenter + rotatedTwitch;
        }
    }

    public override void Exit()
    {
        Transform visual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;
        visual.localPosition = boss.originalVisualLocalPosition;
        visual.localRotation = boss.originalVisualLocalRotation;
    }
}