using System.Collections;
using UnityEngine;

public class GoalAppearEffect : MonoBehaviour
{

    [SerializeField] private float appearTime = 0.5f;

    private Vector3 targetScale;
    private bool initialized = false;
    private Coroutine appearCoroutine;

    private void Awake()
    {
        // SetActive(true) された瞬間にAwakeとOnEnableがほぼ同時に呼ばれるため、
        // ここで先にtargetScaleを確保しておく
        CaptureTargetScaleIfNeeded();
    }

    private void OnEnable()
    {
        // オブジェクトが最初からActiveな場合など、Awakeより先にOnEnableが
        // 呼ばれるケースに備えて念のためここでも確保する
        CaptureTargetScaleIfNeeded();

        // SetActive(true) されるたびに必ずスケール0から再生し直す
        transform.localScale = Vector3.zero;

        if (appearCoroutine != null)
        {
            StopCoroutine(appearCoroutine);
        }
        appearCoroutine = StartCoroutine(AppearRoutine());
    }

    private void OnDisable()
    {
        // 非表示になったら次回のためにコルーチン参照をクリア
        appearCoroutine = null;
    }

    private void CaptureTargetScaleIfNeeded()
    {
        if (!initialized)
        {
            // まだ0になっていない「本来の完成スケール」をここで記録する
            targetScale = transform.localScale;
            initialized = true;
        }
    }

    private IEnumerator AppearRoutine()
    {
        float timer = 0f;

        while (timer < appearTime)
        {
            timer += Time.unscaledDeltaTime;

            float t = timer / appearTime;

            // Overshoot
            float scale = Mathf.Lerp(
                0f,
                1.2f,
                Mathf.SmoothStep(0f, 1f, t)
            );

            transform.localScale = targetScale * scale;

            yield return null;
        }

        // 少し縮めて完成
        float returnTime = 0.15f;
        timer = 0f;

        Vector3 startScale = targetScale * 1.2f;

        while (timer < returnTime)
        {
            timer += Time.unscaledDeltaTime;

            float t = timer / returnTime;

            transform.localScale =
                Vector3.Lerp(startScale, targetScale, t);

            yield return null;
        }

        transform.localScale = targetScale;
        appearCoroutine = null;
    }

}