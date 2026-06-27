using System.Collections;
using UnityEngine;

public class OpeningEventManager : BaseEventManager
{
    [Header("💬 プレイヤーの吹き出し参照")]
    [SerializeField] private ImageBubble playerBubble;

    [Header("🎮 起き上がり連打（QTE）設定")]
    [Tooltip("画面が明るくなった後、起き上がるためにボタンを何回押す必要があるか")]
    [SerializeField] private int requiredWakeUpTaps = 5;
    [Tooltip("ボタンを押したときにプレイヤーがピクッと跳ねる強さ")]
    [SerializeField] private float tapJumpForce = 0.4f;

    [Header("🎬 起き上がりモーション設定")]
    [Tooltip("起き上がった時に固定してほしい右向きのY軸角度（RescueEventと同じ310fをデフォルトにしています）")]
    [SerializeField] private float wakeUpYAngle = 310f; // ✨【新設】右向き角度の固定用
    [Tooltip("連打完了後、起き上がるまでにかける時間（秒）")]
    [SerializeField] private float standUpDuration = 1.5f;
    [Tooltip("起き上がる時の回転の滑らかさ")]
    [SerializeField] private float standUpSmoothing = 5.0f;

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
                playerVisualTransform.localRotation = Quaternion.Euler(0f, wakeUpYAngle, 90f);
            }
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

        // 💤 寝ぼけスタンプ
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
        SoundManager.Instance.FadeBGMVolume(0.2f, 0.0f);

        if (playerVisualTransform != null)
        {
            float rotTimer = 0f;
            // 起き上がった時の最終目標角度（Y軸=右向き、X・Z軸=0度）
            Quaternion targetRot = Quaternion.Euler(0f, wakeUpYAngle, 0f);

            while (rotTimer < standUpDuration)
            {
                rotTimer += Time.unscaledDeltaTime;

                // 右を向いた状態のまま、滑らかに起き上がらせる！
                playerVisualTransform.localRotation = Quaternion.Lerp(
                    playerVisualTransform.localRotation,
                    targetRot,
                    Time.deltaTime * standUpSmoothing
                );
                yield return null;
            }
            playerVisualTransform.localRotation = targetRot;
        }

        // 起き上がって「よし行くぞ！」のOKスタンプ
        yield return StartCoroutine(Speak(playerBubble, ImageBubble.StampType.OK, 1.0f));

        CompleteOpeningEvent();
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

        // 戻すときも右向き角度（wakeUpYAngle）がズレないように保護
        playerVisualTransform.localPosition = originalVisualLocalPos;
        playerVisualTransform.localRotation = Quaternion.Euler(0f, wakeUpYAngle, 90f);
    }

    protected override void OnSkipWarp()
    {
        isWaitingForTaps = false;
        if (playerBubble != null) playerBubble.StartFadeOut();

        if (playerVisualTransform != null)
        {
            // スキップ時も一瞬で右向き直立にする
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

        EndEvent();
    }
}