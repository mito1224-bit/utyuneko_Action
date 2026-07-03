using UnityEngine;
using System.Collections;

public class StageSecondBossController : MonoBehaviour
{
    private StageSecondBossHealth healthComponent;
    public float currentHP => healthComponent != null ? healthComponent.currentHP : 100f;
    public float maxHP => healthComponent != null ? healthComponent.maxHP : 100f;

    [Header("基礎ステータス")]
    [Tooltip("💫 スタン中にプレイヤーから受けるダメージの倍率（3倍！）")]
    public float stunDamageMultiplier = 3.0f;

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

    [Header("⚙️ 地雷攻撃（技③）の個別設定")]
    public int maxActiveMines = 5;
    public int minMineSpawnCount = 1;
    public int maxMineSpawnCount = 2;
    [Range(0.1f, 1.0f)] public float mineCenterRangeRatio = 0.4f;
    public float bombSpawnAirHeight = 4.0f;
    public LayerMask groundLayer;

    [Header("⚙️ 一直線即爆発（グリッド爆撃）の個別設定")]
    public Sprite instantLineWarningSprite;
    public float instantLineWarningDuration = 0.6f;

    [Header("⚙️ プレイヤー追従連撃（技④）の設定")]
    public int followAttackCount = 4;
    public float followAttackInterval = 0.5f;

    [Header("⚙️ 攻撃時の巨大化演出の設定")]
    public float attackPulseScaleMultiplier = 1.5f;

    [Header("⚙️ 高速ホバー移動・残像演出の設定")]
    public float afterimageDuration = 0.5f;
    public float afterimageInterval = 0.02f;
    public Color afterimageColor = new Color(0.3f, 0.6f, 1f, 0.65f);

    [Header("⚙️ スタン（気絶）演出の設定")]
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


    [Header("必殺技（吸引）の設定")]
    public float ultPullRadius = 15f;
    public float ultPullForce = 12f;
    public float ultExplosionRadius = 6.0f;
    public float ultDuration = 5.0f;
    public GameObject ultExplosionEffect;
    public float ultMaxApproachSpeed = 10f;
    [Range(0f, 1f)] public float ultBurstPullMultiplier = 0.2f;

    [Header("⚠️ 必殺技用の演出・判定オブジェクト設定")]
    public GameObject ultIndicatorRoot;
    public Transform ultRedCircleTransform;
    public GameObject ultDamageAreaObject;

    public StageSecondBossIdleState StateIdle { get; private set; }
    public StageSecondBossBombTimedState StateBombTimed { get; private set; }
    public StageSecondBossBombMineState StateBombMine { get; private set; }
    public StageSecondBossBombInstantLineState StateBombInstantLine { get; private set; }
    public StageSecondBossBombFollowState StateBombFollow { get; private set; }
    public StageSecondBossUltimateState StateUltimate { get; private set; }
    public StageSecondBossStunState StateStun { get; private set; }
    public StageSecondBossDeadState StateDead { get; private set; }

    private StageSecondBossBaseState currentState;
    public string currentDebugStateName;

    private Transform playerTransform;
    private Rigidbody2D playerRb2D;

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

        healthComponent = GetComponent<StageSecondBossHealth>();
        if (healthComponent == null) healthComponent = GetComponentInChildren<StageSecondBossHealth>();
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

        TransitionToState(StateIdle);
    }

    void Update()
    {
        if (currentState != null) currentState.Update();

        if (currentUltCooldownTimer > 0f) currentUltCooldownTimer -= Time.deltaTime;

        if (currentState != StateStun && currentState != StateDead && currentState != StateUltimate)
        {
            ultTimeTimer += Time.deltaTime;
            if (ultTimeTimer >= ultTimeInterval)
            {
                shouldForceUltimate = true;
            }

            if (lastUltHP - currentHP >= ultHpInterval)
            {
                shouldForceUltimate = true;
            }
        }
    }

    void FixedUpdate() { if (currentState != null) currentState.FixedUpdate(); }

    public void TransitionToState(StageSecondBossBaseState newState)
    {
        if (currentState == newState) return;
        if (currentState != null) currentState.Exit();
        currentState = newState;
        currentDebugStateName = newState.GetType().Name;
        if (currentState != null) currentState.Enter();
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

    // ===================================================================
    // 🛠️【大修正】気絶カウンター受付
    // 必殺技（StateUltimate）中のみスタンへの遷移を許可するように完全固定！
    // ===================================================================
    public void OnMineCounterHit()
    {
        if (currentState == StateUltimate)
        {
            Debug.Log("🛡️ 必殺技（ウルト）の強制遮断に成功！気絶落下します。");
            TransitionToState(StateStun);
        }
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
            if (afterimageTimer >= afterimageInterval)
            {
                afterimageTimer = 0f;
                CreateAfterimage();
            }
            yield return null;
        }
        transform.position = targetPos;
    }

    public IEnumerator HoverMoverRoutine(Vector3 targetPos, float duration)
    {
        return HoverMoveRoutine(targetPos, duration);
    }

    private void CreateAfterimage()
    {
        Transform visualTarget = ultVisualOffsetObject != null ? ultVisualOffsetObject : transform;
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

        SpriteRenderer[] spriteRenderers = clone.GetComponentsInChildren<SpriteRenderer>();
        MeshRenderer[] meshRenderers = clone.GetComponentsInChildren<MeshRenderer>();
        SkinnedMeshRenderer[] skinnedRenderers = clone.GetComponentsInChildren<SkinnedMeshRenderer>();

        foreach (var sr in spriteRenderers) if (sr != null) sr.color = afterimageColor;

        StartCoroutine(FadeOutAfterimageCloneRoutine(clone, spriteRenderers, meshRenderers, skinnedRenderers));
    }

    private IEnumerator FadeOutAfterimageCloneRoutine(GameObject clone, SpriteRenderer[] sprites, MeshRenderer[] meshes, SkinnedMeshRenderer[] skinneds)
    {
        System.Collections.Generic.List<Material> createdMaterials = new System.Collections.Generic.List<Material>();

        foreach (var smr in skinneds)
        {
            if (smr == null || !smr.enabled) continue;
            Mesh bakedMesh = new Mesh();
            smr.BakeMesh(bakedMesh);
            GameObject meshObj = smr.gameObject;
            Destroy(smr);
            MeshFilter mf = meshObj.AddComponent<MeshFilter>();
            mf.sharedMesh = bakedMesh;
            MeshRenderer mr = meshObj.AddComponent<MeshRenderer>();
            Material newMat = new Material(Shader.Find("Sprites/Default")) { color = afterimageColor };
            mr.material = newMat;
            createdMaterials.Add(newMat);
        }

        foreach (var mr in meshes)
        {
            if (mr == null) continue;
            Material newMat = new Material(Shader.Find("Sprites/Default")) { color = afterimageColor };
            mr.material = newMat;
            createdMaterials.Add(newMat);
        }

        float t = 0f;
        float duration = afterimageDuration;

        while (t < duration)
        {
            t += Time.deltaTime;
            float ratio = Mathf.Clamp01(t / duration);
            float alpha = Mathf.Lerp(afterimageColor.a, 0f, ratio);

            foreach (var sr in sprites) if (sr != null) { Color c = sr.color; c.a = alpha; sr.color = c; }
            foreach (var mat in createdMaterials) if (mat != null) { Color c = mat.color; c.a = alpha; mat.color = c; }
            yield return null;
        }

        foreach (var mat in createdMaterials) if (mat != null) Destroy(mat);
        var meshFilters = clone.GetComponentsInChildren<MeshFilter>();
        foreach (var mf in meshFilters) if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name == "") Destroy(mf.sharedMesh);

        Destroy(clone);
    }

    public Transform GetPlayerTransform() => playerTransform;
    public Rigidbody2D GetPlayerRigidbody() => playerRb2D;
}