using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BaseEventManager : MonoBehaviour
{
    [Header("🎬 スキップ・フェード設定")]
    [Tooltip("このイベントで長押しスキップを許可するかどうか（オープニング等はチェックを外す）")]
    [SerializeField] private bool allowSkip = true;

    [SerializeField] protected float defaultDisplayTime = 1.5f;
    [SerializeField] private float requiredSkipHoldTime = 0.8f;
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeOutSpeed = 2.0f;

    [Header("📦 動的生成するUIプレハブの設定")]
    [SerializeField] private GameObject eventCanvasPrefab;

    protected GameObject spawnedCanvasInstance;
    protected CanvasGroup skipCircleGroup;
    protected Image skipCircleGauge;
    protected CanvasGroup skipFadeCanvasGroup;

    protected Transform playerTransform;
    protected PlayerController playerController;

    protected Coroutine activeTimelineCoroutine;

    private float skipHoldTimer = 0f;
    private bool isEventActive = false;
    private bool isEventSkipped = false;
    private bool isFadingIn = false;

    protected virtual void Awake()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerController = playerObj.GetComponent<PlayerController>();
        }
    }

    protected void StartEvent()
    {
        if (eventCanvasPrefab == null)
        {
            Debug.LogError($"[{gameObject.name}] eventCanvasPrefab（UIプレハブ）がセットされていません！");
            return;
        }

        spawnedCanvasInstance = Instantiate(eventCanvasPrefab, this.transform);

        CanvasGroup[] allCanvasGroups = spawnedCanvasInstance.GetComponentsInChildren<CanvasGroup>(true);
        foreach (var cg in allCanvasGroups)
        {
            if (cg.gameObject.name == "SkipFadeImage") skipFadeCanvasGroup = cg;
            if (cg.gameObject.name == "SkipCircle_Group") skipCircleGroup = cg;
        }

        Image[] allImages = spawnedCanvasInstance.GetComponentsInChildren<Image>(true);
        foreach (var img in allImages)
        {
            if (img.gameObject.name == "Gauge_Circle") skipCircleGauge = img;
        }

        if (skipFadeCanvasGroup != null)
        {
            skipFadeCanvasGroup.alpha = 0f;
            skipFadeCanvasGroup.blocksRaycasts = false;
        }
        if (skipCircleGroup != null) skipCircleGroup.alpha = 0f;
        if (skipCircleGauge != null) skipCircleGauge.fillAmount = 0f;

        isEventActive = true;
        isEventSkipped = false;
        isFadingIn = false;
        skipHoldTimer = 0f;

        BlockPlayerInput();
    }

    protected virtual void Update()
    {
        if (!isEventActive || isEventSkipped) return;

        if (!allowSkip) return;

        bool isHolding = (Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0)) && !isFadingIn;

        if (isHolding)
        {
            if (skipCircleGroup != null) skipCircleGroup.alpha = Mathf.MoveTowards(skipCircleGroup.alpha, 1f, Time.unscaledDeltaTime * 6f);
            skipHoldTimer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(skipHoldTimer / requiredSkipHoldTime);
            if (skipCircleGauge != null) skipCircleGauge.fillAmount = progress;

            if (skipHoldTimer >= requiredSkipHoldTime) StartCoroutine(SkipFadeInAndWarpSequence());
        }
        else
        {
            if (!isFadingIn)
            {
                if (skipHoldTimer > 0f)
                {
                    skipHoldTimer -= Time.unscaledDeltaTime * 3.0f;
                    skipHoldTimer = Mathf.Max(0f, skipHoldTimer);
                    if (skipCircleGauge != null) skipCircleGauge.fillAmount = Mathf.Clamp01(skipHoldTimer / requiredSkipHoldTime);
                }
                if (skipHoldTimer <= 0f && skipCircleGroup != null)
                {
                    skipCircleGroup.alpha = Mathf.MoveTowards(skipCircleGroup.alpha, 0f, Time.unscaledDeltaTime * 5f);
                }
            }
        }
    }

    private IEnumerator SkipFadeInAndWarpSequence()
    {
        isEventSkipped = true; isFadingIn = true;
        if (skipCircleGroup != null) skipCircleGroup.alpha = 0f;
        if (skipFadeCanvasGroup != null) skipFadeCanvasGroup.blocksRaycasts = true;

        float fadeTimer = 0f;
        while (fadeTimer < fadeInDuration)
        {
            fadeTimer += Time.unscaledDeltaTime;
            if (skipFadeCanvasGroup != null) skipFadeCanvasGroup.alpha = Mathf.Clamp01(fadeTimer / fadeInDuration);
            yield return null;
        }
        if (skipFadeCanvasGroup != null) skipFadeCanvasGroup.alpha = 1f;

        if (activeTimelineCoroutine != null) { StopCoroutine(activeTimelineCoroutine); activeTimelineCoroutine = null; }
        StopAllCoroutines();

        OnSkipWarp();
        StartCoroutine(FadeOutAndEndRoutine());
    }

    protected IEnumerator FadeOutAndEndRoutine()
    {
        isEventActive = false;

        yield return new WaitForSecondsRealtime(0.15f);
        while (skipFadeCanvasGroup != null && skipFadeCanvasGroup.alpha > 0f)
        {
            skipFadeCanvasGroup.alpha -= fadeOutSpeed * Time.unscaledDeltaTime;
            yield return null;
        }

        // フェードアウトが終わったら、新設した共通の終了関数を呼ぶ
        EndEvent();
    }

    // ===================================================================
    // フェードなしで即座にイベントを安全に完全終了させる共通の出口
    // ===================================================================
    protected void EndEvent()
    {
        isEventActive = false;
        isEventSkipped = false;
        isFadingIn = false;
        skipHoldTimer = 0f;

        // 生成したキャンバス（裏で生き残っていたプレハブ）を確実に破壊！
        if (spawnedCanvasInstance != null)
        {
            Destroy(spawnedCanvasInstance);
            spawnedCanvasInstance = null;
        }

        ReleasePlayerInput();
        OnEventFullyCompleted();
    }

    protected virtual void OnSkipWarp() { EndEvent(); }
    protected virtual void OnEventFullyCompleted() { }

    protected void BlockPlayerInput()
    {
        if (playerController != null && playerController.inputActions != null)
        {
            playerController.inputActions.Player.Disable();
            playerController.TransitionToState(playerController.StateNormal);
        }
    }

    protected void BenjaminReleasePlayerInput()
    {
        if (playerController != null && playerController.inputActions != null) playerController.inputActions.Player.Enable();
    }

    protected void ReleasePlayerInput()
    {
        if (playerController != null && playerController.inputActions != null) playerController.inputActions.Player.Enable();
    }

    protected IEnumerator Wait(float duration) { yield return new WaitForSecondsRealtime(duration); }
    protected IEnumerator Speak(ImageBubble bubble, ImageBubble.StampType stampType, float customDuration = -1f)
    {
        if (bubble == null) yield break;
        bubble.ShowStamp(stampType);
        yield return null;
        float displayDuration = (customDuration > 0f) ? customDuration : defaultDisplayTime;
        yield return new WaitForSecondsRealtime(displayDuration);
        bubble.StartFadeOut();
    }
}