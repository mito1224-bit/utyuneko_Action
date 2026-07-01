using Unity.VisualScripting;
using UnityEngine;

public class PlayerState_Burst : IPlayerState
{
    private PlayerController p;

    private bool isCharge = false;
    private int reflectCount = 0;
    private float burstDuration = 5.0f;
    private float burstTimer;
    private Vector2 burstDirection;
    private float currentSpeed;
    private Vector2 lastVelocity;

    public void Enter(PlayerController player)
    {
        p = player;
        isCharge = false;

        SoundManager.Instance.PlaySE(SeType.PlayerBurstBegin);

        reflectCount = 0;

        if (p.anim != null)
        {
            p.anim.SetBool("isBurst", true);
        }

        p.OnCollisionEnterEvent += OnCollisionStay;

        if (p.useTrail && p.trailRenderer != null) p.trailRenderer.enabled = true;
        if (p.useAfterImage && p.afterImageEffect != null) p.afterImageEffect.enabled = true;

        if (p.aimPivot != null) burstDirection = (Vector2)p.aimPivot.up.normalized;
        else burstDirection = Vector2.right;

        burstTimer = burstDuration;
        p.currentBurstCount++;
        Debug.Log($"バースト発射！ 回数: {p.currentBurstCount} / {p.maxBurstCount}");

        currentSpeed = p.chargeForceLevels[p.currentChargeLevel];
        p.rb2D.linearVelocity = burstDirection * currentSpeed;

        // ドリル回転の蓄積角度をリセット
        if (p.visualManager != null)
        {
            p.visualManager.ResetDrillRotation();
        }

        Debug.Log($"バースト発射！ チャージレベル: {p.currentChargeLevel} / 速度: {currentSpeed}");
    }

    public void UpdateState()
    {
        burstTimer -= Time.deltaTime;
        float speed = p.rb2D.linearVelocity.magnitude;
        bool isSpeedTooLowAndGrounded = (speed <= p.moveSpeed) && p.IsGrounded();

        if (p.inputActions.Player.Charge.WasPressedThisFrame() && p.currentBurstCount < p.maxBurstCount)
        {
            isCharge = true;
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

            if (p.canCancelBurstWithJump)
            {
                p.rb2D.linearVelocity = new Vector2(p.rb2D.linearVelocity.x, p.jumpForce);

                SoundManager.Instance.PlaySE(SeType.PlayerJump);
                SoundManager.Instance.PlaySE(SeType.PlayerJump2);

                if (p.visualManager != null)
                {
                    p.visualManager.TriggerJumpStretch();
                }
            }
            else
            {
                p.rb2D.linearVelocity = new Vector2(p.rb2D.linearVelocity.x, p.rb2D.linearVelocity.y);
            }
        }
    }

    public void FixedUpdateState()
    {
        lastVelocity = p.rb2D.linearVelocity;

        // ★ビジュアルマネージャーへ演出の更新を委託
        if (p.visualManager != null && p.rb2D.linearVelocity.sqrMagnitude > 0.1f)
        {
            // 回転（しなり・ドリル）の更新
            p.visualManager.UpdateRotation(p.rb2D.linearVelocity.x, p.rb2D.linearVelocity.y, p.rb2D.linearVelocity.magnitude);
            // 伸縮タイマーの更新
            p.visualManager.UpdateSquashAndStretch();
        }
    }

    private void OnCollisionStay(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & p.GetGroundLayerMask()) != 0)
        {
            SoundManager.Instance.PlaySE(SeType.PlayerWallHit);

            Vector2 incomingVector = lastVelocity;
            if (incomingVector.magnitude < 1f) return;

            Vector2 wallNormal = Vector2.zero;
            foreach (var contact in collision.contacts)
            {
                wallNormal += contact.normal;
            }
            wallNormal = wallNormal.normalized;

            Vector2 reflectedDirection = Vector3.Reflect(incomingVector.normalized, wallNormal);

            burstDirection = reflectedDirection.normalized;
            currentSpeed = incomingVector.magnitude * p.reflectEfficiency;

            p.rb2D.linearVelocity = burstDirection * currentSpeed;
            burstTimer = burstDuration;

            // 上下左右固定バウンドの伸縮をセット
            if (p.visualManager != null)
            {
                p.visualManager.TriggerSquash(wallNormal, incomingVector);
            }

            reflectCount++;
            //反射回数が最大反射回数を超えていたら通常状態に遷移
            if (p.maxReflect < reflectCount)
            {
                p.TransitionToState(p.StateNormal);
                p.rb2D.linearVelocity = new Vector2(p.rb2D.linearVelocity.x * 0.5f, p.rb2D.linearVelocity.y * 0.5f);
            }

            Debug.Log($"バースト中衝突反射！ 速度: {currentSpeed}");
        }

        //TimeManager.Instance.TriggerIndividualHitStop(p.gameObject, collision.gameObject, 0.03f);
    }

    public void Exit()
    {
        Debug.Log("バースト終了");
        p.OnCollisionEnterEvent -= OnCollisionStay;

        // 空中チャージへの遷移時以外ならアニメーションを戻す
        if (p.anim != null && !isCharge)
        {
            p.anim.SetBool("isBurst", false);
        }

        // マネージャーに見た目のリセットを依頼
        if (p.visualManager != null)
        {
            p.visualManager.ResetVisuals();
        }

        if (p.trailRenderer != null && !isCharge)
        {
            p.trailRenderer.Clear();
            p.trailRenderer.enabled = false;
        }

        if (p.afterImageEffect != null && !isCharge)
        {
            p.afterImageEffect.enabled = false;
        }
    }
}