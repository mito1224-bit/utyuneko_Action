using UnityEngine;

public class PlayerState_Burst : IPlayerState
{
    private PlayerController p;

    private float burstDuration = 5.0f;
    private float burstTimer;
    private Vector2 burstDirection; // Vector2に変更
    private float currentSpeed;
    private Vector2 lastVelocity;   // Vector2に変更

    public void Enter(PlayerController player)
    {
        p = player;
        p.OnCollisionEnterEvent += OnCollisionEnter;

        if (p.useTrail && p.trailRenderer != null) p.trailRenderer.enabled = true;
        if (p.useAfterImage && p.afterImageEffect != null) p.afterImageEffect.enabled = true;

        if (p.aimPivot != null) burstDirection = (Vector2)p.aimPivot.up.normalized;
        else burstDirection = Vector2.right;

        burstTimer = burstDuration;

        p.currentBurstCount++;
        Debug.Log($"バースト発射！ 回数: {p.currentBurstCount} / {p.maxBurstCount}");

        currentSpeed = p.chargeForceLevels[p.currentChargeLevel];
        p.rb2D.linearVelocity = burstDirection * currentSpeed;

        Debug.Log($"バースト発射！ チャージレベル: {p.currentChargeLevel} / 速度: {currentSpeed}");
    }

    public void UpdateState()
    {
        burstTimer -= Time.deltaTime;
        float speed = p.rb2D.linearVelocity.magnitude;
        bool isSpeedTooLowAndGrounded = (speed <= p.moveSpeed) && p.IsGrounded();

        if (p.inputActions.Player.Charge.WasPressedThisFrame() && p.currentBurstCount < p.maxBurstCount)
        {
            Debug.Log("バースト中：空中再チャージ！");
            p.TransitionToState(p.StateCharge);
            return;
        }

        if (burstTimer <= 0f || isSpeedTooLowAndGrounded)
        {
            p.TransitionToState(p.StateNormal);
        }
        if (p.inputActions.Player.Jump.triggered)
        {
            p.TransitionToState(p.StateNormal);
            p.rb2D.linearVelocity = new Vector2(p.rb2D.linearVelocity.x, p.jumpForce);
        }
    }

    public void FixedUpdateState()
    {
        // 毎フレーム、衝突前の速度ベクトルを記憶
        lastVelocity = p.rb2D.linearVelocity;
    }

    // 2Dの衝突イベント（Collision2D）で完璧な反射を計算
    private void OnCollisionEnter(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & p.GetGroundLayerMask()) != 0)
        {
            Vector2 incomingVector = lastVelocity;
            if (incomingVector.magnitude < 1f) return;

            // 2Dの衝突点すべての法線を平均化
            Vector2 wallNormal = Vector2.zero;
            foreach (var contact in collision.contacts)
            {
                wallNormal += contact.normal;
            }
            wallNormal = wallNormal.normalized;

            // Vector2 で反射角を計算
            Vector2 reflectedDirection = Vector2.Reflect(incomingVector.normalized, wallNormal);

            burstDirection = reflectedDirection.normalized;
            currentSpeed = incomingVector.magnitude * p.reflectEfficiency;

            // Rigidbody2D の速度を直接上書きして弾き飛ばす！
            p.rb2D.linearVelocity = burstDirection * currentSpeed;

            burstTimer = burstDuration;

            Debug.Log($"2Dの床・壁で完璧な反射！ 速度: {currentSpeed}");
        }
    }

    public void Exit()
    {
        Debug.Log("バースト終了");
        p.OnCollisionEnterEvent -= OnCollisionEnter;

        if (p.trailRenderer != null)
        {
            p.trailRenderer.Clear();
            p.trailRenderer.enabled = false;
        }

        if (p.afterImageEffect != null)
        {
            p.afterImageEffect.enabled = false;
        }

        p.rb2D.linearVelocity = p.rb2D.linearVelocity * 0.2f;
    }
}