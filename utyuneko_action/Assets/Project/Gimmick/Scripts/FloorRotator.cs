using UnityEngine;

/// <summary>
/// 2Dオブジェクトを回転させるスクリプト。
/// インスペクターから回転速度・方向・イージング・往復回転などを設定できます。
/// </summary>
public class FloorRotator : MonoBehaviour
{
    // =====================================================================
    //  回転基本設定
    // =====================================================================
    [Header("── 回転基本設定 ──────────────────")]

    [Tooltip("回転速度（度/秒）")]
    [Min(0f)]
    public float rotationSpeed = 90f;

    public enum RotationDirection { Clockwise = -1, CounterClockwise = 1 }

    [Tooltip("回転方向")]
    public RotationDirection direction = RotationDirection.CounterClockwise;

    // =====================================================================
    //  ピボットオフセット
    // =====================================================================
    [Header("── ピボットオフセット ─────────────")]

    [Tooltip("回転中心点のオフセット（ローカル座標）\n(0,0) でオブジェクト中心を軸に回転します")]
    public Vector2 pivotOffset = Vector2.zero;

    // =====================================================================
    //  イージング（加速・減速）
    // =====================================================================
    [Header("── イージング ───────────────────")]

    [Tooltip("イージングを使用する")]
    public bool useEasing = false;

    [Tooltip("最大速度に達するまでの加速時間（秒）")]
    [Min(0f)]
    public float accelerationTime = 1f;

    [Tooltip("停止するまでの減速時間（秒）")]
    [Min(0f)]
    public float decelerationTime = 1f;

    // =====================================================================
    //  往復回転（Oscillation）
    // =====================================================================
    [Header("── 往復回転（Oscillation）─────────")]

    [Tooltip("往復回転モードを有効にする\n（振り子・ドアのような動き）")]
    public bool oscillate = false;

    [Tooltip("往復する角度の範囲（度）\n例: 45 → ±45° 往復")]
    [Min(0f)]
    public float oscillateAngle = 45f;

    public enum OscillationTurnMode { Instant, Smooth }

    [Tooltip("折り返し時の切り替え方法")]
    public OscillationTurnMode oscillationTurnMode = OscillationTurnMode.Smooth;

    // =====================================================================
    //  内部状態
    // =====================================================================
    private bool _hasAppliedPivotOffset = false;
    private bool _wantsRotation = true;
    private bool _wasOscillating = false;
    private Vector2 _appliedPivotOffset = Vector2.zero;

    private float _currentSpeed = 0f;
    private float _oscillationTime = 0f; // 往復運動用の累積時間

    private Transform _pivotTransform;
    private Quaternion _oscillationBaseLocalRotation = Quaternion.identity;

    // =====================================================================
    //  初期化
    // =====================================================================
    void Start()
    {
        ApplyPivotOffset(force: true, preserveObjectWorldPose: true);
        CaptureOscillationBaseRotation();
        _wasOscillating = oscillate;
        _currentSpeed = useEasing ? 0f : GetClampedRotationSpeed();
    }

    // =====================================================================
    //  毎フレーム更新
    // =====================================================================
    void Update()
    {
        HandlePivotOffsetChange();
        HandleOscillationModeChange();
        UpdateSpeed();

        if (_currentSpeed <= 0f) return;

        if (oscillate)
        {
            UpdateOscillationNew();
        }
        else
        {
            UpdateContinuousRotation(_currentSpeed * Time.deltaTime);
        }
    }

    // =====================================================================
    //  通常回転
    // =====================================================================
    private void UpdateContinuousRotation(float deltaAngle)
    {
        float rotAmount = deltaAngle * (int)direction;
        GetRotationTarget().Rotate(0f, 0f, rotAmount);
    }

    // =====================================================================
    //  往復回転 (タイムベースに刷新して振動を完全に排除)
    // =====================================================================
    private void UpdateOscillationNew()
    {
        if (oscillateAngle <= 0f)
        {
            GetRotationTarget().localRotation = _oscillationBaseLocalRotation;
            return;
        }

        // 速度と時間から、往復の進行度を安全に計算
        _oscillationTime += Time.deltaTime * _currentSpeed;

        // 1往復に必要な「角度分」の移動量（往路と復路で 4 * oscillateAngle）
        float period = oscillateAngle * 4f;
        if (period <= 0f) return;

        // 0 ～ period の間に時間を丸める（Mathf.PingPongの代わり）
        float t = Mathf.Repeat(_oscillationTime, period);

        // 三角波（-1 ～ 1）を作る
        float rawLinearProgress = 0f;
        if (t < period * 0.25f) // 0 -> 1
            rawLinearProgress = t / (period * 0.25f);
        else if (t < period * 0.75f) // 1 -> -1
            rawLinearProgress = 1f - 2f * ((t - period * 0.25f) / (period * 0.5f));
        else // -1 -> 0
            rawLinearProgress = -1f + ((t - period * 0.75f) / (period * 0.25f));

        float finalAngle = 0f;

        if (oscillationTurnMode == OscillationTurnMode.Instant)
        {
            // 直線的な往復（端でパッと跳ね返る）
            finalAngle = rawLinearProgress * oscillateAngle;
        }
        else
        {
            // なめらかな往復（端で減速する。正弦波（Sin）を利用）
            // rawLinearProgress (-1～1) を 正弦波の角度 (-PI/2 ～ PI/2) にマッピング
            finalAngle = Mathf.Sin(rawLinearProgress * Mathf.PI * 0.5f) * oscillateAngle;
        }

        // 回転方向の適用
        finalAngle *= (int)direction;

        // ベース回転からのオフセットとして適用
        GetRotationTarget().localRotation = _oscillationBaseLocalRotation * Quaternion.Euler(0f, 0f, finalAngle);
    }

    // =====================================================================
    //  イージング
    // =====================================================================
    private void UpdateSpeed()
    {
        float targetSpeed = _wantsRotation ? GetClampedRotationSpeed() : 0f;

        if (!useEasing)
        {
            _currentSpeed = targetSpeed;
            return;
        }

        float easingTime = _wantsRotation ? accelerationTime : decelerationTime;

        if (easingTime > 0f)
        {
            float referenceSpeed = Mathf.Max(_currentSpeed, GetClampedRotationSpeed());
            float step = referenceSpeed / easingTime * Time.deltaTime;
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, step);
        }
        else
        {
            _currentSpeed = targetSpeed;
        }
    }

    // =====================================================================
    //  ピボットセットアップ
    // =====================================================================
    private void ApplyPivotOffset(bool force, bool preserveObjectWorldPose)
    {
        bool needsPivot = pivotOffset != Vector2.zero;

        // 浮動小数点数（float）の比較誤差を考慮し、近似値でチェック
        bool offsetChanged = !_hasAppliedPivotOffset || Vector2.Distance(_appliedPivotOffset, pivotOffset) > 0.0001f;
        bool pivotStateMismatch = needsPivot != (_pivotTransform != null);

        if (!force && !offsetChanged && !pivotStateMismatch)
        {
            return;
        }

        if (preserveObjectWorldPose)
        {
            ApplyPivotOffsetPreservingObjectPose(needsPivot);
        }
        else
        {
            ApplyPivotOffsetPreservingRotationCenter(needsPivot);
        }

        _appliedPivotOffset = pivotOffset;
        _hasAppliedPivotOffset = true;
    }

    private void ApplyPivotOffsetPreservingObjectPose(bool needsPivot)
    {
        Transform outerParent = _pivotTransform != null ? _pivotTransform.parent : transform.parent;

        if (_pivotTransform != null)
        {
            transform.SetParent(outerParent, true);
            DestroyPivotObject(_pivotTransform.gameObject);
            _pivotTransform = null;
        }

        if (!needsPivot) return;

        Vector3 pivotWorldPosition = transform.TransformPoint(new Vector3(pivotOffset.x, pivotOffset.y, 0f));

        GameObject pivotGO = new GameObject($"{gameObject.name}_Pivot");
        pivotGO.transform.SetParent(outerParent, true);
        pivotGO.transform.position = pivotWorldPosition;
        pivotGO.transform.rotation = transform.rotation;

        transform.SetParent(pivotGO.transform, true);
        _pivotTransform = pivotGO.transform;
    }

    private void ApplyPivotOffsetPreservingRotationCenter(bool needsPivot)
    {
        Transform outerParent = _pivotTransform != null ? _pivotTransform.parent : transform.parent;
        Vector3 rotationCenterWorld = _pivotTransform != null ? _pivotTransform.position : transform.position;
        Quaternion worldRotation = transform.rotation;

        if (!needsPivot)
        {
            if (_pivotTransform == null) return;

            GameObject pivotObject = _pivotTransform.gameObject;
            transform.SetParent(outerParent, true);
            transform.position = rotationCenterWorld;
            transform.rotation = worldRotation;
            _pivotTransform = null;
            DestroyPivotObject(pivotObject);
            return;
        }

        Transform pivot = _pivotTransform;
        if (pivot == null)
        {
            GameObject pivotGO = new GameObject($"{gameObject.name}_Pivot");
            pivot = pivotGO.transform;
        }

        pivot.SetParent(outerParent, true);
        pivot.position = rotationCenterWorld;
        pivot.rotation = worldRotation;

        transform.SetParent(pivot, false);
        transform.localPosition = new Vector3(-pivotOffset.x, -pivotOffset.y, 0f);
        transform.localRotation = Quaternion.identity;
        _pivotTransform = pivot;
    }

    private void HandlePivotOffsetChange()
    {
        bool needsPivot = pivotOffset != Vector2.zero;
        bool pivotMissing = needsPivot && _pivotTransform == null;

        // ここも誤差を考慮した比較に修正
        bool offsetChanged = !_hasAppliedPivotOffset || (!Mathf.Approximately(_appliedPivotOffset.x, pivotOffset.x)||
                                                         !Mathf.Approximately(_appliedPivotOffset.y, pivotOffset.y));

        if (!offsetChanged && !pivotMissing) return;

        ApplyPivotOffset(force: true, preserveObjectWorldPose: false);

        if(oscillate)
        {
            ResetOscillationTime();
        }
    }

    private void ResetOscillationTime()
    {
        _oscillationTime = 0f;
        CaptureOscillationBaseRotation();
    }

    private void DestroyPivotObject(GameObject pivotObject)
    {
        if (pivotObject == null) return;

        if (Application.isPlaying)
            Destroy(pivotObject);
        else
            DestroyImmediate(pivotObject);
    }

    private Transform GetRotationTarget()
    {
        return _pivotTransform != null ? _pivotTransform : transform;
    }

    private float GetClampedRotationSpeed()
    {
        return Mathf.Max(0f, rotationSpeed);
    }

    private void CaptureOscillationBaseRotation()
    {
        _oscillationBaseLocalRotation = GetRotationTarget().localRotation;
    }

    private void HandleOscillationModeChange()
    {
        if (oscillate == _wasOscillating) return;

        if (oscillate)
        {
            ResetOscillationTime();
        }

        _wasOscillating = oscillate;
    }

    // =====================================================================
    //  外部制御 API
    // =====================================================================

    public void StartRotation()
    {
        _wantsRotation = true;
        if (!useEasing) _currentSpeed = GetClampedRotationSpeed();
    }

    public void StopRotation()
    {
        _wantsRotation = false;
        if (!useEasing) _currentSpeed = 0f;
    }

    public bool IsRotating => _wantsRotation || _currentSpeed > 0.0001f;

    public void ReverseDirection()
    {
        direction = direction == RotationDirection.Clockwise
            ? RotationDirection.CounterClockwise
            : RotationDirection.Clockwise;
    }

    public void SetSpeed(float speed)
    {
        rotationSpeed = Mathf.Max(0f, speed);
        if (!useEasing && _wantsRotation) _currentSpeed = rotationSpeed;
    }
}