using UnityEngine;

public class CameraFollowWithZoom : MonoBehaviour
{
    [Header("追従対象")]
    public Transform target;

    [Header("基本の位置オフセット")]
    public Vector3 offset = new Vector3(0, 5, -10);

    [Header("位置追従のなめらかさ")]
    public float positionSmoothSpeed = 3f;

    [Header("視野角（FOV）の調整")]
    public float heightThreshold = 3f;
    public float minFOV = 60f;
    public float maxFOV = 90f;
    public float fovSensitivity = 2f;
    public float fovSmoothSpeed = 5f;

    [Header("バウンド軽減用")]
    public float heightFilterSpeed = 2f;

    [Header("2D地面の判定設定")]
    public LayerMask groundLayer2D = ~0;

    [Header("カメラの完全固定モード")]
    public bool isLocked = false;
    public Vector3 lockedPosition;
    private float lockedZOffset; // ?? 固定中専用のZオフセット（引き量）を保存する変数

    private Camera cam;
    private float filteredFloatingHeight;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam != null) cam.fieldOfView = minFOV;

        if (target != null)
        {
            transform.position = target.position + offset;
        }
    }

    void LateUpdate()
    {
        if (target == null || cam == null) return;

        Vector3 targetPosition;
        float currentZOffset = offset.z; // 通常時のZ位置

        // --------------------------------------------------
        // 1. 位置とZ軸の計算
        // --------------------------------------------------
        if (isLocked)
        {
            // 固定時は指定された位置を使うが、Z軸だけはエリア専用の引き量（lockedZOffset）にする
            targetPosition = new Vector3(lockedPosition.x, lockedPosition.y, lockedZOffset);
        }
        else
        {
            // 通常時はプレイヤーをヌルッと追従
            targetPosition = target.position + offset;
        }

        // カメラをなめらかに目標位置（Zの引きも含む）へ移動させる
        transform.position = Vector3.Lerp(transform.position, targetPosition, positionSmoothSpeed * Time.deltaTime);

        // --------------------------------------------------
        // 2. 通常時のみ動く：地面からの高さに応じた自動FOV計算（これまでの機能）
        // --------------------------------------------------
        float targetFOV = minFOV;

        if (!isLocked)
        {
            float currentFloatingHeight = 0f;
            Vector2 rayStart = new Vector2(target.position.x, target.position.y);
            RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, 100f, groundLayer2D);

            if (hit.collider != null)
            {
                currentFloatingHeight = target.position.y - hit.point.y;
            }

            filteredFloatingHeight = Mathf.Lerp(filteredFloatingHeight, currentFloatingHeight, heightFilterSpeed * Time.deltaTime);

            if (filteredFloatingHeight > heightThreshold)
            {
                float excessHeight = filteredFloatingHeight - heightThreshold;
                targetFOV = minFOV + (excessHeight * fovSensitivity);
                targetFOV = Mathf.Clamp(targetFOV, minFOV, maxFOV);
            }
        }
        else
        {
            // ボス戦（固定）中は、FOVを通常の基本サイズ（minFOV）で固定しておく
            targetFOV = minFOV;
        }

        // FOVを変更
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, fovSmoothSpeed * Time.deltaTime);
    }

    // ??【修正】引数を「固定中のZ座標」に変更
    public void LockCamera(Vector3 positionToLock, float targetZValue)
    {
        lockedPosition = positionToLock;
        lockedZOffset = targetZValue; // 固定中のZ位置（引き量）を保存
        isLocked = true;
    }

    public void UnlockCamera()
    {
        isLocked = false;
    }
}

//using UnityEngine;

//public class CameraFollowWithZoom : MonoBehaviour
//{
//    [Header("追従対象")]
//    public Transform target;

//    [Header("基本の位置オフセット")]
//    public Vector3 offset = new Vector3(0, 5, -10);

//    [Header("位置追従のなめらかさ")]
//    public float positionSmoothSpeed = 3f;

//    [Header("視野角（FOV）の調整")]
//    public float heightThreshold = 3f;
//    public float minFOV = 60f;
//    public float maxFOV = 90f;
//    public float fovSensitivity = 2f;
//    public float fovSmoothSpeed = 5f;

//    [Header("バウンド軽減用")]
//    public float heightFilterSpeed = 2f;

//    [Header("2D地面の判定設定")]
//    public LayerMask groundLayer2D = ~0;

//    [Header("カメラの完全固定モード")]
//    public bool isLocked = false;
//    public Vector3 lockedPosition;
//    private float lockedFOV; // ?? 固定中専用のFOVを保存する変数

//    private Camera cam;
//    private float filteredFloatingHeight;

//    void Start()
//    {
//        cam = GetComponent<Camera>();
//        if (cam != null) cam.fieldOfView = minFOV;

//        if (target != null)
//        {
//            transform.position = target.position + offset;
//        }
//    }

//    void LateUpdate()
//    {
//        if (target == null || cam == null) return;

//        Vector3 targetPosition;
//        float targetFOV = minFOV; // 基本のターゲットFOV

//        // ?? 固定モードの判定
//        if (isLocked)
//        {
//            targetPosition = lockedPosition;
//            transform.position = Vector3.Lerp(transform.position, targetPosition, positionSmoothSpeed * Time.deltaTime);

//            // ?? 固定中は、エリアから指定された「専用のFOV」をターゲットにする
//            targetFOV = lockedFOV;
//        }
//        else
//        {
//            // 通常時の移動処理
//            targetPosition = target.position + offset;
//            transform.position = Vector3.Lerp(transform.position, targetPosition, positionSmoothSpeed * Time.deltaTime);

//            // 通常時は「地面からの高さ」に応じて自動計算する（これまでの機能）
//            float currentFloatingHeight = 0f;
//            Vector2 rayStart = new Vector2(target.position.x, target.position.y);
//            RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, 100f, groundLayer2D);

//            if (hit.collider != null)
//            {
//                currentFloatingHeight = target.position.y - hit.point.y;
//            }

//            filteredFloatingHeight = Mathf.Lerp(filteredFloatingHeight, currentFloatingHeight, heightFilterSpeed * Time.deltaTime);

//            if (filteredFloatingHeight > heightThreshold)
//            {
//                float excessHeight = filteredFloatingHeight - heightThreshold;
//                targetFOV = minFOV + (excessHeight * fovSensitivity);
//                targetFOV = Mathf.Clamp(targetFOV, minFOV, maxFOV);
//            }
//        }

//        // 最終的なFOVの変更（固定中も通常時もヌルッと変化する）
//        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, fovSmoothSpeed * Time.deltaTime);
//    }

//    // ??【修正】引数に「引くためのFOV」を追加
//    public void LockCamera(Vector3 positionToLock, float targetFovValue)
//    {
//        lockedPosition = positionToLock;
//        lockedFOV = targetFovValue; // 固定中のFOVを保存
//        isLocked = true;
//    }

//    public void UnlockCamera()
//    {
//        isLocked = false;
//    }
//}

//[Header("追従対象")]
//public Transform target;

//[Header("基本の位置オフセット")]
//public Vector3 offset = new Vector3(0, 5, -10);

//[Header("位置追従のなめらかさ")]
//[Tooltip("数値を小さくするほどカメラがヌルッと遅れて追従し、プレイヤーの跳ねによる縦揺れを吸収します")]
//public float positionSmoothSpeed = 3f;

//[Header("視野角（FOV）の調整")]
//public float heightThreshold = 3f;  // 地面からこの高さ（ユニット）を浮いたら広げる
//public float minFOV = 60f;
//public float maxFOV = 90f;
//public float fovSensitivity = 2f;
//public float fovSmoothSpeed = 5f;

//[Header("バウンド軽減用（FOVの伸縮用）")]
//public float heightFilterSpeed = 2f;

//[Header("2D地面の判定設定")]
//public LayerMask groundLayer2D = ~0;

//[Header("【新機能】カメラの移動制限範囲")]
//public bool useBounds = false; // 範囲制限を有効にするか（トリガーがONにします）
//public Vector2 minBounds;      // 画面の中心が行ける最小の(X, Y)
//public Vector2 maxBounds;      // 画面の中心が行ける最大の(X, Y)

//private Camera cam;
//private float filteredFloatingHeight;

//void Start()
//{
//    cam = GetComponent<Camera>();
//    if (cam != null) cam.fieldOfView = minFOV;

//    // ゲーム開始時にカメラが遠くからすっ飛んでくるのを防ぐため、初期位置を合わせる
//    if (target != null)
//    {
//        transform.position = target.position + offset;
//    }
//}

//void LateUpdate()
//{
//    if (target == null || cam == null) return;

//    // --------------------------------------------------
//    // 1. 通常の目標位置を計算し、Lerpでなめらかに追従
//    // --------------------------------------------------
//    Vector3 targetPosition = target.position + offset;

//    // --------------------------------------------------
//    // 2. 【新機能】もし範囲制限がONなら、目標位置を四角い枠の中に閉じ込める
//    // --------------------------------------------------
//    if (useBounds)
//    {
//        float clampedX = Mathf.Clamp(targetPosition.x, minBounds.x, maxBounds.x);
//        float clampedY = Mathf.Clamp(targetPosition.y, minBounds.y, maxBounds.y);

//        // Z軸（カメラの奥行き）はそのままに、XとYだけを制限
//        targetPosition = new Vector3(clampedX, clampedY, targetPosition.z);
//    }

//    // 実際にカメラを移動させる
//    transform.position = Vector3.Lerp(transform.position, targetPosition, positionSmoothSpeed * Time.deltaTime);

//    // --------------------------------------------------
//    // 3. 2Dレイキャストで地面の高さを調べ、FOVをなめらかに計算
//    // --------------------------------------------------
//    float currentFloatingHeight = 0f;
//    Vector2 rayStart = new Vector2(target.position.x, target.position.y);
//    RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, 100f, groundLayer2D);

//    if (hit.collider != null)
//    {
//        currentFloatingHeight = target.position.y - hit.point.y;
//    }

//    // FOV計算用の高さをなめらかにする（バウンドでのグワングワン防止）
//    filteredFloatingHeight = Mathf.Lerp(filteredFloatingHeight, currentFloatingHeight, heightFilterSpeed * Time.deltaTime);

//    // FOVの計算
//    float targetFOV = minFOV;
//    if (filteredFloatingHeight > heightThreshold)
//    {
//        float excessHeight = filteredFloatingHeight - heightThreshold;
//        targetFOV = minFOV + (excessHeight * fovSensitivity);
//        targetFOV = Mathf.Clamp(targetFOV, minFOV, maxFOV);
//    }

//    // FOVを変更
//    cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, fovSmoothSpeed * Time.deltaTime);
//}

//// 外部（エリアトリガー）からカメラの範囲を上書きするための関数
//public void SetBounds(Vector2 min, Vector2 max)
//{
//    minBounds = min;
//    maxBounds = max;
//    useBounds = true;
//}

//// 制限を解除して元の自由な追従に戻す関数
//public void ClearBounds()
//{
//    useBounds = false;
//}
//[Header("追従対象")]
//public Transform target;

//[Header("基本の位置オフセット")]
//public Vector3 offset = new Vector3(0, 5, -10);

//[Header("【新機能】位置追従のなめらかさ")]
//[Tooltip("数値を小さくするほどカメラがヌルッと遅れて追従し、プレイヤーの跳ねによる縦揺れを吸収します")]
//public float positionSmoothSpeed = 3f;

//[Header("視野角（FOV）の調整")]
//public float heightThreshold = 3f;
//public float minFOV = 60f;
//public float maxFOV = 90f;
//public float fovSensitivity = 2f;
//public float fovSmoothSpeed = 5f;

//[Header("バウンド軽減用（FOVの伸縮用）")]
//public float heightFilterSpeed = 2f;

//[Header("2D地面の判定設定")]
//public LayerMask groundLayer2D = ~0;

//private Camera cam;
//private float filteredFloatingHeight;

//void Start()
//{
//    cam = GetComponent<Camera>();
//    if (cam != null) cam.fieldOfView = minFOV;

//    // ゲーム開始時にカメラが遠くからすっ飛んでくるのを防ぐため、初期位置を合わせる
//    if (target != null)
//    {
//        transform.position = target.position + offset;
//    }
//}

//void LateUpdate()
//{
//    if (target == null || cam == null) return;

//    // 1. 【ここを改良】線形補間（Lerp）を使って、カメラ位置をヌルッと追従させる
//    Vector3 targetPosition = target.position + offset;

//    // 現在のカメラ位置から、目標位置に向けて「positionSmoothSpeed」の強さで滑らかに近づける
//    transform.position = Vector3.Lerp(transform.position, targetPosition, positionSmoothSpeed * Time.deltaTime);

//    // 2. 2Dレイキャストで地面の高さを調べる
//    float currentFloatingHeight = 0f;
//    Vector2 rayStart = new Vector2(target.position.x, target.position.y);
//    RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, 100f, groundLayer2D);

//    if (hit.collider != null)
//    {
//        currentFloatingHeight = target.position.y - hit.point.y;
//    }

//    // 3. FOV計算用の高さをなめらかにする
//    filteredFloatingHeight = Mathf.Lerp(filteredFloatingHeight, currentFloatingHeight, heightFilterSpeed * Time.deltaTime);

//    // 4. FOVの計算
//    float targetFOV = minFOV;
//    if (filteredFloatingHeight > heightThreshold)
//    {
//        float excessHeight = filteredFloatingHeight - heightThreshold;
//        targetFOV = minFOV + (excessHeight * fovSensitivity);
//        targetFOV = Mathf.Clamp(targetFOV, minFOV, maxFOV);
//    }

//    // 5. FOVを変更
//    cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, fovSmoothSpeed * Time.deltaTime);
//}