using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StageSecondBossBombInstantLineState : StageSecondBossBaseState
{
    // 💡 途中で中断されても全消去できるように、リストをメンバ変数に格上げ
    private List<GameObject> activeWarningVisuals = new List<GameObject>();

    public StageSecondBossBombInstantLineState(StageSecondBossController boss) : base(boss) { }

    public override void Enter()
    {
        activeWarningVisuals.Clear();
        Debug.Log("ボス技②：上空中央へ高速ホバー移動 ＆ 巨大化グリッド爆撃");
        boss.StartCoroutine(ExecuteGridAttackWithWarningRoutine());
    }

    private IEnumerator ExecuteGridAttackWithWarningRoutine()
    {
        float topCenterX = (boss.stageMinX + boss.stageMaxX) / 2f;
        float topY = boss.stageMaxY - 1.8f;
        Vector3 hoverTargetPos = new Vector3(topCenterX, topY, boss.transform.position.z);

        yield return boss.StartCoroutine(boss.HoverMoveRoutine(hoverTargetPos, 0.3f));

        SoundManager.Instance.PlaySE(SeType.EnemyCharge);

        if (boss.timedBombPrefab == null)
        {
            boss.TransitionToState(boss.StateIdle);
            yield break;
        }

        Vector3 launchPos = boss.tossLaunchPoint != null ? boss.tossLaunchPoint.position : boss.transform.position;

        int totalColumns = 5;
        int bombsPerColumn = 4;
        int safeColumnIndex = Random.Range(0, totalColumns);

        float startX = boss.stageMinX + 2.0f;
        float endX = boss.stageMaxX - 2.0f;
        float startY = boss.stageMinY + 1.0f;
        float endY = boss.stageMaxY - 1.0f;

        List<Vector3> targetPositions = new List<Vector3>();

        float explosionRadius = 3.0f;
        if (boss.timedBombPrefab.TryGetComponent<StageSecondBossTimedBomb>(out var bombComp))
        {
            explosionRadius = bombComp.explosionRadius;
        }

        for (int col = 0; col < totalColumns; col++)
        {
            if (col == safeColumnIndex) continue;

            float xRatio = (float)col / (totalColumns - 1);
            float targetX = Mathf.Lerp(startX, endX, xRatio);

            for (int row = 0; row < bombsPerColumn; row++)
            {
                float yRatio = (float)row / (bombsPerColumn - 1);
                float targetY = Mathf.Lerp(startY, endY, yRatio);
                Vector3 targetPos = new Vector3(targetX, targetY, 0f);
                targetPositions.Add(targetPos);

                if (boss.instantLineWarningSprite != null)
                {
                    GameObject warningObj = new GameObject("InstantLineWarningVisual");
                    warningObj.transform.position = targetPos;

                    SpriteRenderer sr = warningObj.AddComponent<SpriteRenderer>();
                    sr.sprite = boss.instantLineWarningSprite;
                    sr.color = boss.instantLineWarningColor;
                    sr.sortingOrder = -1;
                    if (boss.instantLineWarningMaterial)
                    {
                        sr.material = boss.instantLineWarningMaterial;
                    }

                    float spriteWidth = sr.sprite.bounds.size.x;
                    float spriteHeight = sr.sprite.bounds.size.y;

                    if (spriteWidth > 0.0001f && spriteHeight > 0.0001f)
                    {
                        float targetScaleX = (explosionRadius * 2f) / spriteWidth;
                        float targetScaleY = (explosionRadius * 2f) / spriteHeight;
                        warningObj.transform.localScale = new Vector3(targetScaleX, targetScaleY, 1f);
                    }
                    else
                    {
                        warningObj.transform.localScale = new Vector3(explosionRadius * 2f, explosionRadius * 2f, 1f);
                    }

                    activeWarningVisuals.Add(warningObj); // メンバ変数リストへ蓄積
                }
            }
        }

        // ===================================================================
        // 🛠️【修正：第2形態高速化システム】
        // 怒りモード（フェーズ2）の時は、倍率（例: 1.4倍）で割り算することで、
        // 予兆時間が自動的にギュギュッと短縮されて超高速で投げてくるようになります！
        // ===================================================================
        yield return new WaitForSeconds(boss.instantLineWarningDuration / boss.attackSpeedMultiplier);

        ClearAllWarnings(); // 通常ルートの消去

        Transform visual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;
        Vector3 originalScale = visual.localScale;
        float pulseDuration = 0.15f;
        float pulseT = 0f;
        Vector3 targetScale = originalScale * boss.attackPulseScaleMultiplier;

        while (pulseT < pulseDuration * 0.4f)
        {
            pulseT += Time.deltaTime;
            visual.localScale = Vector3.Lerp(originalScale, targetScale, pulseT / (pulseDuration * 0.4f));
            yield return null;
        }

        SoundManager.Instance.PlaySE(SeType.EnemyRangeAttack);
        foreach (Vector3 targetPos in targetPositions)
        {
            GameObject bombObj = Object.Instantiate(boss.timedBombPrefab, launchPos, Quaternion.identity);
            var timedBomb = bombObj.GetComponent<StageSecondBossTimedBomb>();
            if (timedBomb != null)
            {
                timedBomb.fuseDuration = 0f;
                timedBomb.InitializeToss(launchPos, targetPos, 0.6f, 3.0f);
            }
        }

        pulseT = 0f;
        while (pulseT < pulseDuration * 0.6f)
        {
            pulseT += Time.deltaTime;
            visual.localScale = Vector3.Lerp(targetScale, originalScale, pulseT / (pulseDuration * 0.6f));
            yield return null;
        }
        visual.localScale = originalScale;

        yield return new WaitForSeconds(0.8f / boss.attackSpeedMultiplier);
        boss.TransitionToState(boss.StateIdle);
    }

    private void ClearAllWarnings()
    {
        foreach (var warning in activeWarningVisuals)
        {
            if (warning != null) Object.Destroy(warning);
        }
        activeWarningVisuals.Clear();
    }

    public override void Exit()
    {
        ClearAllWarnings();
    }
}