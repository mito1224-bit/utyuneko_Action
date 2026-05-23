using System.Collections;
using UnityEngine;

public class DirectionalLaunchPanel : MonoBehaviour
{
    [Header("射出設定")]
    [SerializeField] private float maxBoostSpeed = 100f; // 最高速度の制限
    [SerializeField] private float minimumBoostSpeed = 30f; // 最低保証速度
    [SerializeField] private float boostMultiplier = 1.5f; // スピード倍率

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();
            PlayerController player = other.GetComponent<PlayerController>();

            if (rb != null && player != null)
            {
                // 1. パネルの「下方向」を射出方向に決定
                Vector3 launchDirection = -transform.up;

                // 2. 射出速度の計算（あなたの元のロジック通り）
                float currentSpeed = rb.linearVelocity.magnitude;
                float boostedSpeed = currentSpeed * currentSpeed; // 必要に応じて multiplier に変更してください
                float clampedSpeed = Mathf.Clamp(boostedSpeed, minimumBoostSpeed, maxBoostSpeed);

                // 3. プレイヤーの状態を「バースト中」に切り替える
                // (この瞬間、プレイヤー側でエイム方向への上書きが走ります)
                player.TransitionToState(player.StateBurst);

                // 4. ★【ここが魔法】プレイヤーの上書き処理が終わった「直後」に、パネルの速度で再上書きする
                StartCoroutine(ForceLaunchNextFrame(rb, launchDirection.normalized, clampedSpeed));
            }
        }
    }

    // 次の物理フレームで速度を強制上書きするコルーチン
    private IEnumerator ForceLaunchNextFrame(Rigidbody rb, Vector3 direction, float speed)
    {
        // FixedUpdate（物理演算）の1フレーム分、あるいはEnterの処理が終わるまでほんの一瞬だけ待つ
        yield return new WaitForFixedUpdate();

        if (rb != null)
        {
            // プレイヤーのStateBurstが設定した速度を、上から力技で「パネルの速度」に書き換える！
            rb.linearVelocity = direction * speed;

            Debug.Log($"パネル単体で完結！ 速度を後出し上書きしました: {speed}");
        }
    }
}