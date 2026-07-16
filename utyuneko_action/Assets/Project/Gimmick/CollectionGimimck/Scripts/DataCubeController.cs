
using System.Collections; // コルーチンを使うために必要
using UnityEngine;

public class DataCubeController : MonoBehaviour
{

    public enum AnimationType
    {
        Default, // 1. シンプルな浮き上がり（等速）
        Bound,   // 2. 勢いよく飛び跳ねてバウンド（AnimationCurve使用）
        Magnet   // 3. プレイヤーに吸い込まれる
    }

    [Header("データキューブの設定")]
    [Tooltip("このデータキューブの識別番号（例: 1枚目は 1、2枚目は 2）")]
    [SerializeField] private int dataCubeID = 1;

    [Header("演出の切り替え")]
    [SerializeField] private AnimationType animationType = AnimationType.Default;

    [Header("共通の設定")]
    [SerializeField] private float animationDuration = 1.0f; // 演出時間
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0, 0, 360); // 1秒あたりの回転角

    [Header("浮き上がり / バウンド用の設定")]
    [SerializeField] private float moveUpDistance = 1.5f;     // 浮き上がる距離
    [Tooltip("Bound設定の時だけ使用。縦軸1.2くらいまで突き抜ける山を作るとバウンドします")]
    [SerializeField] private AnimationCurve boundCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public int DataCubeIndex => dataCubeID - 1;

    [Header("カメライベント")]
    [SerializeField] private CameraFollowWithZoom eventCamera;
    [SerializeField] private CameraBoundsTrigger BoundTriggerCamera;

    [Header("カメライベント時間")]
    [SerializeField] private float dataCameraWait = 0.4f;     // DataCubeにカメラが寄るまで待つ時間
    [SerializeField] private float cameraReturnTime = 1.0f;   // Playerへ戻る時間

    [Header("カメラ速度")]
    [SerializeField] private float eventPositionSpeed = 3.0f;
    [SerializeField] private float eventZoomSpeed = 2.0f;

    [Header("取得前ポップ演出")]
    [SerializeField] private float popDuration = 0.35f;       // 拡大→戻る時間
    [SerializeField] private float popScaleMultiplier = 1.5f;


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
            Debug.Log($"DataCubeID = {dataCubeID}");
            // ★【重要】1始まりのIDを、プログラム用の「0始まり（インデックス）」に変換して通知する
            int zeroBasedIndex = dataCubeID - 1;
            OnDataCubeCollected?.Invoke(zeroBasedIndex);

            // エフェクトを発生させる（こちらは MainItem になっていますね！）
            FXManager.Instance.Play(FXType.DataGetFlash, transform.position);

            // 演出を開始し、終了後に消滅させる
            StartCoroutine(DataCubeCollectEventRoutine(collision.transform));
        }
    }

    private IEnumerator DataCubeCollectEventRoutine(Transform playerTransform)
    {
        // 当たり判定を切って二重取得を防ぐ
        if (TryGetComponent<Collider2D>(out var col))
        {
            col.enabled = false;
        }

        // 見た目の親があるなら親ごと動かす
        Transform targetTransform = transform.parent != null ? transform.parent : transform;

        // カメラ範囲制御を一時OFF
        if (BoundTriggerCamera != null)
        {
            BoundTriggerCamera.gameObject.SetActive(false);
        }

        // ==================================================
        // ① DataCube用カメライベント
        // ==================================================
        if (eventCamera != null)
        {
            eventCamera.StartTrackTarget(
                targetTransform,
                eventPositionSpeed,
                eventZoomSpeed
            );
        }
        else
        {
            Debug.LogWarning("【デバッグ】EventCameraが登録されていません。");
        }

        // カメラがDataCubeに寄るまで待つ
        yield return new WaitForSeconds(dataCameraWait);

        // ==================================================
        // ② 拡大 → 縮小演出
        // ==================================================
        yield return StartCoroutine(PopScaleRoutine(targetTransform));

        // ==================================================
        // ③ 今までの取得演出
        //    Default / Bound / Magnet
        // ==================================================
        yield return StartCoroutine(CollectAnimationRoutine(playerTransform, targetTransform));

        // ==================================================
        // ④ カメラをPlayerに戻す
        // ==================================================
        if (eventCamera != null)
        {
            eventCamera.ReturnToPlayerFromEvent(cameraReturnTime);
        }

        yield return new WaitForSeconds(cameraReturnTime);

        // カメラ範囲制御を戻す
        if (BoundTriggerCamera != null)
        {
            BoundTriggerCamera.gameObject.SetActive(true);
        }

    }

    private IEnumerator PopScaleRoutine(Transform targetTransform)
    {
        Vector3 startScale = targetTransform.localScale;
        Vector3 popScale = startScale * popScaleMultiplier;

        float halfDuration = popDuration * 0.5f;

        float elapsed = 0f;

        // 拡大
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / halfDuration);
            float easeT = Mathf.Sin(t * Mathf.PI * 0.5f);

            targetTransform.localScale = Vector3.Lerp(startScale, popScale, easeT);

            yield return null;
        }

        elapsed = 0f;

        // 元サイズへ縮小
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / halfDuration);
            float easeT = Mathf.Sin(t * Mathf.PI * 0.5f);

            targetTransform.localScale = Vector3.Lerp(popScale, startScale, easeT);

            yield return null;
        }

        targetTransform.localScale = startScale;
    }

    private IEnumerator CollectAnimationRoutine(Transform playerTransform, Transform targetTransform)
    {
        float elapsed = 0f;

        Vector3 startPosition = targetTransform.position;
        Vector3 endPosition = startPosition + Vector3.up * moveUpDistance;
        Vector3 startScale = targetTransform.localScale;

        // 回転の補間用
        Quaternion startRotation = targetTransform.rotation;
        Quaternion endRotation = startRotation * Quaternion.Euler(rotationSpeed * animationDuration);

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / animationDuration);

            // インスペクターで選ばれた演出タイプに応じて処理を切り替える
            switch (animationType)
            {
                case AnimationType.Default:
                    // シンプルな浮き上がり + 回転 + 縮小
                    targetTransform.position = Vector3.Lerp(startPosition, endPosition, t);
                    targetTransform.Rotate(rotationSpeed * Time.deltaTime);
                    targetTransform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                    break;

                case AnimationType.Bound:
                    // AnimationCurve と LerpUnclamped を使ったバウンド演出
                    float curveT = boundCurve.Evaluate(t);

                    targetTransform.position = Vector3.LerpUnclamped(startPosition, endPosition, curveT);
                    targetTransform.rotation = Quaternion.LerpUnclamped(startRotation, endRotation, curveT);

                    // scaleだけはマイナスにならないように軽く保護
                    float scaleT = Mathf.Clamp01(curveT);
                    targetTransform.localScale = Vector3.Lerp(startScale, Vector3.zero, scaleT);
                    break;

                case AnimationType.Magnet:
                    // プレイヤーの動きをリアルタイムに追いかけて吸い込まれる演出
                    Vector3 currentPlayerPos = playerTransform.position;

                    targetTransform.position = Vector3.Lerp(startPosition, currentPlayerPos, t);
                    targetTransform.rotation = Quaternion.Lerp(startRotation, endRotation, t);
                    targetTransform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                    break;
            }

            yield return null;
        }

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