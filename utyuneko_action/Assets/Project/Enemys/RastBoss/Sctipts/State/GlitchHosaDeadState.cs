using UnityEngine;
using System.Collections;

/// <summary>
/// 👑 崩壊した補佐：死亡（撃破時）シネマティック演出ステート
/// 👑【のけぞり15度固定版】顔が地面に埋まらないよう、指定された角度を完全に維持して綺麗に自由落下します。
/// </summary>
public class GlitchHosaDeadState : GlitchHosaBaseState
{
    private float twitchTimer = 0f;

    private float freezeDuration = 0.45f;
    private float freezeTimer = 0f;
    private bool isVelocityApplied = false;

    private float deadTimer = 0f;
    private Material whiteFlashMaterial;
    private float flashToggleTimer = 0f;
    private float flashInterval = 0.025f;

    private Vector3 originalCameraLocalPos;
    private Transform mainCameraTransform;
    private float cameraShakeMagnitude = 0.35f;

    private bool isEventTriggered = false;

    public GlitchHosaDeadState(GlitchHosaController boss) : base(boss) { }

    public override void Enter()
    {
        twitchTimer = 0f;
        deadTimer = 0f;
        flashToggleTimer = 0f;
        freezeTimer = freezeDuration;
        isVelocityApplied = false;
        isEventTriggered = false;
        boss.isDeadGrounded = false;

        boss.SetAllDamageSourcesEnabled(false);
        boss.HideBossStamp();

        TimeManager.Instance.TriggerGlobalSlowMotion(1.0f, 0.2f);
        SoundManager.Instance.StopBGM(1.0f);

        Shader guiTextShader = Shader.Find("GUI/Text Shader");
        whiteFlashMaterial = new Material(guiTextShader != null ? guiTextShader : Shader.Find("Sprites/Default"));
        whiteFlashMaterial.color = Color.white;

        Debug.Log("<color=red>💀 補佐：システムダウン。正面ちょい上向きのまま落下を開始します。</color>");

        SoundManager.Instance.StopLoopSE(boss.gameObject);
        if (boss.bossAnimator != null) boss.bossAnimator.speed = 0f;

        boss.ForceResetAllMaterials();
        boss.targetScale = boss.originalVisualLocalScale;

        Transform player = boss.GetPlayerTransform();
        if (player != null)
        {
            boss.targetYRotation = boss.transform.position.x > player.position.x ? boss.dashYRotationLeft : boss.dashYRotationRight;
        }
        else
        {
            boss.targetYRotation = boss.defaultYRotation;
        }

        // 👑 指定された正面ちょい上の角度（deadXRotation = 15.0f）をカチッと初期設定！
        boss.targetXRotation = boss.deadXRotation;
        boss.targetZRotation = 0f;
        boss.targetVisualOffset = Vector3.zero;

        boss.ApplyGlobalFlashMaterial(whiteFlashMaterial);

        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
            originalCameraLocalPos = mainCameraTransform.localPosition;
        }

        Rigidbody2D rb = boss.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }

        ShakeTarget.Instance.Shake(3.0f, 2.0f);
    }

    public override void Update()
    {
        if (freezeTimer > 0f)
        {
            freezeTimer -= Time.unscaledDeltaTime;
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
        // 🔥【タイムライン後編】のけぞり姿勢を維持したまま放物線落下
        // ----------------------------------------------------------------===
        twitchTimer += Time.unscaledDeltaTime * 12f;

        if (Random.value > 0.85f && !boss.isDeadGrounded)
        {
            float twitchX = Mathf.Sin(twitchTimer) * 0.015f;
            float twitchY = Mathf.Cos(twitchTimer * 2.0f) * 0.015f;
            boss.targetVisualOffset = new Vector3(twitchX, twitchY, 0f);
        }
        else if (boss.isDeadGrounded)
        {
            boss.targetVisualOffset = Vector3.zero;
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

            if (!isEventTriggered)
            {
                isEventTriggered = true;
                if (boss.postBossEventTrigger != null)
                {
                    boss.postBossEventTrigger.SetActive(true);
                    Debug.Log("<color=green>🎬 BossDead：ボス撃破後イベント用トリガーオブジェクトをアクティブ化しました！</color>");
                }
            }
        }
    }

    private void StartKnockbackFalling()
    {
        if (isVelocityApplied) return;
        isVelocityApplied = true;

        // 👑 落下中も角度を完璧に固定ホールドして埋まりを防止！
        boss.targetXRotation = boss.deadXRotation;
        boss.targetZRotation = 0f;

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

        boss.targetXRotation = boss.defaultXRotation;
        boss.targetYRotation = boss.defaultYRotation;
        boss.targetZRotation = 0f;
        boss.targetVisualOffset = Vector3.zero;
        boss.targetScale = boss.originalVisualLocalScale;

        boss.ForceResetAllMaterials();
        if (whiteFlashMaterial != null)
        {
            Object.Destroy(whiteFlashMaterial);
            whiteFlashMaterial = null;
        }
    }
}