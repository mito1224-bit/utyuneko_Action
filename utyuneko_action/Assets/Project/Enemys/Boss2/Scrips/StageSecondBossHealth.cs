using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class StageSecondBossHealth : MonoBehaviour
{
    [Header("基礎ステータス")]
    public float maxHP = 100f;
    public float currentHP;

    [Header("⚙️ UI HPバーとの連動設定")]
    public StageSecondBossHPBar bossHpBar;

    [Header("⚙️ バリアシステムの設定")]
    public bool hasBarrier = true;
    public bool immuneToPlayerWhenBarrierOn = true;
    public GameObject barrierVisualObject;
    public GameObject barrierBreakEffect;

    [Tooltip("🛡️ バリア破壊エフェクトのサイズ倍率設定（インセクターから直接大きさを固定調整できます）")]
    public float barrierBreakEffectScale = 1.0f;

    [Header("⚙️ プレイヤーからの被弾ダメージ設定")]
    public float bombDirectDamage = 15f;
    public float basePlayerDamage = 4f;
    public float playerSpeedDamageMultiplier = 0.4f;
    public float hitStopTime = 0.1f;

    [Header("⚙️ 被弾インターバル（無敵時間）の設定")]
    public float damageInterval = 0.5f;
    private float invincibilityTimer = 0f;

    [Header("⚙️ 被弾時フラッシュ演出の設定")]
    public float damageFlashDuration = 0.08f;
    public Material customFlashMaterial;

    private StageSecondBossController controller;
    private Material defaultFlashMaterial;

    private struct RendererDefaultMat
    {
        public SpriteRenderer sr;
        public SkinnedMeshRenderer smr;
        public MeshRenderer mr;
        public Material origMat;
    }
    private List<RendererDefaultMat> defaultMaterials = new List<RendererDefaultMat>();

    private bool isFlashing = false;
    private Coroutine flashCoroutine;

    void Awake()
    {
        currentHP = maxHP;
        controller = GetComponent<StageSecondBossController>();
        if (controller == null) controller = GetComponentInParent<StageSecondBossController>();
    }

    void Start()
    {
        Shader guiTextShader = Shader.Find("GUI/Text Shader");
        if (guiTextShader != null) defaultFlashMaterial = new Material(guiTextShader) { color = Color.white };
        else defaultFlashMaterial = new Material(Shader.Find("Sprites/Default")) { color = Color.white };

        Transform visualRoot = controller != null ? (controller.ultVisualOffsetObject != null ? controller.ultVisualOffsetObject : controller.transform) : transform;
        defaultMaterials.Clear();
        foreach (var sr in visualRoot.GetComponentsInChildren<SpriteRenderer>(true)) if (sr != null) defaultMaterials.Add(new RendererDefaultMat { sr = sr, origMat = sr.sharedMaterial });
        foreach (var smr in visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (smr != null) defaultMaterials.Add(new RendererDefaultMat { smr = smr, origMat = smr.sharedMaterial });
        foreach (var mr in visualRoot.GetComponentsInChildren<MeshRenderer>(true)) if (mr != null) defaultMaterials.Add(new RendererDefaultMat { mr = mr, origMat = mr.sharedMaterial });

        UpdateBarrierVisual();
    }

    void Update()
    {
        if (invincibilityTimer > 0f)
        {
            invincibilityTimer -= Time.deltaTime;
        }
    }

    public void UpdateBarrierVisual()
    {
        if (barrierVisualObject != null)
        {
            barrierVisualObject.SetActive(hasBarrier);
        }
    }

    public void ResetBarrier()
    {
        hasBarrier = true;
        UpdateBarrierVisual();
    }

    public void StopFlashAndReset()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }
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
        if (controller != null && controller.currentDebugStateName == "StageSecondBossDeadState") return;

        if (hitObj.CompareTag("Player"))
        {
            PlayerController player = hitObj.GetComponent<PlayerController>();
            if (player != null && player.CurrentState == player.StateBurst)
            {
                if (hasBarrier && immuneToPlayerWhenBarrierOn)
                {
                    Debug.Log("<color=blue>🛡️ ボス：バリア作動中！ プレイヤーの攻撃を無効化しました。</color>");
                    return;
                }

                float playerSpeed = player.rb2D != null ? player.rb2D.linearVelocity.magnitude : 0f;
                float finalDamage = basePlayerDamage + (playerSpeed * playerSpeedDamageMultiplier);

                TakeDamage(finalDamage);
            }
            return;
        }

        if (hitObj.TryGetComponent<StageSecondBossTimedBomb>(out var timedBomb) && timedBomb.IsBlownAway)
        {
            ProcessBombHit(hitObj);
            return;
        }

        if (hitObj.TryGetComponent<StageSecondBossMineBomb>(out var mineBomb) && mineBomb.IsBlownAway)
        {
            ProcessBombHit(hitObj);
            return;
        }
    }

    private void ProcessBombHit(GameObject bombObj)
    {
        if (controller != null && controller.currentDebugStateName == "StageSecondBossDeadState") return;

        if (controller != null && controller.currentDebugStateName == "StageSecondBossUltimateState")
        {
            if (controller.StateUltimate != null && controller.StateUltimate.isCounterAcceptable)
            {
                Debug.Log("<color=red>🛡️ 必殺技チャージ中に爆弾直撃！ 確定遮断スタン！</color>");
                UpdateBarrierVisual();
                controller.OnMineCounterHit();
                Destroy(bombObj);

                TimeManager.Instance.TriggerGlobalSlowMotion(1.0f, 0.2f);
                return;
            }

            // ⏳ ウルトチャージ中の通常被弾によるバリア破壊
            if (hasBarrier)
            {
                hasBarrier = false;
                if (barrierBreakEffect != null)
                {
                    GameObject fx = Object.Instantiate(barrierBreakEffect, controller.transform.position, Quaternion.identity);
                    fx.transform.localScale = Vector3.one * barrierBreakEffectScale; // 👈 サイズの適用！
                }
            }

            DamageFlashRoutine();
        }

        // ⏳ 通常状態での爆弾カウンター直撃によるバリア破壊
        if (hasBarrier)
        {
            hasBarrier = false;

            if (barrierBreakEffect != null)
            {
                GameObject fx = Object.Instantiate(barrierBreakEffect, controller.transform.position, Quaternion.identity);
                fx.transform.localScale = Vector3.one * barrierBreakEffectScale; // 👈 サイズの適用！
            }

            UpdateBarrierVisual();
            Debug.Log("<color=green>⚡ 爆弾カウンター直撃！ バリアが剥がれました。</color>");
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(DamageFlashRoutine());

            TimeManager.Instance.TriggerGlobalHitStop(hitStopTime);
        }
        else
        {
            TakeDamage(bombDirectDamage);
        }

        Destroy(bombObj);
    }

    public void TakeDamage(float damage)
    {
        if (controller != null && controller.currentDebugStateName == "StageSecondBossDeadState") return;

        if (controller != null && controller.currentDebugStateName != "StageSecondBossStunState")
        {
            if (invincibilityTimer > 0f)
            {
                Debug.Log("<color=gray>🛡️ ボス：無敵時間中のためダメージを無効化しました。</color>");
                return;
            }
        }

        if (controller != null && controller.currentDebugStateName == "StageSecondBossStunState")
        {
            damage *= controller.stunDamageMultiplier;
        }
        else if (controller != null && controller.currentDebugStateName == "StageSecondBossPhaseTransitionState")
        {
            damage *= controller.phaseTransitionDamageMultiplier;
            Debug.Log($"<color=orange>🛡️ ボス：大咆哮ガード発動中！ 被ダメージを {controller.phaseTransitionDamageMultiplier * 100f}% に軽減しました。</color>");
        }

        currentHP -= damage;

        if (controller != null && controller.currentDebugStateName != "StageSecondBossStunState")
        {
            invincibilityTimer = damageInterval;
        }

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
            if (controller != null) controller.TransitionToState(controller.StateDead);
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

        foreach (var dm in defaultMaterials)
        {
            if (dm.sr != null) { dm.sr.sharedMaterial = dm.origMat; dm.sr.color = Color.white; }
            if (dm.smr != null) dm.smr.sharedMaterial = dm.origMat;
            if (dm.mr != null) dm.mr.sharedMaterial = dm.origMat;
        }

        isFlashing = false;
        flashCoroutine = null;
    }
}