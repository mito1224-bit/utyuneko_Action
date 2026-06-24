
using UnityEngine;
using System.Collections; // コルーチンを使うために必要

public class DataCubeController : MonoBehaviour
{
    [Header("データキューブの設定")]
    [Tooltip("このデータキューブの識別番号（例: 1枚目は 1、2枚目は 2）")]
    [SerializeField] private int dataCubeID = 1;

    [Header("演出の設定")]
    [SerializeField] private float animationDuration = 1.0f; // 演出時間
    [SerializeField] private float moveUpDistance = 1.5f;     // 浮き上がる距離
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0, 0, 360); // 1秒あたりの回転角

    // 将来リザルトやマネージャーに「何番目のデータキューブを取ったか」を伝えるイベント
    public static System.Action<int> OnDataCubeCollected;

    private bool isCollected = false; // 二重取得防止用

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // すでに取得済みなら何もしない
        if (isCollected) return;

        if (collision.CompareTag("Player"))
        {
            isCollected = true; // 取得フラグを立てる

            // ★【重要】1始まりのIDを、プログラム用の「0始まり（インデックス）」に変換して通知する
            int zeroBasedIndex = dataCubeID - 1;
            OnDataCubeCollected?.Invoke(zeroBasedIndex);

            // エフェクトを発生させる（こちらは MainItem になっていますね！）
            FXManager.Instance.Play(FXType.MainItem, transform.position);

            // 演出を開始し、終了後に消滅させる
            StartCoroutine(CollectAnimationRoutine());
        }
    }

    private IEnumerator CollectAnimationRoutine()
    {
        // 1. プレイヤーと再び当たらないように、自分のコライダーを即座に無効化
        if (TryGetComponent<Collider2D>(out var col)) col.enabled = false;

        // 演出対象を決定（親がいれば親、いなければ自分自身をターゲットにする）
        Transform targetTransform = transform.parent != null ? transform.parent : transform;

        float elapsed = 0f;
        Vector3 startPosition = targetTransform.position;
        Vector3 endPosition = startPosition + Vector3.up * moveUpDistance;
        Vector3 startScale = targetTransform.localScale;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration; // 0から1へ変化

            // --- すべて targetTransform（親）に対して処理を行う ---

            // A. 親ごと上に移動
            targetTransform.position = Vector3.Lerp(startPosition, endPosition, t);

            // B. 親ごと回転（2DゲームならZ軸、3DゲームならY軸などインスペクターで調整可能）
            targetTransform.Rotate(rotationSpeed * Time.deltaTime);

            // C. 親ごと次第に小さくする
            targetTransform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);

            yield return null; // 1フレーム待機
        }

        // 2. 演出が終わったら、ターゲット（親オブジェクト）ごと削除
        Destroy(targetTransform.gameObject);
    }
}

//using UnityEngine;

//public class DataCubeController : MonoBehaviour
//{
//    [Header("データキューブの設定")]
//    [Tooltip("このデータキューブの識別番号（例: 1枚目は 1、2枚目は 2）")]
//    [SerializeField] private int dataCubeID = 1;

//    // 将来リザルトやマネージャーに「何番目のデータキューブを取ったか」を伝えるイベント
//    public static System.Action<int> OnDataCubeCollected;

//    private void OnTriggerEnter2D(Collider2D collision)
//    {
//        if (collision.CompareTag("Player"))
//        {
//            // ★【重要】1始まりのIDを、プログラム用の「0始まり（インデックス）」に変換して通知する
//            int zeroBasedIndex = dataCubeID - 1;
//            OnDataCubeCollected?.Invoke(zeroBasedIndex);

//            FXManager.Instance.Play(FXType.MainItem, transform.position);

//            // 自分自身（子）ではなく、親のオブジェクトごと消去する
//            if (transform.parent != null)
//            {
//                Destroy(transform.parent.gameObject);
//            }
//            else
//            {
//                Destroy(gameObject); // 万が一、親がいない場合の保険
//            }
//        }
//    }
//}