using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【パターン3：地獄級・2連隙間埋め絨毯爆撃版】
/// 👑 1回だけの美しい単発奥ワープ ＆ Y座標を少し下げた快適配置版！
/// </summary>
public class HosaP1_BackBombsState : GlitchHosaBaseState
{
    private List<GameObject> activeWarningVisuals = new List<GameObject>();

    public HosaP1_BackBombsState(GlitchHosaController boss) : base(boss) { }

    /// <summary>
    /// 👑【動的同期システム】爆弾プレハブから物理爆発半径を取得し、直径に変換して返します。
    /// </summary>
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
        // ===================================================================
        // ① 👑【ロジック修正】無駄な2連続ワープを廃止し、1回で指定位置へワープ
        // ===================================================================
        float centerX = (boss.stageMinX + boss.stageMaxX) / 2f;

        // 👑【高さ修正】最高部固定ではなく、ステージの下限から上限の「65%」の位置に変更（もうちょっと下の方）
        // ※もっと下げたい場合は、0.65f を 0.55f（ほぼ中央）などに微調整してください。
        float targetY = Mathf.Lerp(boss.stageMinY, boss.stageMaxY, 0.65f);

        Vector3 rallyBackPos = new Vector3(centerX, targetY, boss.rallyZOffset);

        Debug.Log($"<color=red>💀 補佐：【2連隙間埋め爆撃】安全地帯は存在しない。ハッキングラリーを開始します！</color>");

        // 👑【修正】手前を経由する無駄なTeleport処理を削除。最初から直接「1回だけ」奥へスクワッシュワープ！
        yield return boss.StartCoroutine(boss.TeleportWithSquashRoutine(rallyBackPos, 0.2f));

        boss.SetAllCollidersEnabled(false);
        boss.SetAllDamageSourcesEnabled(false);
        boss.isBackRallyMode = true;

        // 【遠近感演出】奥のレイヤーに到達したため、ボスを0.55倍に縮小させて奥行き感を強調！
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

        // 💥 第1波：通常のクロス絨毯爆撃（7発同時）
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

        // 💥 第2波：1回目で爆撃してない隙間を狙い撃ち（8発同時）
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

            // 👑 爆弾のY制限なども、ボスの高さ低下に合わせて窮屈にならないよう stageMaxY / stageMinY で綺麗にクランプ
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

        while (rallyBombObj != null && boss.isBackRallyMode)
        {
            yield return null;
        }

        if (!boss.isBackRallyMode) yield break;

        Debug.Log("<color=yellow>⏰ 補佐 : 時限爆弾が自爆しました。手前ステージに戻ります。</color>");
        yield return boss.StartCoroutine(ReturnToFrontWarpRoutine());
    }

    private IEnumerator ReturnToFrontWarpRoutine()
    {
        boss.isBackRallyMode = false;

        // サイズを元の等倍スケールに戻す
        boss.targetScale = boss.originalVisualLocalScale;

        Vector3 frontPos = new Vector3(boss.transform.position.x, boss.transform.position.y, 0f);
        // 手前に戻る時もきれいに1発でワープ！
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

        // 小さくなったまま固定されるのを防ぐ絶対ガード
        boss.targetScale = boss.originalVisualLocalScale;

        Vector3 pos = boss.transform.position;
        pos.z = 0f;
        boss.transform.position = pos;

        boss.SetAllCollidersEnabled(true);
        boss.SetAllDamageSourcesEnabled(true);
        boss.StopAllCoroutines();
    }
}