using UnityEngine;

public class PlayerState_Normal : IPlayerState
{
    // モデルの最初の向き
    static float targetYAngle = 310f;
    private PlayerController p;

    public void Enter(PlayerController player)
    {
        p = player;
        Debug.Log("ステート変更：通常状態（Normal）");

        if (p.visualManager != null && p.visualManager.playerVisual != null)
        {
            float currentVelocityX = p.rb2D.linearVelocity.x;

            if (Mathf.Abs(currentVelocityX) > 0.1f)
            {
                targetYAngle = (currentVelocityX > 0f) ? 310f : 50f;
            }
            else
            {
                if (p.moveInput.x > 0.01f) targetYAngle = 310f;
                else if (p.moveInput.x < -0.01f) targetYAngle = 50f;
                else
                {
                    float currentY = p.visualManager.playerVisual.localRotation.eulerAngles.y;
                    targetYAngle = (currentY > 180f) ? 310f : 50f;
                }
            }

            p.visualManager.playerVisual.localRotation = Quaternion.Euler(0f, targetYAngle, 0f);
        }

        // 通常状態に戻ったら、演出用マネージャー側も一度安全にリセットをかける
        if (p.visualManager != null)
        {
            p.visualManager.ResetVisuals();
        }

        if (p.hoverSensor != null)
        {
            p.hoverSensor.GetComponent<Collider2D>().enabled = true;
        }
    }

    public void UpdateState()
    {
        if (p.inputActions.Player.Charge.IsPressed() && p.currentBurstCount < p.maxBurstCount)
        {
            p.TransitionToState(p.StateCharge);
            return;
        }

        if (p.inputActions.Player.Jump.triggered && p.IsGrounded())
        {
            p.rb2D.linearVelocity = new Vector2(p.rb2D.linearVelocity.x, p.jumpForce);

            if (p.currentBurstCount > 0)
            {
                p.currentBurstCount = 0;
            }
        }

        if (p.IsGrounded() && p.rb2D.linearVelocity.y <= 0.01f)
        {
            if (p.currentBurstCount > 0)
            {
                p.currentBurstCount = 0;
                Debug.Log("バースト回数がリセット");
            }
        }
    }

    public void FixedUpdateState()
    {
        if (p.anim != null)
        {
            p.anim.SetBool("isWalk", p.moveInput.x != 0);
        }

        if (p.IsGrounded())
        {
            Vector2 newVelocity = new Vector2(
                p.moveInput.x * p.moveSpeed,
                p.rb2D.linearVelocity.y
            );
            p.rb2D.linearVelocity = newVelocity;
        }
        else
        {
            float currentX = p.rb2D.linearVelocity.x;

            if (p.moveInput.x != 0)
            {
                float currentMaxSpeed = Mathf.Max(Mathf.Abs(currentX), p.moveSpeed);
                float targetX = p.moveInput.x * currentMaxSpeed;

                float turnSensitivity = 8.0f;

                if (Mathf.Sign(p.moveInput.x) != Mathf.Sign(currentX))
                {
                    turnSensitivity = 16.0f;
                }

                float newX = Mathf.Lerp(currentX, targetX, Time.fixedDeltaTime * turnSensitivity);
                p.rb2D.linearVelocity = new Vector2(newX, p.rb2D.linearVelocity.y);
            }
            else
            {
                float brakeSpeed = (Mathf.Abs(currentX) > p.moveSpeed) ? 15.0f : 35.0f;
                float newX = Mathf.MoveTowards(currentX, 0f, Time.fixedDeltaTime * brakeSpeed);
                p.rb2D.linearVelocity = new Vector2(newX, p.rb2D.linearVelocity.y);
            }
        }

        // 左右移動中のしなり（Lean）処理
        if (p.visualManager != null && p.visualManager.playerVisual != null && p.anim != null)
        {
            float targetLeanAngle = 0.0f;

            // p.leanAngle ではなく、マネージャー側の p.visualManager.leanAngle を見に行きます！
            if (p.moveInput.x > 0.01f)
            {
                targetYAngle = 310f;
                targetLeanAngle = -(p.moveInput.x * p.visualManager.leanAngle);
            }
            else if (p.moveInput.x < -0.01f)
            {
                targetYAngle = 50f;
                targetLeanAngle = p.moveInput.x * p.visualManager.leanAngle;
            }

            Quaternion targetRotation = Quaternion.Euler(targetLeanAngle, targetYAngle, 0f);

            // ★修正の肝：スムージングの速度もマネージャー側を参照します！
            p.visualManager.playerVisual.localRotation = Quaternion.Lerp(
                p.visualManager.playerVisual.localRotation,
                targetRotation,
                Time.fixedDeltaTime * p.visualManager.leanSmoothing
            );
        }

        if (p.visualManager != null)
        {
            p.visualManager.UpdateSquashAndStretch();
        }
    }

    public void Exit() { }
}