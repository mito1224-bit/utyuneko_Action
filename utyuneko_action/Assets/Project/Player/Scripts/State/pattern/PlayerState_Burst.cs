using UnityEngine;

public class PlayerState_Burst : IPlayerState
{
    private PlayerController p;

    private float burstDuration = 5.0f;
    private float burstTimer;
    private Vector3 burstDirection;
    private float currentSpeed;
    private Vector3 lastVelocity; // ★以前のコードにあった「激突直前の速度」を記憶する箱

    public void Enter(PlayerController player)
    {
        p = player;
        p.OnCollisionEnterEvent += OnCollisionEnter;

        // 演出のON/OFF（前回までの完璧なコード）
        if (p.useTrail && p.trailRenderer != null) p.trailRenderer.enabled = true;
        if (p.useAfterImage && p.afterImageEffect != null) p.afterImageEffect.enabled = true;

        if (p.aimPivot != null) burstDirection = p.aimPivot.up.normalized;
        else burstDirection = Vector3.right;

        burstTimer = burstDuration;
        p.rb.useGravity = true; // 物理を有効化

        // バーストが実行されたのでカウンターを1増やす！
        p.currentBurstCount++;
        Debug.Log($"バースト発射！ 回数: {p.currentBurstCount} / {p.maxBurstCount}");

        // 溜めたレベルに応じたスピードで発射
        currentSpeed = p.chargeForceLevels[p.currentChargeLevel];
        p.rb.linearVelocity = burstDirection * currentSpeed;

        Debug.Log($"バースト発射！ チャージレベル: {p.currentChargeLevel} / 速度: {currentSpeed}");
    }

    public void UpdateState()
    {
        burstTimer -= Time.deltaTime;
        float speed = p.rb.linearVelocity.magnitude;
        bool isSpeedTooLowAndGrounded = (speed <= p.moveSpeed) && p.IsGrounded();

        // バースト中の「空中再チャージ」判定
        // チャージボタンが「今押された」かつ「まだ最大回数未満」なら、チャージ状態へ逆戻り
        if (p.inputActions.Player.Charge.WasPressedThisFrame() && p.currentBurstCount < p.maxBurstCount)
        {
            Debug.Log("バースト中：空中再チャージ！");
            p.TransitionToState(p.StateCharge);
            return;
        }

        // 通常の終了判定（回数が尽きて失速した、またはジャンプでキャンセルなど）
        if (burstTimer <= 0f || isSpeedTooLowAndGrounded)
        {
            p.TransitionToState(p.StateNormal);
        }
        if (p.inputActions.Player.Jump.triggered)
        {
            p.TransitionToState(p.StateNormal);
            p.rb.linearVelocity = new Vector3(p.rb.linearVelocity.x, p.jumpForce, 0.0f);
        }
    }

    public void FixedUpdateState()
    {
        // 激突した瞬間に、衝突前の本当の速度で計算できるように、毎フレームの速度を記録するだけにする。
        // リジッドボディの速度を無理に上書きしないので、Unityの物理と100%シンクロします。
        lastVelocity = p.rb.linearVelocity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (((1 << collision.gameObject.layer) & p.GetGroundLayerMask()) != 0)
        {
            // 激突直前のリアルな速度ベクトルを取り出す
            Vector3 incomingVector = lastVelocity;
            if (incomingVector.magnitude < 1f) return;

            // 壁・床の向き（法線）を取得
            Vector3 wallNormal = collision.contacts[0].normal;

            // ★激突直前の進行方向から、完璧な反射角を計算！
            Vector3 reflectedDirection = Vector3.Reflect(incomingVector.normalized, wallNormal);
            reflectedDirection.z = 0f;

            // 反射方向の更新
            burstDirection = reflectedDirection.normalized;

            // スピードの減衰
            currentSpeed = incomingVector.magnitude * p.reflectEfficiency;

            // 新しい反射速度を Rigidbody に直接ガツンと代入！
            p.rb.linearVelocity = burstDirection * currentSpeed;

            burstTimer = burstDuration;

            Debug.Log($"床・壁で完璧な反射！ 速度: {currentSpeed}");
        }
    }

    public void Exit()
    {
        Debug.Log("バースト終了");
        p.OnCollisionEnterEvent -= OnCollisionEnter;

        if (p.trailRenderer != null)
        {
            // Clear() を呼ぶことで、通常状態に戻った後に「残った線」が
            // テレテレとプレイヤーに付いてくるのを防ぎ、ピタッと消せます
            p.trailRenderer.Clear();
            p.trailRenderer.enabled = false;
        }

        if (p.afterImageEffect != null)
        {
            p.afterImageEffect.enabled = false;
        }

        // 終わる時は少し減速させて通常状態へバトンタッチ
        p.rb.linearVelocity = p.rb.linearVelocity * 0.2f;
    }
}