using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【第2形態専用コンボ②】👑 3方向高速大回転ビーム × 怒涛の予測偏差6連爆撃
/// ビームを3本に減らして安地を広げた分、回転速度を2倍近くにアップ！
/// さらに爆弾の投擲間隔を限界まで狭め、次々と弾幕が降り注ぐ派手な絵面にリニューアル。
/// </summary>
public class HosaP2_BeamBombsState : GlitchHosaBaseState
{
    private GameObject laserPivot;
    private List<GameObject> activeWarningLines = new List<GameObject>();
    private List<GameObject> activeWarningCircles = new List<GameObject>();

    public HosaP2_BeamBombsState(GlitchHosaController boss) : base(boss) { }

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
        laserPivot = null;
        activeWarningLines.Clear();
        activeWarningCircles.Clear();
        boss.StartCoroutine(ExecuteBeamBombsSequence());
    }

    private IEnumerator ExecuteBeamBombsSequence()
    {
        // ===================================================================
        // ① ステージ空中センターへワープ移動
        // ===================================================================
        float centerX = (boss.stageMinX + boss.stageMaxX) / 2f;
        float centerY = (boss.stageMinY + boss.stageMaxY) / 2f;
        Vector3 centerPos = new Vector3(centerX, centerY, 0f);

        Debug.Log("<color=red>🌀 補佐P2：ハッキング・トライ・レーザー ＆ 怒涛の偏差爆撃シーケンス発動！</color>");

        yield return boss.StartCoroutine(boss.TeleportWithSquashRoutine(centerPos, 0.2f));

        laserPivot = new GameObject("HosaP2ComboLaserPivot");
        laserPivot.transform.position = centerPos;

        float laserLength = 35f;
        float beamDuration = 5.5f / boss.attackSpeedMultiplier;
        float chargeDuration = 1.0f / boss.attackSpeedMultiplier; // タメを少しキブキブにしてテンポ向上

        // ===================================================================
        // ② 👑【レイアウト修正】十字から「3方向（120度等間隔のY字）」へ変更！
        // ===================================================================
        float[] angles = { 0f, 120f, 240f };
        for (int i = 0; i < angles.Length; i++)
        {
            float angleRad = angles[i] * Mathf.Deg2Rad;
            Vector3 offsetDir = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f);

            GameObject lineObj = new GameObject($"HosaP2ComboWarningLine_{i}");
            lineObj.transform.SetParent(laserPivot.transform);
            lineObj.transform.localPosition = offsetDir * (laserLength * 0.5f);
            lineObj.transform.localRotation = Quaternion.Euler(0f, 0f, angles[i]);

            if (boss.dashWarningSprite != null)
            {
                SpriteRenderer sr = lineObj.AddComponent<SpriteRenderer>();
                sr.sprite = boss.dashWarningSprite;
                sr.color = boss.dashWarningColor;
                sr.sortingOrder = -1;
                if (boss.dashWarningMaterial != null) sr.material = boss.dashWarningMaterial;

                float w = sr.sprite.bounds.size.x;
                float h = sr.sprite.bounds.size.y;
                if (w > 0.0001f && h > 0.0001f)
                {
                    lineObj.transform.localScale = new Vector3(laserLength / w, (0.8f * boss.rotatingBeamThickness) / h, 1f);
                }
            }
            activeWarningLines.Add(lineObj);

            Vector3 beamEndPos = centerPos + offsetDir * laserLength;
            Vector3 safeTarget = new Vector3(beamEndPos.x + 0.001f, beamEndPos.y + 0.001f, beamEndPos.z + 0.001f);
            BarrierManager.Instance.SpawnLaser(centerPos, safeTarget, beamDuration, laserPivot.transform);
        }

        foreach (Transform child in laserPivot.transform)
        {
            if (child.name.StartsWith("HosaP2ComboWarningLine")) continue;

            child.localScale = new Vector3(child.localScale.x * boss.rotatingBeamThickness, child.localScale.y * boss.rotatingBeamThickness, child.localScale.z);
            LineRenderer[] lrs = child.GetComponentsInChildren<LineRenderer>(true);
            foreach (var lr in lrs) if (lr != null) lr.widthMultiplier *= boss.rotatingBeamThickness;
        }

        SoundManager.Instance.PlaySE(SeType.EnemyCharge);

        // 予兆線の点滅タメ
        float chargeTimer = 0f;
        while (chargeTimer < chargeDuration)
        {
            chargeTimer += Time.deltaTime;
            float alpha = boss.dashWarningColor.a * (0.6f + Mathf.Sin(Time.time * 30f) * 0.4f);
            foreach (var line in activeWarningLines)
            {
                if (line != null)
                {
                    SpriteRenderer sr = line.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.color = new Color(boss.dashWarningColor.r, boss.dashWarningColor.g, boss.dashWarningColor.b, alpha);
                }
            }
            yield return null;
        }

        foreach (var line in activeWarningLines) if (line != null) Object.Destroy(line);
        activeWarningLines.Clear();

        // ビーム実体化（発射！）
        ParticleSystem[] pss = laserPivot.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in pss) if (ps != null) ps.Play();

        SoundManager.Instance.PlaySE(SeType.EnemyExplosion);

        // ===================================================================
        // ③ 👑【速度＆爆撃強化】ビーム大回転 ＆ 怒涛の高速偏差爆撃トス
        // ===================================================================
        Transform player = boss.GetPlayerTransform();
        Rigidbody2D playerRb = boss.GetPlayerRigidbody();
        Vector3 launchPos = boss.tossLaunchPoint != null ? boss.tossLaunchPoint.position : boss.transform.position;

        // 👑 爆弾の総数を3発から「6発」に倍増！
        int bombCount = 6;
        // 👑 投げるインターバルを 1.3秒 から「0.65秒」へ半減！次々にボコボコ降ってきます
        float bombTossInterval = 0.65f / boss.attackSpeedMultiplier;
        float bombTimer = 0.4f; // 開幕1発目を少し早めに投げる

        float elapsed = 0f;
        // 👑 回転速度を 22f から「42f」へ大幅スピードアップ！プレイヤーを強制的に走らせます
        float rotationSpeed = 42f * boss.attackSpeedMultiplier;

        while (elapsed < beamDuration)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            bombTimer += dt;

            // ビームの超高速回転
            if (laserPivot != null)
            {
                laserPivot.transform.Rotate(Vector3.forward, rotationSpeed * dt);
            }

            // テンポよく爆弾を連続トス
            if (bombTimer >= bombTossInterval && bombCount > 0)
            {
                bombTimer = 0f;
                bombCount--;

                if (player != null)
                {
                    Vector3 pPos = player.position;
                    float clampMinX = boss.stageMinX + 2.5f;
                    float clampMaxX = boss.stageMaxX - 2.5f;
                    float clampMinY = boss.stageMinY + 1.2f;
                    float clampMaxY = boss.stageMaxY - 1.2f;

                    // プレイヤーの移動速度に応じた偏差射撃
                    float leadTime = 0.45f;
                    float predX = pPos.x;
                    float predY = pPos.y;

                    if (playerRb != null && playerRb.linearVelocity.magnitude > 0.5f)
                    {
                        predX += playerRb.linearVelocity.x * leadTime;
                        predY += playerRb.linearVelocity.y * leadTime;
                    }

                    Vector3 targetPos = new Vector3(
                        Mathf.Clamp(predX, clampMinX, clampMaxX),
                        Mathf.Clamp(predY, clampMinY, clampMaxY),
                        0f
                    );

                    boss.StartCoroutine(SpawnTossBombSequence(launchPos, targetPos));
                }
            }

            yield return null;
        }

        CleanUpAllObjects();
        boss.TransitionToState(boss.StateIdle);
    }

    private IEnumerator SpawnTossBombSequence(Vector3 launchPos, Vector3 targetPos)
    {
        if (boss.timedBombPrefab == null) yield break;

        GameObject warningObj = null;
        float dynamicDiameter = GetBombExplosionDiameter();

        if (boss.dashWarningSprite != null)
        {
            warningObj = new GameObject("HosaP2ComboBombWarning");
            warningObj.transform.position = targetPos;

            SpriteRenderer sr = warningObj.AddComponent<SpriteRenderer>();
            sr.sprite = boss.dashWarningSprite;
            sr.color = boss.dashWarningColor;
            sr.sortingOrder = -1;
            if (boss.dashWarningMaterial != null) sr.material = boss.dashWarningMaterial;

            float w = sr.sprite.bounds.size.x;
            float h = sr.sprite.bounds.size.y;
            if (w > 0.0001f && h > 0.0001f)
            {
                warningObj.transform.localScale = new Vector3(dynamicDiameter / w, dynamicDiameter / h, 1f);
            }
            activeWarningCircles.Add(warningObj);
        }

        // 👑 爆弾が次々降ってくるため、予測表示の受付時間を 0.4秒 から「0.28秒」に短縮してスリルを強化！
        yield return new WaitForSeconds(0.28f);

        if (warningObj != null)
        {
            activeWarningCircles.Remove(warningObj);
            Object.Destroy(warningObj);
        }

        SoundManager.Instance.PlaySE(SeType.EnemyExplosion);
        GameObject bombObj = Object.Instantiate(boss.timedBombPrefab, launchPos, Quaternion.identity);
        var bomb = bombObj.GetComponent<GlitchHosaTimedBomb>();
        if (bomb != null)
        {
            bomb.explodeOnLand = true;
            // 落下までの滞空時間(第3引数)を0.45fから0.35fへ高速化し、キレ味を高めました
            bomb.InitializeToss(launchPos, targetPos, 0.35f, 2.8f);
        }
    }

    private void CleanUpAllObjects()
    {
        if (laserPivot != null) { Object.Destroy(laserPivot); laserPivot = null; }
        foreach (var line in activeWarningLines) if (line != null) Object.Destroy(line);
        foreach (var circle in activeWarningCircles) if (circle != null) Object.Destroy(circle);
        activeWarningLines.Clear();
        activeWarningCircles.Clear();
    }

    public override void Exit()
    {
        CleanUpAllObjects();
        boss.StopAllCoroutines();
    }
}