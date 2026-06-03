using UnityEngine;

public class PlayerState_Normal : IPlayerState
{
    private PlayerController p;

    public void Enter(PlayerController player)
    {
        p = player;
        Debug.Log("ステート変更：通常状態（Normal）");

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
        if (p.IsGrounded())
        {
            // 【接地時】これまでの通常移動（キビキビ動く）
            Vector2 newVelocity = new Vector2(
                p.moveInput.x * p.moveSpeed,
                p.rb2D.linearVelocity.y
            );
            p.rb2D.linearVelocity = newVelocity;
        }
        else
        {
            // 【空中時】ジャンプとバースト後で両立する、超快適な空中制御！
            float currentX = p.rb2D.linearVelocity.x;

            if (p.moveInput.x != 0)
            {
                // ① 目標速度を「現在の勢い（絶対値）」に合わせて動的に変える
                // これにより、バースト直後の超高速を維持したまま左右に曲がれるようになります
                float currentMaxSpeed = Mathf.Max(Mathf.Abs(currentX), p.moveSpeed);
                float targetX = p.moveInput.x * currentMaxSpeed;

                // ② 方向転換の感度（ベース値）
                float turnSensitivity = 8.0f;

                // ③ 進行方向と「逆」の入力を入れた（反転したい）時は、さらに力を2倍にする
                // これがアクションゲームの「キビキビ感」を生む超重要ポイントです
                if (Mathf.Sign(p.moveInput.x) != Mathf.Sign(currentX))
                {
                    turnSensitivity = 16.0f;
                }

                // ④ Lerpを使って、現在の速度スケールに合わせた滑らかかつ強力な方向転換
                float newX = Mathf.Lerp(currentX, targetX, Time.fixedDeltaTime * turnSensitivity);
                p.rb2D.linearVelocity = new Vector2(newX, p.rb2D.linearVelocity.y);
            }
            else
            {
                // 入力がない場合：
                // 通常ジャンプの範囲内ならピタッと止まり、バースト後の超高速状態なら少し滑るように減速
                float brakeSpeed = (Mathf.Abs(currentX) > p.moveSpeed) ? 15.0f : 35.0f;
                float newX = Mathf.MoveTowards(currentX, 0f, Time.fixedDeltaTime * brakeSpeed);
                p.rb2D.linearVelocity = new Vector2(newX, p.rb2D.linearVelocity.y);
            }
        }
    }

    public void Exit() { }
}