using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SpeedBoostPad : MonoBehaviour
{
    [Header("射出設定")]
    [SerializeField] private float boostMultiplier = 1.5f;   // スピード倍率
    [SerializeField] private float minimumBoostSpeed = 30f;  // 最低保証速度
    [SerializeField] private float maxBoostSpeed = 100f;     // 最高速度の制限

    // 1. 2D用に OnTriggerEnter2D に変更
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // 2. 2Dのコンポーネントを取得
            Rigidbody2D rb = other.GetComponent<Rigidbody2D>();
            PlayerController player = other.GetComponent<PlayerController>();

            if (rb != null && player != null)
            {
                // ヒットストップ（必要に応じてコメントアウトを解除）
                // HitStopManager.Instance.Stop(0.035f);

                // 3. 現在の速度と、進んでいる「向き」を取得 (Vector2)
                Vector2 currentVelocity = rb.linearVelocity;
                float currentSpeed = currentVelocity.magnitude;

                // 4. もし完全に止まっていたら、パッドの「上方向（transform.up）」を進む向きにする（2Dの安全対策）
                // ※2Dでは正面が「Z方向（forward）」ではなく「Y方向（up）」または「X方向（right）」になることが多いため、upにしています。
                Vector2 moveDirection = currentSpeed > 0.01f ? currentVelocity.normalized : (Vector2)transform.up;

                // 5. 速度の計算とクランプ
                float boostedSpeed = currentSpeed * boostMultiplier;
                float finalSpeed = Mathf.Clamp(boostedSpeed, minimumBoostSpeed, maxBoostSpeed);

                // 6. プレイヤーの状態を「バースト中」に切り替える
                // (これでトレイルや残像がONになり、通常移動入力が遮断されます)
                //player.TransitionToState(player.StateBurst);

                // 7. コルーチンを動かして、1フレーム後に進行方向へ加速を叩き込む
                StartCoroutine(ForceBoostNextFrame(player, rb, moveDirection, finalSpeed));
            }
        }
    }

    private IEnumerator ForceBoostNextFrame(PlayerController player, Rigidbody2D rb, Vector2 direction, float speed)
    {
        // StateBurstのEnter処理（エイム発射など）が通り過ぎるのを1フレーム待つ
        yield return new WaitForFixedUpdate();

        if (rb != null)
        {
            // 割り出した「進行方向」と「加速後の速度」で物理を上書き！
            rb.linearVelocity = direction * speed;

            // (オプション) パネルと同様、この加速でバースト回数を消費させたくない場合はコメントアウトを解除してください
            // player.currentBurstCount = Mathf.Max(0, player.currentBurstCount - 1);

            Debug.Log($"[2Dブーストパッド] 元の速度: {rb.linearVelocity.magnitude / boostMultiplier} -> 加速後: {speed}");
        }
    }
}