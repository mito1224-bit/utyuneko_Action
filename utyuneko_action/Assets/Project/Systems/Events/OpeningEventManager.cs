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
    private Vector3 originalVisualLocalPos; // 揺れから元に戻すための初期座標
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

                // Y軸を「右向き（wakeUpYAngle）」に固定したまま、Z軸を90度傾けて床に寝かせます！
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

        // 1. 最初は真っ黒な画面のまま1.5秒待つ
        yield return StartCoroutine(Wait(1.5f));

        // 2. まず自動でジワジワと画面を明るく（フェードアウト）させる
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

        // 寝ぼけスタンプ
        yield return StartCoroutine(Speak(playerBubble, ImageBubble.StampType.Confusion, 1.5f));

        // 3. 画面が完全に明るくなった「後」、ボタン連打で体を揺らす
        currentTapCount = 0;
        isWaitingForTaps = true;

        while (currentTapCount < requiredWakeUpTaps)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            {
                currentTapCount++;
                if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
                shakeCoroutine = StartCoroutine(TapJumpShakeRoutine());
            }
            yield return null;
        }
        isWaitingForTaps = false;

        // 4. 連打完了！「ハッ！」として起き上がる
        yield return StartCoroutine(Speak(playerBubble, ImageBubble.StampType.Surprise, 0.8f));

        SoundManager.Instance.PlayBGM(BgmType.Opening, 1.0f);
        SoundManager.Instance.FadeBGMVolume(0.2f, 1.0f);

        // 起き上がりの開始と同時に「isBurst」を解除
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

        // ===================================================================
        // 👀【新設】起き上がった直後の「きょろきょろ（見回し）」演出
        // ===================================================================
        yield return StartCoroutine(Wait(0.2f));

        // 「ここはどこだ？」のハテナスタンプ（!?）を出しつつ見回す
        Coroutine lookAroundSpeak = StartCoroutine(Speak(playerBubble, ImageBubble.StampType.Hatena, 1.8f));

        if (playerVisualTransform != null)
        {
            // ① 左を見渡す (本来の右向きから、指定した角度分だけ左にひねる)
            Quaternion lookLeftRot = Quaternion.Euler(0f, wakeUpYAngle - lookAroundAngleRange, 0f);
            yield return StartCoroutine(RotateVisualSmoothRoutine(lookLeftRot, lookAroundDuration));
            yield return StartCoroutine(Wait(lookAroundPauseTime)); // 見つめたまま少し停止

            // ② 右を見渡す (本来の右向きから、指定した角度分だけ右にひねる)
            Quaternion lookRightRot = Quaternion.Euler(0f, wakeUpYAngle + lookAroundAngleRange, 0f);
            yield return StartCoroutine(RotateVisualSmoothRoutine(lookRightRot, lookAroundDuration));
            yield return StartCoroutine(Wait(lookAroundPauseTime)); // 見つめたまま少し停止

            // ③ 正面（wakeUpYAngle）に向き直る
            Quaternion lookFrontRot = Quaternion.Euler(0f, wakeUpYAngle, 0f);
            yield return StartCoroutine(RotateVisualSmoothRoutine(lookFrontRot, lookAroundDuration));
        }

        yield return lookAroundSpeak; // スタンプが綺麗に消え去るのを同期して待つ
        yield return StartCoroutine(Wait(0.3f));


        // 6. 完全に覚醒して通常状態へ移行
        if (playerController.hoverSensor != null)
        {
            playerController.hoverSensor.enabled = true;
        }

        playerController.TransitionToState(playerController.StateNormal);

        // 起き上がって「よし行くぞ！」のOKスタンプ
        yield return StartCoroutine(Speak(playerBubble, ImageBubble.StampType.OK, 1.0f));

        CompleteOpeningEvent();
    }

    /// <summary>
    /// ✨【新設】きょろきょろ演出用の、滑らかな時間ベースの回転サブコルーチン
    /// </summary>
    private IEnumerator RotateVisualSmoothRoutine(Quaternion targetRot, float duration)
    {
        if (playerVisualTransform == null) yield break;

        float elapsed = 0f;
        Quaternion startRot = playerVisualTransform.localRotation;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float tSmooth = Mathf.SmoothStep(0f, 1f, t); // 首振りの動き始めと終わりに綺麗な緩急をつける

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

        StartCoroutine(FadeOutAndEndRoutine());
    }
}