using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 👑 崩壊した補佐：全体を管理するメインブレイン（突進速度安全ロック・白飛び完全復旧版）
/// </summary>
public class GlitchHosaController : MonoBehaviour
{
    private GlitchHosaHealth healthComponent;
    public float currentHP => healthComponent != null ? healthComponent.currentHP : 100f;
    public float maxHP => healthComponent != null ? healthComponent.maxHP : 100f;

    public Vector3 originalVisualLocalPosition { get; private set; }
    public Quaternion originalVisualLocalRotation { get; private set; }
    public Vector3 originalVisualLocalScale { get; private set; }

    private List<RendererData> originalRendererData = new List<RendererData>();

    private struct RendererData
    {
        public SpriteRenderer spriteRenderer;
        public SkinnedMeshRenderer skinnedRenderer;
        public MeshRenderer meshRenderer;
        public Material originalMaterial;
    }

    [Header("😈 崩壊した補佐 - 形態設定")]
    public float phase2Threshold = 0.7f;
    public float phase3Threshold = 0.3f;
    public int hosaCurrentPhase { get; private set; } = 1;

    [Header("🏎️ 突進（ダッシュ）移動速度の安全ロック設定")]
    [Tooltip("突進技の基本移動速度（すり抜け防止のため 18〜22 あたりが2Dアクションの限界値としてオススメです）")]
    public float baseDashSpeed = 20f;
    [Tooltip("第3形態（怒り）の時、突進スピードを何倍にするか。ここを 1.0 にすれば後半もすり抜けを100%防止できます")]
    public float dashSpeedPhase3Multiplier = 1.1f;

    [Header("💀 プレイヤーから強奪した能力フラグ")]
    public bool isLineStolen = false;
    public bool isReflectionStolen = false;
    public bool isSlowStolen = false;

    [Header("⚙️ スタン・攻撃パラメータ")]
    public float stunDamageMultiplier = 3.0f;
    public float attackSpeedMultiplier { get; set; } = 1.0f;
    public float miniStunDuration = 1.2f;
    public float longStunDuration = 3.5f;
    public float stunDuration = 4.0f;
    public float stunRecoveryDuration = 0.6f;
    public float stunPivotOffsetY = 0.6f;
    public float stunGravityAmount = 1.8f;
    public float deadRotationAngle = 90f;

    [Header("✨ 浮遊（ホバー）アニメーション設定")]
    public float hoverSpeed = 2.2f;
    public float hoverAmount = 0.28f;

    [Header("通常攻撃 of 生成用プレハブ")]
    public GameObject timedBombPrefab;
    public Transform tossLaunchPoint;

    [Header("⚡ 激激突・衝撃波（ショックウェーブ）専用設定")]
    public GameObject shockwavePrefab;

    [Header("🎯 各攻撃専用・設定")]
    public float rotatingBeamThickness = 3.5f;
    public float cloneSniperThickness = 1.5f;
    public float rallyZOffset = 15.0f;

    [Header("🤖 グラフィック・境界線の設定")]
    public Transform ultVisualOffsetObject;

    [Header("👑 回転・縮小（押しつぶし）専用オブジェクト設定")]
    public Transform rotationOffsetObject;
    public Transform squashOffsetObject;

    public Animator bossAnimator;
    public Transform topLeftBoundary;
    public Transform bottomRightBoundary;

    [Header("👑 3Dモデル・立体角度カスタマイズ設定")]
    public float defaultXRotation = -15.0f;
    public float defaultYRotation = 0.0f;
    public float dashZTiltAngle = 15.0f;
    public float dashYRotationRight = 310.0f;
    public float dashYRotationLeft = 50.0f;

    public float stageMinX => topLeftBoundary != null ? topLeftBoundary.position.x : -15f;
    public float stageMaxX => bottomRightBoundary != null ? bottomRightBoundary.position.x : 15f;
    public float stageMaxY => topLeftBoundary != null ? topLeftBoundary.position.y : 10f;
    public float stageMinY => bottomRightBoundary != null ? bottomRightBoundary.position.y : 0f;

    [Header("⚙️ 演出用パラメータ")]
    public float afterimageDuration = 0.5f;
    public float afterimageInterval = 0.02f;
    public Color afterimageColor = new Color(1f, 0.15f, 0.15f, 0.65f);

    [Header("🎯 突進・警告インジケーター設定")]
    public Sprite dashWarningSprite;
    public Material dashWarningMaterial;
    public Color dashWarningColor = new Color(1f, 0f, 0f, 0.35f);
    public float dashWarningDuration = 0.6f;

    [HideInInspector] public bool isBackRallyMode = false;
    private bool isPhaseTransitioning = false;
    [HideInInspector] public bool isKnockbacking { get; private set; } = false;

    [HideInInspector] public float targetXRotation;
    [HideInInspector] public float targetYRotation;
    [HideInInspector] public float targetZRotation;
    [HideInInspector] public Vector3 targetScale;
    [HideInInspector] public Vector3 targetVisualOffset;

    public GlitchHosaIdleState StateIdle { get; private set; }
    public HosaP1_BeamState StateP1_Beam { get; private set; }
    public HosaP1_ClonesState StateP1_Clones { get; private set; }
    public HosaP1_WallDashState StateP1_WallDash { get; private set; }
    public HosaP1_BackBombsState StateP1_BackBombs { get; private set; }
    public GlitchHosaStunState StateStun { get; private set; }

    public HosaP2_CloneDashState StateP2_CloneDash { get; private set; }
    public HosaP2_BeamBombsState StateP2_BeamBombs { get; private set; }
    public HosaP2_HackingStealState StateP2_HackingSteal { get; private set; }

    private GlitchHosaBaseState currentState;
    public string currentDebugStateName;
    private Transform playerTransform;
    private Rigidbody2D playerRb2D;

    void Awake()
    {
        StateIdle = new GlitchHosaIdleState(this);
        StateP1_Beam = new HosaP1_BeamState(this);
        StateP1_Clones = new HosaP1_ClonesState(this);
        StateP1_WallDash = new HosaP1_WallDashState(this);
        StateP1_BackBombs = new HosaP1_BackBombsState(this);
        StateStun = new GlitchHosaStunState(this);

        StateP2_CloneDash = new HosaP2_CloneDashState(this);
        StateP2_BeamBombs = new HosaP2_BeamBombsState(this);
        StateP2_HackingSteal = new HosaP2_HackingStealState(this);

        healthComponent = GetComponent<GlitchHosaHealth>();
        if (healthComponent == null) healthComponent = GetComponentInChildren<GlitchHosaHealth>();

        Transform squashTarget = GetSquashTarget();
        Transform rotationTarget = GetRotationTarget();

        originalVisualLocalPosition = squashTarget.localPosition;
        originalVisualLocalRotation = Quaternion.Euler(defaultXRotation, defaultYRotation, 0f);
        originalVisualLocalScale = squashTarget.localScale;

        targetXRotation = defaultXRotation;
        targetYRotation = defaultYRotation;
        targetZRotation = 0f;
        targetScale = originalVisualLocalScale;
        targetVisualOffset = Vector3.zero;
        rotationTarget.localRotation = originalVisualLocalRotation;

        Transform visualRoot = ultVisualOffsetObject != null ? ultVisualOffsetObject : transform;
        originalRendererData.Clear();
        foreach (var sr in visualRoot.GetComponentsInChildren<SpriteRenderer>(true)) if (sr != null) originalRendererData.Add(new RendererData { spriteRenderer = sr, originalMaterial = sr.sharedMaterial });
        foreach (var smr in visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (smr != null) originalRendererData.Add(new RendererData { skinnedRenderer = smr, originalMaterial = smr.sharedMaterial });
        foreach (var mr in visualRoot.GetComponentsInChildren<MeshRenderer>(true)) if (mr != null) originalRendererData.Add(new RendererData { meshRenderer = mr, originalMaterial = mr.sharedMaterial });
    }

    void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerObj.TryGetComponent<Rigidbody2D>(out playerRb2D);
        }
        if (bossAnimator == null) bossAnimator = GetComponentInChildren<Animator>();
        TransitionToState(StateIdle);
    }

    void Update()
    {
        if (currentState != null) currentState.Update();

        if (currentDebugStateName != "GlitchHosaStunState" && !isKnockbacking)
        {
            float hoverY = Mathf.Sin(Time.time * hoverSpeed) * hoverAmount;
            targetVisualOffset = new Vector3(0f, hoverY, 0f);
        }

        float hpRatio = currentHP / maxHP;

        if (hpRatio <= phase3Threshold && hosaCurrentPhase < 3 && !isPhaseTransitioning && !isKnockbacking)
        {
            StartCoroutine(PhaseTransitionSequence(3, 1.5f, "<color=red>🔥 崩壊した補佐：最終形態突入！攻撃速度が1.5倍に超加速！</color>"));
        }
        else if (hpRatio <= phase2Threshold && hosaCurrentPhase < 2 && !isPhaseTransitioning && !isKnockbacking)
        {
            StartCoroutine(PhaseTransitionSequence(2, 1.0f, "<color=orange>⚡ 崩壊した補佐：第2形態突入！能力強奪ハッキングシーケンスを開始！</color>"));
        }
    }

    void LateUpdate()
    {
        transform.rotation = Quaternion.identity;

        Transform squashTarget = GetSquashTarget();
        if (squashTarget != null)
        {
            squashTarget.localScale = targetScale;
            squashTarget.localPosition = originalVisualLocalPosition + targetVisualOffset;
        }

        Transform rotationTarget = GetRotationTarget();
        if (rotationTarget != null)
        {
            rotationTarget.localRotation = Quaternion.Euler(targetXRotation, targetYRotation, targetZRotation);
        }
    }

    private IEnumerator PhaseTransitionSequence(int nextPhase, float speedMultiplier, string debugLogText)
    {
        isPhaseTransitioning = true;
        hosaCurrentPhase = nextPhase;
        attackSpeedMultiplier = speedMultiplier;
        Debug.Log(debugLogText);

        if (isBackRallyMode || Mathf.Abs(transform.position.z) > 0.1f)
        {
            isBackRallyMode = false;
            SetAllCollidersEnabled(true);
            SetAllDamageSourcesEnabled(true);

            Vector3 frontAirPos = new Vector3(transform.position.x, transform.position.y, 0f);
            yield return StartCoroutine(TeleportWithSquashRoutine(frontAirPos, 0.15f));
        }

        isPhaseTransitioning = false;
        TransitionToState(StateStun);
    }

    void FixedUpdate() { if (currentState != null) currentState.FixedUpdate(); }

    public void TransitionToState(GlitchHosaBaseState newState)
    {
        if (currentState == newState) return;
        if (currentState != null) currentState.Exit();

        if (healthComponent != null) healthComponent.StopFlashAndReset(false);
        ForceResetAllMaterials();

        Transform squashTarget = GetSquashTarget();
        Transform rotationTarget = GetRotationTarget();

        if (squashTarget != null) squashTarget.localScale = originalVisualLocalScale;
        if (rotationTarget != null) rotationTarget.localRotation = originalVisualLocalRotation;

        targetXRotation = defaultXRotation;
        targetYRotation = defaultYRotation;
        targetZRotation = 0f;
        targetScale = originalVisualLocalScale;
        targetVisualOffset = Vector3.zero;

        StopAllCoroutines();

        currentState = newState;
        currentDebugStateName = newState.GetType().Name;
        if (currentState != null) currentState.Enter();
    }

    public void SetAllCollidersEnabled(bool enabled)
    {
        var colliders = GetComponentsInChildren<Collider2D>(true);
        foreach (var col in colliders) if (col != null) col.enabled = enabled;
    }

    public void SetBarrierActive(bool active, bool force = false)
    {
        if (healthComponent != null)
        {
            if (active && (force || isReflectionStolen))
            {
                healthComponent.hasBarrier = true;
            }
            else
            {
                healthComponent.hasBarrier = false;
            }
            healthComponent.UpdateBarrierVisual();
        }
    }

    public void ResetBarrier()
    {
        SetBarrierActive(isReflectionStolen);
    }

    public float GetHosaStunDuration()
    {
        float hpRatio = currentHP / maxHP;
        if (hpRatio <= phase3Threshold || hpRatio <= phase2Threshold)
        {
            return stunDuration;
        }
        return hosaCurrentPhase >= 2 ? longStunDuration : miniStunDuration;
    }

    public void SetAllDamageSourcesEnabled(bool enabled)
    {
        var sources = GetComponentsInChildren<DamageSource>(true);
        foreach (var src in sources) if (src != null) src.enabled = enabled;
    }

    public Transform GetSquashTarget()
    {
        if (squashOffsetObject != null) return squashOffsetObject;
        if (ultVisualOffsetObject != null) return ultVisualOffsetObject;
        return transform;
    }

    public Transform GetRotationTarget()
    {
        if (rotationOffsetObject != null) return rotationOffsetObject;
        if (ultVisualOffsetObject != null) return ultVisualOffsetObject;
        return transform;
    }

    public void SetFacingDirection(float dirX)
    {
        if (Mathf.Abs(dirX) > 0.01f)
        {
            targetXRotation = defaultXRotation;
            if (dirX > 0f)
            {
                targetYRotation = dashYRotationRight;
                targetZRotation = -dashZTiltAngle;
            }
            else
            {
                targetYRotation = dashYRotationLeft;
                targetZRotation = dashZTiltAngle;
            }
        }
    }

    public IEnumerator TeleportOutRoutine(float duration = 0.12f)
    {
        Vector3 origScale = originalVisualLocalScale;
        SoundManager.Instance.PlaySE(SeType.EnemyCharge);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float ratio = Mathf.Clamp01(t / duration);
            float smoothRatio = Mathf.SmoothStep(0f, 1f, ratio);

            float x = Mathf.Lerp(origScale.x, 0.01f, smoothRatio);
            float y = Mathf.Lerp(origScale.y, origScale.y * 1.5f, smoothRatio);
            targetScale = new Vector3(x, y, origScale.z);
            yield return null;
        }
        targetScale = new Vector3(0.01f, origScale.y * 1.5f, origScale.z);
    }

    public IEnumerator TeleportInRoutine(Vector3 targetPos, float duration = 0.12f)
    {
        Vector3 origScale = originalVisualLocalScale;

        targetScale = new Vector3(0.01f, origScale.y * 1.5f, origScale.z);
        transform.position = targetPos;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float ratio = Mathf.Clamp01(t / duration);
            float smoothRatio = Mathf.SmoothStep(0f, 1f, ratio);

            float x = Mathf.Lerp(0.01f, origScale.x, smoothRatio);
            float y = Mathf.Lerp(origScale.y, origScale.y * 1.5f, smoothRatio);
            targetScale = new Vector3(x, y, origScale.z);
            yield return null;
        }
        targetScale = origScale;
        targetXRotation = defaultXRotation;
        targetYRotation = defaultYRotation;
        targetZRotation = 0f;
    }

    public IEnumerator TeleportWithSquashRoutine(Vector3 targetPos, float duration = 0.12f)
    {
        yield return StartCoroutine(TeleportOutRoutine(duration));
        yield return StartCoroutine(TeleportInRoutine(targetPos, duration));
    }

    public IEnumerator KnockbackToStageFrontRoutine()
    {
        isKnockbacking = true;
        isBackRallyMode = false;

        Vector3 origScale = originalVisualLocalScale;
        Vector3 startPos = transform.position;

        float centerX = (stageMinX + stageMaxX) / 2f;
        float centerY = stageMinY + stunPivotOffsetY;
        Vector3 targetFrontPos = new Vector3(centerX, centerY, 0f);

        float duration = 0.8f;
        float arcHeight = 5.5f;
        float t = 0f;

        SoundManager.Instance.PlaySE(SeType.PlayerWallHit);

        Rigidbody2D hosaRb = GetComponent<Rigidbody2D>();
        if (hosaRb != null)
        {
            hosaRb.bodyType = RigidbodyType2D.Kinematic;
            hosaRb.linearVelocity = Vector2.zero;
            hosaRb.angularVelocity = 0f;
        }

        targetYRotation = defaultYRotation;
        targetZRotation = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float ratio = Mathf.Clamp01(t / duration);
            float smoothRatio = ratio * (2f - ratio);

            transform.position = new Vector3(
                Mathf.Lerp(startPos.x, targetFrontPos.x, smoothRatio),
                Mathf.Lerp(startPos.y, targetFrontPos.y, smoothRatio) + Mathf.Sin(smoothRatio * Mathf.PI) * arcHeight,
                Mathf.Lerp(startPos.z, targetFrontPos.z, smoothRatio)
            );

            targetXRotation += 950f * Time.deltaTime;
            targetScale = origScale;

            yield return null;
        }

        targetXRotation = -40f;
        targetYRotation = defaultYRotation;
        targetZRotation = 10f;

        targetScale = origScale;
        transform.position = targetFrontPos;

        SetAllCollidersEnabled(true);
        SetAllDamageSourcesEnabled(true);

        isKnockbacking = false;
        StateStun.startAsGrounded = true;
        TransitionToState(StateStun);
    }

    public void ForceResetAllMaterials()
    {
        if (healthComponent != null && healthComponent.isFlashing) return;

        foreach (var data in originalRendererData)
        {
            if (data.spriteRenderer != null) { data.spriteRenderer.sharedMaterial = data.originalMaterial; data.spriteRenderer.color = Color.white; }
            if (data.skinnedRenderer != null) data.skinnedRenderer.sharedMaterial = data.originalMaterial;
            if (data.meshRenderer != null) data.meshRenderer.sharedMaterial = data.originalMaterial;
        }
    }

    public void ApplyGlobalFlashMaterial(Material mat)
    {
        if (mat == null) return;
        foreach (var data in originalRendererData)
        {
            if (data.spriteRenderer != null)
            {
                data.spriteRenderer.sharedMaterial = mat;
                data.spriteRenderer.color = Color.white;
            }
            if (data.skinnedRenderer != null) data.skinnedRenderer.sharedMaterial = mat;
            if (data.meshRenderer != null) data.meshRenderer.sharedMaterial = mat;
        }
    }

    public IEnumerator HoverMoveRoutine(Vector3 targetPos, float duration)
    {
        Vector3 startPos = transform.position;
        float t = 0f;
        float afterimageTimer = 0f;

        Transform squashTarget = GetSquashTarget();
        Transform rotationTarget = GetRotationTarget();
        Vector3 origScale = originalVisualLocalScale;

        float dirX = targetPos.x - startPos.x;

        CreateAfterimage();
        while (t < duration)
        {
            t += Time.deltaTime;
            float ratio = Mathf.Clamp01(t / duration);
            float smoothRatio = Mathf.SmoothStep(0f, 1f, ratio);
            transform.position = Vector3.Lerp(startPos, targetPos, smoothRatio);

            targetScale = origScale;

            if (Mathf.Abs(dirX) > 0.01f)
            {
                targetXRotation = defaultXRotation;
                if (dirX > 0f)
                {
                    targetYRotation = dashYRotationRight;
                    targetZRotation = -dashZTiltAngle;
                }
                else
                {
                    targetYRotation = dashYRotationLeft;
                    targetZRotation = dashZTiltAngle;
                }
            }

            afterimageTimer += Time.deltaTime;
            if (afterimageTimer >= afterimageInterval)
            {
                afterimageTimer = 0f;
                CreateAfterimage();
            }
            yield return null;
        }
        transform.position = targetPos;
    }

    private void CreateAfterimage()
    {
        Transform visualTarget = ultVisualOffsetObject != null ? visualTarget = ultVisualOffsetObject : transform;
        if (visualTarget == null) return;
        GameObject clone = Instantiate(visualTarget.gameObject, visualTarget.position, visualTarget.rotation);
        clone.name = "HosaHoverAfterimage_Clone";
        clone.transform.SetParent(null);
        clone.transform.localScale = visualTarget.lossyScale;
        if (clone.TryGetComponent<GlitchHosaController>(out var c)) Destroy(c);
        if (clone.TryGetComponent<Rigidbody2D>(out var rb)) Destroy(rb);
        if (clone.TryGetComponent<Collider2D>(out var col)) Destroy(col);
        if (clone.TryGetComponent<Animator>(out var anim)) Destroy(anim);
        foreach (var childAnim in clone.GetComponentsInChildren<Animator>()) Destroy(childAnim);
        foreach (var childCol in clone.GetComponentsInChildren<Collider2D>()) Destroy(childCol);

        clone.AddComponent<StageSecondBossAfterimageFade>().Initialize(afterimageDuration, afterimageColor);
    }

    public Transform GetPlayerTransform() => playerTransform;
    public Rigidbody2D GetPlayerRigidbody() => playerRb2D;
}