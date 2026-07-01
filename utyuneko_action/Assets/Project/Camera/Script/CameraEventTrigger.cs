using System.Collections;
using UnityEngine;

public class CameraEventTrigger2D : MonoBehaviour
{
    [Header("オブジェクト設定")]
    [SerializeField] private GameObject player;                   // プレイヤーオブジェクト
    [SerializeField] private MonoBehaviour playerMovement;         // プレイヤーの移動スクリプト
    [SerializeField] private CameraFollowWithZoom cameraFollow;   // カメラの親オブジェクト

    [Header("イベントカメラ設定")]
    [SerializeField] private Transform cameraTarget;               // カメラの移動先（空のオブジェクト等）

    [Header("演出・時間設定")]
    [SerializeField] private float timeToTarget = 1.5f;            // 目的地へ移動するのにかける時間（秒）
    [SerializeField] private float freezeDuration = 3f;            // 目的地に到着してから固定する秒数
    [SerializeField] private float timeToReturn = 1.5f;            // プレイヤーの元に戻るのにかける時間（秒）

    [Header("イベント専用のカメラ速度（数値を小さくするとゆっくり滑らかになります）")]
    [Range(0.1f, 20f)][SerializeField] private float eventPositionSpeed = 3f; // 移動のなめらかさ
    [Range(0.1f, 20f)][SerializeField] private float eventZoomSpeed = 2f;     // ズームのなめらかさ

    private bool isEventPlaying = false;
    private PlayerController playerController;

    private void Start()
    {
        if (player != null)
        {
            playerController = player.GetComponent<PlayerController>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject == player && !isEventPlaying)
        {
            StartCoroutine(PlayCameraEvent());
        }
    }

    private IEnumerator PlayCameraEvent()
    {
        isEventPlaying = true;

        // --- 1. プレイヤーを安全・完全に停止させる ---
        if (playerMovement != null) playerMovement.enabled = false;

        if (playerController != null)
        {
            // ★ここが超重要！
            // イベント突入時に、現在のステート（StateBurstなど）のExit()を強制的に走らせ、
            // 安全な通常状態（StateNormal）に戻します。これで転がりの内部処理やエフェクトが綺麗に消えます。
            playerController.TransitionToState(playerController.StateNormal);

            // 入力をシャットアウト
            if (playerController.inputActions != null)
            {
                playerController.inputActions.Player.Disable();
            }

            // プレイヤーのUpdateやFixedUpdateを一時停止
            playerController.enabled = false;
        }

        // 物理的な慣性を完全にゼロにする（ユーザーの環境に合わせて linearVelocity に変更）
        Rigidbody2D rb2d = player.GetComponent<Rigidbody2D>();
        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
            rb2d.angularVelocity = 0f;
        }

        // --- 2. カメラの挙動 ---
        if (cameraFollow != null && cameraTarget != null)
        {
            float originalPosSpeed = cameraFollow.positionSmoothSpeed;
            float originalZoomSpeed = cameraFollow.zoomSmoothSpeed;

            cameraFollow.positionSmoothSpeed = eventPositionSpeed;
            cameraFollow.zoomSmoothSpeed = eventZoomSpeed;

            cameraFollow.LockCamera(cameraTarget.position, cameraTarget.position.z);

            yield return new WaitForSeconds(timeToTarget);

            cameraFollow.transform.position = new Vector3(cameraTarget.position.x, cameraTarget.position.y, cameraTarget.position.z);

            yield return new WaitForSeconds(freezeDuration);

            cameraFollow.UnlockCamera();

            yield return new WaitForSeconds(timeToReturn);

            cameraFollow.positionSmoothSpeed = originalPosSpeed;
            cameraFollow.zoomSmoothSpeed = originalZoomSpeed;
        }
        else
        {
            yield return new WaitForSeconds(freezeDuration);
        }

        // --- 3. 全てのカメラワークが終了したら、プレイヤーを復帰 ---
        if (playerController != null)
        {
            playerController.enabled = true;
            if (playerController.inputActions != null)
            {
                playerController.inputActions.Player.Enable();
            }
        }
        if (playerMovement != null) playerMovement.enabled = true;

        // このイベントトリガーを削除して終了
        Destroy(gameObject);
    }
}