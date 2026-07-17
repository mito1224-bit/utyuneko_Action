using System.Collections;
using UnityEngine;

public class CoreCubeController : MonoBehaviour
{
    // ここにProjectビューの「Goalプレハブ」をアタッチ
    [SerializeField] private GameObject goalPrefab;

    // ここにヒエラルキー上に作った「空のオブジェクト」をアタッチ
    [Header("ゴールの出現位置（空のオブジェクト）")]
    [SerializeField] private Transform spawnPoint;


    [Header("カメライベント")]
    [SerializeField] private CameraFollowWithZoom eventCamera;
    [SerializeField] private CameraBoundsTrigger BoundTriggerCamera;


    [Header("演出時間")]
    [SerializeField] private float cameraMoveWait = 1.0f;     // カメラがGoal位置へ寄るまで待つ時間
    [SerializeField] private float goalViewWait = 1.0f;       // Goal出現後に見せる時間
    [SerializeField] private float cameraReturnTime = 1.0f;

    [Header("CoreCube消滅演出")]
    [SerializeField] private Transform visualRoot; // 見た目だけを入れる。未設定なら自分自身を使う
    [SerializeField] private float coreDisappearTime = 3.0f;
    [SerializeField] private float coreRotateSpeed = 720f;

    [Header("カメライベント待機時間")]
    [SerializeField] private float coreCameraWait = 0.5f; // CoreCubeを見る時間
    [SerializeField] private float goalCameraWait = 0.5f; // Goal



    [Header("カメラ速度")]
    [SerializeField] private float eventPositionSpeed = 3.0f;
    [SerializeField] private float eventZoomSpeed = 2.0f;

    private bool isActivated = false;

    
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isActivated) return;

        if (collision.CompareTag("Player"))
        {
            isActivated = true;

            // 二重判定防止
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.enabled = false;
            }
            SoundManager.Instance.PlaySE(SeType.ItemCoreGet);
            StartCoroutine(GoalAppearEventRoutine(collision.gameObject));
        }
    }

    private IEnumerator GoalAppearEventRoutine(GameObject player)
    {
        // Playerの速度を止める
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (goalPrefab == null)
        {
            Debug.LogError("【デバッグ】エラー: Goalプレハブが登録されていません。");
            yield break;
        }

        // Goalを出す位置
        Vector3 goalPosition = transform.parent != null
            ? transform.parent.position
            : transform.position;

        if (spawnPoint != null)
        {
            goalPosition = spawnPoint.position;
        }
        else
        {
            Debug.LogWarning("【デバッグ】警告: Spawn Pointが未設定のため、キューブの位置に生成します。");
        }

        if(BoundTriggerCamera) BoundTriggerCamera.gameObject.SetActive(false);

        // ==================================================
        // ① CoreCube用カメライベント
        // ==================================================
        Transform coreCameraTarget = transform;

        if (eventCamera != null)
        {
            eventCamera.StartTrackTarget(
                coreCameraTarget,
                eventPositionSpeed,
                eventZoomSpeed
            );
        }
        else
        {
            Debug.LogWarning("【デバッグ】EventCameraが登録されていません。");
        }

        // カメラがCoreCubeに寄るまで少し待つ
        yield return new WaitForSeconds(coreCameraWait);

        // CoreCube本体が回転しながら縮小して消える
        yield return StartCoroutine(DisappearCoreCube());

        // ==================================================
        // ② Goal出現位置用カメライベント
        // ==================================================
        Transform goalCameraTarget = spawnPoint != null ? spawnPoint : transform;

        if (eventCamera != null)
        {
            eventCamera.StartTrackTarget(
                goalCameraTarget,
                eventPositionSpeed,
                eventZoomSpeed
            );
        }

        // カメラがGoal出現位置に寄るまで待つ
        yield return new WaitForSeconds(goalCameraWait);

        // Goal生成
        GameObject goal = Instantiate(goalPrefab, goalPosition, Quaternion.identity);

        Debug.Log($"【デバッグ】Goalを生成しました: {goalPosition}");

        // Goalを見せる
        yield return new WaitForSeconds(goalViewWait);

        // カメラをPlayerに戻す
        if (eventCamera != null)
        {
            eventCamera.ReturnToPlayerFromEvent(cameraReturnTime);
        }

        yield return new WaitForSeconds(cameraReturnTime);

        if (BoundTriggerCamera) BoundTriggerCamera.gameObject.SetActive(true);

        // 最後にCoreCube本体を削除
        if (transform.parent != null)
        {
            Destroy(transform.parent.gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private IEnumerator DisappearCoreCube()
    {
        Transform target = visualRoot != null ? visualRoot : transform;

        Vector3 startScale = target.localScale;
        Vector3 popScale = startScale * 1.8f;
        float elapsedTime = 0f;

        while (elapsedTime < coreDisappearTime)
        {
            elapsedTime += Time.deltaTime;

            //float t = elapsedTime / coreDisappearTime;
            float t = Mathf.Clamp01(elapsedTime / coreDisappearTime);

            // 回転
            target.Rotate(
                0f,
                0f,
                coreRotateSpeed * Time.deltaTime
            );
            Vector3 currentScale;
            if (t < 0.4f)
            {
                // 最初の20%で拡大
                float popT = t / 0.2f;
                currentScale = Vector3.Lerp(
                    startScale,
                    popScale,
                    Mathf.Sin(popT * Mathf.PI * 0.5f)
                );
            }
            else
            {
                // 残り80%で縮小
                float shrinkT = (t - 0.2f) / 0.8f;

                currentScale = Vector3.Lerp(
                    popScale,
                    Vector3.zero,
                    Mathf.Sin(shrinkT * Mathf.PI * 0.5f)
                );
            }

            target.localScale = currentScale;
            yield return null;
        }

        target.localScale = Vector3.zero;
    }

}
