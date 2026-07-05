using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class StageSecondBossHPBar : MonoBehaviour
{
    [Header("⚙️ UIコンポーネントの登録")]
    [Tooltip("即座に減るメインの赤ゲージSliderをここにドラッグしてね")]
    [SerializeField] private Slider mainHpSlider;

    [Tooltip("遅れて減る残像の白ゲージSliderをここにドラッグしてね")]
    [SerializeField] private Slider subHpSlider;

    [Header("⚙️ 演出パラメータの設定")]
    public float scaleExpandDuration = 0.4f;
    public float hpChargeDuration = 1.0f;
    public float subGaugeFollowSpeed = 2.5f;
    public float fadeOutDuration = 0.8f;

    private CanvasGroup canvasGroup;
    private float targetMaxHP;
    private float currentHP;
    private bool isAppearing = true;
    private bool isDying = false;

    private List<RectTransform> childTransforms = new List<RectTransform>();
    private List<Vector3> originalChildPositions = new List<Vector3>();

    private Coroutine appearSequenceCoroutine;
    private Coroutine shakeCoroutine;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        // 親の配置（アンカー）を壊さず、子供の見た目パーツだけを安全に揺らすための記憶
        childTransforms.Clear();
        originalChildPositions.Clear();
        foreach (Transform child in transform)
        {
            if (child.TryGetComponent<RectTransform>(out var rect))
            {
                childTransforms.Add(rect);
                originalChildPositions.Add(rect.localPosition);
            }
        }
    }

    void Update()
    {
        if (isAppearing || isDying) return;

        // ===================================================================
        // 🛠️【TimeScale対策】ダメージ残像ゲージ（サブゲージ）のじわじわ追従処理
        // Time.deltaTime から Time.unscaledDeltaTime に変更！
        // ゲームがスロー中であっても、残像は現実時間の速度で滑らかに減少追従します。
        // ===================================================================
        if (subHpSlider != null && mainHpSlider != null)
        {
            if (subHpSlider.value > mainHpSlider.value)
            {
                subHpSlider.value = Mathf.Lerp(subHpSlider.value, mainHpSlider.value, Time.unscaledDeltaTime * subGaugeFollowSpeed);
                if (subHpSlider.value - mainHpSlider.value < 0.1f)
                {
                    subHpSlider.value = mainHpSlider.value;
                }
            }
        }
    }

    public void StartAppearAnimation(float maxHP, float startHP)
    {
        targetMaxHP = maxHP;
        currentHP = startHP;
        isDying = false;

        if (canvasGroup != null) canvasGroup.alpha = 1f;

        if (mainHpSlider != null) { mainHpSlider.maxValue = maxHP; mainHpSlider.value = 0f; }
        if (subHpSlider != null) { subHpSlider.maxValue = maxHP; subHpSlider.value = 0f; }

        if (appearSequenceCoroutine != null) StopCoroutine(appearSequenceCoroutine);
        appearSequenceCoroutine = StartCoroutine(AppearSequenceRoutine());
    }

    private IEnumerator AppearSequenceRoutine()
    {
        isAppearing = true;

        // ===================================================================
        // 🛠️【TimeScale対策】フェーズ①：全体のスケールXが 0 から 1 へ広がる
        // ===================================================================
        transform.localScale = new Vector3(0f, 1f, 1f);
        float t = 0f;
        while (t < scaleExpandDuration)
        {
            // Time.deltaTime ではなく unscaledDeltaTime を累積させることでリアルタイムに固定
            t += Time.unscaledDeltaTime;
            float ratio = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / scaleExpandDuration));
            transform.localScale = new Vector3(ratio, 1f, 1f);
            yield return null;
        }
        transform.localScale = Vector3.one;

        // ===================================================================
        // 🛠️【TimeScale対策】フェーズ②：HPが 0 から MAX までチャージされる
        // ===================================================================
        t = 0f;
        while (t < hpChargeDuration)
        {
            t += Time.unscaledDeltaTime;
            float ratio = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / hpChargeDuration));
            float chargeValue = Mathf.Lerp(0f, targetMaxHP, ratio);

            if (mainHpSlider != null) mainHpSlider.value = chargeValue;
            if (subHpSlider != null) subHpSlider.value = chargeValue;
            yield return null;
        }

        if (mainHpSlider != null) mainHpSlider.value = currentHP;
        if (subHpSlider != null) subHpSlider.value = currentHP;

        isAppearing = false;
        appearSequenceCoroutine = null;
    }

    public void UpdateHP(float newHP)
    {
        currentHP = newHP;
        if (!isAppearing && mainHpSlider != null)
        {
            mainHpSlider.value = newHP;
        }

        if (currentHP <= 0f && !isDying)
        {
            StartCoroutine(FadeOutRoutine());
        }
    }

    private IEnumerator FadeOutRoutine()
    {
        isDying = true;
        float t = 0f;
        if (shakeCoroutine != null) { StopCoroutine(shakeCoroutine); ResetChildPositions(); }

        // ===================================================================
        // 🛠️【TimeScale対策】ボスの死亡に伴うHPバーのフェードアウト処理
        // ===================================================================
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float ratio = Mathf.Clamp01(t / fadeOutDuration);
            if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(1f, 0f, ratio);
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    public void ShakeBar(float duration = 0.25f, float strength = 12f)
    {
        if (childTransforms.Count == 0 || isDying) return;
        if (shakeCoroutine != null) { StopCoroutine(shakeCoroutine); ResetChildPositions(); }

        if (isAppearing)
        {
            isAppearing = false;
            if (appearSequenceCoroutine != null) { StopCoroutine(appearSequenceCoroutine); appearSequenceCoroutine = null; }
            transform.localScale = Vector3.one;
            if (mainHpSlider != null) mainHpSlider.value = currentHP;
            if (subHpSlider != null) subHpSlider.value = currentHP;
        }

        shakeCoroutine = StartCoroutine(ShakeRoutine(duration, strength));
    }

    private IEnumerator ShakeRoutine(float duration, float strength)
    {
        float elapsed = 0f;

        // ===================================================================
        // 🛠️【TimeScale対策】被弾時のガタガタ激しいシェイク（揺れ）処理
        // ここが一番の肝！TimeScaleに関係なく、現実の0.2秒間でキビキビと振動します。
        // ===================================================================
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float offsetX = Random.Range(-strength, strength);
            float offsetY = Random.Range(-strength, strength);
            Vector3 shakeOffset = new Vector3(offsetX, offsetY, 0f);

            for (int i = 0; i < childTransforms.Count; i++)
            {
                if (childTransforms[i] != null) childTransforms[i].localPosition = originalChildPositions[i] + shakeOffset;
            }
            yield return null;
        }
        ResetChildPositions();
        shakeCoroutine = null;
    }

    private void ResetChildPositions()
    {
        for (int i = 0; i < childTransforms.Count; i++)
        {
            if (childTransforms[i] != null && i < originalChildPositions.Count) childTransforms[i].localPosition = originalChildPositions[i];
        }
    }
}