using System.Collections;
using TMPro; // TextMeshProを使うために必要
using UnityEngine;
using UnityEngine.UI;
using static ResultScreen;

public class ResultScreen : MonoBehaviour
{
    [Header("UIテキストの登録（インスペクターからアタッチ）")]
    [SerializeField] private TextMeshProUGUI bitCubeText;     // Bitキューブ用（例: "00 / 50"）
    [SerializeField] private TextMeshProUGUI dataCubeText;    // Dataキューブ用（例: "0 / 3"）
    [SerializeField] private TextMeshProUGUI completionText;  // 達成度用（例: "85.5 %"）

    [Header("ドーナツ型ゲージの登録")]
    [SerializeField] private Image completionGaugeImage;     // ★ここにドーナツ型Imageをアタッチする枠！

    [Header("演出設定")]
    [SerializeField] private float animationDuration = 1.5f; // ゲージが最大になるまでの時間（秒）
    [SerializeField] private float bitAnimationDuration = 1.0f; // ★追加：Bitカウントアップにかける時間

    [Header("ゲージのイージング設定（カーブ）")]
    [SerializeField] private AnimationCurve gaugeEasingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // ★構造体を作って、インスペクターで見やすく管理します
    [System.Serializable]
    public struct DataCubeSlotUI
    {
        public GameObject slotRoot;         // スロットの親（これ自体を非表示にして数を調整する）
        public Image actualCubeImage;  // 前面の本物コイン画像（演出で大きくしてはめる用）
    }


    [Header("[DataCube]演出用設定")]
    [SerializeField] private DataCubeSlotUI[] dataCubeSlots;
    [SerializeField] private float delayBetweenCubes = 0.4f; // コインが次々とはまる時の時間差（秒）

    [Header("終了後のUI設定")]
    [SerializeField] private GameObject enterPromptUI; // ここに「Enter」のUI（Image等）をアタッチ
    private bool isResultSequenceFinished = false;     // 演出が終わったかどうかの判定用フラグ

    void Start()
    {

        if (enterPromptUI != null)
        {
            enterPromptUI.SetActive(false);
        }

        // データの引き継ぎ先を GameManager に変更
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[ResultScreen] GameManagerが見つかりません。タイトルからやり直すか、GlobalSystemsがある状態で実行してください。");
            return;
        }

        if (InputManager.Instance != null)
        {
            InputManager.Instance.UI.Enable();
        }
        
        // 1. Bitキューブ（通常コイン）の表示更新をGameManagerから取得
        if (bitCubeText != null)
        {
            bitCubeText.text = $"×0";
        }

        // 2. DataCube（スターコイン）の表示更新をGameManagerから取得
        if (dataCubeText != null)
        {
            dataCubeText.text = $"0 / {GameManager.Instance.FinalDataMax}";
        }

        // ★【超重要】ステージの最大数に合わせて、UIの枠の数を自動調整する
        SetupSlots(GameManager.Instance.FinalDataMax);

        // 3. 総合達成度の取得とアニメーション開始をGameManagerから取得
        float completionRate = GameManager.Instance.FinalCompletionRate;

        if (completionGaugeImage != null)
        {
            completionGaugeImage.fillAmount = 0f;
        }

        // ドーナツゲージのカウントアップ演出を開始
        StartCoroutine(WaitForTransitionAndPlay(completionRate));

        // ? 以前のコードにあった Destroy(DataManager.Instance.gameObject); は完全に削除します
    }

    private void Update()
    {
        // 演出が終わっていない場合は入力を受け付けない
        if (!isResultSequenceFinished) return;

        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Debug.Log("【テスト】スペースキーが押されました！フラグは正常に動いています。");
        }

        // InputManagerが存在するか確認
        if (InputManager.Instance != null)
        {
            // ★【注意】ご自身のInputManagerにあるInputActionの変数名に書き換えてください！
            // 例: InputManager.Instance.SubmitAction.WasPressedThisFrame() など
            if (InputManager.Instance.UI.Submit.WasPressedThisFrame())
            {
                // 連打防止のためにフラグを折る
                isResultSequenceFinished = false;

                Debug.Log("Enterが押されました！次の画面へ進みます。");
                TransitionManager.Instance.ChangeScene("StageSelect", TransitionType.Wipe);
                // TODO: ここにシーン遷移などの処理を追加する
            }
        }
    }

    private IEnumerator WaitForTransitionAndPlay(float completionRate)
    {
        // TransitionManager が存在し、かつトランジション中（フェード中）ならループして待つ
        // ※「isTransitioning」は非公開変数(private)なので、安全策として少し長めのディレイ（1.0秒）も合わせて仕込んでおきます
        yield return new WaitForSeconds(1.0f);

        // フェードインが完全に終わってから、本来の演出シーケンスを開始！
        yield return StartCoroutine(PlayResultSequence(completionRate));
    }

    //ステージの最大数に応じて枠の表示数を変えるメソッド
    private void SetupSlots(int maxCubesForThisStage)
    {
        for (int i = 0; i < dataCubeSlots.Length; i++)
        {
            if (dataCubeSlots[i].slotRoot == null) continue;

            if (i < maxCubesForThisStage)
            {
                // ステージの最大数以内なら、枠（点線）を表示
                dataCubeSlots[i].slotRoot.SetActive(true);
                // 前面の本物画像は一旦消しておく
                if (dataCubeSlots[i].actualCubeImage != null)
                {
                    dataCubeSlots[i].actualCubeImage.gameObject.SetActive(false);
                }
            }
            else
            {
                // 最大数を超えている余分な枠（例：最大3個のステージでの4,5個目の枠）は非表示にする
                dataCubeSlots[i].slotRoot.SetActive(false);
            }
        }
    }

    // 演出全体を順番にコントロールするコルーチン
    private IEnumerator PlayResultSequence(float completionRate)
    {
        yield return StartCoroutine(AnimateBitCubes(GameManager.Instance.FinalBitCurrent));
        yield return new WaitForSeconds(0.2f); // 少し余韻

        // ① まずはドーナツゲージのアニメーションが終わるのを待つ
        yield return StartCoroutine(AnimateGauge(completionRate));

        // 少しだけ余韻のためのウェイト
        yield return new WaitForSeconds(0.3f);

        // ② スターコインがピタッとはまっていく演出を開始
        yield return StartCoroutine(AnimateDataCubes());

        yield return new WaitForSeconds(0.5f);

        // EnterUIを表示
        if (enterPromptUI != null)
        {
            enterPromptUI.SetActive(true);
        }

        // 入力を許可するフラグをON！これがないとUpdateで弾かれてしまいます
        isResultSequenceFinished = true;
    }

    private IEnumerator AnimateBitCubes(int targetBit)
    {
        if (targetBit <= 0) yield break; // 0なら演出スキップ

        float elapsedTime = 0f;

        while (elapsedTime < bitAnimationDuration)
        {
            elapsedTime += Time.deltaTime;
            float linearProgress = elapsedTime / bitAnimationDuration;

            // イージングカーブを使って徐々にゆっくりになるようにする
            float easedProgress = gaugeEasingCurve.Evaluate(linearProgress);

            int currentBit = Mathf.RoundToInt(Mathf.Lerp(0f, targetBit, easedProgress));

            if (bitCubeText != null)
            {
                bitCubeText.text = $"×{currentBit}";
            }

            yield return null;
        }

        // 最後にピッタリ目標の値に合わせる
        if (bitCubeText != null)
        {
            bitCubeText.text = $"×{targetBit}";
        }
    }

    private IEnumerator AnimateGauge(float targetRate)
    {
        float targetFill = targetRate / 100f;
        float elapsedTime = 0f;

        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float linearProgress = elapsedTime / animationDuration;

            // ★【ここが重要】直線的な進捗を、インスペクターで設定したカーブの進捗に変換する！
            float easedProgress = gaugeEasingCurve.Evaluate(linearProgress);

            if (completionGaugeImage != null)
            {
                completionGaugeImage.fillAmount = Mathf.Lerp(0f, targetFill, easedProgress);
            }

            // テキストの更新（変換後の進捗を適用）
            if (completionText != null)
            {
                float currentRate = Mathf.Lerp(0f, targetRate, easedProgress);
                completionText.text = $"{currentRate:F1}%";
            }

            yield return null; // 1フレーム待つ
        }

        // 最後に完全にターゲットの値に固定する（誤差の補正）
        if (completionGaugeImage != null) completionGaugeImage.fillAmount = targetFill;
        if (completionText != null) completionText.text = $"{targetRate:F1}%";
    }

    private IEnumerator AnimateDataCubes()
    {
        //int currentObtained = GameManager.Instance.FinalDataCurrent;
        int maxCubes = GameManager.Instance.FinalDataMax;
        var flags = GameManager.Instance.FinalDataFlags;
        int displayedObtainedCount = 0; // 画面のテキスト更新用のカウンター

        if (maxCubes <= 0) yield break;

        for (int i = 0; i < maxCubes; i++)
        {
            if (flags == null || i >= flags.Count || !flags[i]) continue;
            // インデックスとコンポーネントのチェック
            if (i >= dataCubeSlots.Length || dataCubeSlots[i].actualCubeImage == null) continue;

            // --- 演出の準備 ---
            Image actualImage = dataCubeSlots[i].actualCubeImage;
            Transform actualTransform = actualImage.transform;

            // 1. まず本物画像をアクティブにする
            actualImage.gameObject.SetActive(true);
            // アニメーション全部無効
            //actualImage.color = Color.white;
            actualImage.transform.localScale = Vector3.one;


            // 2. 初期状態を設定（ここが重要）
            float startScale = 8.0f;
            actualTransform.localScale = Vector3.one * startScale;

            Color originalColor = actualImage.color;
            actualImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);

            // --- アニメーション（コルーチン） ---
            float popDuration = 0.5f; // 演出にかかる時間（※速すぎるなら0.4fや0.5fに調整）
            float elapsedTime = 0f;

            while (elapsedTime < popDuration)
            {
                elapsedTime += Time.deltaTime;
                float normalizedTime = elapsedTime / popDuration;

                // イージング
                float easedTime = Mathf.Sin(normalizedTime * Mathf.PI * 0.5f); // 0 -> 1

                // A. 大きさの変化（★ここも startScale -> 1.0f に変更）
                float scale = Mathf.Lerp(startScale, 1.0f, easedTime);
                actualTransform.localScale = Vector3.one * scale;

                // B. 透明度の変化（0 -> 1）
                float alpha = Mathf.Lerp(0f, 1f, easedTime);
                actualImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);

                yield return null; // 1フレーム待つ
            }

            // --- 最後に状態を完全に固定 ---
            actualTransform.localScale = Vector3.one;
            actualImage.color = originalColor; // 元の色（アルファ1）に戻す

            displayedObtainedCount++;
            // テキストの更新（スターコインの獲得数を＋１）
            if (dataCubeText != null)
            {
                dataCubeText.text = $"×{i + 1}";
            }

            // 次のコインまでのウェイト
            yield return new WaitForSeconds(delayBetweenCubes);
        }
    }
}