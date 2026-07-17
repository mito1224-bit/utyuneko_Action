using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 【パターン1】ステージ中央へイージング・ワープし、4方向に静止した赤い警告帯と
/// チャージエフェクトが表示された後、4方向に極太ビームを放ちながら高速回転する大技！
/// 👑 パターン1専用の「rotatingBeamThickness」を参照して極太化を適用します。
/// </summary>
public class HosaP1_BeamState : GlitchHosaBaseState
{
    private List<GameObject> warningLines = new List<GameObject>();
    private GameObject warningPivot;
    private GameObject laserPivot;

    public HosaP1_BeamState(GlitchHosaController boss) : base(boss) { }

    public override void Enter()
    {
        warningLines.Clear();
        warningPivot = null;
        laserPivot = null;
        boss.StartCoroutine(ExecuteRotatingBeamSequence());
    }

    private IEnumerator ExecuteRotatingBeamSequence()
    {
        float centerX = (boss.stageMinX + boss.stageMaxX) / 2f;
        float centerY = (boss.stageMinY + boss.stageMaxY) / 2f;
        Vector3 stageCenter = new Vector3(centerX, centerY, boss.transform.position.z);

        Debug.Log("<color=cyan>🌪️ 補佐：ステージ中央へイージング・ワープ！チャージ＆警告フェーズを開始します！</color>");

        // 👑【修正】中央へのワープ演出時間をハッキリ見える0.2秒に完全統一！
        yield return boss.StartCoroutine(boss.TeleportWithSquashRoutine(stageCenter, 0.2f));

        // ===================================================================
        // 🟥【チャージ＆警告フェーズ】
        // ===================================================================
        laserPivot = new GameObject("HosaBeamLaserPivot");
        laserPivot.transform.position = boss.transform.position;
        laserPivot.transform.rotation = Quaternion.identity;

        float warningDuration = 1.0f / boss.attackSpeedMultiplier;
        float attackDuration = 3.5f;
        float finalAttackDuration = attackDuration / boss.attackSpeedMultiplier;
        float totalLaserLifeTime = warningDuration + finalAttackDuration;

        Vector3[] laserDirs = { Vector3.up, Vector3.right, Vector3.down, Vector3.left };
        foreach (Vector3 dir in laserDirs)
        {
            Vector3 rawTarget = boss.transform.position + dir * 20f;
            Vector3 safeTarget = new Vector3(rawTarget.x + 0.001f, rawTarget.y + 0.001f, rawTarget.z + 0.001f);

            // BarrierManagerを介してレーザーを先行スポーン！
            BarrierManager.Instance.SpawnLaser(boss.transform.position, safeTarget, totalLaserLifeTime, laserPivot.transform);
        }

        // 👑【回転ビーム専用・全方位ビーム本体極太ジャック】
        foreach (Transform child in laserPivot.transform)
        {
            Vector3 localScale = child.localScale;
            localScale.x *= boss.rotatingBeamThickness;
            localScale.y *= boss.rotatingBeamThickness;
            child.localScale = localScale;

            LineRenderer[] lrs = child.GetComponentsInChildren<LineRenderer>(true);
            foreach (var lr in lrs)
            {
                if (lr != null) lr.widthMultiplier *= boss.rotatingBeamThickness;
            }

            ParticleSystem[] pss = child.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in pss)
            {
                if (ps != null)
                {
                    var main = ps.main;
                    main.startSizeMultiplier *= boss.rotatingBeamThickness;
                }
            }
        }

        // 静止警告ラインを4方向に生成
        warningPivot = new GameObject("HosaBeamWarningPivot");
        warningPivot.transform.position = boss.transform.position;

        float lineLength = 30f;
        foreach (Vector3 dir in laserDirs)
        {
            if (boss.dashWarningSprite != null)
            {
                GameObject lineObj = new GameObject("HosaBeamWarningLine");
                lineObj.transform.SetParent(warningPivot.transform);

                lineObj.transform.position = boss.transform.position + dir * (lineLength * 0.5f);
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                lineObj.transform.rotation = Quaternion.Euler(0f, 0f, angle);

                SpriteRenderer sr = lineObj.AddComponent<SpriteRenderer>();
                sr.sprite = boss.dashWarningSprite;
                sr.color = boss.dashWarningColor;
                sr.sortingOrder = -1;

                if (boss.dashWarningMaterial != null) sr.material = boss.dashWarningMaterial;

                float spriteWidth = sr.sprite.bounds.size.x;
                float spriteHeight = sr.sprite.bounds.size.y;

                if (spriteWidth > 0.0001f && spriteHeight > 0.0001f)
                {
                    lineObj.transform.localScale = new Vector3(lineLength / spriteWidth, (0.8f * boss.rotatingBeamThickness) / spriteHeight, 1f);
                }
                warningLines.Add(lineObj);
            }
        }

        SoundManager.Instance.PlaySE(SeType.EnemyCharge);

        float warningTimer = 0f;
        while (warningTimer < warningDuration)
        {
            warningTimer += Time.deltaTime;
            float alpha = boss.dashWarningColor.a * (0.6f + Mathf.Sin(Time.time * 30f) * 0.4f);
            foreach (var line in warningLines)
            {
                if (line != null)
                {
                    SpriteRenderer sr = line.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.color = new Color(boss.dashWarningColor.r, boss.dashWarningColor.g, boss.dashWarningColor.b, alpha);
                }
            }
            yield return null;
        }

        CleanUpWarnings();

        // ===================================================================
        // ⚡【本番ビーム発射フェーズ】
        // ===================================================================
        ShakeTarget.Instance.Shake(finalAttackDuration, 1.2f);
        SoundManager.Instance.PlaySE(SeType.EnemyRangeAttack);

        ParticleSystem[] childPSs = laserPivot.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in childPSs)
        {
            if (ps != null) ps.Play();
        }

        float attackTimer = 0f;
        float beamRotateSpeed = 65f;

        while (attackTimer < finalAttackDuration)
        {
            attackTimer += Time.deltaTime;
            laserPivot.transform.Rotate(0f, 0f, beamRotateSpeed * boss.attackSpeedMultiplier * Time.deltaTime);
            yield return null;
        }

        CleanUpLasers();
        boss.TransitionToState(boss.StateIdle);
    }

    private void CleanUpWarnings()
    {
        foreach (var line in warningLines) if (line != null) Object.Destroy(line);
        warningLines.Clear();
        if (warningPivot != null) { Object.Destroy(warningPivot); warningPivot = null; }
    }

    private void CleanUpLasers()
    {
        if (laserPivot != null) { Object.Destroy(laserPivot); laserPivot = null; }
    }

    public override void Exit()
    {
        CleanUpWarnings();
        CleanUpLasers();
        boss.StopAllCoroutines();
    }
}