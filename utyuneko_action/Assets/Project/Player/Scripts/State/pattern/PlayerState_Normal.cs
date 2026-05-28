using UnityEngine;

public class PlayerState_Normal : IPlayerState
{
    private PlayerController p;

    public void Enter(PlayerController player)
    {
        p = player;
        Debug.Log("ステート変更：通常状態（Normal）");
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
            // Vector2 で速度を代入
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
        // 左右移動を Vector2 で計算
        Vector2 newVelocity = new Vector2(
            p.moveInput.x * p.moveSpeed,
            p.rb2D.linearVelocity.y
        );
        p.rb2D.linearVelocity = newVelocity;
    }

    public void Exit() { }
}