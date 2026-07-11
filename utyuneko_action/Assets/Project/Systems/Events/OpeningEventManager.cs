using System.Collections;
using UnityEngine;

public class OpeningEventManager : BaseEventManager
{
    [Header("プレイヤーの吹き出し参照")]
    [SerializeField] private ImageBubble playerBubble;

    [Header("起き上がり連打（QTE）設定")]
    [Tooltip("画面が明るくなった後、起き上がるためにボタンを何回押す必要があるか")]
    [SerializeField] private int requiredWakeUpTaps = 3;
    [Tooltip("ボタンを押したときにプレイヤーがピクッと跳ねる強さ")]
    [SerializeField] private float tapJumpForce = 0.2f;

    [Header("起き上がりモーション設定")]
    [Tooltip("起き上がった時に固定してほしい右向きのY軸角度（RescueEventと同じ310fをデフォルトにしています）")]
    [SerializeField] private float wakeUpYAngle = 310f;
    [Tooltip("連打完了後、起き上がるまでにかける時間（秒）")]
    [SerializeField] private float standUpDuration = 1.5f;

    [Header("きょろきょろ演出設定")]
    [Tooltip("起き上がった後、左右を見回す角度の幅（現在の角度から±何度動かすか）")]
    [SerializeField] private float lookAroundAngleRange = 45f;
    [Tooltip("1方向を振り向くのにかける時間（秒）")]
    [SerializeField] private float lookAroundDuration = 0.35f;
    [Tooltip("振り向いた先でピタッと凝視して止まる時間（秒）")]
    [SerializeField] private float lookAroundPauseTime = 0.3f;

    private Transform playerVisualTransform;
    private Vector3 originalVisualLocalPos;
    private int currentTapCount = 0;
    private bool isWaitingForTaps = false;
    private Coroutine shakeCoroutine;

    void Start()
    {
        StartCoroutine(SafeStartRoutine());
        SoundManager.Instance.PlaySE(SeType.EventOpening);
    }

    private IEnumerator SafeStartRoutine()
    {
        yield return null;
        TriggerOpening();
    }

    private void TriggerOpening()
    {
        StartEvent();

        if (skipFadeCanvasGroup != null)
        {
            skipFadeCanvasGroup.alpha = 1f;
            skipFadeCanvasGroup.blocksRaycasts = false;
        }

        if (playerController != null && playerController.visualManager != null)
        {
            playerVisualTransform = playerController.visualManager.playerVisual;
            if (playerVisualTransform != null)
            {
                originalVisualLocalPos = playerVisualTransform.localPosition;
                playerVisualTransform.localRotation = Quaternion.Euler(0f, 260f, 90f);
            }

            if (playerController.hoverSensor != null)
            {
                playerController.hoverSensor.enabled = false;
            }

            if (playerController.anim != null)
            {
                playerController.anim.SetBool("isBurst", true);
            }

            playerController.TransitionToState(playerController.StateNone);
        }

        activeTimelineCoroutine = StartCoroutine(OpeningTimelineRoutine());
    }

    private IEnumerator OpeningTimelineRoutine()
    {
        if (skipFadeCanvasGroup == null && spawnedCanvasInstance != null)
        {
            skipFadeCanvasGroup = spawnedCanvasInstance.GetComponentInChildren<CanvasGroup>(true);
            if (skipFadeCanvasGroup != null) skipFadeCanvasGroup.alpha = 1f;
        }

        yield return StartCoroutine(Wait(1.5f));

        float fadeTimer = 0f;
        float openingFadeDuration = 2.0f;
        while (fadeTimer < openingFadeDuration)
        {
            fadeTimer += Time.unscaledDeltaTime;
            if (skipFadeCanvasGroup != null)
            {
                skipFadeCanvasGroup.alpha = Mathf.Lerp(1f, 0f, fadeTimer / openingFadeDuration);
            }
            yield return null;
        }
        if (skipFadeCanvasGroup != null) skipFadeCanvasGroup.alpha = 0f;

        yield return StartCoroutine(Wait(0.3f));

        playerBubble.ShowStamp(ImageBubble.StampType.Confusion);
        SoundManager.Instance.PlayLoopSE(playerController.gameObject, SeType.HosaConfusion);

        currentTapCount = 0;
        isWaitingForTaps = true;

        // 💡 連打の取りこぼしを防ぐための手動フラグ管理変数
        bool wasPressedLastFrame = false;

        while (currentTapCount < requiredWakeUpTaps)
        {
            // ===================================================================
            // 🛠️【バグ修正：100%取りこぼさない鉄壁のエッジ検出連打システム】
            // ===================================================================
            bool isPressedThisFrame = InputManager.Instance.Event.Jump.IsPressed();

            // 「前フレームで押されていなくて、今フレームで新しく押された瞬間」だけを検知！
            if (isPressedThisFrame && !wasPressedLastFrame)
            {
                currentTapCount++;
                if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
                shakeCoroutine = StartCoroutine(TapJumpShakeRoutine());
            }

            // 今フレームの状態を記録して、次のフレームへバトンパス
            wasPressedLastFrame = isPressedThisFrame;

            yield return null;
        }
        isWaitingForTaps = false;

        SoundManager.Instance.StopLoopSE(playerController.gameObject);

        yield return StartCoroutine(Speak(playerBubble, ImageBubble.StampType.Surprise, 0.8f));

        SoundManager.Instance.PlayBGM(BgmType.StageSelect, 3.0f);
        SoundManager.Instance.FadeBGMVolume(0.2f, 2.0f);

        if (playerController.anim != null)
        {
            playerController.anim.SetBool("isBurst", false);
        }

        if (playerVisualTransform != null)
        {
            float rotTimer = 0f;
            Quaternion startRot = playerVisualTransform.localRotation;
            Quaternion targetRot = Quaternion.Euler(0f, wakeUpYAngle, 0f);

            while (rotTimer < standUpDuration)
            {
                rotTimer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(rotTimer / standUpDuration);
                float tSmooth = Mathf.SmoothStep(0f, 1f, t);

                playerVisualTransform.localRotation = Quaternion.Lerp(startRot, targetRot, tSmooth);
                yield return null;
            }
            playerVisualTransform.localRotation = targetRot;
        }

        yield return StartCoroutine(Wait(0.2f));

        Coroutine lookAroundSpeak = StartCoroutine(Speak(playerBubble, ImageBubble.StampType.Hatena, 1.8f));

        if (playerVisualTransform != null)
        {
            Quaternion lookLeftRot = Quaternion.Euler(0f, wakeUpYAngle - lookAroundAngleRange, 0f);
            yield return StartCoroutine(RotateVisualSmoothRoutine(lookLeftRot, lookAroundDuration));
            yield return StartCoroutine(Wait(lookAroundPauseTime));

            Quaternion lookRightRot = Quaternion.Euler(0f, wakeUpYAngle + lookAroundAngleRange, 0f);
            yield return StartCoroutine(RotateVisualSmoothRoutine(lookRightRot, lookAroundDuration));
            yield return StartCoroutine(Wait(lookAroundPauseTime));

            Quaternion lookFrontRot = Quaternion.Euler(0f, wakeUpYAngle, 0f);
            yield return StartCoroutine(RotateVisualSmoothRoutine(lookFrontRot, lookAroundDuration));
        }

        yield return lookAroundSpeak;
        yield return StartCoroutine(Wait(0.3f));

        if (playerController.hoverSensor != null)
        {
            playerController.hoverSensor.enabled = true;
        }

        playerController.TransitionToState(playerController.StateNormal);

        yield return StartCoroutine(Speak(playerBubble, ImageBubble.StampType.OK, 1.0f));

        CompleteOpeningEvent();
    }

    private IEnumerator RotateVisualSmoothRoutine(Quaternion targetRot, float duration)
    {
        if (playerVisualTransform == null) yield break;

        float elapsed = 0f;
        Quaternion startRot = playerVisualTransform.localRotation;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float tSmooth = Mathf.SmoothStep(0f, 1f, t);

            playerVisualTransform.localRotation = Quaternion.Lerp(startRot, targetRot, tSmooth);
            yield return null;
        }
        playerVisualTransform.localRotation = targetRot;
    }

    private IEnumerator TapJumpShakeRoutine()
    {
        float elapsed = 0f;
        float duration = 0.12f;
        Vector3 startWorldPos = playerController.transform.position;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            float jumpCurve = Mathf.Sin(t * Mathf.PI);
            Vector3 worldOffset = Vector3.up * tapJumpForce * jumpCurve;

            playerVisualTransform.position = startWorldPos + worldOffset;
            yield return null;
        }

        playerVisualTransform.localPosition = originalVisualLocalPos;
        playerVisualTransform.localRotation = Quaternion.Euler(0f, 260f, 90f);
    }

    protected override void OnSkipWarp()
    {
        isWaitingForTaps = false;
        if (playerBubble != null) playerBubble.StartFadeOut();

        if (playerVisualTransform != null)
        {
            playerVisualTransform.localRotation = Quaternion.Euler(0f, wakeUpYAngle, 0f);
            playerVisualTransform.localPosition = originalVisualLocalPos;
        }

        GameManager.Instance.AdvanceStoryPhase();
    }

    protected override void OnEventFullyCompleted()
    {
        Debug.Log("ゲームスタート！！！");
    }

    private void CompleteOpeningEvent()
    {
        SoundManager.Instance.FadeBGMVolume(1.0f, 1.0f);

        if (playerVisualTransform != null)
        {
            playerVisualTransform.localRotation = Quaternion.Euler(0f, wakeUpYAngle, 0f);
            playerVisualTransform.localPosition = originalVisualLocalPos;
        }

        GameManager.Instance.AdvanceStoryPhase();

        StartCoroutine(FadeOutAndEndRoutine());
    }
}