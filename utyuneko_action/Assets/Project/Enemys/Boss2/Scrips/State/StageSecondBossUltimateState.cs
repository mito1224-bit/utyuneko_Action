using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class StageSecondBossUltimateState : StageSecondBossBaseState
{
    private float ultTimer = 0f;

    private Transform rangeVisual;
    private MeshRenderer rangeRenderer;
    private Material rangeMaterial;
    private Mesh rangeMesh;

    // 🎨 スクリプト側で管理しているウルトの色（ここが100%優先されます！）
    private Color coreColor = new Color(0.15f, 0.0f, 0.3f, 0.45f);
    private float swirlSpeed = 120f;

    private Vector3 originalLocalScale = Vector3.one;
    private float pulsePhase = 0f;

    private float maxScaleMultiplier = 1.3f;
    private float pulseAmplitude = 0.15f;
    private float minPulseSpeed = 6f;
    private float maxPulseSpeed = 30f;

    private bool isExploded = false;
    private float postExplosionTimer = 0f;
    private float postExplosionDuration = 0.2f;
    private Vector3 explodedScale = Vector3.one;

    private bool isHoverMoving = true;

    public bool isCounterAcceptable { get; private set; } = false;

    private List<SpriteRenderer> affectedSprites = new List<SpriteRenderer>();
    private List<Material> savedSpriteMaterials = new List<Material>();
    private List<SkinnedMeshRenderer> affectedSkinneds = new List<SkinnedMeshRenderer>();
    private List<Material> savedSkinMaterials = new List<Material>();
    private List<MeshRenderer> affectedMeshes = new List<MeshRenderer>();
    private List<Material> savedMeshMaterials = new List<Material>();

    private Material ultRedMaterial;
    private bool isSetupCompleted = false;

    private float blinkTimer = 0f;
    private bool isFlashOn = false;
    private float startFlashInterval = 0.35f;
    private float endFlashInterval = 0.05f;
    private float ultMulDuration = 0f;

    public StageSecondBossUltimateState(StageSecondBossController boss) : base(boss) { }

    public override void Enter()
    {
        ultTimer = 0f;
        pulsePhase = 0f;
        isExploded = false;
        postExplosionTimer = 0f;
        isHoverMoving = true;
        isSetupCompleted = false;
        blinkTimer = 0f;
        isFlashOn = false;
        isCounterAcceptable = false;

        ultMulDuration = boss.ultDuration / boss.attackSpeedMultiplier;

        Debug.Log("<color=red>⚠️ ボス：ウルト発動！中央下部へ高速ホバーワープ！</color>");

        boss.ResetUltTriggers();

        if (boss.bossAnimator != null) boss.bossAnimator.speed = 0f;
        if (boss.ultVisualOffsetObject != null) originalLocalScale = boss.ultVisualOffsetObject.localScale;
        else originalLocalScale = Vector3.one;

        if (boss.ultDamageAreaObject != null) boss.ultDamageAreaObject.SetActive(false);
        if (boss.ultIndicatorRoot != null) boss.ultIndicatorRoot.SetActive(false);

        float centerX = (boss.stageMinX + boss.stageMaxX) / 2f;
        float targetY = boss.stageMinY + 5.0f;
        Vector3 targetPos = new Vector3(centerX, targetY, boss.transform.position.z);

        boss.StartCoroutine(UltimateSequenceRoutine(targetPos));
    }

    private IEnumerator UltimateSequenceRoutine(Vector3 targetPos)
    {
        yield return boss.StartCoroutine(boss.HoverMoveRoutine(targetPos, 0.35f));

        isHoverMoving = false;
        isCounterAcceptable = true;

        SoundManager.Instance.PlayLoopSE(boss.gameObject, SeType.EnemySuction);

        if (boss.ultIndicatorRoot != null)
        {
            boss.ultIndicatorRoot.transform.localScale = new Vector3(boss.ultExplosionRadius * 2f, boss.ultExplosionRadius * 2f, 1f);
            boss.ultIndicatorRoot.SetActive(true);
        }
        if (boss.ultRedCircleTransform != null) boss.ultRedCircleTransform.localScale = Vector3.zero;

        Shader guiTextShader = Shader.Find("GUI/Text Shader");
        ultRedMaterial = new Material(guiTextShader != null ? guiTextShader : Shader.Find("Sprites/Default")) { color = new Color(1f, 0.15f, 0.15f, 1f) };

        Transform visualRoot = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;

        affectedSprites.Clear(); savedSpriteMaterials.Clear();
        foreach (var sr in visualRoot.GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr != null) { affectedSprites.Add(sr); savedSpriteMaterials.Add(sr.sharedMaterial); }
        }
        affectedSkinneds.Clear(); savedSkinMaterials.Clear();
        foreach (var smr in visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (smr != null) { affectedSkinneds.Add(smr); savedSkinMaterials.Add(smr.sharedMaterial); }
        }
        affectedMeshes.Clear(); savedMeshMaterials.Clear();
        foreach (var mr in visualRoot.GetComponentsInChildren<MeshRenderer>())
        {
            if (mr != null) { affectedMeshes.Add(mr); savedMeshMaterials.Add(mr.sharedMaterial); }
        }

        isSetupCompleted = true;
        CreateRangeVisual();
    }

    public override void Update()
    {
        if (isExploded)
        {
            postExplosionTimer += Time.deltaTime;
            float t = Mathf.Clamp01(postExplosionTimer / postExplosionDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (boss.ultVisualOffsetObject != null) boss.ultVisualOffsetObject.localScale = Vector3.Lerp(explodedScale, originalLocalScale, smoothT);
            if (postExplosionTimer >= postExplosionDuration)
            {
                boss.ResetBarrier();
                boss.TransitionToState(boss.StateIdle);
            }
            return;
        }

        if (isHoverMoving || !isSetupCompleted) return;

        UpdateRangeVisual();

        float progress = Mathf.Clamp01(ultTimer / ultMulDuration);
        float currentPulseSpeed = Mathf.Lerp(minPulseSpeed, maxPulseSpeed, progress);
        pulsePhase += Time.deltaTime * currentPulseSpeed;
        float sineWave = Mathf.Sin(pulsePhase);

        float currentBaseScale = Mathf.Lerp(1f, maxScaleMultiplier, progress);
        float finalScaleFactor = currentBaseScale + (sineWave * pulseAmplitude);

        if (boss.ultVisualOffsetObject != null) boss.ultVisualOffsetObject.localScale = originalLocalScale * finalScaleFactor;

        float currentInterval = Mathf.Lerp(startFlashInterval, endFlashInterval, progress);

        blinkTimer += Time.deltaTime;
        if (blinkTimer >= currentInterval)
        {
            blinkTimer = 0f;
            isFlashOn = !isFlashOn;
            ApplyUltimateFlash(isFlashOn);
        }
    }

    private void ApplyUltimateFlash(bool on)
    {
        if (!isSetupCompleted) return;

        if (on && ultRedMaterial != null)
        {
            foreach (var sr in affectedSprites) if (sr != null) sr.sharedMaterial = ultRedMaterial;
            foreach (var smr in affectedSkinneds) if (smr != null) smr.sharedMaterial = ultRedMaterial;
            foreach (var mr in affectedMeshes) if (mr != null) mr.sharedMaterial = ultRedMaterial;
        }
        else
        {
            for (int i = 0; i < affectedSprites.Count; i++)
            {
                if (affectedSprites[i] != null && i < savedSpriteMaterials.Count)
                {
                    affectedSprites[i].sharedMaterial = savedSpriteMaterials[i];
                    affectedSprites[i].color = Color.white;
                }
            }
            for (int i = 0; i < affectedSkinneds.Count; i++) if (affectedSkinneds[i] != null && i < savedSkinMaterials.Count) affectedSkinneds[i].sharedMaterial = savedSkinMaterials[i];
            for (int i = 0; i < affectedMeshes.Count; i++) if (affectedMeshes[i] != null && i < savedMeshMaterials.Count) affectedMeshes[i].sharedMaterial = savedMeshMaterials[i];
        }
    }

    public override void FixedUpdate()
    {
        if (isExploded || isHoverMoving) return;

        ultTimer += Time.fixedDeltaTime;
        float progress = Mathf.Clamp01(ultTimer / ultMulDuration);
        if (boss.ultRedCircleTransform != null) boss.ultRedCircleTransform.localScale = new Vector3(progress, progress, 1f);

        Transform player = boss.GetPlayerTransform();
        Rigidbody2D playerRb = boss.GetPlayerRigidbody();

        if (player != null && playerRb != null)
        {
            Vector2 toCenter = (Vector2)boss.transform.position - playerRb.position;
            float dist = toCenter.magnitude;

            if (dist < boss.ultPullRadius && dist > 0.0001f)
            {
                Vector2 dir = toCenter / dist;
                dir.y = 0f;
                if (dir.magnitude > 0.0001f) dir.Normalize();

                float force = boss.ultPullForce;
                if (player.TryGetComponent<PlayerController>(out var playerController))
                {
                    if (playerController.CurrentState == playerController.StateBurst || playerController.CurrentState == playerController.StateCharge) force *= boss.ultBurstPullMultiplier;
                }

                float approachSpeed = Vector2.Dot(playerRb.linearVelocity, dir);
                if (approachSpeed < boss.ultMaxApproachSpeed && dir != Vector2.zero) playerRb.AddForce(dir * force * playerRb.mass, ForceMode2D.Force);
            }
        }

        StageSecondBossTimedBomb[] timedBombs = Object.FindObjectsByType<StageSecondBossTimedBomb>(FindObjectsSortMode.None);
        foreach (var bomb in timedBombs)
        {
            if (bomb != null && bomb.IsBlownAway)
            {
                if (bomb.TryGetComponent<Rigidbody2D>(out var bombRb))
                {
                    if (bomb.BlownAwayTimer < 0.05f) { bomb.transform.Rotate(Vector3.forward, 450f * Time.fixedDeltaTime); continue; }
                    Vector2 toCenter = (Vector2)boss.transform.position - bombRb.position;
                    float dist = toCenter.magnitude;
                    if (dist < boss.ultPullRadius && dist > 0.0001f)
                    {
                        Vector2 dir = toCenter / dist;
                        float targetSpeed = bomb.blowSpeed + (boss.ultPullForce * (bomb.BlownAwayTimer - 0.25f) * Time.fixedDeltaTime);
                        bombRb.linearVelocity = Vector2.Lerp(bombRb.linearVelocity, dir * targetSpeed, 8.0f * Time.fixedDeltaTime);
                    }
                }
            }
        }

        StageSecondBossMineBomb[] mineBombs = Object.FindObjectsByType<StageSecondBossMineBomb>(FindObjectsSortMode.None);
        foreach (var bomb in mineBombs)
        {
            if (bomb != null && bomb.IsBlownAway)
            {
                if (bomb.TryGetComponent<Rigidbody2D>(out var bombRb))
                {
                    if (bomb.BlownAwayTimer < 0.05f) { bomb.transform.Rotate(Vector3.forward, 450f * Time.fixedDeltaTime); continue; }
                    Vector2 toCenter = (Vector2)boss.transform.position - bombRb.position;
                    float dist = toCenter.magnitude;
                    if (dist < boss.ultPullRadius && dist > 0.0001f)
                    {
                        Vector2 dir = toCenter / dist;
                        float targetSpeed = bomb.blowSpeed + (boss.ultPullForce * (bomb.BlownAwayTimer - 0.25f) * Time.fixedDeltaTime);
                        bombRb.linearVelocity = Vector2.Lerp(bombRb.linearVelocity, dir * targetSpeed, 8.0f * Time.fixedDeltaTime);
                    }
                }
            }
        }

        if (ultTimer >= ultMulDuration) ExecuteBigExplosion();
    }

    private void ExecuteBigExplosion()
    {
        isExploded = true;
        if (boss.ultVisualOffsetObject != null) explodedScale = boss.ultVisualOffsetObject.localScale;

        SoundManager.Instance.StopLoopSE(boss.gameObject);
        SoundManager.Instance.PlaySE(SeType.EnemyExplosion);

        ShakeTarget.Instance.Shake(0.8f, 2.0f);

        ApplyUltimateFlash(false);

        if (boss.ultExplosionEffect != null)
        {
            GameObject fxObj = Object.Instantiate(boss.ultExplosionEffect, boss.transform.position, Quaternion.identity);
            float diameter = boss.ultExplosionRadius * 2f * boss.ultExplosionEffectScaleMultiplier;
            fxObj.transform.localScale = new Vector3(diameter, diameter, 1f);
        }

        if (boss.ultDamageAreaObject != null)
        {
            GameObject explosionArea = Object.Instantiate(boss.ultDamageAreaObject, boss.transform.position, Quaternion.identity);
            explosionArea.transform.SetParent(null);
            boss.SyncColliderSize(explosionArea, boss.ultExplosionRadius);
            explosionArea.SetActive(true);
            Object.Destroy(explosionArea, 0.2f);
            boss.ultDamageAreaObject.SetActive(false);
        }

        StageSecondBossTimedBomb[] timedBombs = Object.FindObjectsByType<StageSecondBossTimedBomb>(FindObjectsSortMode.None);
        foreach (var bomb in timedBombs)
        {
            if (bomb != null) Object.Destroy(bomb.gameObject);
        }

        StageSecondBossMineBomb[] mineBombs = Object.FindObjectsByType<StageSecondBossMineBomb>(FindObjectsSortMode.None);
        foreach (var bomb in mineBombs)
        {
            if (bomb != null) Object.Destroy(bomb.gameObject);
        }

        CleanUpVisuals();
    }

    public override void Exit() { isCounterAcceptable = false; SoundManager.Instance.StopLoopSE(boss.gameObject); ResetBossVisual(); CleanUpVisuals(); boss.ForceResetAllMaterials(); }
    private void ResetBossVisual() { if (boss.bossAnimator != null) boss.bossAnimator.speed = 1f; if (boss.ultVisualOffsetObject != null) boss.ultVisualOffsetObject.localScale = originalLocalScale; ApplyUltimateFlash(false); if (ultRedMaterial != null) { Object.Destroy(ultRedMaterial); ultRedMaterial = null; } }

    private void CreateRangeVisual()
    {
        GameObject go = new GameObject("UltimateBlackHoleRange(動的生成)");
        rangeVisual = go.transform;
        rangeVisual.SetParent(null);
        rangeVisual.position = boss.transform.position;
        MeshFilter mf = go.AddComponent<MeshFilter>();
        rangeMesh = BuildDiscMesh(48);
        mf.sharedMesh = rangeMesh;
        rangeRenderer = go.AddComponent<MeshRenderer>();
        rangeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rangeRenderer.receiveShadows = false;
        rangeRenderer.sortingOrder = -2;

        if (boss.instantLineWarningMaterial != null)
        {
            rangeMaterial = new Material(boss.instantLineWarningMaterial);
        }
        else
        {
            rangeMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        rangeMaterial.renderQueue = 3000;
        rangeRenderer.material = rangeMaterial;
    }

    private Mesh BuildDiscMesh(int segments) { Mesh mesh = new Mesh { name = "UltimateDiscMesh" }; Vector3[] verts = new Vector3[segments + 1]; Color[] cols = new Color[segments + 1]; verts[0] = Vector3.zero; cols[0] = Color.white; for (int i = 0; i < segments; i++) { float a = (i / (float)segments) * Mathf.PI * 2f; verts[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f); cols[i + 1] = new Color(1f, 1f, 1f, 0.2f); } int[] tris = new int[segments * 3]; for (int i = 0; i < segments; i++) { tris[i * 3] = 0; tris[i * 3 + 1] = i + 1; tris[i * 3 + 2] = (i + 1) % segments + 1; } mesh.vertices = verts; mesh.colors = cols; mesh.triangles = tris; mesh.RecalculateBounds(); return mesh; }

    private void UpdateRangeVisual()
    {
        if (rangeVisual == null) return;
        rangeVisual.position = boss.transform.position;
        rangeVisual.localScale = Vector3.one * boss.ultPullRadius;
        if (swirlSpeed != 0f) rangeVisual.Rotate(0f, 0f, swirlSpeed * Time.deltaTime, Space.Self);

        // ===================================================================
        // 🛠️【大修正：常にスクリプトの色（coreColor）を最優先で適用】
        // ===================================================================
        // 💡 条件分岐を完全撤廃！
        // 自作シェーダーが当たっていようが関係なく、毎フレーム coreColor を
        // マテリアルに流し込むことで、シェーダーは「模様」として機能しつつ、
        // 色味はスクリプトの紫色が綺麗に乗算・優先されるようになります。
        if (rangeMaterial != null)
        {
            rangeMaterial.color = coreColor;
        }
    }

    private void CleanUpVisuals() { if (boss.ultIndicatorRoot != null) boss.ultIndicatorRoot.SetActive(false); if (rangeVisual != null) Object.Destroy(rangeVisual.gameObject); if (rangeMaterial != null) Object.Destroy(rangeMaterial); if (rangeMesh != null) Object.Destroy(rangeMesh); }
}