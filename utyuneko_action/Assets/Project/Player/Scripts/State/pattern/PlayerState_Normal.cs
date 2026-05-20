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
        // ジャンプボタンが押され、かつ地面にいるならジャンプ！
        // （本体が持っているInputSystemや着地判定の関数を借りて使います）
        if (p.inputActions.Player.Jump.triggered && p.IsGrounded())
        {
            p.rb.linearVelocity = new Vector3(p.rb.linearVelocity.x, p.jumpForce, 0.0f);
        }

        // ★【追加】空中にいる時に、もし右クリック（またはコントローラーのボタン）が押されたらチャージへ！
        // ※事前にInput Action画面で「Charge」というButtonトリガーを作っておく必要があります
        if (p.inputActions.Player.Charge.triggered)
        {
            // 司令塔に頼んで、ステートを「チャージ」に切り替えてもらう！
            p.TransitionToState(p.StateCharge);
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