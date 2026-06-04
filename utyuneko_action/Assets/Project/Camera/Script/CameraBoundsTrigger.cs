using UnityEngine;

public class CameraBoundsTrigger : MonoBehaviour
{
    // ?? エリアごとに「カメラをどれくらい後ろに引くか」を直接Z座標で指定する
    [Header("このエリアに入った時のカメラのZ座標（引き量）")]
    [Tooltip("通常のカメラのデフォルト（offset.z）が -10 の場合、さらに引きたいときは -15 や -20 などの数値を入れます")]
    public float areaZOffset = -15f;

    [Header("カメラを固定する場所（空のオブジェクト等を指定）")]
    [Tooltip("ここに指定したオブジェクトの位置にカメラが固定されます。空欄ならこのトリガーの中心になります。")]
    public Transform cameraTargetPoint;

    private CameraFollowWithZoom cameraController;
    private BoxCollider2D triggerCollider;

    void Start()
    {
        cameraController = Camera.main.GetComponent<CameraFollowWithZoom>();
        triggerCollider = GetComponent<BoxCollider2D>();
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && cameraController != null)
        {
            Vector3 lockBasePosition = transform.position; // デフォルトはトリガーの中心

            if (cameraTargetPoint != null)
            {
                lockBasePosition = cameraTargetPoint.position;
            }

            // カメラに位置と「専用のZ座標」を伝えてロック
            cameraController.LockCamera(lockBasePosition, areaZOffset);
        }
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && cameraController != null)
        {
            cameraController.UnlockCamera();
        }
    }

    void OnDrawGizmos()
    {
        if (triggerCollider == null) triggerCollider = GetComponent<BoxCollider2D>();
        if (triggerCollider == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, triggerCollider.size);

        Gizmos.color = Color.cyan;
        Vector3 pointPos = cameraTargetPoint != null ? cameraTargetPoint.position : transform.position;
        Gizmos.DrawSphere(pointPos, 0.5f);
    }
}

//using UnityEngine;

//public class CameraBoundsTrigger : MonoBehaviour
//{
//    [Header("このエリアに入った時のカメラの視野角(FOV)")]
//    public float areaFOV = 75f;

//    // ??【新機能】カメラを固定したいターゲット位置
//    [Header("カメラを固定する場所（空のオブジェクト等を指定）")]
//    [Tooltip("ここに指定したオブジェクトの位置にカメラが固定されます。空欄ならこのトリガーの中心になります。")]
//    public Transform cameraTargetPoint;

//    private CameraFollowWithZoom cameraController;
//    private BoxCollider2D triggerCollider;

//    void Start()
//    {
//        cameraController = Camera.main.GetComponent<CameraFollowWithZoom>();
//        triggerCollider = GetComponent<BoxCollider2D>();
//    }

//    void OnTriggerEnter2D(Collider2D collision)
//    {
//        if (collision.CompareTag("Player") && cameraController != null)
//        {
//            // ??【修正】カメラの固定位置を決定する
//            Vector3 lockBasePosition = transform.position; // デフォルトはトリガーの中心

//            // もしインスペクターで特定の固定場所が指定されていたら、その位置を使う
//            if (cameraTargetPoint != null)
//            {
//                lockBasePosition = cameraTargetPoint.position;
//            }

//            Vector3 targetLockPos = new Vector3(
//                lockBasePosition.x,
//                lockBasePosition.y,
//                cameraController.transform.position.z // カメラの奥行きは維持
//            );

//            // カメラに位置とFOVを伝えてロック
//            cameraController.LockCamera(targetLockPos, areaFOV);
//        }
//    }

//    void OnTriggerExit2D(Collider2D collision)
//    {
//        if (collision.CompareTag("Player") && cameraController != null)
//        {
//            cameraController.UnlockCamera();
//        }
//    }

//    void OnDrawGizmos()
//    {
//        if (triggerCollider == null) triggerCollider = GetComponent<BoxCollider2D>();
//        if (triggerCollider == null) return;

//        // ボス戦エリアの枠を緑で描画
//        Gizmos.color = Color.green;
//        Gizmos.DrawWireCube(transform.position, triggerCollider.size);

//        // ??【新機能】カメラの固定位置に、編集画面で目印のアイコンを描く
//        Gizmos.color = Color.cyan; // 水色
//        Vector3 pointPos = cameraTargetPoint != null ? cameraTargetPoint.position : transform.position;
//        Gizmos.DrawSphere(pointPos, 0.5f); // 固定位置に小さな球体を表示
//    }
//}


//public class CameraBoundsTrigger : MonoBehaviour
//{
//    [Header("カメラが動ける範囲（このオブジェクトの中心からの距離）")]
//    [Tooltip("オブジェクトの中心から、カメラがどれだけ左・下に行けるか（基本はマイナス値）")]
//    public Vector2 minCameraOffset = new Vector2(-10f, -5f);

//    [Tooltip("オブジェクトの中心から、カメラがどれだけ右・上に行けるか（基本はプラス値）")]
//    public Vector2 maxCameraOffset = new Vector2(10f, 5f);

//    private CameraFollowWithZoom cameraController;

//    void Start()
//    {
//        cameraController = Camera.main.GetComponent<CameraFollowWithZoom>();
//    }

//    void OnTriggerEnter2D(Collider2D collision)
//    {
//        if (collision.CompareTag("Player"))
//        {
//            // 世界の絶対座標に変換してカメラに伝える
//            Vector2 worldMin = (Vector2)transform.position + minCameraOffset;
//            Vector2 worldMax = (Vector2)transform.position + maxCameraOffset;
//            cameraController.SetBounds(worldMin, worldMax);
//        }
//    }

//    void OnTriggerExit2D(Collider2D collision)
//    {
//        if (collision.CompareTag("Player"))
//        {
//            cameraController.ClearBounds();
//        }
//    }

//    // オブジェクトの位置を中心として、綺麗に枠を描く
//    void OnDrawGizmosSelected()
//    {
//        Gizmos.color = Color.red;

//        // 枠の中心点
//        Vector3 center = transform.position + new Vector3((minCameraOffset.x + maxCameraOffset.x) / 2, (minCameraOffset.y + maxCameraOffset.y) / 2, 0);
//        // 枠のサイズ
//        Vector3 size = new Vector3(maxCameraOffset.x - minCameraOffset.x, maxCameraOffset.y - minCameraOffset.y, 1);

//        Gizmos.DrawWireCube(center, size);
//    }
//}