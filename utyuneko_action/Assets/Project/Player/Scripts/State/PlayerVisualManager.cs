using UnityEngine;

public class PlayerVisualManager : MonoBehaviour
{
    private PlayerController p;

    [Header("移動中の傾き演出設定")]
    [Tooltip("移動や高度に合わせて、最大で何度まで体を前のめりに傾けるか")]
    public float leanAngle = 20.0f;
    [Tooltip("体が傾くとき、または直立に戻るときの滑らかさ（大きいほどキビキビ動く）")]
    public float leanSmoothing = 10.0f;
    [Tooltip("チャージ中にエイム方向を向くときの滑らかさ（大きいほどキビキビ動く）")]
    public float chargeLeanSmoothing = 20.0f;

    [Header("回転設定")]
    [Tooltip("バースト・チャージ中に回転させるか")]
    public bool useDrillRotation = true;
    [Tooltip("1秒間に何度自転するか")]
    public float drillSpeed = 1440f;

    [Header("伸縮演出設定")]
    [Tooltip("壁に当たった時、伸縮させるか")]
    public bool useSquashAndStretch = true;
    [Range(0f, 1f)]
    [Tooltip("壁に当たった時、最大でどれくらい潰すか（0に近いほど潰れる）")]
    public float maxSquashAmount = 0.5f;
    [Tooltip("潰れた形から元の丸い形に戻るまでの時間（秒）")]
    public float squashDuration = 0.2f;

    [Header("対象オブジェクトの参照")]
    [Tooltip("伸縮（スケール変形）を担当する親オブジェクト (SquashPivot)")]
    public Transform playerSquashVisual;
    [Tooltip("見た目（振り向き・傾き・回転）を担当する子オブジェクト (PlayerCenter)")]
    public Transform playerVisual;

    // 内部管理用の隠しプロパティ
    private Vector3 originalScale;
    private float currentXRotation = 0f;
    private float currentSquashTimer = 0f;
    private Vector3 targetSquashScale;

    /// <summary>
    /// 脳（PlayerController）から最初に1回呼び出されて初期化する関数
    /// </summary>
    public void Initialize(PlayerController controller)
    {
        p = controller;
        if (playerVisual != null)
        {
            originalScale = playerVisual.localScale;
        }
    }

    /// <summary>
    /// キャラクターのベース向き、しなり、ドリル回転を更新する共通関数
    /// </summary>
    public void UpdateRotation(float currentVelocityX, float currentVelocityY, float speed, float customYAngle = -1f)
    {
        if (playerVisual == null) return;

        // 左右の向き（引数で指定があればそれを優先、なければ速度で決定）
        float targetYAngle = (customYAngle >= 0f) ? customYAngle : ((currentVelocityX > 0f) ? 310f : 50f);

        // 高度に応じた上下のしなり計算
        float normalizedVelY = Mathf.Clamp(currentVelocityY / p.burstSpeed, -1f, 1f);
        float directionSign = (targetYAngle == 310f) ? -1f : 1f;
        float targetLeanAngle = normalizedVelY * leanAngle * 1.5f * directionSign;

        Quaternion baseRotation = Quaternion.Euler(targetLeanAngle, targetYAngle, 0f);

        // ドリル回転の上乗せ
        if (useDrillRotation)
        {
            float rotationDirection = -1f;
            float speedRatio = speed / p.burstSpeed;
            float currentDrillSpeed = drillSpeed * speedRatio;

            currentXRotation += currentDrillSpeed * Time.fixedDeltaTime * rotationDirection;
            Quaternion drillRotation = Quaternion.Euler(currentXRotation, 0f, 0f);

            baseRotation = baseRotation * drillRotation;
        }

        // 滑らかに回転を追従
        playerVisual.localRotation = Quaternion.Lerp(
            playerVisual.localRotation,
            baseRotation,
            Time.fixedDeltaTime * 15.0f
        );
    }

    /// <summary>
    /// 親オブジェクト（SquashPivot）の伸縮タイマーを毎フレーム進めて形を元に戻す関数
    /// </summary>
    public void UpdateSquashAndStretch()
    {
        if (!useSquashAndStretch || playerSquashVisual == null) return;

        // 親オブジェクトはねじれを一切持たせないように0度固定
        playerSquashVisual.localRotation = Quaternion.identity;

        if (currentSquashTimer > 0f)
        {
            currentSquashTimer -= Time.fixedDeltaTime;
            float t = 1f - (currentSquashTimer / squashDuration);

            playerSquashVisual.localScale = Vector3.Lerp(
                targetSquashScale,
                originalScale,
                Mathf.SmoothStep(0f, 1f, t)
            );
        }
        else
        {
            playerSquashVisual.localScale = originalScale;
        }
    }

    /// <summary>
    /// 外部（ステート）から激突した瞬間に呼び出され、ペシャンコサイズをセットする関数
    /// </summary>
    public void TriggerSquash(Vector2 wallNormal, Vector2 incomingVector)
    {
        if (!useSquashAndStretch || playerSquashVisual == null) return;

        float impactIntensity = Mathf.Clamp01(incomingVector.magnitude / p.burstSpeed);
        float squashAmount = 1f - ((1f - maxSquashAmount) * impactIntensity);
        float stretchAmount = 1f / squashAmount;

        // 上下左右固定の潰れ判定
        if (Mathf.Abs(wallNormal.x) > Mathf.Abs(wallNormal.y))
        {
            // 左右の壁：横（X）を縮めて、縦（Y）を伸ばす
            targetSquashScale = new Vector3(originalScale.x * squashAmount, originalScale.y * stretchAmount, originalScale.z);
        }
        else
        {
            // 上下の床・天井：縦（Y）を縮めて、横（X）を伸ばす
            targetSquashScale = new Vector3(originalScale.x * stretchAmount, originalScale.y * squashAmount, originalScale.z);
        }

        currentSquashTimer = squashDuration;
    }

    /// <summary>
    /// ステートが切り替わった時などに、演出用の変形や角度を安全にリセットする関数
    /// </summary>
    public void ResetVisuals()
    {
        // 1. 親（SquashPivot）の伸縮と回転をまっすぐに戻す
        if (playerSquashVisual != null)
        {
            playerSquashVisual.localScale = originalScale;
            playerSquashVisual.localRotation = Quaternion.identity;
        }

        // 子（PlayerCenter）の回転としなりを完全にリセットして正面を向かせる！
        if (playerVisual != null)
        {
            // 現在のY軸の回転（右310度か、左50度か）をチェック
            float currentY = playerVisual.localRotation.eulerAngles.y;

            // 180度より大きければ右（310度）、小さければ左（50度）とパキッと判定
            float targetY = (currentY > 180f) ? 310f : 50f;

            // X軸（しなり）とZ軸（ドリル回転）を完全に 0 にして、左右の向き（Y軸）だけにする！
            playerVisual.localRotation = Quaternion.Euler(0f, targetY, 0f);
        }
    }

    /// <summary>
    /// 自転角度の蓄積を最初からやり直したい時用のリセット関数
    /// </summary>
    public void ResetDrillRotation()
    {
        currentXRotation = 0f;
    }
}