using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerState_Normal : IPlayerState
{
    // モデルの最初の向き
    static float targetYAngle = 310f;
    private PlayerController p;

    private bool wasGroundedLastFrame;

    public void Enter(PlayerController player)
    {
        p = player;
        Debug.Log("ステート変更：通常状態（Normal）");

        if (p.anim != null)
        {
            p.anim.SetBool("isBurst", false);
        }

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

        wasGroundedLastFrame = p.IsGrounded();
    }

    public void UpdateState()
    {
        if (p.inputActions.Player.Charge.IsPressed() && p.currentBurstCount < p.maxBurstCount)
        {
            p.TransitionToState(p.StateCharge);
            return;
        }

        //ジャンプ
        if (p.inputActions.Player.Jump.triggered && p.IsGrounded())
        {
            SoundManager.Instance.PlaySE(SeType.PlayerJump);
            SoundManager.Instance.PlaySE(SeType.PlayerJump2);

            p.rb2D.linearVelocity = new Vector2(p.rb2D.linearVelocity.x, p.jumpForce);

            if (p.visualManager != null)
            {
                p.visualManager.TriggerJumpStretch();
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
            else
            {
                // 生の角度をそのまま入れるのではなく、180度より大きければ「右(310)」、
                // 小さければ「左(50)」へと、近い方の綺麗な角度にカチッとスナップさせます！
                float currentY = p.visualManager.playerVisual.localRotation.eulerAngles.y;
                targetYAngle = (currentY > 180f) ? 310f : 50f;
            }

            // 確定した綺麗なおすすめ角度（310 or 50）に向かって Lerp させる
            Quaternion targetRotation = Quaternion.Euler(targetLeanAngle, targetYAngle, 0f);

            p.visualManager.playerVisual.localRotation = Quaternion.Lerp(
                p.visualManager.playerVisual.localRotation,
                targetRotation,
                Time.fixedDeltaTime * p.visualManager.leanSmoothing
            );
        }

        bool isGroundedNow = p.IsGrounded();

        //「前フレームは空中だった」かつ「今フレームは接地している」なら着地した瞬間！
        if (isGroundedNow && !wasGroundedLastFrame)
        {
            // 下方向にしっかり落ちている時だけ潰す（床を歩いている時の誤作動防止）
            if (p.rb2D.linearVelocity.y <= 0.1f)
            {
                if (p.visualManager != null)
                {
                    p.visualManager.TriggerLandSquash();
                }

                if (!BaseEventManager.IsAnyEventPlaying)
                {
                    if (SceneManager.GetActiveScene().name != "TitleScene")
                    {
                        SoundManager.Instance.PlaySE(SeType.PlayerLanding);
                        SoundManager.Instance.PlaySE(SeType.PlayerLanding2);
                    }
                }
            }
        }

        // 最後に、今の接地状態を「前フレームの状態」として記憶して次のフレームへ
        wasGroundedLastFrame = isGroundedNow;

        if (p.visualManager != null)
        {
            p.visualManager.UpdateHoverBobbing(p.rb2D.linearVelocity.x);
        }

        // 既存の伸縮更新処理
        if (p.visualManager != null)
        {
            p.visualManager.UpdateSquashAndStretch();
        }
    }

    public void Exit() { }
}