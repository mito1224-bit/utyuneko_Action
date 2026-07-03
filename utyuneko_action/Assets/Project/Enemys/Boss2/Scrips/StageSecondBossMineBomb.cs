using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class StageSecondBossMineBomb : MonoBehaviour
{
    public float activeTriggerRadius = 2.5f;
    public float fuseDuration = 2.0f;
    public float blowSpeed = 25f;
    public GameObject explosionEffect;

    public float ultimateCounterDamage = 35f;
    public float normalCounterDamage = 20f;

    public float baseScaleMultiplier = 1.4f;
    public float pulseAmplitude = 0.15f;
    public float minPulseSpeed = 5f;
    public float maxPulseSpeed = 35f;

    [SerializeField] private GameObject energyCoreObject;
    public Color targetRedColor = new Color(1f, 0f, 0f, 0.6f);
    public float minCoreScaleMultiplier = 0.1f;
    public float maxCoreScaleMultiplier = 3.0f;

    [SerializeField] private GameObject indicatorRoot;
    [SerializeField] private Transform redCircleTransform;
    [SerializeField] private GameObject bombVisual;

    [SerializeField] private GameObject damageAreaObject;

    private Rigidbody2D rb2d;
    private bool isTriggered = false;
    private Transform playerTransform;
    private float fuseTimer = 0f;
    private int groundLayerId;

    private Vector3 originalVisualScale = Vector3.one;
    private Vector3 originalCoreScale = Vector3.one;
    private float pulsePhase = 0f;
    private Renderer coreRenderer;

    private bool isTossing = false;
    private Vector3 tossStartPos;
    private Vector3 tossTargetPos;
    private float tossDuration = 0.6f;
    private float tossArcHeight = 3.0f;
    private float tossTimer = 0f;

    public bool IsBlownAway { get; private set; } = false;
    public float BlownAwayTimer { get; private set; } = 0f;

    void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
        rb2d.bodyType = RigidbodyType2D.Kinematic;
        groundLayerId = LayerMask.NameToLayer("Ground");

        if (bombVisual != null) originalVisualScale = bombVisual.transform.localScale;
        if (energyCoreObject != null)
        {
            originalCoreScale = energyCoreObject.transform.localScale;
            coreRenderer = energyCoreObject.GetComponent<Renderer>();
        }
    }

    public void InitializeToss(Vector3 startPos, Vector3 targetPos, float duration = 0.6f, float arcHeight = 3.0f)
    {
        tossStartPos = startPos;
        tossTargetPos = targetPos;
        tossDuration = duration;
        tossArcHeight = arcHeight;
        tossTimer = 0f;
        isTossing = true;

        transform.position = startPos;

        // 🛠️【跳ね上がり防止】投擲中はボス本体とのめり込みによる位置ズレを防ぐためコライダーをオフに
        if (TryGetComponent<Collider2D>(out var col)) col.enabled = false;

        if (indicatorRoot != null)
        {
            indicatorRoot.transform.SetParent(null);
            indicatorRoot.transform.position = targetPos;
            indicatorRoot.transform.localScale = new Vector3(activeTriggerRadius * 2f, activeTriggerRadius * 2f, 1f);
            indicatorRoot.SetActive(true);
        }
        if (redCircleTransform != null) redCircleTransform.localScale = Vector3.zero;
        if (damageAreaObject != null) damageAreaObject.SetActive(false);
        if (energyCoreObject != null) energyCoreObject.SetActive(false);
    }

    void Start()
    {
        if (!isTossing)
        {
            GameObject playerObj = FindFirstObjectByType<PlayerController>()?.gameObject;
            if (playerObj != null) playerTransform = playerObj.transform;

            if (indicatorRoot != null)
            {
                indicatorRoot.SetActive(true);
                indicatorRoot.transform.localScale = new Vector3(activeTriggerRadius * 2f, activeTriggerRadius * 2f, 1f);
            }
            if (redCircleTransform != null) redCircleTransform.localScale = Vector3.zero;
            if (damageAreaObject != null) damageAreaObject.SetActive(false);
            if (energyCoreObject != null) energyCoreObject.SetActive(false);
        }
    }

    void Update()
    {
        if (IsBlownAway)
        {
            BlownAwayTimer += Time.deltaTime;
            return;
        }

        if (isTossing)
        {
            tossTimer += Time.deltaTime;
            float t = Mathf.Clamp01(tossTimer / tossDuration);

            Vector3 currentPos = Vector3.Lerp(tossStartPos, tossTargetPos, t);
            currentPos.y += Mathf.Sin(t * Mathf.PI) * tossArcHeight;
            transform.position = currentPos;

            if (bombVisual != null) bombVisual.transform.Rotate(Vector3.forward, 450f * Time.deltaTime);

            if (t >= 1f)
            {
                isTossing = false;
                transform.position = tossTargetPos;

                // 🛠️【めり込み防止解除】配置が完了したのでコライダーを正常化
                if (TryGetComponent<Collider2D>(out var col)) col.enabled = true;

                // ===================================================================
                // 🛠️【角度バグ修正】
                // 投擲移動が終わった瞬間に、グルグル回っていたビジュアルのローカル角度を
                // 0度（正面向きの真っ直ぐな状態）にピタッとリセットします！
                // ===================================================================
                if (bombVisual != null) bombVisual.transform.localRotation = Quaternion.identity;

                GameObject playerObj = FindFirstObjectByType<PlayerController>()?.gameObject;
                if (playerObj != null) playerTransform = playerObj.transform;
            }
            return;
        }

        if (!isTriggered && playerTransform != null)
        {
            if (Vector3.Distance(transform.position, playerTransform.position) < activeTriggerRadius)
            {
                isTriggered = true;
                fuseTimer = 0f;
                SoundManager.Instance.PlaySE(SeType.EnemyExplosionDelay);

                if (energyCoreObject != null) energyCoreObject.SetActive(true);
            }
        }

        if (isTriggered)
        {
            fuseTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(fuseTimer / fuseDuration);
            if (redCircleTransform != null) redCircleTransform.localScale = new Vector3(progress, progress, 1f);

            if (bombVisual != null)
            {
                float currentPulseSpeed = Mathf.Lerp(minPulseSpeed, maxPulseSpeed, progress);
                pulsePhase += Time.deltaTime * currentPulseSpeed;

                float sineWave = Mathf.Sin(pulsePhase);
                float currentBaseScale = Mathf.Lerp(1f, baseScaleMultiplier, progress);
                float finalScaleFactor = currentBaseScale + (sineWave * pulseAmplitude);

                bombVisual.transform.localScale = originalVisualScale * finalScaleFactor;

                if (energyCoreObject != null && coreRenderer != null)
                {
                    float coreScaleFactor = Mathf.Lerp(minCoreScaleMultiplier, maxCoreScaleMultiplier, progress) + (sineWave * 0.05f);
                    energyCoreObject.transform.localScale = originalCoreScale * coreScaleFactor;

                    float blinkFactor = (sineWave + 1f) * 0.5f;
                    Color blinkColor = targetRedColor;
                    blinkColor.a = Mathf.Lerp(0.1f, targetRedColor.a, blinkFactor * progress);
                    coreRenderer.material.color = blinkColor;
                }
            }

            if (fuseTimer >= fuseDuration) NormalExplode();
        }
    }

    void NormalExplode()
    {
        if (IsBlownAway) return;
        ExecuteExplosionCore();
    }

    private void OnTriggerEnter2D(Collider2D other) { ProcessCollision(other.gameObject); }
    private void OnCollisionEnter2D(Collision2D collision) { ProcessCollision(collision.gameObject); }

    private void ProcessCollision(GameObject hitObj)
    {
        if (isTossing) return;

        if (IsBlownAway)
        {
            if (hitObj.CompareTag("Enemy"))
            {
                StageSecondBossController boss = hitObj.GetComponent<StageSecondBossController>();
                if (boss != null)
                {
                    if (boss.currentDebugStateName == "StageSecondBossUltimateState")
                    {
                        boss.TakeDamage(ultimateCounterDamage);
                        boss.OnMineCounterHit();
                    }
                    else
                    {
                        boss.TakeDamage(normalCounterDamage);
                    }
                }
                ExecuteExplosionCore();
            }
            else if (hitObj.layer == groundLayerId)
            {
                ExecuteExplosionCore();
            }
            return;
        }

        if (hitObj.CompareTag("Player"))
        {
            PlayerController player = hitObj.GetComponent<PlayerController>();
            if (player != null && player.CurrentState == player.StateBurst)
            {
                isTriggered = false;
                IsBlownAway = true;
                BlownAwayTimer = 0f;

                if (indicatorRoot != null) Destroy(indicatorRoot);
                rb2d.bodyType = RigidbodyType2D.Dynamic;

                Vector2 flyDirection = Vector2.right;
                if (hitObj.TryGetComponent<Rigidbody2D>(out var playerRb) && playerRb.linearVelocity.magnitude > 0.1f)
                {
                    flyDirection = playerRb.linearVelocity.normalized;
                }
                rb2d.linearVelocity = flyDirection * blowSpeed;
                SoundManager.Instance.PlaySE(SeType.PlayerBurstBegin);

                if (bombVisual != null) bombVisual.transform.localScale = originalVisualScale;
                if (energyCoreObject != null) energyCoreObject.SetActive(false);
            }
            else
            {
                NormalExplode();
            }
        }
    }

    private void ExecuteExplosionCore()
    {
        rb2d.linearVelocity = Vector2.zero;
        rb2d.bodyType = RigidbodyType2D.Kinematic;
        if (TryGetComponent<Collider2D>(out var col)) col.enabled = false;

        if (bombVisual != null) bombVisual.SetActive(false);
        if (energyCoreObject != null) energyCoreObject.SetActive(false);

        SoundManager.Instance.PlaySE(SeType.EnemyExplosion);
        if (explosionEffect != null) Instantiate(explosionEffect, transform.position, Quaternion.identity);

        if (damageAreaObject != null)
        {
            damageAreaObject.transform.SetParent(null);
            SyncColliderSize(damageAreaObject, activeTriggerRadius);
            damageAreaObject.SetActive(true);
            Destroy(damageAreaObject, 0.2f);
        }

        if (indicatorRoot != null)
        {
            indicatorRoot.SetActive(true);
            if (redCircleTransform != null) redCircleTransform.localScale = new Vector3(1f, 1f, 1f);
            Destroy(indicatorRoot, 0.15f);
        }

        Destroy(gameObject, 0.15f);
    }

    private void SyncColliderSize(GameObject targetObj, float radius)
    {
        if (targetObj.TryGetComponent<CircleCollider2D>(out var circleCol)) circleCol.radius = radius;
        else if (targetObj.TryGetComponent<BoxCollider2D>(out var boxCol)) boxCol.size = new Vector2(radius * 2f, radius * 2f);
    }
}