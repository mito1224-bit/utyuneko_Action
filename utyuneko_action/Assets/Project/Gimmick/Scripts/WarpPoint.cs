using UnityEngine;

public class WarpPoint : MonoBehaviour
{
    [SerializeField] private Transform warpTarget;
    // 2DなのでオフセットもVector2に変更（インスペクターでZ軸を気にしなくてよくなります）
    [SerializeField] private Vector2 warpOffset = new Vector2(0f, 1f);

    // 2D用のトリガーイベントに変更
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // プレイヤーのコンポーネントを取得
            PlayerController player = other.GetComponent<PlayerController>();

            if (player != null)
            {
                // 1. 速度と回転を完全にリセットする（Vector2ベースに変更）
                // これをやらないと、バースト中（超高速）にワープした際、
                // ワープ先でもその速度のまま壁に激突します。
                player.rb2D.linearVelocity = Vector2.zero;
                player.rb2D.angularVelocity = 0f; // 2DのangularVelocityはfloat型なので 0f にします

                // 2. ステートを「通常状態」に強制的に戻す
                // バースト中やチャージ中にワープした場合、状態がおかしくなるのを防ぎます。
                player.TransitionToState(player.StateNormal);

                // 3. 座標を書き換える
                Vector3 targetPosition = warpTarget.position + (Vector3)warpOffset;

                // 2.5Dゲームのバグ防止（ワープの拍子にZ軸がズレないよう、元の位置か0を死守する）
                targetPosition.z = 0f;

                player.transform.position = targetPosition;

                Debug.Log($"[{gameObject.name}] プレイヤーの状態を安全にresetしてワープさせました。");
            }
        }
    }
}