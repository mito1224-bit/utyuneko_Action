using System.Collections;
using UnityEngine;

public class StageSecondBossBombTimedState : StageSecondBossBaseState
{
    public StageSecondBossBombTimedState(StageSecondBossController boss) : base(boss) { }

    public override void Enter()
    {
        boss.StartCoroutine(ExecuteAttackRoutine());
    }

    private IEnumerator ExecuteAttackRoutine()
    {
        Debug.Log("ボス技①：ステージ中心高度へホバー移動 ＆ 狙い撃ちクロス爆撃");

        Transform player = boss.GetPlayerTransform();
        if (player == null || boss.timedBombPrefab == null)
        {
            boss.TransitionToState(boss.StateIdle);
            yield break;
        }

        float centerX = (boss.stageMinX + boss.stageMaxX) / 2f;
        float targetX;

        if (player.position.x > centerX)
        {
            targetX = Mathf.Lerp(boss.stageMinX, centerX, 0.4f);
        }
        else
        {
            targetX = Mathf.Lerp(centerX, boss.stageMaxX, 0.6f);
        }

        // ===================================================================
        // 🛠️【高度修正】高さ（Y座標）をプレイヤー依存から「ステージの中心高度」へ！
        // ===================================================================
        float targetY = (boss.stageMinY + boss.stageMaxY) / 2f;

        // 左右の壁に埋まらないように最終安全ガードクランプ
        targetX = Mathf.Clamp(targetX, boss.stageMinX + 2.0f, boss.stageMaxX - 2.0f);

        Vector3 hoverTargetPos = new Vector3(targetX, targetY, boss.transform.position.z);

        // 🚀 0.3秒でステージ中心高度の逆サイドへ残像ホバー退避！
        yield return boss.StartCoroutine(boss.HoverMoveRoutine(hoverTargetPos, 0.3f));

        // 設置タメ演出開始
        Vector3 launchPos = boss.tossLaunchPoint != null ? boss.tossLaunchPoint.position : boss.transform.position;
        int count = boss.straightBombCount;
        bool isVertical = Random.value > 0.5f;

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

        // 🚀 爆弾生成
        SoundManager.Instance.PlaySE(SeType.EnemyRangeAttack);
        if (isVertical)
        {
            float bombX = Mathf.Clamp(player.position.x, boss.stageMinX + 1.5f, boss.stageMaxX - 1.5f);
            float startY = boss.stageMinY + 1.0f;
            float endY = boss.stageMaxY - 1.0f;

            for (int i = 0; i < count; i++)
            {
                float ratio = (float)i / (count - 1);
                float targetCombinedY = Mathf.Lerp(startY, endY, ratio);
                Vector3 targetPos = new Vector3(bombX, targetCombinedY, 0f);

                GameObject bombObj = Object.Instantiate(boss.timedBombPrefab, launchPos, Quaternion.identity);
                var timedBomb = bombObj.GetComponent<StageSecondBossTimedBomb>();
                if (timedBomb != null) timedBomb.InitializeToss(launchPos, targetPos, 0.5f, 3.0f);
            }
        }
        else
        {
            float startX = boss.stageMinX + 1.5f;
            float endX = boss.stageMaxX - 1.5f;
            float bombY = Mathf.Clamp(player.position.y + 0.5f, boss.stageMinY + 1.0f, boss.stageMaxY - 1.0f);

            for (int i = 0; i < count; i++)
            {
                float ratio = (float)i / (count - 1);
                float targetCombinedX = Mathf.Lerp(startX, endX, ratio);
                Vector3 targetPos = new Vector3(targetCombinedX, bombY, 0f);

                GameObject bombObj = Object.Instantiate(boss.timedBombPrefab, launchPos, Quaternion.identity);
                var timedBomb = bombObj.GetComponent<StageSecondBossTimedBomb>();
                if (timedBomb != null) timedBomb.InitializeToss(launchPos, targetPos, 0.5f, 3.0f);
            }
        }

        t = 0f;
        while (t < pulseDuration * 0.6f)
        {
            t += Time.deltaTime;
            visual.localScale = Vector3.Lerp(targetScale, originalScale, t / (pulseDuration * 0.6f));
            yield return null;
        }
        visual.localScale = originalScale;

        yield return new WaitForSeconds(0.4f);
        boss.TransitionToState(boss.StateIdle);
    }
}