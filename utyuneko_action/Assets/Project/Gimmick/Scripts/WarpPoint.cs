using UnityEngine;

public class WarpPoint : MonoBehaviour
{
    [SerializeField] private Transform warpTarget;
    [SerializeField] private Vector3 warpOffset = new Vector3(0f, 1f, 0f);

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // プレイヤーのコンポーネントを取得
            PlayerController player = other.GetComponent<PlayerController>();

            if (player != null)
            {
                // 1. 速度を完全にリセットする（超重要）
                // これをやらないと、バースト中（超高速）にワープした際、
                // ワープ先でもその速度のまま壁に激突します。
                player.rb.linearVelocity = Vector3.zero; // Unity 2025以降は linearVelocity / 以前は velocity
                player.rb.angularVelocity = Vector3.zero;

                // 2. ステートを「通常状態」に強制的に戻す
                // バースト中やチャージ中にワープした場合、状態がおかしくなるのを防ぎます。
                player.TransitionToState(player.StateNormal);

                // 3. 座標を書き換える
                player.transform.position = warpTarget.position + warpOffset;

                Debug.Log($"[{gameObject.name}] プレイヤーの状態を安全にリセットしてワープさせました。");
            }
        }
    }
}