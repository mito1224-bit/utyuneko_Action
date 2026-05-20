using UnityEngine;

public class DirectionalLaunchPanel : MonoBehaviour
{
    [Header("射出設定")]
    [SerializeField] private float launchForce = 35f; // 飛ばす強さ
    [SerializeField] private float hitStopDuration = 0.08f; // 手応え用のヒットストップ

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Rigidbody2D rb = other.GetComponent<Rigidbody2D>();
            PlayerMovement movement = other.GetComponent<PlayerMovement>();

            if (rb != null)
            {
                // 1. ヒットストップで「ガツン」とさせる
                if (HitStopManager.Instance != null)
                {
                    HitStopManager.Instance.Stop(hitStopDuration);
                }

                // 2. パネルの「上方向」を射出方向に決定
                // パネルを回転させれば、transform.up がその方向を向きます
                Vector2 launchDirection = transform.up;

                // 3. 速度を上書き（弾丸モードを強制ONにする）
                rb.linearVelocity = launchDirection * launchForce;

                // 4. PlayerMovement側の状態を「バースト中」に書き換える
                // これにより、飛んでいる間は左右移動が無効化されます
                if (movement != null)
                {
                    // 外部からフラグをいじるために、PlayerMovement側で
                    // isBursting を public にするか、専用の関数を呼ぶ必要があります
                    movement.Invoke("EnableBurstFromExternal", 0);
                }

                // ★演出：射出した瞬間にパーティクルなどを出すと最高です
                Debug.Log($"パネル射出: 方向 {launchDirection}");
            }
        }
    }
}