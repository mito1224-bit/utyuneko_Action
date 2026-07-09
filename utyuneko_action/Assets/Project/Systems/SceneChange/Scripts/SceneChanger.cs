using UnityEngine;
using UnityEngine.SceneManagement; // シーン切り替えに必要
using System.Collections;

public class SceneChanger : MonoBehaviour
{
    [SerializeField] private string nextSceneName; // インスペクターからシーン名を指定

    [Header("ワープ演出設定")]
    [SerializeField] private float warpDuration = 1.5f; // 吸い込まれるまでの時間
    [SerializeField] private float spinSpeed = 1080f; // 回転速度（度/秒）

    [Header("引き伸ばし設定 (Feature 1)")]
    [SerializeField, Tooltip("どれくらい縦に長く伸びるか")]
    private float stretchMultiplier = 3.0f;

    [Header("カメラズーム設定 (Feature 4)")]
    [SerializeField, Tooltip("演出完了時のカメラサイズの倍率（1より小さいとズームイン）")]
    private float zoomTargetZ = -20f;

    private bool isWarping = false; // 連続接触によるバグ防止フラグ

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isWarping) return;
        // 接触したオブジェクトが「Player」タグを持っているかチェック
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();

            if (player != null)
            {
                isWarping = true;
                // 演出コルーチンを開始
                StartCoroutine(WarpAnimationRoutine(player));
            }
        }
    }

    private IEnumerator WarpAnimationRoutine(PlayerController player)
    {
        // プレイヤーの操作と物理演算を無効化
        player.TransitionToState(player.StateNone);

        // 【Feature 4: カメラズーム】
        // カメラの神スクリプトを取得して、ゴール地点(this.transform)へのズームを命令！
        CameraFollowWithZoom cam = Camera.main.GetComponent<CameraFollowWithZoom>();
        if (cam != null)
        {
            // StartZoomTrack(ターゲット, 目標Z値, 到達までの時間)
            cam.StartZoomTrack(this.transform, zoomTargetZ, warpDuration);
        }
        if (player.rb2D != null)
        {
            player.rb2D.linearVelocity = Vector2.zero;
            player.rb2D.simulated = false;
        }

        // 【Feature 2: 残像・トレイルの強制ON】
        // スピンしながら吸い込まれる軌跡を強調して螺旋を描く
        //if (player.trailRenderer != null) player.trailRenderer.enabled = true;
        //if (player.afterImageEffect != null) player.afterImageEffect.enabled = true;

        Camera mainCam = Camera.main;
        CameraFollowWithZoom camWithZoom = null;
        float startCamZ = -50f;

        if (mainCam != null)
        {
            startCamZ = mainCam.transform.position.z;
            camWithZoom = mainCam.GetComponent<CameraFollowWithZoom>();

            if (camWithZoom != null)
            {
                // カメラの通常追従を強制ロック（位置はゴールの中心、Z軸は現在の位置からスタート）
                camWithZoom.LockCamera(transform.position, startCamZ);
            }
        }

        Transform playerTransform = player.transform;
        Vector3 startPos = playerTransform.position;
        Vector3 goalPos = transform.position;
        Vector3 startScale = playerTransform.localScale;

        //// 【Feature 4: カメラの準備】
        //Camera mainCam = Camera.main;
        //float startCamSize = 5f;
        //float targetCamSize = 5f;
        //if (mainCam != null)
        //{
        //    startCamSize = mainCam.orthographicSize;
        //    targetCamSize = startCamSize * zoomFactor; // 指定倍率までズームイン
        //}

        float elapsed = 0f;

        while (elapsed < warpDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / warpDuration;
            float easeIn = t * t; // 加速度的な変化

            // 位置の移動
            playerTransform.position = Vector3.Lerp(startPos, goalPos, easeIn);

            // 回転
            playerTransform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

            // 【Feature 1: 引き伸ばし (スパゲッティ化)】
            float currentX = Mathf.Lerp(startScale.x, 0f, easeIn);
            float currentY = Mathf.Lerp(startScale.y, startScale.y * stretchMultiplier, easeIn) * (1f - easeIn);
            playerTransform.localScale = new Vector3(currentX, currentY, startScale.z);

            if (mainCam != null)
            {
                float currentZ = Mathf.Lerp(startCamZ, zoomTargetZ, easeIn);

                // 直接カメラの座標を書き換える
                mainCam.transform.position = new Vector3(transform.position.x, transform.position.y, currentZ);

                // カメラスクリプト内部の変数も同期させてガタつきを防ぐ
                if (camWithZoom != null)
                {
                    camWithZoom.lockedPosition = transform.position;
                    // カメラ側スクリプト内部のZ補間ロジックに無理やり割り込む形にするため、目標値を更新し続ける
                    camWithZoom.LockCamera(transform.position, currentZ);
                }
            }

            yield return null;
        }

        // 最終状態確定（見えなくなる）
        playerTransform.position = goalPos;
        playerTransform.localScale = Vector3.zero;

        // データ保存とシーン遷移
        if (DataManager.Instance != null)
        {
            DataManager.Instance.ProcessStageClear();
        }
        else
        {
            Debug.LogWarning("[SceneChanger] DataManagerが見つかりません。");
        }

        TransitionManager.Instance.ChangeScene(nextSceneName, TransitionType.Wipe);
    }
}