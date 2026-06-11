using UnityEngine;

public class CameraBoundsTrigger : MonoBehaviour
{
    [Header("カメラを固定する場所（空欄ならコライダーの中心になります）")]
    [Tooltip("ここに指定したオブジェクトの位置にカメラが固定されます。空欄なら自動でコライダーの真ん中になります。")]
    public Transform cameraTargetPoint;

    [Header("カメラの引き量（Z座標の固定値）")]
    [Tooltip("トリガーに入ったときのカメラのZ座標を指定します。マイナスの値で指定してください（例: -15 や -20 など）。通常のカメラのデフォルトは -10 です。")]
    public float targetZOffset = -15f;

    [Header("手動割り当て（空欄なら自動取得します）")]
    public CameraFollowWithZoom customCameraController;

    private BoxCollider2D triggerCollider;
    private Camera mainCamera;

    void Start()
    {
        InitializeReferences();
    }

    private void InitializeReferences()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        if (customCameraController == null && mainCamera != null)
        {
            customCameraController = mainCamera.GetComponent<CameraFollowWithZoom>();
        }

        if (triggerCollider == null) triggerCollider = GetComponent<BoxCollider2D>();
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            InitializeReferences();

            if (customCameraController != null)
            {
                // コライダーの実際の中心点(Offsetも考慮)を基準にする（TargetPointがあればそちらを優先）
                Vector3 lockBasePosition = triggerCollider != null ? triggerCollider.bounds.center : transform.position;

                if (cameraTargetPoint != null)
                {
                    lockBasePosition = cameraTargetPoint.position;
                }

                // インスペクターで設定した「固定の引き量（Z値）」をそのまま渡してカメラをロック
                customCameraController.LockCamera(lockBasePosition, targetZOffset);
            }
            else
            {
                Debug.LogWarning("[CameraBoundsTrigger] CameraFollowWithZoomスクリプトが見つかりません。カメラオブジェクトを手動で割り当ててください。");
            }
        }
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            InitializeReferences();
            if (customCameraController != null)
            {
                customCameraController.UnlockCamera();
            }
        }
    }

    void OnDrawGizmos()
    {
        if (triggerCollider == null) triggerCollider = GetComponent<BoxCollider2D>();

        Gizmos.color = Color.cyan;
        Vector3 pointPos = cameraTargetPoint != null ? cameraTargetPoint.position :
                           (triggerCollider != null ? triggerCollider.bounds.center : transform.position);
        Gizmos.DrawSphere(pointPos, 0.4f);
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