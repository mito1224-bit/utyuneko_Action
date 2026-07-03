using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class StageSecondBossTimedBomb : MonoBehaviour
{
    [Header("爆弾の基本設定")]
    public float fuseDuration = 3.0f;
    public float explosionRadius = 3.0f;
    public float blowSpeed = 25f;
    public GameObject explosionEffect;

    [Header("💓 爆弾本体の鼓動演出設定")]
    public float baseScaleMultiplier = 1.4f;
    public float pulseAmplitude = 0.15f;
    public float minPulseSpeed = 5f;
    public float maxPulseSpeed = 35f;

    [Header("🔴 エネルギーコアの設定")]
    [SerializeField] private GameObject energyCoreObject;
    public Color targetRedColor = new Color(1f, 0f, 0f, 0.6f);
    public float minCoreScaleMultiplier = 0.1f;
    public float maxCoreScaleMultiplier = 3.0f;

    [Header("🎯 予兆・ビジュアル演出の設定")]
    [SerializeField] private GameObject indicatorRoot;
    [SerializeField] private Transform redCircleTransform;
    [SerializeField] private GameObject bombVisual;

    [Header("⚔️ 攻撃用オブジェクトの設定")]
    [SerializeField] private GameObject damageAreaObject;

    private Rigidbody2D rb2d;
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

        // ===================================================================
        // 🛠 {安心設計} 時限爆弾側も、投擲中のめり込みバグを完全に予防します
        // ===================================================================
        if (TryGetComponent<Collider2D>(out var col)) col.enabled = false;

        if (indicatorRoot != null)
        {
            indicatorRoot.transform.SetParent(null);
            indicatorRoot.transform.position = targetPos;
            indicatorRoot.transform.localScale = new Vector3(explosionRadius * 2f, explosionRadius * 2f, 1f);
            indicatorRoot.SetActive(true);
        }
        if (redCircleTransform != null) redCircleTransform.localScale = Vector3.zero;
        if (damageAreaObject != null) damageAreaObject.SetActive(false);
        if (energyCoreObject != null) energyCoreObject.SetActive(true);
    }

    void Start()
    {
        if (!isTossing)
        {
            if (indicatorRoot != null)
            {
                indicatorRoot.SetActive(true);
                indicatorRoot.transform.localScale = new Vector3(explosionRadius * 2f, explosionRadius * 2f, 1f);
            }
            if (redCircleTransform != null) redCircleTransform.localScale = Vector3.zero;
            if (damageAreaObject != null) damageAreaObject.SetActive(false);
            if (energyCoreObject != null) energyCoreObject.SetActive(true);
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

                // ===================================================================
                // 🛠【安心設計】着弾したのでコライダーをオンに戻します
                // ===================================================================
                if (TryGetComponent<Collider2D>(out var col)) col.enabled = true;

                if (bombVisual != null) bombVisual.transform.localRotation = Quaternion.identity;
            }
            return;
        }

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
                        boss.TakeDamage(20f);
                        boss.OnMineCounterHit();
                    }
                    else
                    {
                        boss.TakeDamage(20f);
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
            SyncColliderSize(damageAreaObject, explosionRadius);
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

    void OnDestroy()
    {
        if (indicatorRoot != null) Destroy(indicatorRoot);
        if (coreRenderer != null && coreRenderer.material != null) Destroy(coreRenderer.material);
    }
}