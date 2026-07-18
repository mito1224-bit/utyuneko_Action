using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 👑 崩壊した補佐：全体を管理するメインブレイン（物理更新パイプライン完全復旧版）
/// </summary>
[RequireComponent(typeof(GlitchHosaVisual))]
public class GlitchHosaController : MonoBehaviour, IEventActor
{
    private GlitchHosaHealth healthComponent;
    public float currentHP => healthComponent != null ? healthComponent.currentHP : 100f;
    public float maxHP => healthComponent != null ? healthComponent.maxHP : 100f;

    public Vector3 originalVisualLocalPosition { get; private set; }
    public Quaternion originalVisualLocalRotation { get; private set; }
    public Vector3 originalVisualLocalScale { get; private set; }

    // 分離した演出専門クラスへの参照枠
    public GlitchHosaVisual Visual { get; private set; }

    [Header("😈 崩壊した補佐 - 形態設定")]
    public float phase2Threshold = 0.7f;
    public float phase3Threshold = 0.3f;
    public int hosaCurrentPhase { get; private set; } = 1;

    [Header("🏎️ 突進（ダッシュ）移動速度の安全ロック設定")]
    public float baseDashSpeed = 20f;
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

    [Header("⚡ 激突・衝撃波（ショックウェーブ）専用設定")]
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

    public float stunXRotation = -45.0f;
    public float deadXRotation = 15.0f;

    public float stageMinX => topLeftBoundary != null ? topLeftBoundary.position.x : -15f;
    public float stageMaxX => bottomRightBoundary != null ? bottomRightBoundary.position.x : 15f;
    public float stageMaxY => topLeftBoundary != null ? topLeftBoundary.position.y : 10f;
    public float stageMinY => bottomRightBoundary != null ? bottomRightBoundary.position.y : 0f;

    [Header("🎯 突進・警告インジケーター設定")]
    public Sprite dashWarningSprite;
    public Material dashWarningMaterial;
    public Color dashWarningColor = new Color(1f, 0f, 0f, 0.35f);
    public float dashWarningDuration = 0.6f;

    [Header("🎥 カメラ干渉防止設定")]
    [Tooltip("カメラの境界線制限トリガー")]
    public CameraBoundsTrigger stageCamera;

    [Header("🎬 撃破後シネマティックイベント設定")]
    [Tooltip("ボス撃破・着地後に自動でActive(true)にしたいトリガーオブジェクト")]
    public GameObject postBossEventTrigger;

    public bool isDeadGrounded { get; set; }

    public System.Action<Collision2D> OnCollisionEnterEvent;

    private ImageBubble bossImageBubble;

    public int pendingNextPhase { get; private set; }
    public float pendingSpeedMultiplier { get; private set; }

    [HideInInspector] public bool isBackRallyMode = false;
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
    public GlitchHosaAppearState StateAppear { get; private set; }
    public GlitchHosaPhaseTransitionState StatePhaseTransition { get; private set; }
    public GlitchHosaDeadState StateDead { get; private set; }

    private GlitchHosaBaseState currentState;
    public string currentDebugStateName;
    private Transform playerTransform;
    private Rigidbody2D playerRb2D;

    private float afterimageTimer = 0f;

    void Awake()
    {
        Visual = GetComponent<GlitchHosaVisual>();

        StateIdle = new GlitchHosaIdleState(this);
        StateP1_Beam = new HosaP1_BeamState(this);
        StateP1_Clones = new HosaP1_ClonesState(this);
        StateP1_WallDash = new HosaP1_WallDashState(this);
        StateP1_BackBombs = new HosaP1_BackBombsState(this);
        StateStun = new GlitchHosaStunState(this);

        StateP2_CloneDash = new HosaP2_CloneDashState(this);
        StateP2_BeamBombs = new HosaP2_BeamBombsState(this);
        StateP2_HackingSteal = new HosaP2_HackingStealState(this);
        StateAppear = new GlitchHosaAppearState(this);
        StatePhaseTransition = new GlitchHosaPhaseTransitionState(this);
        StateDead = new GlitchHosaDeadState(this);

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

        if (ultVisualOffsetObject == null && bossAnimator != null) ultVisualOffsetObject = bossAnimator.transform;
        if (squashOffsetObject == null) squashOffsetObject = ultVisualOffsetObject;
        if (rotationOffsetObject == null) rotationOffsetObject = ultVisualOffsetObject;

        Visual.Initialize(this);

        bossImageBubble = GetComponentInChildren<ImageBubble>(true);
        if (postBossEventTrigger != null) postBossEventTrigger.SetActive(false);

        TransitionToState(StateAppear);
    }

    void Update()
    {
        if (currentState != null) currentState.Update();

        if (currentDebugStateName != "GlitchHosaStunState" &&
            currentDebugStateName != "GlitchHosaAppearState" &&
            currentDebugStateName != "GlitchHosaPhaseTransitionState" &&
            currentDebugStateName != "GlitchHosaDeadState" &&
            !isKnockbacking)
        {
            float hoverY = Mathf.Sin(Time.time * hoverSpeed) * hoverAmount;
            targetVisualOffset = new Vector3(0f, hoverY, 0f);
        }

        if (currentDebugStateName == "GlitchHosaPhaseTransitionState" ||
            currentDebugStateName == "GlitchHosaDeadState" ||
            isKnockbacking) return;

        float hpRatio = currentHP / maxHP;

        if (hpRatio <= phase3Threshold && hosaCurrentPhase < 3)
        {
            pendingNextPhase = 3;
            pendingSpeedMultiplier = 1.5f;
            TransitionToState(StatePhaseTransition);
        }
        else if (hpRatio <= phase2Threshold && hosaCurrentPhase < 2)
        {
            pendingNextPhase = 2;
            pendingSpeedMultiplier = 1.0f;
            TransitionToState(StatePhaseTransition);
        }
    }

    // 👑【超重要：心臓部復旧】これが消えていたため各Stateの物理移動が一切動いていませんでした！
    void FixedUpdate()
    {
        if (currentState != null) currentState.FixedUpdate();
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

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (currentDebugStateName == "GlitchHosaDeadState")
        {
            isDeadGrounded = true;
        }

        OnCollisionEnterEvent?.Invoke(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        OnCollisionEnterEvent?.Invoke(collision);
    }

    public void TransitionToState(GlitchHosaBaseState newState)
    {
        if (currentState is GlitchHosaDeadState) return;

        if (newState is GlitchHosaStunState)
        {
            float hpRatio = currentHP / maxHP;
            if ((hpRatio <= phase2Threshold && hosaCurrentPhase < 2) ||
                (hpRatio <= phase3Threshold && hosaCurrentPhase < 3) ||
                currentState is GlitchHosaPhaseTransitionState)
            {
                return;
            }
        }

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

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic; // 安全のため通常時はKinematicへ
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        StopAllCoroutines();

        currentState = newState;
        currentDebugStateName = newState.GetType().Name;
        if (currentState != null) currentState.Enter();
    }

    public void ConfirmPhaseActivation(int phase, float speedMult)
    {
        hosaCurrentPhase = phase;
        attackSpeedMultiplier = speedMult;
        Debug.Log($"<color=green>⚙️ システム：フェーズ {phase} への内部 data 同期が安全に完了しました。</color>");
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

    public IEnumerator HoverMoveRoutine(Vector3 targetPos, float duration)
    {
        Vector3 startPos = transform.position;
        float t = 0f;
        afterimageTimer = 0f;

        Vector3 origScale = originalVisualLocalScale;
        float dirX = targetPos.x - startPos.x;

        Visual.CreateAfterimage();
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
                targetYRotation = dirX > 0f ? dashYRotationRight : dashYRotationLeft;
                targetZRotation = dirX > 0f ? -dashZTiltAngle : dashZTiltAngle;
            }

            afterimageTimer += Time.deltaTime;
            if (afterimageTimer >= Visual.afterimageInterval)
            {
                afterimageTimer = 0f;
                Visual.CreateAfterimage();
            }
            yield return null;
        }
        transform.position = targetPos;
    }

    public IEnumerator KnockbackToStageFrontRoutine()
    {
        isKnockbacking = true;

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

        targetXRotation = stunXRotation;
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

    public void ForceResetAllMaterials() => Visual.ForceResetAllMaterials(healthComponent != null && healthComponent.isFlashing);
    public void ApplyGlobalFlashMaterial(Material mat) => Visual.ApplyGlobalFlashMaterial(mat);
    public IEnumerator TeleportOutRoutine(float duration = 0.12f) => Visual.TeleportOutRoutine(duration);
    public IEnumerator TeleportInRoutine(Vector3 targetPos, float duration = 0.12f) => Visual.TeleportInRoutine(targetPos, duration);
    public IEnumerator TeleportWithSquashRoutine(Vector3 targetPos, float duration = 0.12f) => Visual.TeleportWithSquashRoutine(targetPos, duration);

    public void ShowBossStamp(ImageBubble.StampType type) { if (bossImageBubble != null) bossImageBubble.ShowStamp(type); }
    public void HideBossStamp() { if (bossImageBubble != null) bossImageBubble.StartFadeOut(); }

    public Transform GetPlayerTransform() => playerTransform;
    public Rigidbody2D GetPlayerRigidbody() => playerRb2D;

    private Coroutine hosaReactionCoroutine;
    private IEnumerator HosaBobbingLocalRoutine(float duration, float speed, float amount) { Transform bossAnimatorTransform = bossAnimator != null ? bossAnimator.transform : transform; float elapsed = 0f; while (elapsed < duration) { elapsed += Time.deltaTime; float offsetY = Mathf.Sin(elapsed * speed) * amount; bossAnimatorTransform.localPosition = new Vector3(0f, offsetY, 0f); yield return null; } bossAnimatorTransform.localPosition = Vector3.zero; }
    private IEnumerator HosaSpinLocalRoutine(float duration, float speed) { Transform bossAnimatorTransform = bossAnimator != null ? bossAnimator.transform : transform; float elapsed = 0f; while (elapsed < duration) { elapsed += Time.deltaTime; bossAnimatorTransform.Rotate(Vector3.up, speed * Time.deltaTime); yield return null; } Quaternion startRot = bossAnimatorTransform.localRotation; float lerpT = 0f; while (lerpT < 1f) { lerpT += Time.deltaTime * 5f; bossAnimatorTransform.localRotation = Quaternion.Slerp(startRot, Quaternion.identity, Mathf.Clamp01(lerpT)); yield return null; } bossAnimatorTransform.localRotation = Quaternion.identity; }
    private IEnumerator HosaSurpriseJumpLocalRoutine(float duration, float height, float twitchMagnitude) { Transform bossAnimatorTransform = bossAnimator != null ? bossAnimator.transform : transform; float elapsed = 0f; while (elapsed < duration) { elapsed += Time.deltaTime; float t = Mathf.Clamp01(elapsed / duration); float arcY = Mathf.Sin(t * Mathf.PI) * height; float twitchX = Random.Range(-twitchMagnitude, twitchMagnitude); float twitchY = Random.Range(-twitchMagnitude, twitchMagnitude); bossAnimatorTransform.localPosition = new Vector3(twitchX, arcY + twitchY, 0f); yield return null; } bossAnimatorTransform.localPosition = Vector3.zero; }
    private IEnumerator HosaTwitchLocalRoutine(float duration, float magnitude) { Transform bossAnimatorTransform = bossAnimator != null ? bossAnimator.transform : transform; float elapsed = 0f; while (elapsed < duration) { elapsed += Time.deltaTime; float offsetX = Random.Range(-magnitude, magnitude); float offsetY = Random.Range(-magnitude, magnitude); bossAnimatorTransform.localPosition = new Vector3(offsetX, offsetY, 0f); yield return null; } bossAnimatorTransform.localPosition = Vector3.zero; }
    private IEnumerator HosaDarkSlideLocalRoutine(float dirX, float duration) { Transform bossAnimatorTransform = bossAnimator != null ? bossAnimator.transform : transform; float elapsed = 0f; while (elapsed < duration) { elapsed += Time.deltaTime; float t = Mathf.Clamp01(elapsed / duration); float smoothT = Mathf.Sin(t * Mathf.PI); bossAnimatorTransform.localPosition = new Vector3(dirX * smoothT, 0f, 0f); yield return null; } bossAnimatorTransform.localPosition = Vector3.zero; }

    public void PlayReaction(ImageBubble.StampType type, float duration = 2.0f)
    {
        if (hosaReactionCoroutine != null) StopCoroutine(hosaReactionCoroutine);
        if (bossAnimator != null) { bossAnimator.transform.localPosition = Vector3.zero; bossAnimator.transform.localRotation = Quaternion.identity; }

        switch (type)
        {
            case ImageBubble.StampType.OK: case ImageBubble.StampType.Maru: hosaReactionCoroutine = StartCoroutine(HosaBobbingLocalRoutine(0.4f, 25f, 0.2f)); break;
            case ImageBubble.StampType.Question: case ImageBubble.StampType.Hatena: hosaReactionCoroutine = StartCoroutine(FuncHosaTilt()); IEnumerator FuncHosaTilt() { float t = 0f; Transform bossAnimatorTransform = bossAnimator != null ? bossAnimator.transform : transform; Quaternion origRot = bossAnimatorTransform.localRotation; while (t < 0.3f) { t += Time.deltaTime; bossAnimatorTransform.Rotate(Vector3.forward, 60f * Time.deltaTime); yield return null; } yield return new WaitForSeconds(0.2f); t = 0f; while (t < 0.3f) { t += Time.deltaTime; bossAnimatorTransform.localRotation = Quaternion.Slerp(bossAnimatorTransform.localRotation, origRot, t / 0.3f); yield return null; } bossAnimatorTransform.localRotation = origRot; } break;
            case ImageBubble.StampType.Surprise: case ImageBubble.StampType.Denger: case ImageBubble.StampType.Enemy: hosaReactionCoroutine = StartCoroutine(HosaSurpriseJumpLocalRoutine(0.35f, 1.0f, 0.08f)); break;
            case ImageBubble.StampType.Doya: hosaReactionCoroutine = StartCoroutine(HosaBobbingLocalRoutine(0.5f, 8f, 0.05f)); break;
            case ImageBubble.StampType.Sweat: case ImageBubble.StampType.Confusion: case ImageBubble.StampType.Dokuro: hosaReactionCoroutine = StartCoroutine(HosaTwitchLocalRoutine(0.8f, 0.12f)); break;
            case ImageBubble.StampType.Joy: hosaReactionCoroutine = StartCoroutine(HosaBobbingLocalRoutine(0.8f, 20f, 0.3f)); break;
            case ImageBubble.StampType.Star: hosaReactionCoroutine = StartCoroutine(HosaSpinLocalRoutine(0.4f, 1080f)); break;
            case ImageBubble.StampType.Go: case ImageBubble.StampType.Right: case ImageBubble.StampType.Left: float slideDir = (type == ImageBubble.StampType.Left) ? -0.8f : 0.8f; hosaReactionCoroutine = StartCoroutine(HosaDarkSlideLocalRoutine(slideDir, 0.3f)); break;
            case ImageBubble.StampType.Batu: hosaReactionCoroutine = StartCoroutine(HosaBobbingLocalRoutine(0.5f, 10f, -0.25f)); break;
        }
    }
}