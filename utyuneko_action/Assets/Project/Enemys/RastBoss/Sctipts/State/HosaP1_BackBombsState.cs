using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【パターン3：地獄級・2連隙間埋め絨毯爆撃版】
/// 👑【着地後スタンカウント確定版】
/// スタン時間が短すぎて空中で復帰してしまうバグを完全粉砕！
/// 吹き飛んで地面にしっかり落ちてからカウントを開始し、極上の手触りへ調整しました。
/// </summary>
public class HosaP1_BackBombsState : GlitchHosaBaseState
{
    private List<GameObject> activeWarningVisuals = new List<GameObject>();

    public HosaP1_BackBombsState(GlitchHosaController boss) : base(boss) { }

    private float GetBombExplosionDiameter()
    {
        if (boss.timedBombPrefab != null)
        {
            if (boss.timedBombPrefab.TryGetComponent<GlitchHosaTimedBomb>(out var bomb))
            {
                return bomb.explosionRadius * 2f;
            }
        }
        return 6.4f;
    }

    public override void Enter()
    {
        activeWarningVisuals.Clear();
        boss.StartCoroutine(ExecuteBackBombsSequence());
    }

    private IEnumerator ExecuteBackBombsSequence()
    {
        float centerX = (boss.stageMinX + boss.stageMaxX) / 2f;
        float targetY = Mathf.Lerp(boss.stageMinY, boss.stageMaxY, 0.65f);
        Vector3 rallyBackPos = new Vector3(centerX, targetY, boss.rallyZOffset);

        Debug.Log($"<color=red>💀 補佐：【2連隙間埋め爆撃】安全地帯は存在しない。ハッキングラリーを開始します！</color>");

        yield return boss.StartCoroutine(boss.TeleportWithSquashRoutine(rallyBackPos, 0.2f));

        boss.SetAllCollidersEnabled(false);
        boss.SetAllDamageSourcesEnabled(false);
        boss.isBackRallyMode = true;

        boss.targetScale = boss.originalVisualLocalScale * 0.55f;

        if (boss.timedBombPrefab == null)
        {
            yield return boss.StartCoroutine(ReturnToFrontWarpRoutine());
            yield break;
        }

        Transform player = boss.GetPlayerTransform();
        Rigidbody2D playerRb = boss.GetPlayerRigidbody();
        if (player == null)
        {
            yield return boss.StartCoroutine(ReturnToFrontWarpRoutine());
            yield break;
        }

        Vector3 launchPos = boss.tossLaunchPoint != null ? boss.tossLaunchPoint.position : boss.transform.position;
        float dynamicDiameter = GetBombExplosionDiameter();

        // ===================================================================
        // ②【開幕】ステージ全域を分断する「2連クロス絨毯爆撃」
        // ===================================================================
        float minY = boss.stageMinY + 1.2f;
        float maxY = boss.stageMaxY + 1.5f;
        float segmentW = (boss.stageMaxX - boss.stageMinX) / 8f;

        Vector3[] zigzagTargets1 = new Vector3[7];
        for (int i = 0; i < 7; i++)
        {
            float targetX = boss.stageMinX + segmentW * (i + 1);
            targetY = (i % 2 == 0) ? minY : maxY;
            zigzagTargets1[i] = new Vector3(targetX, targetY, 0f);
        }

        foreach (Vector3 target in zigzagTargets1) SpawnTargetWarning(target, dynamicDiameter);
        yield return new WaitForSeconds(0.4f);
        ClearAllWarnings();

        SoundManager.Instance.PlaySE(SeType.EnemyExplosion);
        foreach (Vector3 target in zigzagTargets1)
        {
            GameObject bombObj = Object.Instantiate(boss.timedBombPrefab, launchPos, Quaternion.identity);
            var bomb = bombObj.GetComponent<GlitchHosaTimedBomb>();
            if (bomb != null) { bomb.explodeOnLand = true; bomb.InitializeToss(launchPos, target, 0.45f, 2.0f); }
           
        }

        yield return new WaitForSeconds(0.5f);

        if (player == null) { yield return boss.StartCoroutine(ReturnToFrontWarpRoutine()); yield break; }
       

        Vector3[] zigzagTargets2 = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            float targetX = boss.stageMinX + segmentW * (i + 0.5f);
            targetY = (i % 2 == 0) ? maxY : minY;
            zigzagTargets2[i] = new Vector3(targetX, targetY, 0f);
        }

        foreach (Vector3 target in zigzagTargets2) SpawnTargetWarning(target, dynamicDiameter);
        yield return new WaitForSeconds(0.4f);
        ClearAllWarnings();

        SoundManager.Instance.PlaySE(SeType.EnemyExplosion);
        foreach (Vector3 target in zigzagTargets2)
        {
            GameObject bombObj = Object.Instantiate(boss.timedBombPrefab, launchPos, Quaternion.identity);
            var bomb = bombObj.GetComponent<GlitchHosaTimedBomb>();
            if (bomb != null) { bomb.explodeOnLand = true; bomb.InitializeToss(launchPos, target, 0.45f, 2.0f); }
           
        }

        yield return new WaitForSeconds(0.55f);

        // ===================================================================
        // ③【中盤】トリプルワイド予測包囲爆撃 4連打
        // ===================================================================
        int predictWaveCount = 4;
        for (int step = 0; step < predictWaveCount; step++)
        {
            if (player == null) break;

            Vector3 pPos = player.position;
            float clampMinX = boss.stageMinX + 2.0f;
            float clampMaxX = boss.stageMaxX - 2.0f;
            float clampMinY = boss.stageMinY + 1.0f;
            float clampMaxY = boss.stageMaxY - 1.0f;

            float leadTime = 0.45f;
            float basePredX = pPos.x + (playerRb != null ? playerRb.linearVelocity.x * leadTime : 0f);
            float basePredY = pPos.y + (playerRb != null ? playerRb.linearVelocity.y * leadTime : 0f);

            if (playerRb == null || playerRb.linearVelocity.magnitude < 0.5f)
            {
                basePredX = pPos.x + (Random.value > 0.5f ? 2.0f : -2.0f);
            }

            float spreadWidth = 5.2f;
            float spreadHeight = 2.0f;

            Vector3 targetCenter = new Vector3(Mathf.Clamp(basePredX, clampMinX, clampMaxX), Mathf.Clamp(basePredY, clampMinY, clampMaxY), 0f);
            Vector3 targetLeft = new Vector3(Mathf.Clamp(basePredX - spreadWidth, clampMinX, clampMaxX), Mathf.Clamp(basePredY + spreadHeight, clampMinY, clampMaxY), 0f);
            Vector3 targetRight = new Vector3(Mathf.Clamp(basePredX + spreadWidth, clampMinX, clampMaxX), Mathf.Clamp(basePredY - spreadHeight, clampMinY, clampMaxY), 0f);

            SpawnTargetWarning(targetCenter, dynamicDiameter);
            SpawnTargetWarning(targetLeft, dynamicDiameter);
            SpawnTargetWarning(targetRight, dynamicDiameter);
            yield return new WaitForSeconds(0.35f);
            ClearAllWarnings();

            SoundManager.Instance.PlaySE(SeType.EnemyExplosion);

            GameObject b1 = Object.Instantiate(boss.timedBombPrefab, launchPos, Quaternion.identity);
            var bomb1 = b1.GetComponent<GlitchHosaTimedBomb>();
            if (bomb1 != null) { bomb1.explodeOnLand = true; bomb1.InitializeToss(launchPos, targetCenter, 0.4f, 2.0f); }
           

            GameObject b2 = Object.Instantiate(boss.timedBombPrefab, launchPos, Quaternion.identity);
            var bomb2 = b2.GetComponent<GlitchHosaTimedBomb>();
            if (bomb2 != null) { bomb2.explodeOnLand = true; bomb2.InitializeToss(launchPos, targetLeft, 0.45f, 2.4f); }
           

            GameObject b3 = Object.Instantiate(boss.timedBombPrefab, launchPos, Quaternion.identity);
            var bomb3 = b3.GetComponent<GlitchHosaTimedBomb>();
            if (bomb3 != null) { bomb3.explodeOnLand = true; bomb3.InitializeToss(launchPos, targetRight, 0.5f, 2.8f); }
           

            yield return new WaitForSeconds(0.5f / boss.attackSpeedMultiplier);
        }

        yield return new WaitForSeconds(0.3f);

        // ===================================================================
        // ④【終盤】ラリー時限爆弾 ＆ 十字デストラップ完全包囲
        // ===================================================================
        if (player == null) { yield return boss.StartCoroutine(ReturnToFrontWarpRoutine()); yield break; }
       

        float playerAreaX = Mathf.Clamp(player.position.x, boss.stageMinX + 4.5f, boss.stageMaxX - 4.5f);
        float bGroundY = boss.stageMinY + 1.2f;

        Vector3 centerRallyTarget = new Vector3(playerAreaX, bGroundY, 0f);
        Vector3 leftTrapTarget = new Vector3(playerAreaX - 3.2f, bGroundY, 0f);
        Vector3 rightTrapTarget = new Vector3(playerAreaX + 3.2f, bGroundY, 0f);
        Vector3 topTrapTarget = new Vector3(playerAreaX, bGroundY + 3.2f, 0f);
        Vector3 bottomTrapTarget = new Vector3(playerAreaX, bGroundY - 1.0f, 0f);

        SpawnTargetWarning(centerRallyTarget, dynamicDiameter);
        SpawnTargetWarning(leftTrapTarget, dynamicDiameter);
        SpawnTargetWarning(rightTrapTarget, dynamicDiameter);
        SpawnTargetWarning(topTrapTarget, dynamicDiameter);
        SpawnTargetWarning(bottomTrapTarget, dynamicDiameter);
        yield return new WaitForSeconds(0.55f);
        ClearAllWarnings();

        SoundManager.Instance.PlaySE(SeType.EnemyExplosion);

        Vector3[] trapTargets = { leftTrapTarget, rightTrapTarget, topTrapTarget, bottomTrapTarget };
        foreach (Vector3 trap in trapTargets)
        {
            GameObject bTrap = Object.Instantiate(boss.timedBombPrefab, launchPos, Quaternion.identity);
            var bombTrap = bTrap.GetComponent<GlitchHosaTimedBomb>();
            if (bombTrap != null) { bombTrap.explodeOnLand = true; bombTrap.InitializeToss(launchPos, trap, 0.5f, 2.2f); }
           
        }

        GameObject rallyBombObj = Object.Instantiate(boss.timedBombPrefab, launchPos, Quaternion.identity);
        var rallyBomb = rallyBombObj.GetComponent<GlitchHosaTimedBomb>();
        if (rallyBomb != null)
        {
            rallyBomb.explodeOnLand = false;
            rallyBomb.fuseDuration = 4.0f;
            rallyBomb.InitializeToss(launchPos, centerRallyTarget, 0.5f, 3.5f);
        }

        // 👑【今回の重要改修：地面落下完全同期システム】
        // 時限爆弾が爆発するか、あるいはボス自身が手前にノックバックで戻される状態になるまで待機
        while (rallyBombObj != null && boss.isBackRallyMode)
        {
            yield return null;
        }

        if (!boss.isBackRallyMode) yield break;

        Debug.Log("<color=yellow>⏰ 補佐 : 時限爆弾が自爆しました。プレイヤーの接地を確認してから手前ステージに戻ります。</color>");

        // ===================================================================
        // 🛑【スタン空中解除防止ガード】
        // プレイヤーのRigidbodyを取得し、地面（速度がほぼ0、かつY座標が着地している）に
        // しっかりと落ちきるまで、次の手前ワープ＆スタン復旧処理を完全にウェイトします！
        // ===================================================================
        if (playerRb != null)
        {
            // プレイヤーが落下運動を終えて完全に地面に着地するのを待つ (Y軸速度の安定化を検知)
            while (Mathf.Abs(playerRb.linearVelocity.y) > 0.1f)
            {
                yield return null;
            }
            // 地面に激突した瞬間からの「ドスッ」という重みを出すため、着地後にさらに0.4秒だけ余韻（スタン時間）を追加！
            yield return new WaitForSeconds(0.4f);
        }

        yield return boss.StartCoroutine(ReturnToFrontWarpRoutine());
    }

    private IEnumerator ReturnToFrontWarpRoutine()
    {
        boss.isBackRallyMode = false;
        boss.targetScale = boss.originalVisualLocalScale;

        Vector3 frontPos = new Vector3(boss.transform.position.x, boss.transform.position.y, 0f);
        yield return boss.StartCoroutine(boss.TeleportWithSquashRoutine(frontPos, 0.2f));

        boss.SetAllCollidersEnabled(true);
        boss.SetAllDamageSourcesEnabled(true);
        boss.TransitionToState(boss.StateIdle);
    }

    private void SpawnTargetWarning(Vector3 targetPos, float diameter)
    {
        if (boss.dashWarningSprite != null)
        {
            GameObject warningObj = new GameObject("HosaBombWarningVisual");
            warningObj.transform.position = targetPos;

            SpriteRenderer sr = warningObj.AddComponent<SpriteRenderer>();
            sr.sprite = boss.dashWarningSprite;
            sr.color = boss.dashWarningColor;
            sr.sortingOrder = -1;

            if (boss.dashWarningMaterial != null) sr.material = boss.dashWarningMaterial;

            float spriteWidth = sr.sprite.bounds.size.x;
            float spriteHeight = sr.sprite.bounds.size.y;

            if (spriteWidth > 0.0001f && spriteHeight > 0.0001f)
            {
                warningObj.transform.localScale = new Vector3(diameter / spriteWidth, diameter / spriteHeight, 1f);
            }
            activeWarningVisuals.Add(warningObj);
        }
    }

    private void ClearAllWarnings()
    {
        foreach (var warning in activeWarningVisuals) if (warning != null) Object.Destroy(warning);
        activeWarningVisuals.Clear();
    }

    public override void Exit()
    {
        ClearAllWarnings();
        boss.isBackRallyMode = false;
        boss.targetScale = boss.originalVisualLocalScale;

        Vector3 pos = boss.transform.position;
        pos.z = 0f;
        boss.transform.position = pos;

        boss.SetAllCollidersEnabled(true);
        boss.SetAllDamageSourcesEnabled(true);
        boss.StopAllCoroutines();
    }
}