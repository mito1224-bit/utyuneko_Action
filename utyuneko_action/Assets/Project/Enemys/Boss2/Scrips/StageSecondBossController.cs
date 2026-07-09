using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class StageSecondBossController : MonoBehaviour
{
    private StageSecondBossHealth healthComponent;
    public float currentHP => healthComponent != null ? healthComponent.currentHP : 100f;
    public float maxHP => healthComponent != null ? healthComponent.maxHP : 100f;

    public DamageSource cachedDamageSource { get; private set; }
    public Vector3 originalVisualLocalPosition { get; private set; }
    public Quaternion originalVisualLocalRotation { get; private set; }

    // ===================================================================
    // 🛠️【新設】巨大化バグを完全に根絶するため、ゲーム開始時の初期サイズを記憶する変数
    // ===================================================================
    public Vector3 originalVisualLocalScale { get; private set; }

    private List<RendererData> originalRendererData = new List<RendererData>();

    private struct RendererData
    {
        public SpriteRenderer spriteRenderer;
        public SkinnedMeshRenderer skinnedRenderer;
        public MeshRenderer meshRenderer;
        public Material originalMaterial;
    }

    [Header("基礎ステータス")]
    public float stunDamageMultiplier = 3.0f;

    [Header("🔥 第2フェーズ（怒りモード）の設定")]
    public float phase2HpThresholdRatio = 0.5f;
    public float phase2SpeedMultiplier = 1.4f;
    [Tooltip("第2フェーズ移行（威嚇咆哮）演出中の被弾ダメージ倍率。0.2ならダメージ80%カット")]
    public float phaseTransitionDamageMultiplier = 0.2f;

    public float attackSpeedMultiplier { get; set; } = 1.0f;
    public bool isPhase2Started { get; set; } = false;

    [Header("通常攻撃の基本設定")]
    public GameObject timedBombPrefab;
    public GameObject mineBombPrefab;
    public Transform tossLaunchPoint;

    [Header("🤖 3D階層・グラフィックオブジェクトの設定")]
    public Transform ultVisualOffsetObject;
    public Animator bossAnimator;

    [Header("🤖 自動配置・壁埋まり安全ガードの設定")]
    public Transform topLeftBoundary;
    public Transform bottomRightBoundary;

    public float stageMinX => topLeftBoundary != null ? topLeftBoundary.position.x : -15f;
    public float stageMaxX => bottomRightBoundary != null ? bottomRightBoundary.position.x : 15f;
    public float stageMaxY => topLeftBoundary != null ? topLeftBoundary.position.y : 10f;
    public float stageMinY => bottomRightBoundary != null ? bottomRightBoundary.position.y : 0f;

    [Header("バランス調整用パラメータ")]
    public int straightBombCount = 5;
    public int instantLineBombCount = 4;
    public float mineBomMinInterval = 10f;
    public bool mineBomAttack { get; set; } = false;
    private float mineTimeTimer = 0f;

    [Header("⚙️ 地雷攻撃（技③）の個別設定")]
    public int maxActiveMines = 5;
    public int minMineSpawnCount = 1;
    public int maxMineSpawnCount = 2;
    [Range(0.1f, 1.0f)] public float mineCenterRangeRatio = 0.4f;
    public float bombSpawnAirHeight = 4.0f;
    public LayerMask groundLayer;

    [Header("⚙️ 一直線即爆発（グリッド爆撃）の個別設定")]
    public Sprite instantLineWarningSprite;
    public Material instantLineWarningMaterial;
    public float instantLineWarningDuration = 0.6f;
    public Color instantLineWarningColor = new Color(1f, 1f, 1f, 1f);

    [Header("⚙️ プレイヤー追従連撃（技④）の設定")]
    public int followAttackCount = 4;
    public float followAttackInterval = 0.5f;

    [Header("⚙️ 攻撃時の巨大化演出の設定")]
    public float attackPulseScaleMultiplier = 1.5f;

    [Header("⚙️ 高速ホバー移動・残像演出の設定")]
    public float afterimageDuration = 0.5f;
    public float afterimageInterval = 0.02f;
    public Color afterimageColor = new Color(0.3f, 0.6f, 1f, 0.65f);

    [Header("⚙️ スターン（気絶）演出の設定")]
    public float stunDuration = 4.0f;
    public float stunRecoveryDuration = 0.6f;
    public float stunGravityAmount = 1.8f;
    public float stunRotateSpeed = 720f;
    public float stunPivotOffsetY = 0.6f;
    public float stunGroundYOffset = 2.0f;

    [Header("⚙️ 必殺技（ウルト）の発生間隔設定")]
    public float ultCooldownDuration = 15.0f;
    public float currentUltCooldownTimer { get; set; } = 0f;
    public bool CanUseUltimate => currentUltCooldownTimer <= 0f;

    [Header("⚙️ 必殺技（ウルト）の優先発動トリガー設定")]
    public float ultTimeInterval = 30f;
    private float ultTimeTimer = 0f;
    public float ultHpInterval = 25f;
    private float lastUltHP;
    public bool shouldForceUltimate { get; set; } = false;

    [Header("⚙️ 死亡・イベント演出の設定")]
    public float eventTriggerDistance = 2.5f;
    public GameObject specialDisappearEffect;
    public bool isDeathEventStarted { get; set; } = false;
    private bool isAlreadyDisappeared = false;

    public bool isDeadGrounded { get; set; } = false;

    [Header("💀 死亡時にアクティブ化するイベントトリガー")]
    [Tooltip("EventTriggerArea2Dがアタッチされた、ボス戦後の吸い込みイベント用トリガーオブジェクトをセット")]
    public GameObject absorbEventTriggerObject;

    [Header("必殺技（吸引）の設定")]
    public float ultPullRadius = 15f;
    public float ultPullForce = 12f;
    public float ultExplosionRadius = 6.0f;
    public float ultDuration = 5.0f;
    public GameObject ultExplosionEffect;
    public float ultExplosionEffectScaleMultiplier = 1.0f;
    public float ultMaxApproachSpeed = 10f;
    [Range(0f, 1f)] public float ultBurstPullMultiplier = 0.2f;

    [Header("⚠️ 必殺技用の演出・判定オブジェクト設定")]
    public GameObject ultIndicatorRoot;
    public Transform ultRedCircleTransform;
    public GameObject ultDamageAreaObject;
    public CameraBoundsTrigger BossStatgeCamera;

    public StageSecondBossIdleState StateIdle { get; private set; }
    public StageSecondBossBombTimedState StateBombTimed { get; private set; }
    public StageSecondBossBombMineState StateBombMine { get; private set; }
    public StageSecondBossBombInstantLineState StateBombInstantLine { get; private set; }
    public StageSecondBossBombFollowState StateBombFollow { get; private set; }
    public StageSecondBossUltimateState StateUltimate { get; private set; }
    public StageSecondBossStunState StateStun { get; private set; }
    public StageSecondBossDeadState StateDead { get; private set; }
    public StageSecondBossPhaseTransitionState StatePhaseTransition { get; private set; }
    public StageSecondBossBombCrossState StateBombCross { get; private set; }
    public StageSecondBossAppearState StateAppear { get; private set; }

    private StageSecondBossBaseState currentState;
    public string currentDebugStateName;

    private Transform playerTransform;
    private Rigidbody2D playerRb2D;
    private int groundLayerId;

    void Awake()
    {
        StateIdle = new StageSecondBossIdleState(this);
        StateBombTimed = new StageSecondBossBombTimedState(this);
        StateBombMine = new StageSecondBossBombMineState(this);
        StateBombInstantLine = new StageSecondBossBombInstantLineState(this);
        StateBombFollow = new StageSecondBossBombFollowState(this);
        StateUltimate = new StageSecondBossUltimateState(this);
        StateStun = new StageSecondBossStunState(this);
        StateDead = new StageSecondBossDeadState(this);
        StatePhaseTransition = new StageSecondBossPhaseTransitionState(this);
        StateBombCross = new StageSecondBossBombCrossState(this);
        StateAppear = new StageSecondBossAppearState(this);

        healthComponent = GetComponent<StageSecondBossHealth>();
        if (healthComponent == null) healthComponent = GetComponentInChildren<StageSecondBossHealth>();

        cachedDamageSource = GetComponent<DamageSource>();
        if (cachedDamageSource == null) cachedDamageSource = GetComponentInChildren<DamageSource>();

        Transform visualTarget = ultVisualOffsetObject != null ? visualTarget = ultVisualOffsetObject : transform;
        originalVisualLocalPosition = visualTarget.localPosition;
        originalVisualLocalRotation = visualTarget.localRotation;

        // ===================================================================
        // 🛠️【修正】Awakeのタイミングで、インスペクターで設定された本来の初期大きさを記憶！
        // ===================================================================
        originalVisualLocalScale = visualTarget.localScale;

        originalRendererData.Clear();
        foreach (var sr in visualTarget.GetComponentsInChildren<SpriteRenderer>(true)) if (sr != null) originalRendererData.Add(new RendererData { spriteRenderer = sr, originalMaterial = sr.sharedMaterial });
        foreach (var smr in visualTarget.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (smr != null) originalRendererData.Add(new RendererData { skinnedRenderer = smr, originalMaterial = smr.sharedMaterial });
        foreach (var mr in visualTarget.GetComponentsInChildren<MeshRenderer>(true)) if (mr != null) originalRendererData.Add(new RendererData { meshRenderer = mr, originalMaterial = mr.sharedMaterial });

        groundLayerId = LayerMask.NameToLayer("Ground");
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
        if (ultDamageAreaObject != null) ultDamageAreaObject.SetActive(false);
        if (ultIndicatorRoot != null) ultIndicatorRoot.SetActive(false);

        lastUltHP = maxHP;
        TransitionToState(StateAppear);
    }

    void Update()
    {
        if (currentState != null) currentState.Update();

        if (currentUltCooldownTimer > 0f) currentUltCooldownTimer -= Time.deltaTime;

        if (currentState != StateStun && currentState != StateDead && currentState != StateUltimate && currentState != StatePhaseTransition && currentState != StateAppear)
        {
            ultTimeTimer += Time.deltaTime;
            if (ultTimeTimer >= ultTimeInterval) shouldForceUltimate = true;
            if (lastUltHP - currentHP >= ultHpInterval) shouldForceUltimate = true;
        }

        mineTimeTimer += Time.deltaTime;
        if (mineTimeTimer > mineBomMinInterval)
        {
            mineBomAttack = true;
        }

        if (!isPhase2Started && currentHP <= maxHP * phase2HpThresholdRatio && currentState != StateDead && currentState != StateAppear)
        {
            isPhase2Started = true;
            TransitionToState(StatePhaseTransition);
        }

        if (Input.GetKeyDown(KeyCode.P)) TakeDamage(10f);
    }

    void FixedUpdate() { if (currentState != null) currentState.FixedUpdate(); }

    public void TransitionToState(StageSecondBossBaseState newState)
    {
        if (currentState == newState) return;
        if (currentState != null) currentState.Exit();

        if (healthComponent != null) healthComponent.StopFlashAndReset();
        ForceResetAllMaterials();

        // ===================================================================
        // 🛠️【修正：巨大化リセット安全ガード】
        // 技のタメ中にやられてステートが切り替わった際、
        // 巨大化演出コルーチンが止まってもボスのサイズを確実に初期等倍スケールに戻します！
        // ===================================================================
        Transform visualTarget = ultVisualOffsetObject != null ? ultVisualOffsetObject : transform;
        if (visualTarget != null)
        {
            visualTarget.localScale = originalVisualLocalScale;
        }

        StopAllCoroutines();

        currentState = newState;
        currentDebugStateName = newState.GetType().Name;
        if (currentState != null) currentState.Enter();
    }

    private void OnCollisionEnter2D(Collision2D collision) { CheckGroundCollision(collision.gameObject); }
    private void OnTriggerEnter2D(Collider2D other) { CheckGroundCollision(other.gameObject); }

    private void CheckGroundCollision(GameObject hitObj)
    {
        if (hitObj.layer == groundLayerId)
        {
            if (currentDebugStateName == "StageSecondBossStunState" || currentDebugStateName == "StageSecondBossDeadState")
            {
                if (TryGetComponent<Rigidbody2D>(out var rb))
                {
                    rb.mass = 1000f;
                    rb.linearVelocity = Vector2.zero;
                    Debug.Log("<color=cyan>⚓ ボス：物理的に地面に着地！ Massを1000にロックして鉄壁化しました。</color>");
                }
            }

            if (currentDebugStateName == "StageSecondBossDeadState")
            {
                isDeadGrounded = true;

                if (absorbEventTriggerObject != null && !absorbEventTriggerObject.activeSelf)
                {
                    absorbEventTriggerObject.SetActive(true);
                    Debug.Log("<color=green>🎬 ボス：地面に激突着地完了！ 吸い込み演出用の【EventTriggerArea2D】を実体化させました。</color>");
                }
            }
        }
    }

    public void SetAllDamageSourcesEnabled(bool enabled)
    {
        var sources = GetComponentsInChildren<DamageSource>(true);
        foreach (var src in sources) if (src != null) src.enabled = enabled;
    }

    public void TakeDamage(float damage) { if (healthComponent != null) healthComponent.TakeDamage(damage); }
    public void ResetBarrier() { if (healthComponent != null) healthComponent.ResetBarrier(); }

    public void ResetUltTriggers()
    {
        ultTimeTimer = 0f;
        lastUltHP = currentHP;
        shouldForceUltimate = false;
        currentUltCooldownTimer = ultCooldownDuration;
    }

    public void ResetMineTriggers()
    {
        mineTimeTimer = 0f;
        mineBomAttack = false;
    }

    public void OnMineCounterHit()
    {
        if (currentState == StateUltimate)
        {
            Debug.Log("🛡️ 必殺技（ウルト）の強制遮断に成功！気絶落下します。");
            TransitionToState(StateStun);
        }
    }

    public void ForceResetAllMaterials()
    {
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

    public void DisappearBoss()
    {
        if (isAlreadyDisappeared) return;
        isAlreadyDisappeared = true;
        Vector3 disappearPos = transform.position;
        if (specialDisappearEffect != null) Instantiate(specialDisappearEffect, disappearPos, Quaternion.identity);
        else if (ultExplosionEffect != null) Instantiate(ultExplosionEffect, disappearPos, Quaternion.identity);
        SoundManager.Instance.PlaySE(SeType.EnemyExplosion);
        Destroy(gameObject);
    }

    public void SyncColliderSize(GameObject targetObj, float radius)
    {
        if (targetObj == null) return;
        if (targetObj.TryGetComponent<CircleCollider2D>(out var circleCol)) circleCol.radius = radius;
        else if (targetObj.TryGetComponent<BoxCollider2D>(out var boxCol)) boxCol.size = new Vector2(radius * 2f, radius * 2f);
    }

    public IEnumerator HoverMoveRoutine(Vector3 targetPos, float duration)
    {
        Vector3 startPos = transform.position;
        float t = 0f;
        float afterimageTimer = 0f;
        CreateAfterimage();
        while (t < duration)
        {
            t += Time.deltaTime;
            float ratio = Mathf.Clamp01(t / duration);
            float smoothRatio = Mathf.SmoothStep(0f, 1f, ratio);
            transform.position = Vector3.Lerp(startPos, targetPos, smoothRatio);
            afterimageTimer += Time.deltaTime;
            if (afterimageTimer >= afterimageInterval) { afterimageTimer = 0f; CreateAfterimage(); }
            yield return null;
        }
        transform.position = targetPos;
    }

    public IEnumerator HoverMoverRoutine(Vector3 targetPos, float duration) { return HoverMoveRoutine(targetPos, duration); }

    private void CreateAfterimage()
    {
        Transform visualTarget = ultVisualOffsetObject != null ? visualTarget = ultVisualOffsetObject : transform;
        if (visualTarget == null) return;
        GameObject clone = Instantiate(visualTarget.gameObject, visualTarget.position, visualTarget.rotation);
        clone.name = "BossHoverAfterimage_Clone";
        clone.transform.SetParent(null);
        clone.transform.localScale = visualTarget.lossyScale;
        if (clone.TryGetComponent<StageSecondBossController>(out var c)) Destroy(c);
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

public class StageSecondBossAfterimageFade : MonoBehaviour
{
    public void Initialize(float duration, Color targetColor)
    {
        var sprites = GetComponentsInChildren<SpriteRenderer>();
        var meshes = GetComponentsInChildren<MeshRenderer>();
        var skinneds = GetComponentsInChildren<SkinnedMeshRenderer>();

        foreach (var sr in sprites) if (sr != null) sr.color = targetColor;

        List<Material> createdMaterials = new List<Material>();
        foreach (var smr in skinneds)
        {
            if (smr == null || !smr.enabled) continue;
            Mesh bakedMesh = new Mesh(); smr.BakeMesh(bakedMesh); GameObject meshObj = smr.gameObject; Destroy(smr);
            meshObj.AddComponent<MeshFilter>().sharedMesh = bakedMesh;
            Material newMat = new Material(Shader.Find("Sprites/Default")) { color = targetColor };
            meshObj.AddComponent<MeshRenderer>().material = newMat; createdMaterials.Add(newMat);
        }
        foreach (var mr in meshes)
        {
            if (mr == null) continue;
            Material newMat = new Material(Shader.Find("Sprites/Default")) { color = targetColor };
            mr.material = newMat; createdMaterials.Add(newMat);
        }

        StartCoroutine(FadeRoutine(duration, targetColor.a, sprites, createdMaterials));
    }

    private IEnumerator FadeRoutine(float duration, float startAlpha, SpriteRenderer[] sprites, List<Material> materials)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float ratio = Mathf.Clamp01(t / duration);
            float alpha = Mathf.Lerp(startAlpha, 0f, ratio);

            foreach (var sr in sprites) if (sr != null) { Color c = sr.color; c.a = alpha; sr.color = c; }
            foreach (var mat in materials) if (mat != null) { Color c = mat.color; c.a = alpha; mat.color = c; }
            yield return null;
        }
        foreach (var mat in materials) if (mat != null) Destroy(mat);
        var meshFilters = GetComponentsInChildren<MeshFilter>();
        foreach (var mf in meshFilters) if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name == "") Destroy(mf.sharedMesh);
        Destroy(gameObject);
    }
}