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

    [Header("ホバー・歩行バウンド設定")]
    [Tooltip("止まっている時の、アイドリングふよふよの上下幅")]
    public float idleBobbingAmount = 0.05f;
    [Tooltip("走っている時の、ピョコピョコ跳ねる上下幅")]
    public float walkBobbingAmount = 0.12f;
    [Tooltip("走っている時の、跳ねるテンポの速さ")]
    public float walkBobbingSpeed = 20.0f;

    [Header("バースト中の伸縮演出設定")]
    [Tooltip("壁に当たった時、伸縮させるか")]
    public bool useSquashAndStretch = true;
    [Range(0f, 1f)]
    [Tooltip("壁に当たった時、最大でどれくらい潰すか（0に近いほど潰れる）")]
    public float maxSquashAmount = 0.5f;
    [Tooltip("潰れた形から元の丸い形に戻るまでの時間（秒）")]
    public float squashDuration = 0.2f;

    [Header("ジャンプの伸縮演出設定")]
    [Tooltip("ジャンプした時のYサイズ")]
    public float stretchY = 1.25f;
    [Tooltip("着地した時のYサイズ")]
    public float squashY = 0.75f;

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

    private float bobbingTimer = 0f;

    private Quaternion currentBaseRotation = Quaternion.identity;
    private float chargeDrillAngle = 0f;

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
    /// キャラクターのベース向き、しなり、ドリル回転を更新する関数
    /// </summary>
    public void UpdateRotation(float currentVelocityX, float currentVelocityY, float speed, float customYAngle = -1f)
    {
        if (playerVisual == null) return;

        float targetYAngle = (customYAngle >= 0f) ? customYAngle : ((currentVelocityX > 0f) ? 310f : 50f);

        float normalizedVelY = Mathf.Clamp(currentVelocityY / p.burstSpeed, -1f, 1f);
        float directionSign = (targetYAngle == 310f) ? -1f : 1f;
        float targetLeanAngle = normalizedVelY * leanAngle * 1.5f * directionSign;

        // 1. ドリルを含まない、純粋な目標のベース回転を作る
        Quaternion targetBaseRotation = Quaternion.Euler(targetLeanAngle, targetYAngle, 0f);

        // 2. ベースの回転「だけ」を滑らかに Lerp で追従させる（ここが超安全！）
        currentBaseRotation = Quaternion.Lerp(
            currentBaseRotation,
            targetBaseRotation,
            Time.fixedDeltaTime * 15.0f
        );

        // 3. 最後にドリル回転の角度を加算して、ベースの回転に掛け算する
        if (useDrillRotation)
        {
            float rotationDirection = -1f;
            float speedRatio = speed / p.burstSpeed;
            float currentDrillSpeed = drillSpeed * speedRatio;

            currentXRotation += currentDrillSpeed * Time.fixedDeltaTime * rotationDirection;
            currentXRotation %= 360f; // 角度が無限に大きくならないように360度でループさせる

            Quaternion drillRotation = Quaternion.Euler(currentXRotation, 0f, 0f);

            // 確定したベース回転に、自転をガチャンと乗せる！
            playerVisual.localRotation = currentBaseRotation * drillRotation;
        }
        else
        {
            playerVisual.localRotation = currentBaseRotation;
        }
    }

    /// <summary>
    /// チャージ用の回転更新関数
    /// </summary>
    public void UpdateChargeRotation(Vector2 aimDirection, int currentChargeLevel)
    {
        if (playerVisual == null) return;

        float targetYAngle = (aimDirection.x >= 0f) ? 310f : 50f;
        float normalizedAimY = Mathf.Clamp(aimDirection.y, -1f, 1f);
        float targetLeanAngle = normalizedAimY * leanAngle * 1.5f;

        // 1. 純粋な目標のチャージベース回転
        Quaternion targetBaseRotation = Quaternion.Euler(targetLeanAngle, targetYAngle, 0f);

        // 2. ベース回転だけを滑らかにLerp追従
        currentBaseRotation = Quaternion.Lerp(
            currentBaseRotation,
            targetBaseRotation,
            Time.fixedDeltaTime * chargeLeanSmoothing
        );

        // 3. チェックボックスがONなら、チャージ自転を計算して最後に掛け算
        if (p.useRotationInCharge)
        {
            // 前回の「チャージレベルに応じた加速演出」もここに完璧に内包！
            float speedMultiplier = 2f + (currentChargeLevel * 3.0f);
            float currentDrillSpeed = drillSpeed * 0.5f * speedMultiplier;

            chargeDrillAngle += currentDrillSpeed * Time.fixedDeltaTime * -1f;
            chargeDrillAngle %= 360f;

            Quaternion drillRotation = Quaternion.Euler(chargeDrillAngle, 0f, 0f);
            playerVisual.localRotation = currentBaseRotation * drillRotation;
        }
        else
        {
            playerVisual.localRotation = currentBaseRotation;
        }
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
    /// 移動速度に応じて、「ふよふよ・バウンド」をブレンドする関数
    /// </summary>
    public void UpdateHoverBobbing(float currentVelocityX)
    {
        if (playerVisual == null) return;

        // 横方向の移動スピードを絶対値で取得
        float speedX = Mathf.Abs(currentVelocityX);

        float currentSpeed = 3.0f; // 止まっているときのふよふよ速度
        float currentAmount = idleBobbingAmount; // 止まっているときのふよふよ幅

        if (speedX > 0.1f)
        {
            currentSpeed = walkBobbingSpeed;
            currentAmount = walkBobbingAmount;
        }

        bobbingTimer += Time.fixedDeltaTime * currentSpeed;

        // サイン波を使って上下の座標を計算（元のローカル位置からピョコピョコずらす）
        float newY = Mathf.Sin(bobbingTimer) * currentAmount;

        // PlayerCenter(playerVisual) のローカル位置のY座標だけを滑らかに書き換える！
        playerVisual.localPosition = new Vector3(0f, newY, 0f);
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
    /// ジャンプした瞬間に呼び出され、伸びる演出関数
    /// </summary>
    public void TriggerJumpStretch()
    {
        if (!useSquashAndStretch || playerSquashVisual == null) return;

        // 縦(Y)を1.25倍に伸ばし、ボリューム（体積）維持のために横(X)を少し縮める
        float squashX = 1f / stretchY;

        // 2.5Dなので、XとZの両方を縮めてあげるのが綺麗に見せるコツです
        targetSquashScale = new Vector3(originalScale.x * squashX, originalScale.y * stretchY, originalScale.z * squashX);

        // 元のサイズに滑らかに戻るタイマーをセット（既存の仕組みをそのまま流用！）
        currentSquashTimer = squashDuration;
    }

    /// <summary>
    /// 着地した瞬間に呼び出され、潰れる演出関数
    /// </summary>
    public void TriggerLandSquash()
    {
        if (!useSquashAndStretch || playerSquashVisual == null) return;

        // 縦(Y)を0.75倍に押し潰し、横(X, Z)をプニッと広げる
        float stretchX = 1f / squashY;

        targetSquashScale = new Vector3(originalScale.x * stretchX, originalScale.y * squashY, originalScale.z * stretchX);

        // 元のサイズに滑らかに戻るタイマーをセット
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