using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StageSecondBossBombMineState : StageSecondBossBaseState
{
    public StageSecondBossBombMineState(StageSecondBossController boss) : base(boss) { }

    public override void Enter()
    {
        // 💡 演出と移動の制御をコルーチンで一括実行
        boss.StartCoroutine(ExecuteAttackRoutine());
    }

    private IEnumerator ExecuteAttackRoutine()
    {
        Debug.Log("ボス技③：ステージ真ん中へホバー移動 ＆ 空中中央ランダム地雷");

        // ===================================================================
        // 🛠️【要望】ステージの完全な真ん中（空中）の座標を計算して高速移動！
        // ===================================================================
        float centerX = (boss.stageMinX + boss.stageMaxX) / 2f;
        float centerY = (boss.stageMinY + boss.stageMaxY) / 2f; // 上下の真ん中
        Vector3 hoverTargetPos = new Vector3(centerX, centerY, boss.transform.position.z);

        // 🚀 0.3秒でステージの真ん中へ残像ホバー移動！
        yield return boss.StartCoroutine(boss.HoverMoveRoutine(hoverTargetPos, 0.3f));

        // 移動完了後にチャージ開始
        SoundManager.Instance.PlaySE(SeType.EnemyCharge);

        Vector3 launchPos = boss.tossLaunchPoint != null ? boss.tossLaunchPoint.position : boss.transform.position;

        if (boss.mineBombPrefab != null)
        {
            StageSecondBossMineBomb[] activeMines = Object.FindObjectsByType<StageSecondBossMineBomb>(FindObjectsSortMode.None);
            int currentMineCount = activeMines.Length;
            int spawnCount = Random.Range(boss.minMineSpawnCount, boss.maxMineSpawnCount + 1);

            float totalStageWidth = boss.stageMaxX - boss.stageMinX;
            float halfCenterWidth = (totalStageWidth * boss.mineCenterRangeRatio) / 2f;

            Vector3 bossPos = boss.transform.position;
            float avoidBossDistance = 2.5f;
            float avoidMineDistance = 2.5f;

            List<Vector3> newSpawnPositions = new List<Vector3>();

            // 投擲の瞬間に一瞬巨大化（タメ）
            Transform visual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;
            Vector3 originalScale = visual.localScale;
            float pulseDuration = 0.15f;
            float t = 0f;
            Vector3 targetScale = originalScale * boss.attackPulseScaleMultiplier;

            while (t < pulseDuration * 0.4f)
            {
                t += Time.deltaTime;
                visual.localScale = Vector3.Lerp(originalScale, targetScale, t / (pulseDuration * 0.4f));
                yield return null;
            }

            // 🚀 地雷生成
            for (int i = 0; i < spawnCount; i++)
            {
                if (currentMineCount >= boss.maxActiveMines) break;

                Vector3 targetPos = Vector3.zero;
                bool isPositionValid = false;
                int attempts = 0;

                while (!isPositionValid && attempts < 30)
                {
                    attempts++;
                    float randX = Random.Range(centerX - halfCenterWidth, centerX + halfCenterWidth);
                    float randY = Random.Range(boss.stageMinY + 1.5f, boss.stageMaxY - 1.5f);
                    Vector3 testPos = new Vector3(randX, randY, 0f);

                    bool isTooClose = false;
                    if (Vector3.Distance(testPos, bossPos) < avoidBossDistance) isTooClose = true;

                    if (!isTooClose)
                    {
                        foreach (var mine in activeMines)
                        {
                            if (mine != null && Vector3.Distance(testPos, mine.transform.position) < avoidMineDistance)
                            {
                                isTooClose = true;
                                break;
                            }
                        }
                    }

                    if (!isTooClose)
                    {
                        foreach (var pos in newSpawnPositions)
                        {
                            if (Vector3.Distance(testPos, pos) < avoidMineDistance)
                            {
                                isTooClose = true;
                                break;
                            }
                        }
                    }

                    if (!isTooClose)
                    {
                        targetPos = testPos;
                        isPositionValid = true;
                    }
                }

                if (!isPositionValid)
                {
                    float fallbackX = centerX + (i == 0 ? -halfCenterWidth : halfCenterWidth) * 0.5f;
                    float fallbackY = Mathf.Lerp(boss.stageMinY + 1.5f, boss.stageMaxY - 1.5f, 0.5f);
                    targetPos = new Vector3(fallbackX, fallbackY, 0f);
                }

                newSpawnPositions.Add(targetPos);

                GameObject bombObj = Object.Instantiate(boss.mineBombPrefab, launchPos, Quaternion.identity);
                if (bombObj.TryGetComponent<StageSecondBossMineBomb>(out var mineBomb))
                {
                    mineBomb.InitializeToss(launchPos, targetPos, 0.6f, 3.0f);
                }
                currentMineCount++;
            }

            // スーーッと元のサイズに戻る
            t = 0f;
            while (t < pulseDuration * 0.6f)
            {
                t += Time.deltaTime;
                visual.localScale = Vector3.Lerp(targetScale, originalScale, t / (pulseDuration * 0.6f));
                yield return null;
            }
            visual.localScale = originalScale;
        }

        yield return new WaitForSeconds(0.4f / boss.attackSpeedMultiplier);
        boss.TransitionToState(boss.StateIdle);
    }
}