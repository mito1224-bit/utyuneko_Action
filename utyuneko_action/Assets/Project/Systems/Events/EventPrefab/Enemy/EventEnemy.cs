using System.Collections; // コルーチン用
using UnityEngine;

public class EventEnemy : MonoBehaviour
{
    [Header("イベント監督の参照")]
    [SerializeField] private RescueEventManager eventManager;

    [Header("吹っ飛び（ノックバック）の強さ")]
    [SerializeField] private float knockbackForceX = 8f;
    [SerializeField] private float knockbackForceY = 4f;

    private Rigidbody2D rb;
    private bool isDefeated = false; // 二重撃破防止フラグ

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            // 🔒【新設：当たる前の完全フリーズ】
            // インスペクターの設定がどうなっていても、ゲーム開始時にコードから強制的に
            // 「X軸固定」「Y軸固定」「Z軸回転固定」のフルロックを掛けます！
            // これにより、通常状態のdB君がどれだけ歩いて体当たりしてもビクともしなくなります。
            rb.constraints = RigidbodyConstraints2D.FreezePositionX |
                             RigidbodyConstraints2D.FreezePositionY |
                             RigidbodyConstraints2D.FreezeRotation;
        }
    }

    // Is TriggerがOFFなので「Collision2D」で受け取る
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDefeated) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerController p = collision.gameObject.GetComponent<PlayerController>();

            if (p != null && p.CurrentState is PlayerState_Burst)
            {
                // プレイヤーの位置を渡して撃破処理へ
                Defeated(collision.transform.position);

                TimeManager.Instance.TriggerGlobalSlowMotion(1.0f, 0.2f);
            }
        }
    }

    private void Defeated(Vector3 playerPosition)
    {
        isDefeated = true;

        if (rb != null)
        {
            // 🔓【新設：当たった瞬間のフリーズ解除】
            // バースト攻撃が当たったまさにこの瞬間、XとYの移動ロックを完全解除します！
            // （吹っ飛んだ時にゴロゴロ回転して埋まるのを防ぐため、Z軸の回転固定だけは残します）
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        // プレイヤーがいた方向と逆（奥）へ吹き飛ばすベクトルを計算
        float pushDirection = (transform.position.x - playerPosition.x) > 0 ? 1f : -1f;
        Vector2 knockbackVector = new Vector2(pushDirection * knockbackForceX, knockbackForceY);

        // ロックが解けた直後なので、この物理速度（ノックバック）が100%完璧に適用されます！
        rb.linearVelocity = knockbackVector;
        rb.gravityScale = 1f; // 重力を有効にして自然に落ちるように

        // 地面に横たわる演出（Z軸を90度傾ける）
        transform.rotation = Quaternion.Euler(0f, 0f, pushDirection * -90f);

        StartCoroutine(LayDownRoutine());

        // 監督にお知らせ
        if (eventManager != null)
        {
            eventManager.OnEnemyDefeated(this);
        }
    }

    private IEnumerator LayDownRoutine()
    {
        // 0.6秒ほど物理挙動で吹っ飛んで地面に落ちるのを待つ
        yield return new WaitForSeconds(0.6f);

        // 完全に動きを止めてその場に固定（ゾンビバグ・無限滑り防止）
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    // 補佐が頭上に来たあと、マネージャーから呼ばれる縮小消滅デモ
    public void StartAbsorb(Transform targetTransform, float duration)
    {
        StartCoroutine(AbsorbRoutine(targetTransform, duration));
    }

    private IEnumerator AbsorbRoutine(Transform target, float duration)
    {
        Vector3 startPosition = transform.position;
        Vector3 startScale = transform.localScale;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            if (target == null) break;

            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;

            // 1. 【位置】現在の位置から補佐の中心に向かってじわじわ移動
            transform.position = Vector3.Lerp(startPosition, target.position, t);

            // 2. 【サイズ】元のサイズから 0（消滅）に向かってじわじわ縮小
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);

            yield return null;
        }

        Destroy(gameObject);
    }
}