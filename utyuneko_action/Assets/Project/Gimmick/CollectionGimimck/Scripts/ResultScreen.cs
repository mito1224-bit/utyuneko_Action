using UnityEngine;
using TMPro; // TextMeshProを使うために必要

public class ResultScreen : MonoBehaviour
{
    [Header("UIテキストの登録（インスペクターからアタッチ）")]
    [SerializeField] private TextMeshProUGUI bitCubeText;     // Bitキューブ用（例: "00 / 50"）
    [SerializeField] private TextMeshProUGUI dataCubeText;    // Dataキューブ用（例: "0 / 3"）
    [SerializeField] private TextMeshProUGUI completionText;  // 達成度用（例: "85.5 %"）

    void Start()
    {
        // ステージから無事にデータが引き継がれているか確認
        if (DataManager.Instance == null)
        {
            Debug.LogWarning("[ResultScreen] DataManagerが見つかりません。ステージシーンから遊んでください。");
            return;
        }

        // 1. Bitキューブ（通常コイン）の表示更新
        var bitResult = DataManager.Instance.GetNormalCoinResult();
        if (bitCubeText != null)
        {
            bitCubeText.text = $"{bitResult.current} / {bitResult.max}";
        }

        // 2. DataCube（スターコイン）の表示更新
        var dataResult = DataManager.Instance.GetStarCoinResult();
        if (dataCubeText != null)
        {
            dataCubeText.text = $"{dataResult.current} / {dataResult.max}";
        }

        // 3. 総合達成度の表示更新
        float completionRate = DataManager.Instance.GetTotalCompletionRate();
        if (completionText != null)
        {
            // ":F1" をつけると小数点以下1桁に制限できます（例: 98.3%）
            completionText.text = $"{completionRate:F1}%";
        }

        // ?? 重要：リザルト画面を表示し終えたので、役目を終えたDataManagerを削除する
        // これをしないと、次のステージに行った時に古いデータが残ったままになってしまいます
        Destroy(DataManager.Instance.gameObject);
    }
}