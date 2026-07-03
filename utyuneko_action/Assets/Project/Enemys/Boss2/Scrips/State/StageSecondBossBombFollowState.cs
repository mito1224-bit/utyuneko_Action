using System.Collections;
using UnityEngine;

public class StageSecondBossBombFollowState : StageSecondBossBaseState
{
    public StageSecondBossBombFollowState(StageSecondBossController boss) : base(boss) { }

    public override void Enter()
    {
        boss.StartCoroutine(ExecuteFollowAttackRoutine());
    }

    private IEnumerator ExecuteFollowAttackRoutine()
    {
        Transform player = boss.GetPlayerTransform();
        if (player == null || boss.timedBombPrefab == null)
        {
            boss.TransitionToState(boss.StateIdle);
            yield break;
        }

        // ===================================================================
        // 🛠️【高度修正】Y座標をプレイヤー依存から「ステージの中心高度」へ変更！
        // 左右の距離（7.5）を保ちつつ、ステージのど真ん中の高さからスナイプします。
        // ===================================================================
        float sideOffset = Random.value > 0.5f ? 7.5f : -7.5f;
        float targetX = player.position.x + sideOffset;
        float targetY = (boss.stageMinY + boss.stageMaxY) / 2f; // ステージの上下の真ん中

        // ステージの壁や限界から飛び出さないように完璧に安全ガードクランプ！
        targetX = Mathf.Clamp(targetX, boss.stageMinX + 2.0f, boss.stageMaxX - 2.0f);
        targetY = Mathf.Clamp(targetY, boss.stageMinY + 2.0f, boss.stageMaxY - 2.0f);

        Vector3 hoverTargetPos = new Vector3(targetX, targetY, boss.transform.position.z);

        // 🚀 連撃を開始する前に、0.3秒でステージ中心高度の狙撃ポジションへ滑空！
        yield return boss.StartCoroutine(boss.HoverMoveRoutine(hoverTargetPos, 0.3f));

        float explosionRadius = 3.0f;
        if (boss.timedBombPrefab.TryGetComponent<StageSecondBossTimedBomb>(out var bombComp))
        {
            explosionRadius = bombComp.explosionRadius;
        }

        int totalShots = boss.followAttackCount;

        for (int i = 0; i < totalShots; i++)
        {
            if (player == null) break;

            Vector3 targetPos = player.position;
            Vector3 launchPos = boss.tossLaunchPoint != null ? boss.tossLaunchPoint.position : boss.transform.position;

            GameObject warningObj = null;

            if (boss.instantLineWarningSprite != null)
            {
                warningObj = new GameObject("FollowWarningVisual");
                warningObj.transform.position = targetPos;

                SpriteRenderer sr = warningObj.AddComponent<SpriteRenderer>();
                sr.sprite = boss.instantLineWarningSprite;
                sr.color = new Color(1f, 0.25f, 0f, 0.5f);
                sr.sortingOrder = -1;

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
            }

            SoundManager.Instance.PlaySE(SeType.EnemyCharge);

            yield return new WaitForSeconds(boss.instantLineWarningDuration);

            if (warningObj != null) Object.Destroy(warningObj);

            Transform visual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;
            Vector3 originalScale = visual.localScale;
            float pulseDuration = 0.12f;
            float pulseT = 0f;
            Vector3 targetScale = originalScale * boss.attackPulseScaleMultiplier;

            while (pulseT < pulseDuration * 0.4f)
            {
                pulseT += Time.deltaTime;
                visual.localScale = Vector3.Lerp(originalScale, targetScale, pulseT / (pulseDuration * 0.4f));
                yield return null;
            }

            // 🚀 爆弾を投擲！
            SoundManager.Instance.PlaySE(SeType.EnemyRangeAttack);
            GameObject bombObj = Object.Instantiate(boss.timedBombPrefab, launchPos, Quaternion.identity);
            if (bombObj.TryGetComponent<StageSecondBossTimedBomb>(out var timedBomb))
            {
                timedBomb.fuseDuration = 0f;
                timedBomb.InitializeToss(launchPos, targetPos, 0.6f, 3.0f);
            }

            pulseT = 0f;
            while (pulseT < pulseDuration * 0.6f)
            {
                pulseT += Time.deltaTime;
                visual.localScale = Vector3.Lerp(targetScale, originalScale, pulseT / (pulseDuration * 0.6f));
                yield return null;
            }
            visual.localScale = originalScale;

            if (i < totalShots - 1)
            {
                yield return new WaitForSeconds(boss.followAttackInterval);
            }
        }

        yield return new WaitForSeconds(0.8f);
        boss.TransitionToState(boss.StateIdle);
    }
}