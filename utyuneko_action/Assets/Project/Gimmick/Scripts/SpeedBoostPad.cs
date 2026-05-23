using UnityEngine;

public class SpeedBoostPad : MonoBehaviour
{
    [SerializeField] private float boostMultiplier = 1.5f;
    [SerializeField] private float minimumBoostSpeed = 30f;
    [SerializeField] private float maxBoostSpeed = 100f;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // ヒットストップ（必要に応じてコメントアウトを解除してください）
                // HitStopManager.Instance.Stop(0.035f);

                // 1. 現在の速度と、進んでいる「向き」を取得
                Vector3 currentVelocity = rb.linearVelocity;
                float currentSpeed = currentVelocity.magnitude;

                // 2. もし完全に止まっていたら、パッドの「前方向（transform.forward）」を進む向きにする（安全対策）
                Vector3 moveDirection = currentSpeed > 0.01f ? currentVelocity.normalized : transform.forward;

                // 3. 元の速度を 1.5倍 にする
                float boostedSpeed = currentSpeed * boostMultiplier;

                // 4. 最低速度と最高速度の間にクランプ（制限）する
                float finalSpeed = Mathf.Clamp(boostedSpeed, minimumBoostSpeed, maxBoostSpeed);

                // 5. 割り出した「向き」と「最終的な速度」を掛け合わせてリジッドボディに適用
                rb.linearVelocity = moveDirection * finalSpeed;

                // 残像エフェクト（必要に応じてコメントアウトを解除してください）
                // AfterImageManager.Instance.StartEmitting();

                Debug.Log($"ブーストパッド通過: 元の速度 {currentSpeed} -> 加速後の速度 {rb.linearVelocity.magnitude}");
            }
        }
    }
}