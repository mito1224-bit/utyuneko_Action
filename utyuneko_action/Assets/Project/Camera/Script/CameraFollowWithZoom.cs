using System.Collections;
using UnityEngine;

public class CameraFollowWithZoom : MonoBehaviour
{
    [Header("追従対象（空欄なら起動時にPlayerタグから自動取得します）")]
    public Transform target;

    [Header("基本の位置オフセット")]
    public Vector3 offset = new Vector3(0, 5, -10);

    [Header("位置追従のなめらかさ")]
    public float positionSmoothSpeed = 10f;

    [Header("★プレイヤーの向きへの先行表示（Look Ahead）")]
    public float lookAheadDistance = 4f;
    public float lookAheadSmoothSpeed = 4f;

    public bool invertLookAhead = false;

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
    public bool autoEnablePlayerInterpolate = true;
    public bool updateInFixedUpdate = false;
    public bool showZDebugText = true;
    [Header("★ジャンプ時のY軸追従（遊び/デッドゾーン）")]
    public float verticalDeadzoneThreshold = 2.0f; // この高さ以内のジャンプならカメラは上下しない
    private float virtualTargetY; // カメラが実際に追従する仮想のY座標

    private float filteredFloatingHeight;
    private float currentDynamicZ;
    private Rigidbody2D targetRb2D;

    private Vector2 currentLookAhead;
    private float lastFacingSign = 1f;

    private PlayerController playerController;
    private bool isEventWorking = false;

    private float defaultPositionSmoothSpeed;
    private float defaultZoomSmoothSpeed;
    private Coroutine eventCameraCoroutine;

    void Start()
    {
        currentDynamicZ = offset.z;

        defaultPositionSmoothSpeed = positionSmoothSpeed;
        defaultZoomSmoothSpeed = zoomSmoothSpeed;

        if (target == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) target = playerObj.transform;
        }

        if (target != null)
        {
            target.TryGetComponent<Rigidbody2D>(out targetRb2D);
            target.TryGetComponent<PlayerController>(out playerController);

            if (autoEnablePlayerInterpolate && targetRb2D != null)
            {
                targetRb2D.interpolation = RigidbodyInterpolation2D.Interpolate;
            }

            Vector3 startPos = target.position + offset;
            startPos.z = currentDynamicZ;
            transform.position = startPos;
            virtualTargetY = target.position.y;
        }
    }

    void LateUpdate()
    {
        if (!updateInFixedUpdate) MoveCamera(Time.deltaTime);
    }

    void FixedUpdate()
    {
        if (updateInFixedUpdate) MoveCamera(Time.fixedDeltaTime);
    }

    //void MoveCamera(float deltaTime)
    //{
    //    if (target == null) return;

    //    float targetZOffset = offset.z;
    //    if (!isLocked)
    //    {
    //        float currentFloatingHeight = 0f;
    //        Vector2 rayStart = new Vector2(target.position.x, target.position.y);
    //        RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, 100f, groundLayer2D);

    //        if (hit.collider != null) currentFloatingHeight = target.position.y - hit.point.y;

    //        filteredFloatingHeight = Mathf.Lerp(filteredFloatingHeight, currentFloatingHeight, heightFilterSpeed * deltaTime);

    //        if (filteredFloatingHeight > heightThreshold)
    //        {
    //            float excessHeight = filteredFloatingHeight - heightThreshold;
    //            targetZOffset = offset.z - (excessHeight * zoomSensitivity);
    //            targetZOffset = Mathf.Clamp(targetZOffset, maxZOffset, minZOffset);
    //        }
    //    }
    //    else
    //    {
    //        targetZOffset = lockedZOffset;
    //    }

    //    currentDynamicZ = Mathf.Lerp(currentDynamicZ, targetZOffset, zoomSmoothSpeed * deltaTime);

    //    Vector2 targetLookAhead = Vector2.zero;
    //    if (!isLocked)
    //    {
    //        float facingSign = lastFacingSign;
    //        if (targetRb2D != null && Mathf.Abs(targetRb2D.linearVelocity.x) > 0.1f)
    //        {
    //            facingSign = targetRb2D.linearVelocity.x > 0f ? 1f : -1f;
    //        }
    //        lastFacingSign = facingSign;
    //        if (invertLookAhead) facingSign *= -1f;
    //        targetLookAhead.x = facingSign * lookAheadDistance;
    //    }

    //    // --- MoveCamera メソッドの後半部分を以下のように修正 ---

    //    // 【大復活】これが消えていたため先行表示が機能していませんでした！
    //    currentLookAhead = Vector2.Lerp(currentLookAhead, targetLookAhead, lookAheadSmoothSpeed * deltaTime);
    //    // （中略：Look Aheadの計算など）

    //    // 最終的なカメラ位置の計算と移動
    //    Vector3 targetPosition;
    //    if (isLocked)
    //    {
    //        // X, Y は固定位置、Zは lockedZOffset に向かって補間された currentDynamicZ を適用
    //        targetPosition = new Vector3(lockedPosition.x, lockedPosition.y, currentDynamicZ);
    //    }
    //    else
    //    {
    //        targetPosition = target.position + offset + new Vector3(currentLookAhead.x, currentLookAhead.y, 0f);
    //        targetPosition.z = currentDynamicZ;
    //    }

    //    // 最後に全体のポジションを Lerp
    //    transform.position = Vector3.Lerp(transform.position, targetPosition, positionSmoothSpeed * deltaTime);
    //}

    void MoveCamera(float deltaTime)
    {
        if (target == null) return;

        float targetZOffset = offset.z;

        // ==========================================
        // 設定：チャージ中のカメラの広さ・限界値調整
        // ==========================================
        float chargeLookAheadMultiplier = 2.5f; // チャージ中の先行表示の最大距離倍率

        // ★【新設】プレイヤーが画面中央から最大でどれだけ離れていいか（限界距離）
        // この数値を小さくする（例: 4.0f）とプレイヤーがより画面中央寄りに残り、
        // 大きくする（例: 8.0f）とプレイヤーがより画面の端まで寄れるようになります。
        float maxPlayerDistanceInCharge = 7.0f;

        if (!isLocked)
        {
            float currentFloatingHeight = 0f;
            Vector2 rayStart = new Vector2(target.position.x, target.position.y);
            RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, 100f, groundLayer2D);

            if (hit.collider != null) currentFloatingHeight = target.position.y - hit.point.y;

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

        Vector2 targetLookAhead = Vector2.zero;

        float currentPositionSpeed = positionSmoothSpeed;     // 全体の追従スピード
        float currentLookAheadSpeed = lookAheadSmoothSpeed;   // 先行表示の補間スピード

        if (!isLocked)
        {
            if (playerController != null)
            {
                // 【1】チャージ（エイム）中の処理
                if (playerController.CurrentState == playerController.StateCharge)
                {
                    Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(
                        playerController.mousePositionInput.x,
                        playerController.mousePositionInput.y,
                        -Camera.main.transform.position.z
                    ));

                    Vector2 aimDirection = (mouseWorldPos - playerController.transform.position).normalized;

                    // 1. まずは通常通り引っ張る目標位置を計算

                    targetLookAhead =
                        aimDirection *
                        (lookAheadDistance * chargeLookAheadMultiplier);

                    // ★ 2. 【見切れ対策】引っ張るベクトル自体の長さを「最大制限値」でクランプする
                    // これにより、真上や斜め上を向いた時もプレイヤーが画面外へ消えなくなります
                    targetLookAhead = Vector2.ClampMagnitude(targetLookAhead, maxPlayerDistanceInCharge);

                    currentPositionSpeed = 40f; // きびきび動かす（さらに数値を上げました）
                    currentLookAheadSpeed = 30f;

                }
                // 【2】超高速移動（バースト状態 ＆ 速度しきい値超え）の処理
                else if (playerController.CurrentState == playerController.StateBurst &&
                      targetRb2D != null && targetRb2D.linearVelocity.magnitude > 12.0f)
                {
                    Vector2 moveDirection = targetRb2D.linearVelocity.normalized;

                    // ★ 修正：斜め移動時のY軸速度にカメラが過剰反応するのを防ぐため、Yをゼロにする
                    moveDirection.y = 0f;

                    // X方向のみでベクトルを再正規化（念のためゼロベクトル対策）
                    if (moveDirection.sqrMagnitude > 0)
                    {
                        moveDirection = moveDirection.normalized;
                    }

                    targetLookAhead = moveDirection * lookAheadDistance;

                    currentPositionSpeed = 35f;
                    currentLookAheadSpeed = 35f;
                }
                // 【3】通常時（歩き、ジャンプ、着地など）の処理
                else
                {
                    if (targetRb2D != null && Mathf.Abs(targetRb2D.linearVelocity.x) > 0.1f)
                    {
                        lastFacingSign = targetRb2D.linearVelocity.x > 0f ? 1f : -1f;
                    }

                    float facingSign = lastFacingSign;
                    if (invertLookAhead) facingSign *= -1f;

                    targetLookAhead = new Vector2(facingSign * lookAheadDistance, 0f);
                }


                if (!isLocked && targetRb2D != null)
                {
                    // プレイヤーが下方向に落ちている時
                    if (targetRb2D.linearVelocity.y < -0.1f)
                    {


                        float absX = Mathf.Abs(targetRb2D.linearVelocity.x);
                        float absY = Mathf.Abs(targetRb2D.linearVelocity.y);

                        // ほぼ真下に落下している時
                        if (targetRb2D.linearVelocity.y < -0.1f &&
                            absY > absX * 1.5f)
                        {
                            float facingSign = lastFacingSign;
                            if (invertLookAhead) facingSign *= -1f;

                            // 下方向は見ない。横方向の先行表示だけ残す
                            targetLookAhead = new Vector2(
                                facingSign * lookAheadDistance,
                                0f
                            );
                        }

                    }
                }

            }
        }

        // Z軸ズームのなめらかな移動計算
        currentDynamicZ = Mathf.Lerp(currentDynamicZ, targetZOffset, zoomSmoothSpeed * deltaTime);

        currentLookAhead = Vector2.Lerp(currentLookAhead, targetLookAhead, currentLookAheadSpeed * deltaTime);

        // ==========================================
        // 最終的なカメラ位置の計算
        // ==========================================
        if (!isLocked)
        {
            float diffY = target.position.y - virtualTargetY;

            if (diffY > verticalDeadzoneThreshold)
            {
                // プレイヤーが上に飛び出した
                virtualTargetY = target.position.y - verticalDeadzoneThreshold;
            }
            else if (diffY < -verticalDeadzoneThreshold)
            {
                // プレイヤーが下に落ちた
                virtualTargetY = target.position.y + verticalDeadzoneThreshold;
            }
            else
            {
                // ★修正：デッドゾーン内にいる時（着地時）のズレ補正

                // Y軸の速度がほぼ0（着地・床移動中）かつ、足元に地面があるかを判定します。
                bool isGrounded = false;

                if (targetRb2D != null && Mathf.Abs(targetRb2D.linearVelocity.y) < 0.05f)
                {
                    Vector2 rayStart = new Vector2(target.position.x, target.position.y);
                    // プレイヤーの中心から足元までの距離を考慮して長めに判定（例: 1.5f）
                    RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, 1.5f, groundLayer2D);

                    if (hit.collider != null)
                    {
                        isGrounded = true;
                    }
                }

                // 接地しているなら、カメラのY座標を本来のプレイヤー位置にフワッと戻す
                if (playerController.IsGrounded())
                {
                    virtualTargetY = Mathf.Lerp(virtualTargetY, target.position.y, 4f * deltaTime);
                }
            }
        }

        // 最終的なカメラ位置の計算
        Vector3 targetPosition;
        if (isLocked)
        {
            targetPosition = new Vector3(lockedPosition.x, lockedPosition.y, currentDynamicZ);
        }
        else
        {
            // target.position.y の代わりに virtualTargetY を使う
            Vector3 baseTargetPos = new Vector3(target.position.x, virtualTargetY, target.position.z);
            targetPosition = baseTargetPos + offset + new Vector3(currentLookAhead.x, currentLookAhead.y, 0f);
            targetPosition.z = currentDynamicZ;
        }

        transform.position = Vector3.Lerp(transform.position, targetPosition, currentPositionSpeed * deltaTime);
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

    // ===================================================================
    // 🎥 一括版（既存の互換性用）
    // ===================================================================
    public void PlayEventCameraWork(Transform eventTarget, float timeToTarget, float freezeDuration, float timeToReturn, float eventPositionSpeed = 3f, float eventZoomSpeed = 2f)
    {
        if (eventTarget == null) return;
        if (isEventWorking) return;

        eventCameraCoroutine = StartCoroutine(EventCameraWorkRoutine(eventTarget, timeToTarget, freezeDuration, timeToReturn, eventPositionSpeed, eventZoomSpeed));
    }

    private IEnumerator EventCameraWorkRoutine(Transform eventTarget, float timeToTarget, float freezeDuration, float timeToReturn, float eventPosSpeed, float eventZoomSpeed)
    {
        isEventWorking = true;
        SetPlayerActiveState(false);

        positionSmoothSpeed = eventPosSpeed;
        zoomSmoothSpeed = eventZoomSpeed;

        LockCamera(eventTarget.position, eventTarget.position.z);
        yield return new WaitForSeconds(timeToTarget);
        yield return new WaitForSeconds(freezeDuration);

        UnlockCamera();
        yield return new WaitForSeconds(timeToReturn);

        positionSmoothSpeed = defaultPositionSmoothSpeed;
        zoomSmoothSpeed = defaultZoomSmoothSpeed;

        SetPlayerActiveState(true);
        isEventWorking = false;
        eventCameraCoroutine = null;
    }

    // ===================================================================
    // 🛠️【新設】分離版①：指定したターゲットをロックして見続ける（戻らない）
    // ===================================================================
    public void StartTrackTarget(Transform eventTarget, float eventPositionSpeed = 3f, float eventZoomSpeed = 2f)
    {
        if (eventTarget == null) return;

        if (!isEventWorking)
        {
            isEventWorking = true;
            SetPlayerActiveState(false);
        }


        positionSmoothSpeed = eventPositionSpeed;
        zoomSmoothSpeed = eventZoomSpeed;

        LockCamera(eventTarget.position, eventTarget.position.z);
    }

    // ===================================================================
    // 🛠️【新設】分離版②：カメラのロックを解除してプレイヤーになめらかに戻る
    // ===================================================================
    public void ReturnToPlayerFromEvent(float timeToReturn = 1.0f)
    {
        if (!isEventWorking) return;

        // 実行中のコルーチンがあれば安全に上書き停止
        if (eventCameraCoroutine != null) StopCoroutine(eventCameraCoroutine);

        eventCameraCoroutine = StartCoroutine(ReturnToPlayerRoutine(timeToReturn));
    }

    private IEnumerator ReturnToPlayerRoutine(float timeToReturn)
    {
        UnlockCamera(); // カメラ追従ロック解除

        yield return new WaitForSeconds(timeToReturn); // 戻る時間を待つ

        positionSmoothSpeed = defaultPositionSmoothSpeed;
        zoomSmoothSpeed = defaultZoomSmoothSpeed;

        SetPlayerActiveState(true); // プレイヤーの操作を完全解放
        isEventWorking = false;
        eventCameraCoroutine = null;
    }

    //カメラズームイベント用
    // ===================================================================
    // 【新規】ズーム専用・一括版
    // 指定した場所・指定したカメラ距離(Z)へ移動し、時間経過で自動で戻る
    // ===================================================================
    public void PlayZoomEvent(Transform eventTarget, float targetZValue, float timeToTarget, float freezeDuration, float timeToReturn)
    {
        if (eventTarget == null) return;
        if (isEventWorking) return;

        eventCameraCoroutine = StartCoroutine(ZoomEventRoutine(eventTarget, targetZValue, timeToTarget, freezeDuration, timeToReturn));
    }

    private IEnumerator ZoomEventRoutine(Transform eventTarget, float targetZValue, float timeToTarget, float freezeDuration, float timeToReturn)
    {
        isEventWorking = true;
        SetPlayerActiveState(false);

        // --- ① 指定時間（timeToTarget）をかけて確実に移動するフェーズ ---
        Vector3 startPos = transform.position;
        float startZ = currentDynamicZ;
        float elapsed = 0f;

        // 次のフレームから MoveCamera の標準 Lerp を一時的にバイパスするため、ロック状態にする
        LockCamera(eventTarget.position, targetZValue);

        while (elapsed < timeToTarget)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / timeToTarget);
            float curve = Mathf.SmoothStep(0f, 1f, t); // 滑らかな加速・減速（イージング）

            // 座標とZ軸を時間ベースで強制補間
            transform.position = Vector3.Lerp(startPos, new Vector3(eventTarget.position.x, eventTarget.position.y, transform.position.z), curve);
            currentDynamicZ = Mathf.Lerp(startZ, targetZValue, curve);
            transform.position = new Vector3(transform.position.x, transform.position.y, currentDynamicZ);

            yield return null;
        }

        // --- ② ターゲット地点で指定時間（freezeDuration）停止するフェーズ ---
        yield return new WaitForSeconds(freezeDuration);

        // --- ③ Unlockして元の通常速度でプレイヤーに戻るフェーズ ---
        UnlockCamera();
        yield return new WaitForSeconds(timeToReturn);

        SetPlayerActiveState(true);
        isEventWorking = false;
        eventCameraCoroutine = null;
    }
    // ===================================================================
    //【新規】ズーム専用・分離版①（行く方）
    // 指定した場所・指定したカメラ距離(Z)にクローズアップし、そのまま維持する
    // ===================================================================
    public void StartZoomTrack(Transform eventTarget, float targetZValue, float timeToTarget)
    {
        if (eventTarget == null) return;
        if (isEventWorking) return;

        // 分離版①も時間指定で動かすため、内部的に専用のコルーチンを回します
        eventCameraCoroutine = StartCoroutine(StartZoomTrackRoutine(eventTarget, targetZValue, timeToTarget));
    }
    private IEnumerator StartZoomTrackRoutine(Transform eventTarget, float targetZValue, float timeToTarget)
    {
        isEventWorking = true;
        SetPlayerActiveState(false);

        Vector3 startPos = transform.position;
        float startZ = currentDynamicZ;
        float elapsed = 0f;

        LockCamera(eventTarget.position, targetZValue);

        // 指定時間をかけて滑らかにターゲット座標（とズーム）へ移動
        while (elapsed < timeToTarget)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / timeToTarget);
            float curve = Mathf.SmoothStep(0f, 1f, t);

            transform.position = Vector3.Lerp(startPos, new Vector3(eventTarget.position.x, eventTarget.position.y, transform.position.z), curve);
            currentDynamicZ = Mathf.Lerp(startZ, targetZValue, curve);
            transform.position = new Vector3(transform.position.x, transform.position.y, currentDynamicZ);

            yield return null;
        }

        // 移動完了後は、Update側で完全にターゲットをロックオン維持（帰らない）
        eventCameraCoroutine = null;
    }
    // ===================================================================
    // 【新規】ズーム専用・分離版②（帰る方）
    // ズームロックを解除し、指定した時間をかけてプレイヤー（元の通常距離）に戻る
    // ===================================================================
    public void ReturnFromZoomEvent(float timeToReturn = 1.0f)
    {
        if (!isEventWorking) return;

        if (eventCameraCoroutine != null) StopCoroutine(eventCameraCoroutine);
        eventCameraCoroutine = StartCoroutine(ReturnFromZoomRoutine(timeToReturn));
    }

    private IEnumerator ReturnFromZoomRoutine(float timeToReturn)
    {
        UnlockCamera();

        yield return new WaitForSeconds(timeToReturn);

        positionSmoothSpeed = defaultPositionSmoothSpeed;
        zoomSmoothSpeed = defaultZoomSmoothSpeed;

        SetPlayerActiveState(true);
        isEventWorking = false;
        eventCameraCoroutine = null;
    }

    // 強制停止安全弁（スキップ対策も分離版に対応！）
    public void ForceStopEventCameraWork()
    {
        if (eventCameraCoroutine != null)
        {
            StopCoroutine(eventCameraCoroutine);
            eventCameraCoroutine = null;
        }

        if (!isEventWorking) return;

        UnlockCamera();
        positionSmoothSpeed = defaultPositionSmoothSpeed;
        zoomSmoothSpeed = defaultZoomSmoothSpeed;

        if (target != null)
        {
            Vector3 skipTargetPos = target.position + offset;
            skipTargetPos.z = currentDynamicZ;
            transform.position = skipTargetPos;
        }

        SetPlayerActiveState(true);
        isEventWorking = false;
    }

    private void SetPlayerActiveState(bool enable)
    {
        if (target == null) return;

        if (playerController != null)
        {
            if (!enable)
            {
                playerController.TransitionToState(playerController.StateNormal);
                if (playerController.inputActions != null) playerController.inputActions.Player.Disable();
                playerController.enabled = false;
            }
            else
            {
                playerController.enabled = true;
                if (playerController.inputActions != null) playerController.inputActions.Player.Enable();
            }
        }

        MonoBehaviour playerMovement = target.GetComponent("PlayerMovement") as MonoBehaviour;
        if (playerMovement != null) playerMovement.enabled = enable;

        if (!enable && targetRb2D != null)
        {
            targetRb2D.linearVelocity = Vector2.zero;
            targetRb2D.angularVelocity = 0f;
        }
    }
}