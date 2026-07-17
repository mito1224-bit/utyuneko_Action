using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 👑 【第2形態専用コンボ③】能力強奪ハッキングシーケンス
/// どの能力を奪った場合でも、必ず一度ステージ中心へワープしてから攻撃を開始するように統一！
/// さらに反射（跳弾）の移動スピードを大幅に強化した超スリリング版！
/// </summary>
public class HosaP2_HackingStealState : GlitchHosaBaseState
{
    private float survivalTimer = 0f;
    private float targetSurvivalDuration = 6.0f;
    private int stolenAbilityType = 0;

    private GameObject laserPivot;
    private Coroutine attackCoroutine;

    private Vector2 reflectVelocity;
    private bool isReflectInitialized = false;

    // 👑【スピード強化】ベースの跳弾速度を 35f から「50f」へ大幅に引き上げ！
    private float reflectSpeed = 50f;

    private GameObject spawnedAbsorbParticle;
    private List<GameObject> activeWarningVisuals = new List<GameObject>();

    public HosaP2_HackingStealState(GlitchHosaController boss) : base(boss) { }

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
        survivalTimer = 0f;
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

        // 1. プレイヤーの頭上に張り付いて能力をハッキングする威嚇演出
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

        // 奪う能力をランダムに決定 (0: 予測線, 1: 反射, 2: スロー)
        stolenAbilityType = Random.Range(0, 3);

        // ===================================================================
        // 👑【仕様変更のキモ】反射の場合も含め、すべての能力で共通してステージの中心へ移動！
        // ===================================================================
        float centerX = (boss.stageMinX + boss.stageMaxX) / 2f;
        float centerY = (boss.stageMinY + boss.stageMaxY) / 2f;
        Vector3 centerPos = new Vector3(centerX, centerY, 0f);

        // 中心へ華麗にスクワッシュワープ
        yield return boss.StartCoroutine(boss.TeleportWithSquashRoutine(centerPos, 0.2f));

        // 物理挙動を一旦安全に初期化
        Rigidbody2D rb = boss.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }

        // ===================================================================
        // 👑【位置修正】中心に到着完了した『後』に、各能力ごとのセットアップを実行！
        // ===================================================================
        if (stolenAbilityType == 1)
        {
            // 反射能力強奪の確定
            boss.isReflectionStolen = true;
            boss.SetBarrierActive(true, force: true);

            // 斜め4方向のいずれかランダムな角度へ、超高速の初速ベクトルを計算
            float[] baseAngles = { 45f, 135f, 225f, 315f };
            float angleRad = (baseAngles[Random.Range(0, baseAngles.Length)] + Random.Range(-12f, 12f)) * Mathf.Deg2Rad;

            // 👑 ベース速度(50f)に攻撃速度倍率を乗算し、第3形態では最大75fまでマッハ加速！
            float finalSpeed = reflectSpeed * boss.attackSpeedMultiplier;
            reflectVelocity = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)).normalized * finalSpeed;

            // 準備完了フラグを立ててFixedUpdate側で跳ね返り運動を開始
            isReflectInitialized = true;
        }
        else
        {
            if (stolenAbilityType == 0) boss.isLineStolen = true;
            else boss.isSlowStolen = true;
        }

        // 実際の攻撃シーケンスループを開始
        boss.StartCoroutine(ExecuteStealAttackSequence());
    }

    private IEnumerator ExecuteStealAttackSequence()
    {
        while (survivalTimer < targetSurvivalDuration)
        {
            if (stolenAbilityType == 2)
            {
                // スロー強奪（大回転ビーム）
                if (laserPivot == null)
                {
                    laserPivot = new GameObject("HosaStolenLaserPivot");
                    laserPivot.transform.position = boss.transform.position;

                    float[] angles = { 0f, 90f, 180f, 270f };
                    for (int i = 0; i < angles.Length; i++)
                    {
                        Vector3 dir = Quaternion.Euler(0f, 0f, angles[i]) * Vector3.right;
                        BarrierManager.Instance.SpawnLaser(boss.transform.position, boss.transform.position + dir * 35f, targetSurvivalDuration, laserPivot.transform);
                    }

                    ParticleSystem[] pss = laserPivot.GetComponentsInChildren<ParticleSystem>(true);
                    foreach (var ps in pss) if (ps != null) ps.Play();

                    foreach (Transform child in laserPivot.transform)
                    {
                        child.localScale *= boss.rotatingBeamThickness;
                        LineRenderer[] lrs = child.GetComponentsInChildren<LineRenderer>(true);
                        foreach (var lr in lrs) if (lr != null) lr.widthMultiplier *= boss.rotatingBeamThickness;
                    }
                    SoundManager.Instance.PlaySE(SeType.EnemyRangeAttack);
                }

                if (laserPivot != null)
                {
                    laserPivot.transform.Rotate(Vector3.forward, 60f * boss.attackSpeedMultiplier * Time.deltaTime);
                }
            }
            else if (stolenAbilityType == 0)
            {
                // 予測線強奪（正確な予測円付き爆弾トス）
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
            sr.color = boss.dashWarningColor;
            sr.sortingOrder = -1;
            if (boss.dashWarningMaterial != null) sr.material = boss.dashWarningMaterial;

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

        // ===================================================================
        // 👑【反射移動】センターから超マッハスピードで跳ね返り運動を開始！
        // ===================================================================
        if (stolenAbilityType == 1 && isReflectInitialized)
        {
            int groundMask = LayerMask.GetMask("Ground");
            float castRadius = 1.0f; // ボスの衝突球半径
            Vector2 moveDir = reflectVelocity.normalized;
            float castDistance = reflectVelocity.magnitude * Time.fixedDeltaTime + 0.05f;

            RaycastHit2D hit = Physics2D.CircleCast((Vector2)boss.transform.position, castRadius, moveDir, castDistance, groundMask);

            if (hit.collider != null)
            {
                // 壁に接触したら美しく物理跳弾反射！
                reflectVelocity = Vector2.Reflect(reflectVelocity, hit.normal);
                SoundManager.Instance.PlaySE(SeType.PlayerWallHit);
                ShakeTarget.Instance.Shake(0.15f, 2.0f);

                // めり込み防止の座標補正
                boss.transform.position = (Vector3)(hit.point + hit.normal * (castRadius + 0.02f)) + Vector3.forward * boss.transform.position.z;
            }
            else
            {
                // 障害物がなければ直線移動
                boss.transform.position += (Vector3)(reflectVelocity * Time.fixedDeltaTime);
            }
            boss.SetFacingDirection(reflectVelocity.x);
        }
    }

    private void CleanUpAllObjects()
    {
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