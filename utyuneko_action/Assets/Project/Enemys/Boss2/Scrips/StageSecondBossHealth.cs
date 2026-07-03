using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class StageSecondBossHealth : MonoBehaviour
{
    [Header("基礎ステータス")]
    public float maxHP = 100f;
    public float currentHP;

    [Header("⚙️ バリアシステムの設定")]
    public bool hasBarrier = true;
    public bool immuneToPlayerWhenBarrierOn = true;
    public float bombDirectDamage = 35f;
    public GameObject barrierVisualObject;

    [Header("⚙️ プレイヤーからの被弾ダメージ設定")]
    public float basePlayerDamage = 10f;
    public float playerSpeedDamageMultiplier = 0.8f;

    [Header("⚙️ 被弾時フラッシュ演出の設定")]
    public float damageFlashDuration = 0.08f;
    public Material customFlashMaterial;

    private StageSecondBossController controller;
    private Material defaultFlashMaterial;

    private List<SpriteRenderer> affectedSprites = new List<SpriteRenderer>();
    private List<Material> savedSpriteMaterials = new List<Material>();
    private List<SkinnedMeshRenderer> affectedSkinneds = new List<SkinnedMeshRenderer>();
    private List<Material> savedSkinnedMaterials = new List<Material>();
    private List<MeshRenderer> affectedMeshes = new List<MeshRenderer>();
    private List<Material> savedMeshMaterials = new List<Material>();

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

        UpdateBarrierVisual();
    }

    private void OnCollisionEnter2D(Collision2D collision) { EvaluateCollision(collision.gameObject); }
    private void OnTriggerEnter2D(Collider2D other) { EvaluateCollision(other.gameObject); }

    private void EvaluateCollision(GameObject hitObj)
    {
        // 👤 プレイヤーのバースト攻撃
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

        // 💣 時限爆弾カウンター（プレイヤーが跳ね返したもの）
        if (hitObj.TryGetComponent<StageSecondBossTimedBomb>(out var timedBomb) && timedBomb.IsBlownAway)
        {
            ProcessBombHit(hitObj);
            return;
        }

        // 💣 地雷爆弾カウンター（プレイヤーが跳ね返したもの）
        if (hitObj.TryGetComponent<StageSecondBossMineBomb>(out var mineBomb) && mineBomb.IsBlownAway)
        {
            ProcessBombHit(hitObj);
            return;
        }
    }

    // ===================================================================
    // 🛠️【条件厳密化】爆弾直撃時の処理
    // 必殺技（ウルト）吸引中のみスタンを許可し、通常時は絶対にスタンさせない！
    // ===================================================================
    private void ProcessBombHit(GameObject bombObj)
    {
        // 💡 1. 【唯一無二の確定スタン条件】必殺技（ウルト＝吸い込み吸引）中のみ！
        // それ以外の通常技（クロス、グリッド、追従連撃）の時は、バリアを無視してスタンさせたりしません。
        if (controller != null && controller.currentDebugStateName == "StageSecondBossUltimateState")
        {
            Debug.Log("<color=red>⚠️ 必殺技（ウルト）チャージ中に爆弾直撃！ バリアを巻き添え破壊して確定遮断スタン！</color>");

            hasBarrier = false;    // バリア粉砕
            UpdateBarrierVisual(); // バリアの見た目を消去

            controller.OnMineCounterHit(); // ウルトを強制中断して気絶落下へ
            Destroy(bombObj);
            return;
        }

        // 💡 2. 通常時（ウルト中ではない時）にバリアがある場合 ⇄ 【バリアが剥がれるだけ！スタンせず攻撃続行】
        if (hasBarrier)
        {
            hasBarrier = false;
            UpdateBarrierVisual();
            Debug.Log("<color=green>⚡ 爆弾カウンター直撃！ 通常時のバリアが剥がれました（ボスは攻撃を続行します）。</color>");

            // 被弾の白フラッシュ演出
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(DamageFlashRoutine());
        }
        // 💡 3. 通常時（ウルト中ではない時）にバリアが無い場合 ⇄ 【通常攻撃より高い特大ダメージが入るだけ！スタンせず攻撃続行】
        else
        {
            TakeDamage(bombDirectDamage);
            Debug.Log($"<color=magenta>🔥 バリア無しの生身に爆弾直撃！ 通常より高い特大ダメージ: {bombDirectDamage}（ボスは攻撃を続行します）</color>");
        }

        // 当たった爆弾オブジェクトを消去
        Destroy(bombObj);
    }

    public void TakeDamage(float damage)
    {
        if (controller != null && controller.currentDebugStateName == "StageSecondBossDeadState") return;
        if (controller != null && controller.currentDebugStateName == "StageSecondBossStunState") damage *= controller.stunDamageMultiplier;

        currentHP -= damage;

        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(DamageFlashRoutine());

        if (currentHP <= 0f)
        {
            currentHP = 0f;
            if (controller != null) controller.TransitionToState(controller.StateDead);
        }
    }

    public void UpdateBarrierVisual()
    {
        if (barrierVisualObject != null) barrierVisualObject.SetActive(hasBarrier);
    }

    public void ResetBarrier()
    {
        hasBarrier = true;
        UpdateBarrierVisual();
        Debug.Log("<color=blue>🛡️ ボス：バリアを新しく再展開しました！</color>");
    }

    private IEnumerator DamageFlashRoutine()
    {
        Transform visualRoot = controller != null ? (controller.ultVisualOffsetObject != null ? controller.ultVisualOffsetObject : controller.transform) : transform;
        Material matToUse = customFlashMaterial != null ? customFlashMaterial : defaultFlashMaterial;

        if (!isFlashing)
        {
            isFlashing = true;
            var sprites = visualRoot.GetComponentsInChildren<SpriteRenderer>();
            affectedSprites.Clear(); savedSpriteMaterials.Clear();
            foreach (var sr in sprites) { if (sr != null) { affectedSprites.Add(sr); savedSpriteMaterials.Add(sr.sharedMaterial); } }

            var skinneds = visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>();
            affectedSkinneds.Clear(); savedSkinnedMaterials.Clear();
            foreach (var smr in skinneds) { if (smr != null) { affectedSkinneds.Add(smr); savedSkinnedMaterials.Add(smr.sharedMaterial); } }

            var meshes = visualRoot.GetComponentsInChildren<MeshRenderer>();
            affectedMeshes.Clear(); savedMeshMaterials.Clear();
            foreach (var mr in meshes) { if (mr != null) { affectedMeshes.Add(mr); savedMeshMaterials.Add(mr.sharedMaterial); } }
        }

        if (matToUse != null)
        {
            foreach (var sr in affectedSprites) if (sr != null) sr.sharedMaterial = matToUse;
            foreach (var smr in affectedSkinneds) if (smr != null) smr.sharedMaterial = matToUse;
            foreach (var mr in affectedMeshes) if (mr != null) mr.sharedMaterial = matToUse;
        }

        yield return new WaitForSeconds(damageFlashDuration);

        for (int i = 0; i < affectedSprites.Count; i++)
        {
            if (affectedSprites[i] != null && i < savedSpriteMaterials.Count) { affectedSprites[i].sharedMaterial = savedSpriteMaterials[i]; affectedSprites[i].color = Color.white; }
        }
        for (int i = 0; i < affectedSkinneds.Count; i++) if (affectedSkinneds[i] != null && i < savedSkinnedMaterials.Count) affectedSkinneds[i].sharedMaterial = savedSkinnedMaterials[i];
        for (int i = 0; i < affectedMeshes.Count; i++) if (affectedMeshes[i] != null && i < savedMeshMaterials.Count) affectedMeshes[i].sharedMaterial = savedMeshMaterials[i];

        isFlashing = false;
        flashCoroutine = null;
    }
}