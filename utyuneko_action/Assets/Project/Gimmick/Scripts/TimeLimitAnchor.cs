using System.Collections;
using UnityEngine;

public class TimeLimitAnchor : MonoBehaviour
{
    [Header("── 猶予時間設定 ──────────────────")]
    [Tooltip("プレイヤーが触れてから、床が崩れて操作が効かなくなるまでの時間（秒）")]
    [SerializeField] private float holdingDuration = 0.8f;

    private bool _isHolding = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !_isHolding)
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                StartCoroutine(HoldAndFallRoutine(player));
            }
        }
    }

    private IEnumerator HoldAndFallRoutine(PlayerController player)
    {
        _isHolding = true;

        // 【最重要】突入時の綺麗な燃料状態をキープ
        int savedFuel = player.currentBurstCount;

        // 1. プレイヤーをその場にピタッと静止（大砲と同じ物理ロック）
        player.enabled = false;
        player.rb2D.linearVelocity = Vector2.zero;
        player.rb2D.bodyType = RigidbodyType2D.Kinematic;
        player.rb2D.constraints = RigidbodyConstraints2D.FreezeAll;

        // プレイヤーの座標をこのオブジェクトの中心に吸い寄せる
        player.transform.position = transform.position;

        // 2. 制限時間のカウントダウン（この間にプレイヤーは引っ張り操作をする）
        float elapsed = 0f;
        while (elapsed < holdingDuration)
        {
            elapsed += Time.deltaTime;
            player.currentBurstCount = savedFuel; // 燃料を維持

            // ★ もし制限時間内にプレイヤーが指を離して「発射」されたらループを抜ける
            // (プレイヤーの発射状態を検知するフラグがあればここに書く。
            //  なければ、時間切れで自動的に元のステートに戻して発射させる形でもOK)

            yield return null;
        }

        // 3. 時間切れ、または発射による「床の崩落（解放）」
        player.rb2D.constraints = RigidbodyConstraints2D.FreezeRotation;
        player.rb2D.bodyType = RigidbodyType2D.Dynamic;
        player.enabled = true; // 操作権を返す

        // 燃料を維持したまま空中に放り出す
        player.currentBurstCount = savedFuel;

        // 床自体のグラフィックや判定を消す（崩壊）
        GetComponent<SpriteRenderer>().enabled = false;
        GetComponent<Collider2D>().enabled = false;

        // しばらくしたら復活する処理へ...
        yield return new WaitForSeconds(3.0f);
        GetComponent<SpriteRenderer>().enabled = true;
        GetComponent<Collider2D>().enabled = true;
        _isHolding = false;
    }
}