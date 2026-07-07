using UnityEngine;

/// <summary>
/// ボススナイパーのステージ用カメラ固定トリガー（CameraBoundsTrigger のボス戦対応版）。
///
/// 通常の CameraBoundsTrigger は OnTriggerStay2D で毎フレーム LockCamera を呼ぶため、
/// 出現・強化・撃破のイベントカメラ（ボスへの寄り・ズーム）を毎フレーム上書きして潰してしまう。
/// このトリガーは BossSniperCameraDirector.EventCameraActive が立っている間だけ
/// ロック・解除を控えて、イベントカメラに制御を譲る。
/// イベントが終わってフラグが下りると、OnTriggerStay2D が自動的に部屋の固定点へロックし直す
/// （カメラは LockCamera の固定点へ滑らかに戻っていく）。
///
/// セットアップ:
///   - ボスステージを覆っていた既存の CameraBoundsTrigger コンポーネントを外し、
///     代わりにこれを付ける（両方付けると元と同じ競合が起きるので必ず置き換えること）。
///   - 使い方は元と同じ: BoxCollider2D（IsTrigger）＋ 子に "CameraPoint"（任意）＋ targetZOffset。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BossSniperCameraBoundsTrigger : MonoBehaviour
{
    [Header("カメラの引き量（Z座標の固定値）")]
    [Tooltip("トリガーに入ったときのカメラのZ座標を指定します。通常のカメラのデフォルトは -10 です。")]
    public float targetZOffset = -15f;

    // 内部で自動取得するため非公開
    private Transform cameraTargetPoint;
    private CameraFollowWithZoom customCameraController;
    private BoxCollider2D triggerCollider;

    void Start()
    {
        InitializeReferences();
    }

    private void InitializeReferences()
    {
        if (triggerCollider == null) triggerCollider = GetComponent<BoxCollider2D>();

        if (cameraTargetPoint == null)
        {
            Transform foundChild = transform.Find("CameraPoint");
            if (foundChild != null)
            {
                cameraTargetPoint = foundChild;
            }
        }

        if (customCameraController == null)
        {
            GameObject mainCamObj = GameObject.FindWithTag("MainCamera");
            if (mainCamObj != null)
            {
                customCameraController = mainCamObj.GetComponentInParent<CameraFollowWithZoom>();
            }
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        TryLock(collision);
    }

    void OnTriggerStay2D(Collider2D collision)
    {
        TryLock(collision);
    }

    private void TryLock(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;

        // イベントカメラ（出現・強化・撃破の寄り／ズーム）作動中は譲る。
        // ここで LockCamera を呼ぶと毎フレーム上書きしてイベントの寄りを潰してしまう
        if (BossSniperCameraDirector.EventCameraActive) return;

        InitializeReferences();

        if (customCameraController != null)
        {
            Vector3 lockBasePosition = (cameraTargetPoint != null) ? cameraTargetPoint.position :
                                       (triggerCollider != null ? triggerCollider.bounds.center : transform.position);

            customCameraController.LockCamera(lockBasePosition, targetZOffset);
        }
        else
        {
            Debug.LogError($"[BossSniperCameraBoundsTrigger] {gameObject.name} から 'MainCamera' の親にある 'CameraFollowWithZoom' が見つかりません！タグの設定や構造を確認してください。");
        }
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;

        // イベント中の解除もイベントカメラのロックを壊すので控える
        if (BossSniperCameraDirector.EventCameraActive) return;

        InitializeReferences();
        if (customCameraController != null)
        {
            customCameraController.UnlockCamera();
        }
    }

    void OnDrawGizmos()
    {
        if (triggerCollider == null) triggerCollider = GetComponent<BoxCollider2D>();
        if (cameraTargetPoint == null) cameraTargetPoint = transform.Find("CameraPoint");

        Gizmos.color = Color.cyan;
        Vector3 pointPos = (cameraTargetPoint != null) ? cameraTargetPoint.position :
                           (triggerCollider != null ? triggerCollider.bounds.center : transform.position);

        Gizmos.DrawSphere(pointPos, 0.4f);
    }
}