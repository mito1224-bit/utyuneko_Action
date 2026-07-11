using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StageSecondBossBombCrossState : StageSecondBossBaseState
{
    // 💡 途中で中断されても全消去できるように、リストをメンバ変数に格上げ
    private List<GameObject> activeWarningVisuals = new List<GameObject>();

    public StageSecondBossBombCrossState(StageSecondBossController boss) : base(boss) { }

    public override void Enter()
    {
        activeWarningVisuals.Clear();
        Debug.Log("<color=red>⚔️ 新技！ボス技⑤：空中中央へホバー移動 ＆ プレイヤー狙撃・十字クロス爆撃！</color>");
        boss.StartCoroutine(ExecuteCrossAttackWithWarningRoutine());
    }

    private IEnumerator ExecuteCrossAttackWithWarningRoutine()
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

        Transform player = boss.GetPlayerTransform();
        if (player == null)
        {
            boss.TransitionToState(boss.StateIdle);
            yield break;
        }

        Vector3 launchPos = boss.tossLaunchPoint != null ? boss.tossLaunchPoint.position : boss.transform.position;

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

        Vector3 playerPos = player.position;
        playerPos.x = Mathf.Clamp(playerPos.x, startX, endX);
        playerPos.y = Mathf.Clamp(playerPos.y, startY, endY);

        float interval = explosionRadius * 1.65f;

        // 1. 中心点
        targetPositions.Add(new Vector3(playerPos.x, playerPos.y, 0f));

        // 2. 水平ライン
        float currentX = playerPos.x - interval;
        while (currentX >= startX)
        {
            targetPositions.Add(new Vector3(currentX, playerPos.y, 0f));
            currentX -= interval;
        }
        currentX = playerPos.x + interval;
        while (currentX <= endX)
        {
            targetPositions.Add(new Vector3(currentX, playerPos.y, 0f));
            currentX += interval;
        }

        // 3. 垂直ライン
        float currentY = playerPos.y - interval;
        while (currentY >= startY)
        {
            targetPositions.Add(new Vector3(playerPos.x, currentY, 0f));
            currentY -= interval;
        }
        currentY = playerPos.y + interval;
        while (currentY <= endY)
        {
            targetPositions.Add(new Vector3(playerPos.x, currentY, 0f));
            currentY += interval;
        }

        // 警告インジケーター一斉生成
        foreach (Vector3 targetPos in targetPositions)
        {
            if (boss.instantLineWarningSprite != null)
            {
                GameObject warningObj = new GameObject("CrossWarningVisual");
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

        // 十字爆撃
        SoundManager.Instance.PlaySE(SeType.EnemyRangeAttack);
        foreach (Vector3 targetPos in targetPositions)
        {
            GameObject bombObj = Object.Instantiate(boss.timedBombPrefab, launchPos, Quaternion.identity);
            var timedBomb = bombObj.GetComponent<StageSecondBossTimedBomb>();
            if (timedBomb != null)
            {
                timedBomb.fuseDuration = 0f;
                timedBomb.InitializeToss(launchPos, targetPos, 0.55f, 3.0f);
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

    // ===================================================================
    // 🧹【新設：大掃除アンカー】中断時に全インジケーターを一斉爆破消去！
    // ===================================================================
    public override void Exit()
    {
        ClearAllWarnings();
    }
}