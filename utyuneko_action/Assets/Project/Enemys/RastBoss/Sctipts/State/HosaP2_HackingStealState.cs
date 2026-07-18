using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 👑 【第2形態専用コンボ③】能力強奪ハッキングシーケンス
/// 👑【全フェーズ・回転速度統一版】
/// 第3形態でビームが爆速回転するのを防ぐため、攻撃速度倍率の乗算を外して第2形態と同じ一定速度に固定しました！
/// </summary>
public class HosaP2_HackingStealState : GlitchHosaBaseState
{
    private float survivalTimer = 0f;
    private float targetSurvivalDuration = 6.0f;
    private int stolenAbilityType = 0;

    private List<GameObject> warningLines = new List<GameObject>();
    private GameObject warningPivot;
    private GameObject laserPivot;
    private Coroutine attackCoroutine;

    private Vector2 reflectVelocity;
    private bool isReflectInitialized = false;
    private float reflectSpeed = 50f;

    private GameObject spawnedAbsorbParticle;
    private List<GameObject> activeWarningVisuals = new List<GameObject>();

    public HosaP2_HackingStealState(GlitchHosaController boss) : base(boss) { }

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
        survivalTimer = 0f;
        warningLines.Clear();
        warningPivot = null;
        laserPivot = null;
        isReflectInitialized = false;
        spawnedAbsorbParticle = null;
        activeWarningVisuals.Clear();

        boss.isLineStolen = false;
        boss.isReflectionStolen = false;
        boss.isSlowStolen = false;

        attackCoroutine = boss.StartCoroutine(ExecuteHackingIntroductionSequence());
    }

    private IEnumerator ExecuteHackingIntroductionSequence()
    {
        Transform player = boss.GetPlayerTransform();
        if (player == null) { boss.TransitionToState(boss.StateIdle); yield break; }

        float headOffsetY = 2.8f;
        yield return boss.StartCoroutine(boss.TeleportWithSquashRoutine(player.position + new Vector3(0f, headOffsetY, 0f), 0.2f));

        float introTimer = 0f;
        while (introTimer < 1.0f)
        {
            introTimer += Time.deltaTime;
            Vector3 targetHeadPos = player.position + new Vector3(0f, headOffsetY, 0f);
            boss.transform.position = Vector3.Lerp(boss.transform.position, targetHeadPos, Time.deltaTime * 10f);
            boss.targetVisualOffset = new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(-0.05f, 0.05f), 0f);
            yield return null;
        }
        boss.targetVisualOffset = Vector3.zero;

        stolenAbilityType = Random.Range(0, 3);

        float centerX = (boss.stageMinX + boss.stageMaxX) / 2f;
        float centerY = (boss.stageMinY + boss.stageMaxY) / 2f;
        Vector3 centerPos = new Vector3(centerX, centerY, 0f);

        yield return boss.StartCoroutine(boss.TeleportWithSquashRoutine(centerPos, 0.2f));

        Rigidbody2D rb = boss.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.bodyType = RigidbodyType2D.Kinematic; rb.linearVelocity = Vector2.zero; }

        if (stolenAbilityType == 1)
        {
            float[] baseAngles = { 45f, 135f, 225f, 315f };
            float angleRad = (baseAngles[Random.Range(0, baseAngles.Length)] + Random.Range(-12f, 12f)) * Mathf.Deg2Rad;

            float finalSpeed = reflectSpeed * boss.attackSpeedMultiplier;
            reflectVelocity = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)).normalized * finalSpeed;

            isReflectInitialized = true;
            boss.isReflectionStolen = true;
            boss.SetBarrierActive(true, force: true);
        }
        else
        {
            if (stolenAbilityType == 0) boss.isLineStolen = true;
            else boss.isSlowStolen = true;
        }

        boss.StartCoroutine(ExecuteStealAttackSequence());
    }

    private IEnumerator ExecuteStealAttackSequence()
    {
        while (survivalTimer < targetSurvivalDuration)
        {
            if (stolenAbilityType == 2)
            {
                if (laserPivot == null)
                {
                    laserPivot = new GameObject("HosaStolenLaserPivot");
                    laserPivot.transform.position = boss.transform.position;
                    laserPivot.transform.rotation = Quaternion.identity;

                    float warningDuration = 1.0f / boss.attackSpeedMultiplier;
                    float totalLaserLifeTime = targetSurvivalDuration;

                    float[] angles = { 0f, 90f, 180f, 270f };
                    for (int i = 0; i < angles.Length; i++)
                    {
                        Vector3 dir = Quaternion.Euler(0f, 0f, angles[i]) * Vector3.right;
                        BarrierManager.Instance.SpawnLaser(boss.transform.position, boss.transform.position + dir * 35f, totalLaserLifeTime, laserPivot.transform);
                    }

                    foreach (Transform child in laserPivot.transform)
                    {
                        Vector3 localScale = child.localScale;
                        localScale.x *= boss.rotatingBeamThickness;
                        localScale.y *= boss.rotatingBeamThickness;
                        child.localScale = localScale;

                        LineRenderer[] lrs = child.GetComponentsInChildren<LineRenderer>(true);
                        foreach (var lr in lrs) if (lr != null) lr.widthMultiplier *= boss.rotatingBeamThickness;

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

                    warningPivot = new GameObject("HosaStolenWarningPivot");
                    warningPivot.transform.position = boss.transform.position;

                    float lineLength = 35f;
                    for (int i = 0; i < angles.Length; i++)
                    {
                        if (boss.dashWarningSprite != null)
                        {
                            GameObject lineObj = new GameObject("HosaStolenWarningLine");
                            lineObj.transform.SetParent(warningPivot.transform);

                            Vector3 dir = Quaternion.Euler(0f, 0f, angles[i]) * Vector3.right;
                            lineObj.transform.position = boss.transform.position + dir * (lineLength * 0.5f);
                            lineObj.transform.rotation = Quaternion.Euler(0f, 0f, angles[i]);

                            SpriteRenderer sr = lineObj.AddComponent<SpriteRenderer>();
                            sr.sprite = boss.dashWarningSprite;
                            sr.color = boss.dashWarningColor;
                            sr.sortingOrder = -1;

                            if (boss.dashWarningMaterial != null)
                            {
                                sr.material = new Material(boss.dashWarningMaterial);
                                if (sr.material.HasProperty("_Color")) sr.material.SetColor("_Color", boss.dashWarningColor);
                                if (sr.material.HasProperty("_TintColor")) sr.material.SetColor("_TintColor", boss.dashWarningColor);
                                if (sr.material.HasProperty("_ColorTint")) sr.material.SetColor("_ColorTint", boss.dashWarningColor);
                            }

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
                    while (warningTimer < warningDuration && survivalTimer < targetSurvivalDuration)
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

                    SoundManager.Instance.PlaySE(SeType.EnemyRangeAttack);
                    ParticleSystem[] childPSs = laserPivot.GetComponentsInChildren<ParticleSystem>(true);
                    foreach (var ps in childPSs) if (ps != null) ps.Play();
                }

                // 👑【修正】回転速度を第2形態固定（65f）にし、フェーズごとの倍率補正を無効化！
                float beamRotateSpeed = 65f;
                if (laserPivot != null)
                {
                    laserPivot.transform.Rotate(0f, 0f, beamRotateSpeed * Time.deltaTime);
                }
            }
            else if (stolenAbilityType == 0)
            {
                Transform player = boss.GetPlayerTransform();
                if (player != null)
                {
                    Vector3 pPos = player.position;
                    Rigidbody2D playerRb = boss.GetPlayerRigidbody();
                    float leadTime = 0.45f;
                    Vector3 targetPos = new Vector3(
                        Mathf.Clamp(pPos.x + (playerRb != null ? playerRb.linearVelocity.x * leadTime : 0f), boss.stageMinX + 2f, boss.stageMaxX - 2f),
                        Mathf.Clamp(pPos.y + (playerRb != null ? playerRb.linearVelocity.y * leadTime : 0f), boss.stageMinY + 1.2f, boss.stageMaxY - 1.2f),
                        0f
                    );

                    boss.StartCoroutine(SpawnHackingBombSequence(boss.transform.position, targetPos));
                }
                yield return new WaitForSeconds(0.5f / boss.attackSpeedMultiplier);
            }
            yield return null;
        }
    }

    private IEnumerator SpawnHackingBombSequence(Vector3 launchPos, Vector3 targetPos)
    {
        if (boss.timedBombPrefab == null) yield break;

        GameObject warningObj = null;
        float dynamicDiameter = GetBombExplosionDiameter();

        if (boss.dashWarningSprite != null)
        {
            warningObj = new GameObject("HosaHackingBombWarning");
            warningObj.transform.position = targetPos;

            SpriteRenderer sr = warningObj.AddComponent<SpriteRenderer>();
            sr.sprite = boss.dashWarningSprite;

            if (boss.dashWarningMaterial != null)
            {
                sr.material = new Material(boss.dashWarningMaterial);
                if (sr.material.HasProperty("_Color")) sr.material.SetColor("_Color", boss.dashWarningColor);
                if (sr.material.HasProperty("_TintColor")) sr.material.SetColor("_TintColor", boss.dashWarningColor);
                if (sr.material.HasProperty("_ColorTint")) sr.material.SetColor("_ColorTint", boss.dashWarningColor);
            }

            sr.color = boss.dashWarningColor;
            sr.sortingOrder = -1;

            float w = sr.sprite.bounds.size.x;
            float h = sr.sprite.bounds.size.y;
            if (w > 0.0001f && h > 0.0001f)
            {
                warningObj.transform.localScale = new Vector3(dynamicDiameter / w, dynamicDiameter / h, 1f);
            }
            activeWarningVisuals.Add(warningObj);
        }

        yield return new WaitForSeconds(0.3f);

        if (warningObj != null)
        {
            activeWarningVisuals.Remove(warningObj);
            Object.Destroy(warningObj);
        }

        SoundManager.Instance.PlaySE(SeType.EnemyRangeAttack);
        GameObject bombObj = Object.Instantiate(boss.timedBombPrefab, launchPos, Quaternion.identity);
        var bomb = bombObj.GetComponent<GlitchHosaTimedBomb>();
        if (bomb != null)
        {
            bomb.explodeOnLand = true;
            bomb.InitializeToss(launchPos, targetPos, 0.38f, 2.0f);
        }
    }

    public override void Update()
    {
        survivalTimer += Time.deltaTime;
        if (survivalTimer >= targetSurvivalDuration)
        {
            CleanUpAllObjects();
            boss.StateStun.startAsGrounded = true;
            boss.TransitionToState(boss.StateStun);
        }
    }

    public override void FixedUpdate()
    {
        if (survivalTimer >= targetSurvivalDuration) return;

        if (stolenAbilityType == 1 && isReflectInitialized)
        {
            float castRadius = 1.0f;
            Vector2 moveDir = reflectVelocity.normalized;
            float castDistance = reflectVelocity.magnitude * Time.fixedDeltaTime + 0.05f;

            RaycastHit2D[] hits = Physics2D.CircleCastAll((Vector2)boss.transform.position, castRadius, moveDir, castDistance);
            RaycastHit2D validHit = new RaycastHit2D();
            bool hasHitValidWall = false;

            foreach (var hit in hits)
            {
                if (hit.collider != null)
                {
                    if (hit.collider.gameObject != boss.gameObject &&
                        !hit.collider.gameObject.CompareTag("Player") &&
                        !hit.collider.isTrigger &&
                        hit.distance > 0.01f)
                    {
                        validHit = hit;
                        hasHitValidWall = true;
                        break;
                    }
                }
            }

            if (hasHitValidWall)
            {
                reflectVelocity = Vector2.Reflect(reflectVelocity, validHit.normal);
                SoundManager.Instance.PlaySE(SeType.PlayerWallHit);
                ShakeTarget.Instance.Shake(0.15f, 2.0f);

                boss.transform.position = (Vector3)(validHit.point + validHit.normal * (castRadius + 0.02f)) + Vector3.forward * boss.transform.position.z;
            }
            else
            {
                boss.transform.position += (Vector3)(reflectVelocity * Time.fixedDeltaTime);
            }
            boss.SetFacingDirection(reflectVelocity.x);
        }
    }

    private void CleanUpWarnings()
    {
        foreach (var line in warningLines) if (line != null) Object.Destroy(line);
        warningLines.Clear();
        if (warningPivot != null) { Object.Destroy(warningPivot); warningPivot = null; }

    }

    private void CleanUpAllObjects()
    {
        CleanUpWarnings();
        if (laserPivot != null) { Object.Destroy(laserPivot); laserPivot = null; }

        foreach (var warning in activeWarningVisuals)
        {
            if (warning != null) Object.Destroy(warning);
        }
        activeWarningVisuals.Clear();
    }

    public override void Exit()
    {
        if (attackCoroutine != null) boss.StopCoroutine(attackCoroutine);
        if (spawnedAbsorbParticle != null) { Object.Destroy(spawnedAbsorbParticle); spawnedAbsorbParticle = null; }

        CleanUpAllObjects();

        boss.isLineStolen = false;
        boss.isReflectionStolen = false;
        boss.isSlowStolen = false;
        boss.SetBarrierActive(false);
    }
}