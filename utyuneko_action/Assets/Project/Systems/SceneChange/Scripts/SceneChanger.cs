using UnityEngine;
using UnityEngine.SceneManagement; // シーン切り替えに必要
using System.Collections;

public class SceneChanger : MonoBehaviour
{
    [SerializeField, Tooltip("チェックを入れると、このEntry演出はステージクリア扱いになります（ステージ内のゴールに使用）")]
    private bool isGoalObject = false; // ★追加

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

    [Header("退出演出設定 (Exitモード専用)")]
    [SerializeField, Tooltip("出現後にカメラが一度見に行く『次のステージの見どころ』")]
    private Transform nextStageLookPoint;
    [SerializeField] private float lookPanDuration = 1.0f;   // 見どころへ移動する時間
    [SerializeField] private float lookAtDuration = 1.0f;    // 見どころを見せている時間
    [SerializeField] private float returnPanDuration = 1.0f; // プレイヤーへ戻る時間

    [Header("UI")]
    [SerializeField] private ButtonPromptUI buttonPrompt;

    private bool playerInRange = false;
    private PlayerController playerInRangeRef;

    private void Start()
    {
        bool wasThisGateUsed = GameManager.Instance != null &&
                                        GameManager.Instance.TryConsumeLastClearedScene(nextSceneName);

        if (wasThisGateUsed)
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null)
            {
                bool alreadyPlayedReveal = false;
                if (GameManager.Instance != null)
                {
                    StoryPhase phase = GameManager.Instance.CurrentSaveData.currentPhase;
                    alreadyPlayedReveal = GameManager.Instance.IsGateRevealPlayed(phase);
                }
                isWarping = true; // 退出演出中も、入場トリガーが誤発火しないようロックしておく
                StartCoroutine(ExitAnimationRoutine(player, alreadyPlayedReveal));
            }
        }
    }

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
                playerInRange = true;
                playerInRangeRef = player;
                //決定ボタンUIをここで表示
                if (buttonPrompt != null) buttonPrompt.Show(player);


                // StartCoroutine(WarpAnimationRoutine(playerInRangeRef));
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if(other.CompareTag("Player"))
        {
            playerInRange = false;
            playerInRangeRef = null;
            if (buttonPrompt != null) buttonPrompt.Hide();
        }
    }

    private void Update()
    {
        if(playerInRange&&!isWarping&&InputManager.Instance.Player.Submit.triggered)
        {
            isWarping = true;
            SoundManager.Instance.PlaySE(SeType.GimmickWarp);
            if (buttonPrompt != null) buttonPrompt.Hide();
            // 演出コルーチンを開始
            StartCoroutine(WarpAnimationRoutine(playerInRangeRef));
        }
    }

    // ====================================================================
    // Entry演出
    // ====================================================================
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

        if (isGoalObject)
        {
            if (DataManager.Instance != null)
            {
                DataManager.Instance.ProcessStageClear();
            }
            else
            {
                Debug.LogWarning("[SceneChanger] DataManagerが見つかりません。");
            }
        }

        TransitionManager.Instance.ChangeScene(nextSceneName, TransitionType.Wipe);
        // 念のため保険としてロック解除しておく（実害はないが安全策）
        isWarping = false;
    }

    // ====================================================================
    // Exit演出
    // ====================================================================
    private IEnumerator ExitAnimationRoutine(PlayerController player, bool skipReveal)
    {
        // 【1. 出現前の準備】プレイヤーを入場位置に固定し、操作を止める
        player.TransitionToState(player.StateNone);

        Transform playerTransform = player.transform;
        Vector3 spawnPos = transform.position;
        playerTransform.position = spawnPos;

        Vector3 fullScale = playerTransform.localScale;
        playerTransform.localScale = Vector3.zero;

        if (player.rb2D != null)
        {
            player.rb2D.linearVelocity = Vector2.zero;
            player.rb2D.simulated = false;
        }

        Camera mainCam = Camera.main;
        CameraFollowWithZoom camWithZoom = mainCam != null ? mainCam.GetComponent<CameraFollowWithZoom>() : null;

        // Entry演出終了時と同じズームイン状態から始める
        if (camWithZoom != null)
        {
            camWithZoom.LockCamera(spawnPos, zoomTargetZ);
        }

        // 【2. 出現アニメーション：吸い込みの逆再生】
        float elapsed = 0f;
        while (elapsed < warpDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / warpDuration;
            float easeOut = 1f - (1f - t) * (1f - t);

            float currentX = Mathf.Lerp(0f, fullScale.x, easeOut);
            float currentY = Mathf.Lerp(fullScale.y * stretchMultiplier, fullScale.y, easeOut) * easeOut;
            playerTransform.localScale = new Vector3(currentX, currentY, fullScale.z);

            playerTransform.Rotate(0f, 0f, spinSpeed * (1f - easeOut) * Time.deltaTime);

            if (camWithZoom != null)
            {
                float normalZ = camWithZoom.offset.z; // 通常時のズーム距離をそのまま使う
                float currentZ = Mathf.Lerp(zoomTargetZ, normalZ, easeOut);
                camWithZoom.LockCamera(spawnPos, currentZ);
            }

            yield return null;
        }

        playerTransform.localScale = fullScale;
        playerTransform.rotation = Quaternion.identity;

        if (!skipReveal && camWithZoom != null && nextStageLookPoint != null)
        {
            camWithZoom.StartZoomTrack(nextStageLookPoint, camWithZoom.offset.z, lookPanDuration);
            yield return new WaitForSeconds(lookPanDuration + lookAtDuration);

            camWithZoom.ReturnFromZoomEvent(returnPanDuration);
            yield return new WaitForSeconds(returnPanDuration);

            // ★演出を最後まで見終えたのでここで既読マーク
            if (GameManager.Instance != null)
            {
                StoryPhase phase = GameManager.Instance.CurrentSaveData.currentPhase;
                GameManager.Instance.MarkGateRevealPlayed(phase);
            }
        }
        else
        {
            // 見どころ演出がない、またはすでに見た場合は自力で操作を戻す
            if (camWithZoom != null) camWithZoom.UnlockCamera(); // ★追加：ロック解除を忘れずに
            player.TransitionToState(player.StateNormal);
        }

        if (player.rb2D != null) player.rb2D.simulated = true;
        //出現演出が終わったら、このゲートを再び「入れる」状態に戻す
        isWarping = false;
    }
}