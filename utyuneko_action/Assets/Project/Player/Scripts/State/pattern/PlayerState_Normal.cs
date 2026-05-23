using UnityEngine;

public class PlayerState_Normal : IPlayerState
{
    private PlayerController p; // 司令塔（本体）の参照を保管する箱

    // 1. 通常状態になった瞬間の処理
    public void Enter(PlayerController player)
    {
        p = player; // 本体（肉体）の情報をいつでも使えるように記憶する
        Debug.Log("ステート変更：通常状態（Normal）");
    }

    // 2. 毎フレームの入力チェック（Updateの代わり）
    public void UpdateState()
    {
        if (p.inputActions.Player.Charge.IsPressed() && p.currentBurstCount < p.maxBurstCount)
        {
            p.TransitionToState(p.StateCharge);
            return;
        }

        // ジャンプボタンが押され、かつ地面にいるならジャンプ！
        // （本体が持っているInputSystemや着地判定の関数を借りて使います）
        if (p.inputActions.Player.Jump.triggered && p.IsGrounded())
        {
            p.rb.linearVelocity = new Vector3(p.rb.linearVelocity.x, p.jumpForce, 0.0f);

            if (p.currentBurstCount > 0)
            {
                p.currentBurstCount = 0;
                //UI
            }
        }

        if (p.IsGrounded() && p.rb.linearVelocity.y <= 0.01f)
        {
            if (p.currentBurstCount > 0)
            {
                p.currentBurstCount = 0;
                Debug.Log("バースト回数がリセット");

                // もしUI（残弾数表示など）があるなら、ここでUI更新（p.burstUI.UpdateDisplay...など）を呼ぶと完璧です！
            }
        }
    }

    // 3. 毎フレームの物理計算（FixedUpdateの代わり）
    public void FixedUpdateState()
    {
        // 以前組み立てた左右移動の物理計算をここに引っ越し！
        Vector3 newVelocity = new Vector3(
            p.moveInput.x * p.moveSpeed,
            p.rb.linearVelocity.y,
            0.0f
        );
        p.rb.linearVelocity = newVelocity;
    }

    // 4. 通常状態が終わる瞬間の処理
    public void Exit()
    {
        // 次の状態へ行く前に、何か後始末をしたい場合はここに書く
    }
}