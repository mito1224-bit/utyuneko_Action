using UnityEngine;

public class StageSecondBossDeadState : StageSecondBossBaseState
{
    private float twitchTimer = 0f;
    private float deadRotationAngle = 90f;

    [Header("🎬 シネマティック・タイムライン設定")]
    private float freezeDuration = 0.45f;
    private float freezeTimer = 0f;
    private bool isVelocityApplied = false;

    [Header("💡 デジタル暴走・明滅ノイズ設定")]
    private float deadTimer = 0f;
    private Material whiteFlashMaterial;
    private float flashToggleTimer = 0f;
    private float flashInterval = 0.025f;

    [Header("🎥 特製カメラシェイク設定")]
    private Vector3 originalCameraLocalPos;
    private Transform mainCameraTransform;
    private float cameraShakeMagnitude = 0.35f;

    public StageSecondBossDeadState(StageSecondBossController boss) : base(boss) { }

    public override void Enter()
    {
        twitchTimer = 0f;
        deadTimer = 0f;
        flashToggleTimer = 0f;
        freezeTimer = freezeDuration;
        isVelocityApplied = false;
        boss.isDeadGrounded = false;

        // ⏱️ 世界の時間を超スロー（0.2倍速）にする
        TimeManager.Instance.TriggerGlobalSlowMotion(1.0f, 0.2f);

        Shader guiTextShader = Shader.Find("GUI/Text Shader");
        whiteFlashMaterial = new Material(guiTextShader != null ? guiTextShader : Shader.Find("Sprites/Default"));
        whiteFlashMaterial.color = Color.white;

        Debug.Log("<color=red>💀 ボス：戦闘不能。サイズを初期化し、白飛びシェイクを開始します。</color>");

        SoundManager.Instance.StopLoopSE(boss.gameObject);
        if (boss.bossAnimator != null) boss.bossAnimator.speed = 0f;

        if (boss.TryGetComponent<StageSecondBossHealth>(out var health))
        {
            health.StopFlashAndReset();
            health.hasBarrier = false;
            health.UpdateBarrierVisual();
        }
        boss.ForceResetAllMaterials();

        // ===================================================================
        // 🛠️【修正：巨大化残存バグ完全撃破】
        // 死亡ステートに突入したまさにこの瞬間に、ボスのグラフィックパーツの大きさを
        // コントローラーがAwakeで保存しておいた「本来の初期スケール」へと強制リセット！
        // ===================================================================
        Transform bossVisual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;
        bossVisual.localScale = boss.originalVisualLocalScale;

        // シェイク中は100%真っ白に白飛び固定
        boss.ApplyGlobalFlashMaterial(whiteFlashMaterial);

        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
            originalCameraLocalPos = mainCameraTransform.localPosition;
        }

        if (boss.ultDamageAreaObject != null) boss.ultDamageAreaObject.SetActive(false);

        StageSecondBossTimedBomb[] timedBombs = Object.FindObjectsByType<StageSecondBossTimedBomb>(FindObjectsSortMode.None);
        foreach (var bomb in timedBombs) if (bomb != null) Object.Destroy(bomb.gameObject);

        StageSecondBossMineBomb[] mineBombs = Object.FindObjectsByType<StageSecondBossMineBomb>(FindObjectsSortMode.None);
        foreach (var bomb in mineBombs) if (bomb != null) Object.Destroy(bomb.gameObject);

        // タメの間はまだ物理落下させず、その空中位置に完全フリーズ
        Rigidbody2D rb = boss.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }
    }

    public override void Update()
    {
        // ----------------------------------------------------------------===
        // 🔥【タイムライン前編】シェイク中は「ずっと完全白飛び」し続ける！！
        // ----------------------------------------------------------------===
        if (freezeTimer > 0f)
        {
            freezeTimer -= Time.unscaledDeltaTime;

            // シェイク中はランダム点滅させず、100%真っ白な状態を毎フレーム維持！
            boss.ApplyGlobalFlashMaterial(whiteFlashMaterial);

            if (mainCameraTransform != null)
            {
                float shakeX = Random.Range(-cameraShakeMagnitude, cameraShakeMagnitude);
                float shakeY = Random.Range(-cameraShakeMagnitude, cameraShakeMagnitude);
                mainCameraTransform.localPosition = originalCameraLocalPos + new Vector3(shakeX, shakeY, 0f);
            }

            if (freezeTimer <= 0f)
            {
                if (mainCameraTransform != null) mainCameraTransform.localPosition = originalCameraLocalPos;
                StartKnockbackFalling();
            }
            return;
        }

        // ----------------------------------------------------------------===
        // 🔥【タイムライン後編】シェイク終了後、横倒しになって綺麗に落下＆点滅減衰
        // ----------------------------------------------------------------===
        Transform bossVisual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;

        twitchTimer += Time.unscaledDeltaTime * 12f;
        if (Random.value > 0.85f)
        {
            float twitchX = Mathf.Sin(twitchTimer) * 0.012f;
            float twitchY = Mathf.Cos(twitchTimer * 2.0f) * 0.008f;

            Vector3 baseOffset = new Vector3(0f, boss.stunPivotOffsetY, 0f);
            Vector3 finalRotatedOffset = Quaternion.Euler(0f, 0f, deadRotationAngle) * baseOffset;
            Vector3 correctedCenter = boss.originalVisualLocalPosition + (baseOffset - finalRotatedOffset);

            Vector3 rotatedTwitch = Quaternion.Euler(0f, 0f, deadRotationAngle) * new Vector3(twitchX, twitchY, 0f);
            bossVisual.localPosition = correctedCenter + rotatedTwitch;
        }

        if (!boss.isDeadGrounded)
        {
            deadTimer += Time.unscaledDeltaTime;
            float durationToFade = 1.2f;
            float progress = Mathf.Clamp01(deadTimer / durationToFade);
            float currentFlashChance = Mathf.Lerp(0.80f, 0f, progress);

            flashToggleTimer += Time.unscaledDeltaTime;
            if (flashToggleTimer >= flashInterval)
            {
                flashToggleTimer = 0f;
                if (Random.value < currentFlashChance)
                {
                    boss.ApplyGlobalFlashMaterial(whiteFlashMaterial);
                }
                else
                {
                    boss.ForceResetAllMaterials();
                }
            }
        }
        else
        {
            boss.ForceResetAllMaterials();
        }
    }

    private void StartKnockbackFalling()
    {
        if (isVelocityApplied) return;
        isVelocityApplied = true;

        Transform bossVisual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;
        bossVisual.localRotation = Quaternion.Euler(0f, 0f, deadRotationAngle);

        Vector3 baseOffset = new Vector3(0f, boss.stunPivotOffsetY, 0f);
        Vector3 finalRotatedOffset = Quaternion.Euler(0f, 0f, deadRotationAngle) * baseOffset;
        bossVisual.localPosition = boss.originalVisualLocalPosition + (baseOffset - finalRotatedOffset);

        Rigidbody2D rb = boss.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = boss.stunGravityAmount;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.mass = 1f;

            Transform player = boss.GetPlayerTransform();
            float pushPowerX = 4.2f;
            float pushPowerY = 3.5f;
            float finalKnockbackX = 0f;

            if (player != null)
            {
                finalKnockbackX = boss.transform.position.x > player.position.x ? pushPowerX : -pushPowerX;
            }

            rb.linearVelocity = new Vector2(finalKnockbackX, pushPowerY);
        }
    }

    public override void Exit()
    {
        if (mainCameraTransform != null) mainCameraTransform.localPosition = originalCameraLocalPos;

        Transform visual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;
        visual.localPosition = boss.originalVisualLocalPosition;
        visual.localRotation = boss.originalVisualLocalRotation;

        // 念のため退場時にも初期サイズリセットをかけて安全を保証する
        visual.localScale = boss.originalVisualLocalScale;

        boss.ForceResetAllMaterials();
        if (whiteFlashMaterial != null)
        {
            Object.Destroy(whiteFlashMaterial);
            whiteFlashMaterial = null;
        }
    }
}