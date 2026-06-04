using System.Collections;
using UnityEngine;

// 2Dのコライダーが必須であることを保証
[RequireComponent(typeof(Collider2D))]
public class DirectionalLaunchPanel : MonoBehaviour
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
                // 3. パネルの「下方向」を射出方向に決定 (Vector2にキャスト)
                Vector2 launchDirection = -(Vector2)transform.up;

                // 4. 射出速度の計算 (Unity2021以降の linearVelocity に対応)
                float currentSpeed = rb.linearVelocity.magnitude;
                float boostedSpeed = currentSpeed * boostMultiplier;
                float clampedSpeed = Mathf.Clamp(boostedSpeed, minimumBoostSpeed, maxBoostSpeed);

                // 5. プレイヤーの状態を「バースト中」に切り替える
                player.TransitionToState(player.StateBurst);

                // 6. 1フレーム待って物理速度をパネルの方向・速度に強制上書き
                StartCoroutine(ForceLaunchNextFrame(rb, launchDirection.normalized, clampedSpeed));
            }
        }
    }

    // 2D用のリジッドボディを受け取るコルーチン
    private IEnumerator ForceLaunchNextFrame(Rigidbody2D rb, Vector2 direction, float speed)
    {
        // StateBurstのEnter内の処理（エイム方向への発射）が完全に終わるのを待つ
        yield return new WaitForFixedUpdate();

        if (rb != null)
        {
            // パネルの速度で上書き！
            rb.linearVelocity = direction * speed;

            Debug.Log($"[2Dパネル] 速度を後出し上書きしました: {speed} (方向: {direction})");
        }
    }
}