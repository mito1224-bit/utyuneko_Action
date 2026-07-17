using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class GlitchHosaTimedBomb : MonoBehaviour
{
    [Header("爆弾の基本設定")]
    public float fuseDuration = 3.0f;
    public float explosionRadius = 3.0f;
    public float blowSpeed = 75f;

    [Tooltip("👑 ボスに直撃した際のダメージ量です。インスペクターからいつでも調整可能です！")]
    public float bombDirectDamage = 20f; // 👈 【修正：復活】脱落していた変数定義をここに追加！

    public GameObject explosionEffect;

    [Tooltip("💥 爆発エフェクトのサイズ倍率")]
    public float explosionEffectScaleMultiplier = 1.0f;

    [Header("💓 爆弾本体の鼓動演出設定")]
    public float baseScaleMultiplier = 1.4f;
    public float pulseAmplitude = 0.15f;
    public float minPulseSpeed = 5f;
    public float maxPulseSpeed = 35f;

    [Header("🎯 予兆・ビジュアル演出の設定")]
    [SerializeField] private GameObject indicatorRoot;
    [SerializeField] private Transform redCircleTransform;
    [SerializeField] private GameObject bombVisual;

    [Header("⚔️ 攻撃用オブジェクトの設定")]
    [SerializeField] private GameObject damageAreaObject;

    [Header("⚙️ ボス共通・赤フラッシュ点滅の設定")]
    public float startFlashInterval = 0.35f;
    public float endFlashInterval = 0.05f;

    // ラスボス専用フラグ：着地した瞬間に即時爆発させる
    [HideInInspector] public bool explodeOnLand = false;

    private List<SpriteRenderer> affectedSprites = new List<SpriteRenderer>();
    private List<Material> savedSpriteMaterials = new List<Material>();
    private List<SkinnedMeshRenderer> affectedSkinneds = new List<SkinnedMeshRenderer>();
    private List<Material> savedSkinMaterials = new List<Material>();
    private List<MeshRenderer> affectedMeshes = new List<MeshRenderer>();
    private List<Material> savedMeshMaterials = new List<Material>();

    private Material redFlashMaterial;
    private bool isSetupCompleted = false;
    private float blinkTimer = 0f;
    private bool isFlashOn = false;

    private Rigidbody2D rb2d;
    private float fuseTimer = 0f;
    private int groundLayerId;

    private Vector3 originalVisualScale = Vector3.one;
    private float pulsePhase = 0f;

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
    }

    void Start()
    {
        Shader guiTextShader = Shader.Find("GUI/Text Shader");
        redFlashMaterial = new Material(guiTextShader != null ? guiTextShader : Shader.Find("Sprites/Default"));
        redFlashMaterial.color = new Color(1f, 0.15f, 0.15f, 1f);

        Transform rootToSearch = bombVisual != null ? bombVisual.transform : transform;

        affectedSprites.Clear(); savedSpriteMaterials.Clear();
        foreach (var sr in rootToSearch.GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr != null) { affectedSprites.Add(sr); savedSpriteMaterials.Add(sr.sharedMaterial); }
        }
        affectedSkinneds.Clear(); savedSkinMaterials.Clear();
        foreach (var smr in rootToSearch.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (smr != null) { affectedSkinneds.Add(smr); savedSkinMaterials.Add(smr.sharedMaterial); }
        }
        affectedMeshes.Clear(); savedMeshMaterials.Clear();
        foreach (var mr in rootToSearch.GetComponentsInChildren<MeshRenderer>())
        {
            if (mr != null) { affectedMeshes.Add(mr); savedMeshMaterials.Add(mr.sharedMaterial); }
        }

        isSetupCompleted = true;

        if (!isTossing)
        {
            if (indicatorRoot != null)
            {
                indicatorRoot.SetActive(true);
                indicatorRoot.transform.localScale = new Vector3(explosionRadius * 2f, explosionRadius * 2f, 1f);
            }
            if (redCircleTransform != null) redCircleTransform.localScale = Vector3.zero;
            if (damageAreaObject != null) damageAreaObject.SetActive(false);
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
                if (TryGetComponent<Collider2D>(out var col)) col.enabled = true;
                if (bombVisual != null) bombVisual.transform.localRotation = Quaternion.identity;

                if (explodeOnLand)
                {
                    NormalExplode();
                }
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

            float currentInterval = Mathf.Lerp(startFlashInterval, endFlashInterval, progress);

            blinkTimer += Time.deltaTime;
            if (blinkTimer >= currentInterval)
            {
                blinkTimer = 0f;
                isFlashOn = !isFlashOn;
                ApplyRedFlash(isFlashOn);
            }
        }

        if (fuseTimer >= fuseDuration) NormalExplode();
    }

    private void ApplyRedFlash(bool on)
    {
        if (!isSetupCompleted) return;

        if (on && redFlashMaterial != null)
        {
            foreach (var sr in affectedSprites) if (sr != null) sr.sharedMaterial = redFlashMaterial;
            foreach (var smr in affectedSkinneds) if (smr != null) smr.sharedMaterial = redFlashMaterial;
            foreach (var mr in affectedMeshes) if (mr != null) mr.sharedMaterial = redFlashMaterial;
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
        if (IsBlownAway) return;

        if (hitObj.CompareTag("Player"))
        {
            PlayerController player = hitObj.GetComponent<PlayerController>();
            if (player != null && player.CurrentState == player.StateBurst)
            {
                ApplyRedFlash(false);
                player.OnEnemyKilledInBurst();

                SoundManager.Instance.PlaySE(SeType.PlayerBurstBegin);

                StartCoroutine(FlyToBossArcRoutine(player.rb2D));
            }
            else
            {
                NormalExplode();
            }
        }
    }

    private IEnumerator FlyToBossArcRoutine(Rigidbody2D playerRb)
    {
        IsBlownAway = true;
        if (indicatorRoot != null) Destroy(indicatorRoot);

        Collider2D myCol = GetComponent<Collider2D>();
        if (myCol == null) myCol = GetComponentInChildren<Collider2D>();
        if (myCol != null) myCol.enabled = false;

        if (rb2d != null)
        {
            rb2d.bodyType = RigidbodyType2D.Kinematic;
            rb2d.linearVelocity = Vector2.zero;
            rb2d.angularVelocity = 0f;
        }

        GameObject bossObj = null;
        GlitchHosaController hosa = Object.FindFirstObjectByType<GlitchHosaController>();

        if (hosa != null)
        {
            bossObj = hosa.gameObject;
        }

        Vector3 startPos = transform.position;
        float duration = 0.75f;
        float arcHeight = 5.0f;
        float t = 0f;

        bool hitTriggered = false;

        if (bossObj != null)
        {
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float easedT = Mathf.Clamp01(t);

                Vector3 targetPos = bossObj.transform.position;

                Vector3 currentPos = Vector3.Lerp(startPos, targetPos, easedT);
                currentPos.y += Mathf.Sin(easedT * Mathf.PI) * arcHeight;

                transform.position = currentPos;

                if (bombVisual != null)
                {
                    bombVisual.transform.Rotate(Vector3.forward, 720f * Time.deltaTime);
                }

                float dist3D = Vector3.Distance(transform.position, targetPos);
                if (dist3D < 1.2f && !hitTriggered)
                {
                    hitTriggered = true;
                    break;
                }

                yield return null;
            }

            transform.position = bossObj.transform.position;

            if (hosa != null)
            {
                GlitchHosaHealth hosaHealth = hosa.GetComponent<GlitchHosaHealth>();
                if (hosaHealth != null)
                {
                    hosaHealth.ProcessDirectRallyHit(bombDirectDamage);
                }
            }
        }
        else
        {
            Vector2 flyDirection = Vector2.right;
            if (playerRb != null && playerRb.linearVelocity.magnitude > 0.1f)
            {
                flyDirection = playerRb.linearVelocity.normalized;
            }
            if (rb2d != null)
            {
                rb2d.bodyType = RigidbodyType2D.Dynamic;
                rb2d.linearVelocity = flyDirection * blowSpeed;
            }
            yield return new WaitForSeconds(1.0f);
        }

        ExecuteExplosionCore();
    }

    private void ExecuteExplosionCore()
    {
        ApplyRedFlash(false);

        rb2d.linearVelocity = Vector2.zero;
        rb2d.bodyType = RigidbodyType2D.Kinematic;
        if (TryGetComponent<Collider2D>(out var col)) col.enabled = false;

        if (bombVisual != null) bombVisual.SetActive(false);

        SoundManager.Instance.PlaySE(SeType.EnemyExplosion);

        ShakeTarget.Instance.Shake(0.2f, 1.0f);

        if (explosionEffect != null)
        {
            GameObject fxObj = Instantiate(explosionEffect, transform.position, Quaternion.identity);
            float diameter = explosionRadius * 2f * explosionEffectScaleMultiplier;
            fxObj.transform.localScale = new Vector3(diameter, diameter, 1f);
        }

        if (damageAreaObject != null)
        {
            damageAreaObject.transform.SetParent(null);
            SyncColliderSize(damageAreaObject, explosionRadius);

            if (IsBlownAway)
            {
                var areaDs = damageAreaObject.GetComponent<DamageSource>();
                if (areaDs != null) areaDs.enabled = false;
            }

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
        ApplyRedFlash(false);
        if (redFlashMaterial != null) Destroy(redFlashMaterial);
    }
}