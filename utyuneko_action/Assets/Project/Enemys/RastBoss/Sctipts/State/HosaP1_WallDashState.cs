using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HosaP1_WallDashState : GlitchHosaBaseState
{
    private int dashCount = 0;
    private const int maxDashes = 3;
    private GameObject activeWarningLine;

    public HosaP1_WallDashState(GlitchHosaController boss) : base(boss) { }

    public override void Enter()
    {
        dashCount = 0;
        activeWarningLine = null;
        boss.StartCoroutine(ExecuteWallDashSequence());
    }

    private IEnumerator ExecuteWallDashSequence()
    {
        // 👑 開幕：1回目の突進開始位置（右壁側）を先に出して、0.2秒かけてワープインで登場！[cite: 21]
        float initialPlayerY = boss.GetPlayerTransform() != null ? boss.GetPlayerTransform().position.y : boss.transform.position.y;
        initialPlayerY = Mathf.Clamp(initialPlayerY, boss.stageMinY + 1.0f, boss.stageMaxY - 1.0f);
        Vector3 currentStartPos = new Vector3(boss.stageMaxX - 1.5f, initialPlayerY, boss.transform.position.z);
        
        yield return boss.StartCoroutine(boss.TeleportInRoutine(currentStartPos, 0.2f)); // 👑 0.2秒に調整して視認性アップ！[cite: 21]

        while (dashCount < maxDashes)
        {
            dashCount++;

            Vector3 startPos = boss.transform.position; // すでに移動・出現完了している安全な座標[cite: 21]
            Vector3 targetLeftPos = new Vector3(boss.stageMinX + 1.5f, startPos.y, boss.transform.position.z);

            // ダッシュチャージ中のバリア強制展開[cite: 21]
            boss.SetBarrierActive(true, force: true);

            Vector3 dir = targetLeftPos - startPos;
            float distance = dir.magnitude;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Vector3 centerPos = startPos + dir * 0.5f;

            if (boss.dashWarningSprite != null)
            {
                activeWarningLine = new GameObject("HosaDashWarningLine");
                activeWarningLine.transform.position = centerPos;
                activeWarningLine.transform.rotation = Quaternion.Euler(0f, 0f, angle);

                SpriteRenderer sr = activeWarningLine.AddComponent<SpriteRenderer>();
                sr.sprite = boss.dashWarningSprite;
                sr.color = boss.dashWarningColor;
                sr.sortingOrder = -1;

                if (boss.dashWarningMaterial != null) sr.material = boss.dashWarningMaterial;

                float spriteWidth = sr.sprite.bounds.size.x;
                float spriteHeight = sr.sprite.bounds.size.y;

                if (spriteWidth > 0.0001f && spriteHeight > 0.0001f)
                {
                    activeWarningLine.transform.localScale = new Vector3(distance / spriteWidth, 1.5f / spriteHeight, 1f);
                }
            }

            SoundManager.Instance.PlaySE(SeType.EnemyCharge);

            float chargeTimer = 0f;
            float warningDuration = boss.dashWarningDuration / boss.attackSpeedMultiplier;
            float dirX = targetLeftPos.x - startPos.x;

            while (chargeTimer < warningDuration)
            {
                chargeTimer += Time.deltaTime;
                boss.SetFacingDirection(dirX);

                if (activeWarningLine != null)
                {
                    SpriteRenderer sr = activeWarningLine.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        float alpha = boss.dashWarningColor.a * (0.6f + Mathf.Sin(Time.time * 30f) * 0.4f);
                        sr.color = new Color(boss.dashWarningColor.r, boss.dashWarningColor.g, boss.dashWarningColor.b, alpha);
                    }
                }
                yield return null;
            }

            ClearWarningLine();

            SoundManager.Instance.PlaySE(SeType.PlayerBurstBegin);
            boss.SetBarrierActive(true, force: true);

            // 👑 突進！
            yield return boss.StartCoroutine(boss.HoverMoveRoutine(targetLeftPos, 0.22f / boss.attackSpeedMultiplier));

            if (dashCount == maxDashes)
            {
                // 3回目の激突時：シールド大破エフェクト生成[cite: 21]
                var health = boss.GetComponent<GlitchHosaHealth>();
                if (health != null && health.hasBarrier)
                {
                    health.hasBarrier = false;
                    health.UpdateBarrierVisual();
                    if (health.barrierBreakEffect != null)
                    {
                        GameObject fx = Object.Instantiate(health.barrierBreakEffect, boss.transform.position, Quaternion.identity);
                        fx.transform.localScale = Vector3.one * health.barrierBreakEffectScale;
                    }
                }

                boss.SetBarrierActive(false); // バリアを完全に無効化してピヨりへ移行します[cite: 21]

                SoundManager.Instance.PlaySE(SeType.PlayerWallHit);
                ShakeTarget.Instance.Shake(0.3f, 2.5f);

                GameObject runnerObj = new GameObject("HosaShockwaveRunner");
                var runnerBehavior = runnerObj.AddComponent<ShockwaveRunnerHelper>();
                runnerBehavior.SetupAndStart(boss, targetLeftPos);

                boss.TransitionToState(boss.StateStun);
                yield break;
            }

            // 👑【ワープアウト】その場で滑らかに「シュウゥン」と縦に縮んで消滅！[cite: 21]
            yield return boss.StartCoroutine(boss.TeleportOutRoutine(0.2f)); // 👑 しっかり目視できる0.2秒に修正！[cite: 21]
            boss.SetBarrierActive(false);

            // 👑【瞬間移動】縮みきった瞬間に、一瞬で次の右端の座標へ！
            float nextPlayerY = boss.GetPlayerTransform() != null ? boss.GetPlayerTransform().position.y : boss.transform.position.y;
            nextPlayerY = Mathf.Clamp(nextPlayerY, boss.stageMinY + 1.0f, boss.stageMaxY - 1.0f);
            Vector3 nextStartPos = new Vector3(boss.stageMaxX - 1.5f, nextPlayerY, boss.transform.position.z);
            
            boss.transform.position = nextStartPos; // 縮んで姿を消したまま、一瞬で右端へチェンジ！[cite: 21]

            // 👑 移動後の右端で、姿を隠したままインターバルをタメる（全体のテンポ維持のため0.2秒待機）[cite: 21]
            yield return new WaitForSeconds(0.2f);

            // 👑【ワープイン】インターバルが明けたら、パッと等倍へ戻って出現！[cite: 21]
            yield return boss.StartCoroutine(boss.TeleportInRoutine(nextStartPos, 0.2f)); // 👑 0.2秒で気持ちよく展開！[cite: 21]
        }

        boss.TransitionToState(boss.StateIdle);
    }

    private void ClearWarningLine()
    {
        if (activeWarningLine != null)
        {
            Object.Destroy(activeWarningLine);
            activeWarningLine = null;
        }
    }

    public override void Exit()
    {
        ClearWarningLine();
        if (boss != null)
        {
            boss.SetBarrierActive(false);
            boss.targetXRotation = boss.defaultXRotation;
            boss.targetYRotation = boss.defaultYRotation;
            boss.targetZRotation = 0f;
        }
        boss.StopAllCoroutines();
    }
}