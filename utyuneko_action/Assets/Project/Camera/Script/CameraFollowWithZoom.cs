using UnityEngine;

public class CameraFollowWithZoom : MonoBehaviour
{
    [Header("追従対象（空欄なら起動時にPlayerタグから自動取得します）")]
    public Transform target;

    [Header("基本の位置オフセット")]
    public Vector3 offset = new Vector3(0, 5, -10);

    [Header("位置追従のなめらかさ")]
    public float positionSmoothSpeed = 10f;

    // --- 追加：進行方向へのカメラ先行表示設定 ---
    [Header("★進行方向への先行表示（Look Ahead）")]
    [Tooltip("プレイヤーの速度にどれくらいカメラを先行させるか")]
    public float lookAheadFactor = 0.5f;
    [Tooltip("先行表示の最大距離")]
    public Vector2 maxLookAhead = new Vector2(3f, 2f);
    [Tooltip("先行表示が切り替わる（戻る）ときのなめらかさ")]
    public float lookAheadSmoothSpeed = 5f;
    // ----------------------------------------

    [Header("Z軸ズームの調整（高さに応じた引き量）")]
    public float heightThreshold = 3f;
    public float minZOffset = -10f;
    public float maxZOffset = -20f;
    public float zoomSensitivity = 2f;
    public float zoomSmoothSpeed = 5f;

    [Header("バウンド軽減用")]
    public float heightFilterSpeed = 2f;

    [Header("2D地面の判定設定")]
    public LayerMask groundLayer2D = ~0;

    [Header("カメラの完全固定モード")]
    public bool isLocked = false;
    public Vector3 lockedPosition;
    private float lockedZOffset;

    [Header("★カメラ側からのブレ・ガタつき対策")]
    [Tooltip("ONにすると、起動時にプレイヤーのRigidbody2Dの補間(Interpolate)をカメラ側から強制的に有効化してブレを止めます。")]
    public bool autoEnablePlayerInterpolate = true;

    [Tooltip("ONにすると、カメラの更新をFixedUpdate(物理同期)で行います。バースト時のブレが酷い場合はチェックを入れてください。")]
    public bool updateInFixedUpdate = false;

    [Header("?? デバッグ設定（見えなくさせるトリガー）")]
    [Tooltip("ONにすると、ゲーム画面の左上に現在のカメラのZ座標（ズーム状態）をリアルタイム表示します。")]
    public bool showZDebugText = true;

    private float filteredFloatingHeight;
    private float currentDynamicZ;
    private Rigidbody2D targetRb2D;

    // 追加：現在の先行量を管理する変数
    private Vector2 currentLookAhead;

    void Start()
    {
        currentDynamicZ = offset.z;

        if (target == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                target = playerObj.transform;
            }
            else
            {
                Debug.LogError("[CameraFollowWithZoom] 'Player' タグのついたオブジェクトが見つかりません。プレイヤーのタグを確認してください。");
            }
        }

        if (target != null)
        {
            // 進行方向を取得するため、常にRigidbody2Dの取得を試みるように変更
            target.TryGetComponent<Rigidbody2D>(out targetRb2D);

            if (autoEnablePlayerInterpolate && targetRb2D != null)
            {
                targetRb2D.interpolation = RigidbodyInterpolation2D.Interpolate;
            }

            Vector3 startPos = target.position + offset;
            startPos.z = currentDynamicZ;
            transform.position = startPos;
        }
    }

    void LateUpdate()
    {
        if (!updateInFixedUpdate)
        {
            MoveCamera(Time.deltaTime);
        }
    }

    void FixedUpdate()
    {
        if (updateInFixedUpdate)
        {
            MoveCamera(Time.fixedDeltaTime);
        }
    }

    void MoveCamera(float deltaTime)
    {
        if (target == null) return;

        // --------------------------------------------------
        // 1. 通常時のみ動く：地面からの高さに応じた自動Zズーム計算
        // --------------------------------------------------
        float targetZOffset = offset.z;

        if (!isLocked)
        {
            float currentFloatingHeight = 0f;
            Vector2 rayStart = new Vector2(target.position.x, target.position.y);
            RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, 100f, groundLayer2D);

            if (hit.collider != null)
            {
                currentFloatingHeight = target.position.y - hit.point.y;
            }

            filteredFloatingHeight = Mathf.Lerp(filteredFloatingHeight, currentFloatingHeight, heightFilterSpeed * deltaTime);

            if (filteredFloatingHeight > heightThreshold)
            {
                float excessHeight = filteredFloatingHeight - heightThreshold;
                targetZOffset = offset.z - (excessHeight * zoomSensitivity);
                targetZOffset = Mathf.Clamp(targetZOffset, maxZOffset, minZOffset);
            }
        }
        else
        {
            targetZOffset = lockedZOffset;
        }

        currentDynamicZ = Mathf.Lerp(currentDynamicZ, targetZOffset, zoomSmoothSpeed * deltaTime);

        // --------------------------------------------------
        // 【追加】進行方向への先行表示（Look Ahead）の計算
        // --------------------------------------------------
        Vector2 targetLookAhead = Vector2.zero;

        // ロック中ではなく、プレイヤーにRigidbody2Dがついている場合のみ計算
        if (!isLocked && targetRb2D != null)
        {
            // 速度に応じてずらす量を決定（Unityのバージョンによっては .linearVelocity の場合があります）
            targetLookAhead = targetRb2D.linearVelocity * lookAheadFactor;

            // ずらす量が設定した最大値を超えないように制限
            targetLookAhead.x = Mathf.Clamp(targetLookAhead.x, -maxLookAhead.x, maxLookAhead.x);
            targetLookAhead.y = Mathf.Clamp(targetLookAhead.y, -maxLookAhead.y, maxLookAhead.y);
        }

        // 先行量をなめらかに変化させる
        currentLookAhead = Vector2.Lerp(currentLookAhead, targetLookAhead, lookAheadSmoothSpeed * deltaTime);

        // --------------------------------------------------
        // 2. 最終的なカメラ位置の計算と移動
        // --------------------------------------------------
        Vector3 targetPosition;

        if (isLocked)
        {
            targetPosition = new Vector3(lockedPosition.x, lockedPosition.y, currentDynamicZ);
        }
        else
        {
            // 基本位置に、計算した先行量（Look Ahead）を足し算する
            targetPosition = target.position + offset + new Vector3(currentLookAhead.x, currentLookAhead.y, 0f);
            targetPosition.z = currentDynamicZ;
        }

        transform.position = Vector3.Lerp(transform.position, targetPosition, positionSmoothSpeed * deltaTime);
    }

    void OnGUI()
    {
        if (!showZDebugText) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 18;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.cyan;

        Texture2D bgTex = new Texture2D(1, 1);
        bgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
        bgTex.Apply();

        style.normal.background = bgTex;
        style.padding = new RectOffset(10, 10, 5, 5);

        string status = isLocked ? "<color=red>LOCKED</color>" : "NORMAL";
        string debugMessage = $"[Camera Z] Current: {currentDynamicZ:F2}  (Limit: {maxZOffset} ～ {minZOffset})  [{status}]";

        GUILayout.BeginArea(new Rect(10, 10, 600, 40));
        GUILayout.Label(debugMessage, style);
        GUILayout.EndArea();
    }

    public void LockCamera(Vector3 positionToLock, float targetZValue)
    {
        lockedPosition = positionToLock;
        lockedZOffset = targetZValue;
        isLocked = true;
    }

    public void UnlockCamera()
    {
        isLocked = false;
    }
}

//public class CameraFollowWithZoom : MonoBehaviour
//{
//    [Header("追従対象（空欄なら起動時にPlayerタグから自動取得します）")]
//    public Transform target;

//    [Header("基本の位置オフセット")]
//    public Vector3 offset = new Vector3(0, 5, -10);

//    [Header("位置追従のなめらかさ")]
//    public float positionSmoothSpeed = 10f;

//    [Header("Z軸ズームの調整（高さに応じた引き量）")]
//    public float heightThreshold = 3f;
//    public float minZOffset = -10f;
//    public float maxZOffset = -20f;
//    public float zoomSensitivity = 2f;
//    public float zoomSmoothSpeed = 5f;

//    [Header("バウンド軽減用")]
//    public float heightFilterSpeed = 2f;

//    [Header("2D地面の判定設定")]
//    public LayerMask groundLayer2D = ~0;

//    [Header("カメラの完全固定モード")]
//    public bool isLocked = false;
//    public Vector3 lockedPosition;
//    private float lockedZOffset;

//    [Header("★カメラ側からのブレ・ガタつき対策")]
//    [Tooltip("ONにすると、起動時にプレイヤーのRigidbody2Dの補間(Interpolate)をカメラ側から強制的に有効化してブレを止めます。")]
//    public bool autoEnablePlayerInterpolate = true;

//    [Tooltip("ONにすると、カメラの更新をFixedUpdate(物理同期)で行います。バースト時のブレが酷い場合はチェックを入れてください。")]
//    public bool updateInFixedUpdate = false;

//    [Header("?? デバッグ設定（見えなくさせるトリガー）")]
//    [Tooltip("ONにすると、ゲーム画面の左上に現在のカメラのZ座標（ズーム状態）をリアルタイム表示します。")]
//    public bool showZDebugText = true;

//    private float filteredFloatingHeight;
//    private float currentDynamicZ;
//    private Rigidbody2D targetRb2D;

//    void Start()
//    {
//        currentDynamicZ = offset.z;

//        // 【修正】インスペクターが空欄の場合、Playerタグから自動取得
//        if (target == null)
//        {
//            GameObject playerObj = GameObject.FindWithTag("Player");
//            if (playerObj != null)
//            {
//                target = playerObj.transform;
//            }
//            else
//            {
//                Debug.LogError("[CameraFollowWithZoom] 'Player' タグのついたオブジェクトが見つかりません。プレイヤーのタグを確認してください。");
//            }
//        }

//        // ターゲットが見つかった場合の初期化処理
//        if (target != null)
//        {
//            if (autoEnablePlayerInterpolate)
//            {
//                if (target.TryGetComponent<Rigidbody2D>(out targetRb2D))
//                {
//                    targetRb2D.interpolation = RigidbodyInterpolation2D.Interpolate;
//                }
//            }

//            Vector3 startPos = target.position + offset;
//            startPos.z = currentDynamicZ;
//            transform.position = startPos;
//        }
//    }

//    void LateUpdate()
//    {
//        if (!updateInFixedUpdate)
//        {
//            MoveCamera(Time.deltaTime);
//        }
//    }

//    void FixedUpdate()
//    {
//        if (updateInFixedUpdate)
//        {
//            MoveCamera(Time.fixedDeltaTime);
//        }
//    }

//    void MoveCamera(float deltaTime)
//    {
//        if (target == null) return;

//        // --------------------------------------------------
//        // 1. 通常時のみ動く：地面からの高さに応じた自動Zズーム計算
//        // --------------------------------------------------
//        float targetZOffset = offset.z;

//        if (!isLocked)
//        {
//            float currentFloatingHeight = 0f;
//            Vector2 rayStart = new Vector2(target.position.x, target.position.y);
//            RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, 100f, groundLayer2D);

//            if (hit.collider != null)
//            {
//                currentFloatingHeight = target.position.y - hit.point.y;
//            }

//            filteredFloatingHeight = Mathf.Lerp(filteredFloatingHeight, currentFloatingHeight, heightFilterSpeed * deltaTime);

//            if (filteredFloatingHeight > heightThreshold)
//            {
//                float excessHeight = filteredFloatingHeight - heightThreshold;
//                targetZOffset = offset.z - (excessHeight * zoomSensitivity);
//                targetZOffset = Mathf.Clamp(targetZOffset, maxZOffset, minZOffset);
//            }
//        }
//        else
//        {
//            targetZOffset = lockedZOffset;
//        }

//        currentDynamicZ = Mathf.Lerp(currentDynamicZ, targetZOffset, zoomSmoothSpeed * deltaTime);

//        // --------------------------------------------------
//        // 2. 最終的なカメラ位置の計算と移動
//        // --------------------------------------------------
//        Vector3 targetPosition;

//        if (isLocked)
//        {
//            targetPosition = new Vector3(lockedPosition.x, lockedPosition.y, currentDynamicZ);
//        }
//        else
//        {
//            targetPosition = target.position + offset;
//            targetPosition.z = currentDynamicZ;
//        }

//        transform.position = Vector3.Lerp(transform.position, targetPosition, positionSmoothSpeed * deltaTime);
//    }

//    void OnGUI()
//    {
//        if (!showZDebugText) return;

//        GUIStyle style = new GUIStyle();
//        style.fontSize = 18;
//        style.fontStyle = FontStyle.Bold;
//        style.normal.textColor = Color.cyan;

//        Texture2D bgTex = new Texture2D(1, 1);
//        bgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
//        bgTex.Apply();

//        style.normal.background = bgTex;
//        style.padding = new RectOffset(10, 10, 5, 5);

//        string status = isLocked ? "<color=red>LOCKED</color>" : "NORMAL";
//        string debugMessage = $"[Camera Z] Current: {currentDynamicZ:F2}  (Limit: {maxZOffset} ～ {minZOffset})  [{status}]";

//        GUILayout.BeginArea(new Rect(10, 10, 600, 40));
//        GUILayout.Label(debugMessage, style);
//        GUILayout.EndArea();
//    }

//    public void LockCamera(Vector3 positionToLock, float targetZValue)
//    {
//        lockedPosition = positionToLock;
//        lockedZOffset = targetZValue;
//        isLocked = true;
//    }

//    public void UnlockCamera()
//    {
//        isLocked = false;
//    }
//}
