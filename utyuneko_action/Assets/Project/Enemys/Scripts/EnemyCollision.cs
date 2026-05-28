using System.Collections;
using UnityEngine;

/// <summary>
/// エネミーの衝突タイプを一元管理するスクリプト。
///
/// Reflect    : プレイヤーをノックバックさせる
/// Pierce     : プレイヤーをすり抜けさせる（isTrigger=true）
/// Directional: 角度判定で貫通/反射を切り替え（スクリプト方式）
/// PierceZone : 子オブジェクトの貫通コライダーで貫通面を指定（推奨）
///
/// 【PierceZone の仕組み】
///   PierceZoneTrigger が isPiercing フラグを立てることで、
///   本体の OnCollisionEnter が SphereCollider の先当たりで
///   反射してしまうのを防ぐ。
/// </summary>
public class EnemyCollision : MonoBehaviour
{
    public enum CollisionType { Reflect, Pierce, Directional, PierceZone }

    [Header("衝突タイプ")]
    [Tooltip(
        "Reflect    : 全方向ノックバック\n" +
        "Pierce     : 全方向すり抜け\n" +
        "Directional: 角度で貫通/反射を切り替え\n" +
        "PierceZone : 子オブジェクトのコライダーで貫通面を指定（推奨）"
    )]
    public CollisionType collisionType = CollisionType.Reflect;

    [Header("共通設定")]
    public string playerTag = "Player";

    [Header("ノックバック設定（Reflect / Directional / PierceZone）")]
    public float knockbackForce = 15f;

    [Header("Directional 設定（Directional タイプのみ有効）")]
    [Tooltip("貫通を許可する角度帯（複数設定可）\n右(+X)=0°, 前(+Z)=90°, 左=±180°, 後(-Z)=-90°")]
    public AngleRange[] pierceRanges;

    [Tooltip("貫通ゾーンの両端に加える余白（度）。目安: 10〜25°")]
    public float pierceMargin = 15f;

    [Tooltip("貫通時にコリジョンを一時的に無視する秒数")]
    public float pierceIgnoreTime = 0.12f;

    // PierceZoneTrigger から操作されるフラグ
    // true の間は OnCollisionEnter での反射処理をスキップする
    [HideInInspector] public bool isPiercing = false;

    [System.Serializable]
    public struct AngleRange
    {
        public float min;
        public float max;

        public bool Contains(float angleDeg, float margin = 0f)
        {
            float a = Normalize(angleDeg);
            float mn = Normalize(min - margin);
            float mx = Normalize(max + margin);
            if (mn <= mx) return a >= mn && a <= mx;
            return a >= mn || a <= mx;
        }

        private static float Normalize(float a)
        {
            return Mathf.Repeat(a + 180f, 360f) - 180f;
        }
    }

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
    //  Reflect / Directional / PierceZone → OnCollisionEnter
    // =========================================================
    void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag(playerTag)) return;

        // PierceZoneTrigger が「貫通中」を通知していたらスキップ
        if (isPiercing) return;

        float impactSpeed = collision.relativeVelocity.magnitude;

        switch (collisionType)
        {
            case CollisionType.Reflect:
            case CollisionType.PierceZone:
                ApplyKnockback(collision.rigidbody, collision.transform.position);
                enemyHealth?.HandleHit(impactSpeed);
                break;

            case CollisionType.Directional:
                HandleDirectional(collision, impactSpeed);
                break;
        }
    }

    // =========================================================
    //  Pierce → OnTriggerEnter
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
    //  Directional の分岐処理
    // =========================================================
    private void HandleDirectional(Collision collision, float impactSpeed)
    {
        if (myCol == null) return;

        Vector3 approach = -collision.contacts[0].normal;
        Vector3 approachXZ = new Vector3(approach.x, 0f, approach.z);

        if (approachXZ.sqrMagnitude < 0.0001f)
        {
            approachXZ = new Vector3(
                collision.transform.position.x - transform.position.x,
                0f,
                collision.transform.position.z - transform.position.z
            );

            if (approachXZ.sqrMagnitude < 0.0001f)
            {
                ApplyKnockback(collision.rigidbody, collision.transform.position);
                enemyHealth?.HandleHit(impactSpeed);
                return;
            }
        }

        approachXZ.Normalize();
        float angle = Mathf.Atan2(approachXZ.z, approachXZ.x) * Mathf.Rad2Deg;

        if (IsInPierceRange(angle, pierceMargin))
        {
            StartCoroutine(TemporaryIgnore(collision.collider, pierceIgnoreTime));
        }
        else
        {
            ApplyKnockback(collision.rigidbody, collision.transform.position);
            enemyHealth?.HandleHit(impactSpeed);
        }
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

    private bool IsInPierceRange(float angleDeg, float margin = 0f)
    {
        if (pierceRanges == null || pierceRanges.Length == 0) return false;
        foreach (var range in pierceRanges)
            if (range.Contains(angleDeg, margin)) return true;
        return false;
    }

    private IEnumerator TemporaryIgnore(Collider playerCol, float seconds)
    {
        Physics.IgnoreCollision(myCol, playerCol, true);
        yield return new WaitForSeconds(seconds);
        if (myCol != null && playerCol != null)
            Physics.IgnoreCollision(myCol, playerCol, false);
    }
}