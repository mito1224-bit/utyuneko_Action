using UnityEngine;

/// <summary>
/// エネミーの衝突タイプを一元管理するスクリプト。
///
/// Reflect    : 全方向ノックバック（バースト中はすり抜け）
/// Pierce     : 全方向すり抜け（isTrigger=true）
/// PierceZone : 本体は常に反射。貫通は子オブジェクトの PierceZoneTrigger が担当する。
///
/// 【PierceZone の仕組み】
///   特定の方向からのみ貫通させたいエネミー向け。
///   本体（このスクリプト）は OnCollisionEnter で常にノックバック（反射）する。
///   貫通させたい面に子オブジェクト（トリガー Collider + PierceZoneTrigger）を配置し、
///   プレイヤーがバースト中にその子トリガーへ入ったときだけ、
///   PierceZoneTrigger が Physics.IgnoreCollision で本体とのすり抜けを許可する。
///   子トリガーを本体より外側へはみ出して配置することで、
///   本体の反射より先に貫通判定が成立する。
/// </summary>
public class EnemyCollision : MonoBehaviour
{
    public enum CollisionType { Reflect, Pierce, PierceZone }

    [Header("衝突タイプ")]
    [Tooltip(
        "Reflect    : 全方向ノックバック（バースト中はすり抜け）\n" +
        "Pierce     : 全方向すり抜け\n" +
        "PierceZone : 本体は常に反射。貫通は子の PierceZoneTrigger が担当"
    )]
    public CollisionType collisionType = CollisionType.Reflect;

    [Header("共通設定")]
    public string playerTag = "Player";

    [Header("ノックバック設定")]
    public float knockbackForce = 15f;

    [Header("Pierce専用設定（2コライダー方式）")]
    [Tooltip("Pierceタイプ: 床・壁に当たるソリッドのBodyコライダー。プレイヤーとは実行時に衝突無視する。\n" +
             "未設定ならこのGameObjectの非トリガーColliderを自動採用")]
    public Collider2D pierceBodyCollider;
    [Tooltip("Pierceタイプ: プレイヤー検出用トリガーコライダー（ダメージ判定）。\n" +
             "未設定ならこのGameObjectのトリガーColliderを自動採用")]
    public Collider2D pierceDamageTrigger;

    private Collider2D myCol;
    private EnemyHealth enemyHealth;

    void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();

        // 回転だけ固定する（Z回転フリーズ）。位置は固定しない。
        // 巡回移動は EnemyMovement が rb.MovePosition でスイープ移動させ、静的な床・壁にぶつかって
        // 止まる前提（CLAUDE.md 2026/06/25 のすり抜け修正）。ここで FreezeAll にして位置を固定すると
        // MovePosition の移動・壁衝突と干渉してしまうため、位置の拘束はかけない。
        // ※プレイヤーに押されてズレる懸念は Rigidbody2D の Mass / 衝突解決側で調整する。
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (collisionType == CollisionType.Pierce)
        {
            // Pierce: 環境用のソリッドBody + プレイヤー検出用トリガーの2コライダー構成
            AutoAssignPierceColliders();
            if (pierceBodyCollider != null) pierceBodyCollider.isTrigger = false; // 床・壁にソリッドで当たる
            if (pierceDamageTrigger != null) pierceDamageTrigger.isTrigger = true;  // プレイヤー検出はトリガー

            // ダメージ用トリガーが無いと OnTriggerEnter2D が発火せず、HandleHit が一度も呼ばれない
            // ＝プレイヤーが当てても敵が絶対に死なない。コライダー1個のプレハブに Pierce を設定すると起きる。
            // 2コライダー方式（Body=ソリッド + もう1つ=トリガー）にして両方を割り当てること。
            if (pierceDamageTrigger == null)
                Debug.LogWarning($"{gameObject.name}: Pierce なのにダメージ用トリガーColliderがありません。" +
                                 "トリガーのCollider2Dを追加して pierceDamageTrigger に割り当てないと敵が死にません。", this);
        }
        else
        {
            // Reflect / PierceZone: 本体コライダーは常にソリッド
            myCol = GetComponent<Collider2D>();
            if (myCol != null) myCol.isTrigger = false;
        }
    }

    void Start()
    {
        if (collisionType != CollisionType.Pierce || pierceBodyCollider == null) return;

        // プレイヤーはすり抜けさせたいので、Bodyコライダーとプレイヤーの衝突だけ無視する。
        // （床・壁との衝突は維持されるので、すり抜けなくなる）
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null) return;

        foreach (var playerCol in player.GetComponentsInChildren<Collider2D>())
        {
            if (playerCol != null)
                Physics2D.IgnoreCollision(pierceBodyCollider, playerCol, true);
        }
    }

    // Inspector 未設定時に、自身のコライダーから Body/トリガーを自動割り当てする
    private void AutoAssignPierceColliders()
    {
        if (pierceBodyCollider != null && pierceDamageTrigger != null) return;

        foreach (var col in GetComponents<Collider2D>())
        {
            if (col.isTrigger)
            {
                if (pierceDamageTrigger == null) pierceDamageTrigger = col;
            }
            else
            {
                if (pierceBodyCollider == null) pierceBodyCollider = col;
            }
        }
    }

    // =========================================================
    //  Reflect / PierceZone → OnCollisionEnter2D
    // =========================================================
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag(playerTag)) return;

        float impactSpeed = collision.relativeVelocity.magnitude;

        // プレイヤーがバースト状態（PlayerBurstレイヤー）かどうかを判定
        bool isBursting = (collision.gameObject.layer == LayerMask.NameToLayer("PlayerBurst"));

        Vector3 hitFromPos = collision.transform.position;

        switch (collisionType)
        {
            case CollisionType.Reflect:
                // 反射そのものは PlayerState_Burst（壁と同じ Vector3.Reflect）が担当する。
                // バースト中もすり抜けさせず、壁のように跳ね返す。
                // 非バースト時はノックバックで弾く。ダメージはどちらでも与える。
                if (!isBursting)
                {
                    ApplyKnockback(collision.rigidbody, collision.transform.position);
                }
                enemyHealth?.HandleHit(impactSpeed, hitFromPos);
                break;

            case CollisionType.PierceZone:
                // 本体は常に反射。貫通は子オブジェクト（PierceZoneTrigger）が
                // Physics.IgnoreCollision で先に成立させるため、ここに到達した
                // 衝突は「貫通面以外から当たった」とみなして弾く。
                ApplyKnockback(collision.rigidbody, collision.transform.position);
                enemyHealth?.HandleHit(impactSpeed, hitFromPos);
                break;
        }
    }

    // =========================================================
    //  Pierce → OnTriggerEnter（すり抜けつつダメージを与える）
    // =========================================================
    void OnTriggerEnter2D(Collider2D other)
    {
        if (collisionType != CollisionType.Pierce) return;
        if (!other.CompareTag(playerTag)) return;

        Rigidbody2D rb = other.GetComponent<Rigidbody2D>();
        if (rb != null)
            enemyHealth?.HandleHit(rb.linearVelocity.magnitude, other.transform.position);
    }

    // =========================================================
    //  共通ユーティリティ
    // =========================================================
    private void ApplyKnockback(Rigidbody2D targetRb, Vector3 targetPos)
    {
        if (targetRb == null) return;

        // XY平面で水平方向（X）にのみ弾く（縦方向には飛ばさない）
        Vector2 dir = (Vector2)(targetPos - transform.position);
        dir.y = 0f;
        dir = dir.normalized;

        targetRb.linearVelocity = Vector2.zero;
        targetRb.AddForce(dir * knockbackForce, ForceMode2D.Impulse);
    }
}
