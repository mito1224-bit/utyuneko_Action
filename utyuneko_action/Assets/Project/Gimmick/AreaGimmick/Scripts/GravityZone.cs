using UnityEngine;

public class GravityZone : MonoBehaviour
{
    [Header("エリア内での重力の強さ")]
    [SerializeField] private float targetGravityScale = 0f;

    // プレイヤーがエリアに入る前の、元の重力スケールを一時保存する箱
    private float defaultGravityScale = 1f;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 当たったオブジェクトのタグが「Player」かどうかをチェック
        if (collision.CompareTag("Player"))
        {
            // プレイヤーのコントローラーを取得
            PlayerController p = collision.GetComponent<PlayerController>();

            if (p != null && p.rb2D != null)
            {
                // エリアに入る前の元の重力を記憶しておく（出た時に完璧に元に戻すため）
                defaultGravityScale = p.rb2D.gravityScale;

                // プレイヤーのグラビティスケールをターゲットの値に変更
                p.rb2D.gravityScale = targetGravityScale;

                Debug.Log($"【重力エリア進入】重力を {targetGravityScale} に変更しました。");

                // ====================================================================
                // もし無重力専用の移動ステート（上下移動など）を作ったら、
                // 以下のコメントアウトを解除するだけで一発でステートが切り替わるようになる
                // ====================================================================
                // if (targetGravityScale == 0f) {
                //     p.TransitionToState(p.StateZeroGravity);
                // }
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        // エリアから出て行ったとき
        if (collision.CompareTag("Player"))
        {
            PlayerController p = collision.GetComponent<PlayerController>();

            if (p != null && p.rb2D != null)
            {
                // エリアから出たら、記憶しておいた「元の重力」に自動で戻す
                p.rb2D.gravityScale = defaultGravityScale;

                Debug.Log("【重力エリア脱出】重力を元の値に戻しました。");

                // 通常ステートに戻す処理用
                // p.TransitionToState(p.StateNormal);
            }
        }
    }
}