using UnityEngine;

using UnityEngine.UI;

using System.Collections;



/// <summary>

/// ボススナイパーのHPバーUI。

/// BossSniperHealth のイベント（onHPChanged / onDamaged）を受け取って、

/// Slider を動かすだけの表示役。HPの数値管理は BossSniperHealth に任せる。

///

/// 表示方針: 「現フェーズHPだけ」を映す（フェーズごとに満タン→ゼロを繰り返す）。

/// onHPChanged は (現在HP, 現フェーズ最大HP) を渡すので、maxValue も毎回更新して

/// フェーズが変わってHPスケールが変わっても正しく満タン表示になる。

///

/// 残像ゲージ:

///   mainSlider（手前・赤）= 即座に現在HPへ。

///   subSlider（奥・白）  = 遅れて追従。減った瞬間だけ差分が白く見えてダメージ量が伝わる。

///

/// セットアップ:

///   1. Canvas > Slider を2つ重ねて置く（奥=sub/白、手前=main/赤）。Interactable はオフ、ハンドルは削除推奨。

///   2. このスクリプトを Canvas 内の適当なオブジェクトに付け、mainSlider / subSlider を割り当てる。

///   3. ボスの BossSniperHealth を bossHealth に割り当てる（未割り当てなら実行時にシーンから探す）。

///

/// イベントはコード側で自動購読するので、インスペクターでの手動配線は不要。

/// </summary>

public class BossSniperHPBar : MonoBehaviour

{

    [Header("参照")]

    [Tooltip("表示対象のボスHP。未割り当てなら実行時にシーンから探す")]

    public BossSniperHealth bossHealth;



    [Header("ゲージ")]

    [Tooltip("手前・赤。即座に現在HPへ動く")]

    public Slider mainSlider;



    [Tooltip("奥・白。遅れて追従する残像ゲージ（無ければ残像なしでも動く）")]

    public Slider subSlider;



    [Header("演出")]

    [Tooltip("残像（白）が現在HPへ追いつく速さ")]

    public float subFollowSpeed = 3f;



    [Tooltip("被弾時に軽くバーを揺らす強さ（ピクセル）。0で無効")]

    public float shakeStrength = 8f;



    [Tooltip("被弾時の揺れの時間（秒）")]

    public float shakeDuration = 0.15f;



    private RectTransform shakeTarget;   // 揺らす対象（このオブジェクトのRect）

    private Vector2 shakeHomePos;

    private float shakeTimer;



    private CanvasGroup canvasGroup;

    private bool isDying;

    public float fadeOutDuration = 0.8f;



    [Header("出現演出")]

    [Tooltip("バーが横スケール（X）0→1 で広がる時間（秒）")]

    public float appearScaleDuration = 0.4f;



    [Tooltip("ゲージが 0→満タンへチャージされる時間（秒）")]

    public float appearChargeDuration = 1.0f;



    // 出現アニメ中は onHPChanged / SetImmediate による即時反映を抑止する

    private bool isAppearing;

    private Coroutine appearRoutine;

    private float appearStartHP;   // チャージ後に落ち着かせる現在HP

    private float appearMaxHP;



    void Awake()

    {

        shakeTarget = transform as RectTransform;

        if (shakeTarget != null) shakeHomePos = shakeTarget.anchoredPosition;



        canvasGroup = GetComponent<CanvasGroup>();

    }



    void OnEnable()

    {

        // 参照が無ければシーンから探す

        if (bossHealth == null)

        {

#if UNITY_2023_1_OR_NEWER

            bossHealth = Object.FindFirstObjectByType<BossSniperHealth>();

#else

            bossHealth = Object.FindObjectOfType<BossSniperHealth>();

#endif

        }

        Subscribe(true);



        // 既に初期化済みなら現在値を反映（ただし出現アニメ中は上書きしない）

        if (!isAppearing && bossHealth != null && bossHealth.MaxHP > 0f)

        {

            SetImmediate(bossHealth.CurrentHP, bossHealth.MaxHP);

        }

    }



    void OnDisable()

    {

        Subscribe(false);

    }



    private void Subscribe(bool on)

    {

        if (bossHealth == null) return;

        if (on)

        {

            bossHealth.onHPChanged.AddListener(OnHPChanged);

            bossHealth.onDamaged.AddListener(OnDamaged);

        }

        else

        {

            bossHealth.onHPChanged.RemoveListener(OnHPChanged);

            bossHealth.onDamaged.RemoveListener(OnDamaged);

        }

    }



    void Update()

    {

        // 出現アニメ中はチャージ演出に任せる（残像追従を止める）

        if (isAppearing) return;



        // 残像ゲージの追従（unscaled でスロー演出中も実時間で動く）

        if (subSlider != null && mainSlider != null && subSlider.value > mainSlider.value)

        {

            subSlider.value = Mathf.Lerp(subSlider.value, mainSlider.value, Time.unscaledDeltaTime * subFollowSpeed);

            if (subSlider.value - mainSlider.value < 0.01f) subSlider.value = mainSlider.value;

        }



        // 被弾シェイク

        if (shakeTimer > 0f && shakeTarget != null)

        {

            shakeTimer -= Time.unscaledDeltaTime;

            if (shakeTimer > 0f)

            {

                Vector2 off = new Vector2(Random.Range(-shakeStrength, shakeStrength), Random.Range(-shakeStrength, shakeStrength));

                shakeTarget.anchoredPosition = shakeHomePos + off;

            }

            else

            {

                shakeTarget.anchoredPosition = shakeHomePos;

            }

        }

    }



    // ─── イベントハンドラ ─────────────────────────



    // 現在HPが変わった：赤を即追従。最大HPも更新して「現フェーズHPだけ」を映す

    private void OnHPChanged(float current, float max)

    {

        // 出現アニメ中は目標値だけ覚えておき、見た目はチャージ演出に任せる

        if (isAppearing)

        {

            appearStartHP = current;

            appearMaxHP = max;

            return;

        }



        if (mainSlider != null)

        {

            mainSlider.maxValue = max;

            mainSlider.value = current;

        }

        if (subSlider != null)

        {

            subSlider.maxValue = max;

            // 残像は追従に任せる。ただし増加（フェーズ切替で満タン化）は即反映して白残りを防ぐ

            if (current >= subSlider.value) subSlider.value = current;

        }





        if (current <= 0f && !isDying)

        {

            StartCoroutine(FadeOutRoutine());

        }



    }



    // 被弾：バーを軽く揺らす

    private void OnDamaged(float damage)

    {

        if (shakeStrength > 0f) shakeTimer = shakeDuration;

    }



    private void SetImmediate(float current, float max)

    {

        if (mainSlider != null) { mainSlider.maxValue = max; mainSlider.value = current; }

        if (subSlider != null) { subSlider.maxValue = max; subSlider.value = current; }

    }



    /// <summary>

    /// HPバーの出現アニメを再生する（トリガーがボス有効化時に叩く）。

    /// 横スケール（X）0→1 で広がってから、ゲージが 0→満タンへチャージされ、最後に現在HPへ落ち着く。

    /// スロー演出中でも実時間で動くよう unscaled 時間を使う。

    /// </summary>

    public void StartAppearAnimation(float maxHP, float startHP)

    {

        appearMaxHP = maxHP;

        appearStartHP = startHP;

        isDying = false;



        if (canvasGroup != null) canvasGroup.alpha = 1f;

        if (mainSlider != null) { mainSlider.maxValue = maxHP; mainSlider.value = 0f; }

        if (subSlider != null) { subSlider.maxValue = maxHP; subSlider.value = 0f; }



        if (appearRoutine != null) StopCoroutine(appearRoutine);

        appearRoutine = StartCoroutine(AppearRoutine());

    }



    private IEnumerator AppearRoutine()

    {

        isAppearing = true;



        // フェーズ①：横スケール（X）0→1 で広がる

        Vector3 baseScale = transform.localScale;

        transform.localScale = new Vector3(0f, baseScale.y, baseScale.z);

        float t = 0f;

        while (t < appearScaleDuration)

        {

            t += Time.unscaledDeltaTime;

            float ratio = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / appearScaleDuration));

            transform.localScale = new Vector3(ratio * baseScale.x, baseScale.y, baseScale.z);

            yield return null;

        }

        transform.localScale = baseScale;



        // フェーズ②：ゲージが 0→満タンへチャージ

        t = 0f;

        while (t < appearChargeDuration)

        {

            t += Time.unscaledDeltaTime;

            float ratio = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / appearChargeDuration));

            float v = Mathf.Lerp(0f, appearMaxHP, ratio);

            if (mainSlider != null) mainSlider.value = v;

            if (subSlider != null) subSlider.value = v;

            yield return null;

        }



        // 現在HPへ落ち着かせる（アニメ中に届いた最新値を尊重）

        if (bossHealth != null && bossHealth.MaxHP > 0f)

        {

            appearMaxHP = bossHealth.MaxHP;

            appearStartHP = bossHealth.CurrentHP;

        }

        if (mainSlider != null) { mainSlider.maxValue = appearMaxHP; mainSlider.value = appearStartHP; }

        if (subSlider != null) { subSlider.maxValue = appearMaxHP; subSlider.value = appearStartHP; }



        isAppearing = false;

        appearRoutine = null;

    }





    private IEnumerator FadeOutRoutine()

    {

        isDying = true;



        float t = 0f;



        while (t < fadeOutDuration)

        {

            t += Time.unscaledDeltaTime;



            float ratio = Mathf.Clamp01(t / fadeOutDuration);



            if (canvasGroup != null)

                canvasGroup.alpha = Mathf.Lerp(1f, 0f, ratio);



            yield return null;

        }



        if (canvasGroup != null)

            canvasGroup.alpha = 0f;

    }



}