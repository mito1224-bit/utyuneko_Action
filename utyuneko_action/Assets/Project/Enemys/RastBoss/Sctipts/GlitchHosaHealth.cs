using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 崩壊した補佐：HPおよびバリアシステム管理（ダメージ白飛び完全復旧・最終決定版）
/// </summary>
public class GlitchHosaHealth : MonoBehaviour
{
    [Header("基礎ステータス")]
    public float maxHP = 150f;
    public float currentHP;

    [Header("⚙️ UI HPバーとの連動設定")]
    public StageSecondBossHPBar bossHpBar;

    [Header("⚙️ バリアシステムの設定")]
    public bool hasBarrier = false;
    public bool immuneToPlayerWhenBarrierOn = true;
    public GameObject barrierVisualObject;
    public GameObject barrierBreakEffect;
    public float barrierBreakEffectScale = 1.0f;

    [Header("⚙️ プレイヤーからの被弾ダメージ設定")]
    public float bombDirectDamage = 20f;
    public float basePlayerDamage = 4f;
    public float playerSpeedDamageMultiplier = 0.4f;
    public float hitStopTime = 0.1f;

    [Header("⚙️ 被弾時フラッシュ演出の設定")]
    public float damageFlashDuration = 0.08f;
    public Material customFlashMaterial;

    private GlitchHosaController controller;
    private Material defaultFlashMaterial;

    private struct RendererDefaultMat
    {
        public SpriteRenderer sr;
        public SkinnedMeshRenderer smr;
        public MeshRenderer mr;
        public Material origMat;
    }
    private List<RendererDefaultMat> defaultMaterials = new List<RendererDefaultMat>();

    public bool isFlashing { get; private set; } = false;
    private Coroutine flashCoroutine;

    void Awake()
    {
        currentHP = maxHP;
        // 👑【バグ修正①】子オブジェクト階層にアタッチされていても確実に親からコントローラーを検出できるようにガードロック！
        controller = GetComponent<GlitchHosaController>();
        if (controller == null) controller = GetComponentInParent<GlitchHosaController>();
    }

    void Start()
    {
        Shader guiTextShader = Shader.Find("GUI/Text Shader");
        defaultFlashMaterial = new Material(guiTextShader != null ? guiTextShader : Shader.Find("Sprites/Default")) { color = Color.white };

        Transform visualRoot = controller != null ? (controller.ultVisualOffsetObject != null ? controller.ultVisualOffsetObject : controller.transform) : transform;
        defaultMaterials.Clear();
        foreach (var sr in visualRoot.GetComponentsInChildren<SpriteRenderer>(true)) if (sr != null) defaultMaterials.Add(new RendererDefaultMat { sr = sr, origMat = sr.sharedMaterial });
        foreach (var smr in visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (smr != null) defaultMaterials.Add(new RendererDefaultMat { smr = smr, origMat = smr.sharedMaterial });
        foreach (var mr in visualRoot.GetComponentsInChildren<MeshRenderer>(true)) if (mr != null) defaultMaterials.Add(new RendererDefaultMat { mr = mr, origMat = mr.sharedMaterial });

        ResetBarrier();
    }

    public void UpdateBarrierVisual() { if (barrierVisualObject != null) barrierVisualObject.SetActive(hasBarrier); }

    public void ResetBarrier()
    {
        if (controller != null && controller.isReflectionStolen)
        {
            hasBarrier = true;
        }
        else
        {
            hasBarrier = false;
        }
        UpdateBarrierVisual();
    }

    public void StopFlashAndReset(bool force = false)
    {
        if (isFlashing && !force) return;

        if (flashCoroutine != null) { StopCoroutine(flashCoroutine); flashCoroutine = null; }
        isFlashing = false;
        foreach (var dm in defaultMaterials)
        {
            if (dm.sr != null) { dm.sr.sharedMaterial = dm.origMat; dm.sr.color = Color.white; }
            if (dm.smr != null) dm.smr.sharedMaterial = dm.origMat;
            if (dm.mr != null) dm.mr.sharedMaterial = dm.origMat;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision) { EvaluateCollision(collision.gameObject); }
    private void OnTriggerEnter2D(Collider2D other) { EvaluateCollision(other.gameObject); }

    private void EvaluateCollision(GameObject hitObj)
    {
        if (controller != null && controller.currentDebugStateName == "GlitchHosaDeadState") return;

        if (hitObj.CompareTag("Player"))
        {
            if (controller != null && controller.isBackRallyMode)
            {
                Debug.Log("<color=magenta>🛡️ 補佐：奥に退避しているため、直接攻撃は届かない！</color>");
                return;
            }

            PlayerController player = hitObj.GetComponent<PlayerController>();
            if (player != null && player.CurrentState == player.StateBurst)
            {
                if (hasBarrier && immuneToPlayerWhenBarrierOn)
                {
                    Debug.Log("<color=blue>🛡️ 補佐：バリア作動中！プレイヤーの直接攻撃を完全ブロック！</color>");
                    return;
                }

                float playerSpeed = player.rb2D != null ? player.rb2D.linearVelocity.magnitude : 0f;
                float finalDamage = basePlayerDamage + (playerSpeed * playerSpeedDamageMultiplier);
                TakeDamage(finalDamage);
            }
            return;
        }
    }

    public void ProcessDirectRallyHit(float damage)
    {
        if (controller != null && controller.currentDebugStateName == "GlitchHosaStunState") return;

        if (hasBarrier)
        {
            hasBarrier = false;
            if (barrierBreakEffect != null)
            {
                GameObject fx = Instantiate(barrierBreakEffect, controller.transform.position, Quaternion.identity);
                fx.transform.localScale = Vector3.one * barrierBreakEffectScale;
            }
            UpdateBarrierVisual();
        }

        TakeDamage(damage);

        if (controller != null && controller.isBackRallyMode)
        {
            controller.StartCoroutine(controller.KnockbackToStageFrontRoutine());
        }
        else
        {
            controller.TransitionToState(controller.StateStun);
        }
    }

    public void TakeDamage(float damage)
    {
        if (controller != null && controller.currentDebugStateName == "GlitchHosaDeadState") return;

        if (controller != null && controller.currentDebugStateName == "GlitchHosaStunState")
        {
            damage *= controller.stunDamageMultiplier;
        }

        currentHP -= damage;
        ShakeTarget.Instance.Shake(0.2f, 1.5f);

        if (bossHpBar != null)
        {
            bossHpBar.UpdateHP(currentHP);
            bossHpBar.ShakeBar(0.2f, 12f);
        }

        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(DamageFlashRoutine());

        if (currentHP <= 0f)
        {
            currentHP = 0f;
            Debug.Log("💀 補佐撃破！");
        }
        else
        {
            TimeManager.Instance.TriggerGlobalHitStop(hitStopTime);
        }
    }

    private IEnumerator DamageFlashRoutine()
    {
        isFlashing = true;
        Material matToUse = customFlashMaterial != null ? customFlashMaterial : defaultFlashMaterial;

        // 👑【バグ修正②】ボス2と全く同じ方法に修正！
        // 全てパーツに対してフラッシュ用マテリアル（matToUse）が完璧に適用されるように直しました！
        if (matToUse != null)
        {
            foreach (var dm in defaultMaterials)
            {
                if (dm.sr != null) dm.sr.sharedMaterial = matToUse;
                if (dm.smr != null) dm.smr.sharedMaterial = matToUse;
                if (dm.mr != null) dm.mr.sharedMaterial = matToUse;
            }
        }
        yield return new WaitForSeconds(damageFlashDuration);

        StopFlashAndReset(true);
    }
}