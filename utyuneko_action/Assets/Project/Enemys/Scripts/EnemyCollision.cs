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

    private Collider myCol;
    private EnemyHealth enemyHealth;

    void Awake()
    {
        myCol = GetComponent<Collider>();
        enemyHealth = GetComponent<EnemyHealth>();

        if (myCol != null)
            myCol.isTrigger = (collisionType == CollisionType.Pierce);
    }

    // =========================================================
    //  Reflect / PierceZone → OnCollisionEnter
    // =========================================================
    void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag(playerTag)) return;

        float impactSpeed = collision.relativeVelocity.magnitude;

        // プレイヤーがバースト状態（PlayerBurstレイヤー）かどうかを判定
        bool isBursting = (collision.gameObject.layer == LayerMask.NameToLayer("PlayerBurst"));

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
                enemyHealth?.HandleHit(impactSpeed);
                break;

            case CollisionType.PierceZone:
                // 本体は常に反射。貫通は子オブジェクト（PierceZoneTrigger）が
                // Physics.IgnoreCollision で先に成立させるため、ここに到達した
                // 衝突は「貫通面以外から当たった」とみなして弾く。
                ApplyKnockback(collision.rigidbody, collision.transform.position);
                enemyHealth?.HandleHit(impactSpeed);
                break;
        }
    }

    // =========================================================
    //  Pierce → OnTriggerEnter（すり抜けつつダメージを与える）
    // =========================================================
    void OnTriggerEnter(Collider other)
    {
        if (collisionType != CollisionType.Pierce) return;
        if (!other.CompareTag(playerTag)) return;

        Rigidbody rb = other.GetComponent<Rigidbody>();
        if (rb != null)
            enemyHealth?.HandleHit(rb.linearVelocity.magnitude);
    }

    // =========================================================
    //  共通ユーティリティ
    // =========================================================
    private void ApplyKnockback(Rigidbody targetRb, Vector3 targetPos)
    {
        if (targetRb == null) return;

        Vector3 dir = (targetPos - transform.position);
        dir.y = 0f;
        dir = dir.normalized;

        targetRb.linearVelocity = Vector3.zero;
        targetRb.AddForce(dir * knockbackForce, ForceMode.Impulse);
    }
}
