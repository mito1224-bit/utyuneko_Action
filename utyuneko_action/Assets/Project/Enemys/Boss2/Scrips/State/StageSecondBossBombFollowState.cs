using System.Collections;
using UnityEngine;

public class StageSecondBossBombFollowState : StageSecondBossBaseState
{
    // 💡 途中で中断されても消せるように、メンバ変数として保持する
    private GameObject currentWarningObj;

    public StageSecondBossBombFollowState(StageSecondBossController boss) : base(boss) { }

    public override void Enter()
    {
        currentWarningObj = null;
        boss.StartCoroutine(ExecuteFollowAttackRoutine());
    }

    private IEnumerator ExecuteFollowAttackRoutine()
    {
        Transform player = boss.GetPlayerTransform();
        Rigidbody2D playerRb = boss.GetPlayerRigidbody();

        if (player == null || boss.timedBombPrefab == null)
        {
            boss.TransitionToState(boss.StateIdle);
            yield break;
        }

        float explosionRadius = 3.0f;
        if (boss.timedBombPrefab.TryGetComponent<StageSecondBossTimedBomb>(out var bombComp))
        {
            explosionRadius = bombComp.explosionRadius;
        }

        int totalShots = boss.followAttackCount;
        float attackHeight = boss.stageMaxY - 2.0f;
        float leadAheadTime = 0.25f;

        Debug.Log($"<color=red>🎯 ボス：未来予測・偏差連続爆撃を開始！</color>");

        for (int i = 0; i < totalShots; i++)
        {
            if (player == null) break;

            float chargeTimer = 0f;
            float currentChargeDuration = boss.instantLineWarningDuration / boss.attackSpeedMultiplier;

            Vector3 targetPredictPos = player.position;
            if (playerRb != null)
            {
                targetPredictPos.x += playerRb.linearVelocity.x * leadAheadTime;
            }

            Vector3 bombTargetPos = targetPredictPos;

            // 警告円を予測位置に生成
            if (boss.instantLineWarningSprite != null)
            {
                currentWarningObj = new GameObject("FollowWarningVisual");
                currentWarningObj.transform.position = bombTargetPos;

                SpriteRenderer sr = currentWarningObj.AddComponent<SpriteRenderer>();
                sr.sprite = boss.instantLineWarningSprite;
                sr.color = new Color(1f, 0.2f, 0f, 0.6f);
                sr.sortingOrder = -1;

                float spriteWidth = sr.sprite.bounds.size.x;
                float spriteHeight = sr.sprite.bounds.size.y;
                if (spriteWidth > 0.0001f && spriteHeight > 0.0001f)
                {
                    currentWarningObj.transform.localScale = new Vector3((explosionRadius * 2f) / spriteWidth, (explosionRadius * 2f) / spriteHeight, 1f);
                }
            }

            SoundManager.Instance.PlaySE(SeType.EnemyCharge);

            while (chargeTimer < currentChargeDuration)
            {
                chargeTimer += Time.deltaTime;
                if (player != null)
                {
                    Vector3 freshPredictPos = player.position;
                    if (playerRb != null)
                    {
                        freshPredictPos.x += playerRb.linearVelocity.x * leadAheadTime;
                    }

                    bombTargetPos = Vector3.Lerp(bombTargetPos, freshPredictPos, 6f * Time.deltaTime);
                    if (currentWarningObj != null) currentWarningObj.transform.position = bombTargetPos;

                    float desiredX = Mathf.Clamp(bombTargetPos.x, boss.stageMinX + 1.5f, boss.stageMaxX - 1.5f);
                    Vector3 followPos = new Vector3(desiredX, attackHeight, boss.transform.position.z);
                    boss.transform.position = Vector3.Lerp(boss.transform.position, followPos, 12f * Time.deltaTime);
                }
                yield return null;
            }

            // 通常ルートの消去
            if (currentWarningObj != null)
            {
                Object.Destroy(currentWarningObj);
                currentWarningObj = null;
            }

            // 投擲時の巨大化タメ演出
            Transform visual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;
            Vector3 originalScale = visual.localScale;
            float pulseDuration = 0.1f;
            float pulseT = 0f;
            Vector3 targetScale = originalScale * boss.attackPulseScaleMultiplier;

            while (pulseT < pulseDuration * 0.4f)
            {
                pulseT += Time.deltaTime;
                visual.localScale = Vector3.Lerp(originalScale, targetScale, pulseT / (pulseDuration * 0.4f));
                yield return null;
            }

            Vector3 launchPos = boss.tossLaunchPoint != null ? boss.tossLaunchPoint.position : boss.transform.position;
            SoundManager.Instance.PlaySE(SeType.EnemyRangeAttack);

            GameObject bombObj = Object.Instantiate(boss.timedBombPrefab, launchPos, Quaternion.identity);
            if (bombObj.TryGetComponent<StageSecondBossTimedBomb>(out var timedBomb))
            {
                timedBomb.fuseDuration = 0f;
                timedBomb.InitializeToss(launchPos, bombTargetPos, 0.45f, 1.5f);
            }

            while (pulseT < pulseDuration * 0.6f)
            {
                pulseT += Time.deltaTime;
                visual.localScale = Vector3.Lerp(targetScale, originalScale, pulseT / (pulseDuration * 0.6f));
                yield return null;
            }
            visual.localScale = originalScale;

            if (i < totalShots - 1)
            {
                float intervalTimer = 0f;
                float currentInterval = boss.followAttackInterval / boss.attackSpeedMultiplier;
                while (intervalTimer < currentInterval)
                {
                    intervalTimer += Time.deltaTime;
                    if (player != null)
                    {
                        Vector3 freshPredictPos = player.position;
                        if (playerRb != null) freshPredictPos.x += playerRb.linearVelocity.x * leadAheadTime;

                        float desiredX = Mathf.Clamp(freshPredictPos.x, boss.stageMinX + 1.5f, boss.stageMaxX - 1.5f);
                        Vector3 followPos = new Vector3(desiredX, attackHeight, boss.transform.position.z);
                        boss.transform.position = Vector3.Lerp(boss.transform.position, followPos, 6f * Time.deltaTime);
                    }
                    yield return null;
                }
            }
        }

        yield return new WaitForSeconds(0.6f / boss.attackSpeedMultiplier);
        boss.TransitionToState(boss.StateIdle);
    }

    // ===================================================================
    // 🧹【新設：大掃除アンカー】ボスがスタンしたり倒された瞬間に強制執行！
    // ===================================================================
    public override void Exit()
    {
        if (currentWarningObj != null)
        {
            Object.Destroy(currentWarningObj);
            currentWarningObj = null;
        }
    }
}