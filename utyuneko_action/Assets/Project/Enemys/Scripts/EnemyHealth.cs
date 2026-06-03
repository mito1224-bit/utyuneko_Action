using UnityEngine;

/// <summary>
/// エネミーのHP管理・ダメージ計算・撃破のみを担当する。
/// 衝突タイプの判定は EnemyCollision に分離済み。
/// 吹き飛ばし演出は EnemyKnockback（同GameObjectにアタッチ）に委譲する。
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    [Header("ステータス設定")]
    public int maxHp = 10;
    private int currentHp;

    [Header("ダメージ判定設定")]
    [Tooltip("ダメージを与えるための最低スピード")]
    public float damageSpeedThreshold = 5.0f;

    [Tooltip("最低スピードを満たしたときの基本ダメージ")]
    public int baseDamage = 1;

    [Tooltip("超過スピード1ごとの追加ダメージ倍率")]
    public float speedDamageMultiplier = 1.0f;

    private EnemyKnockback knockback;

    void Awake()
    {
        knockback = GetComponent<EnemyKnockback>();
    }

    void Start()
    {
        currentHp = maxHp;
    }

    /// <summary>
    /// EnemyCollision から衝突速度と被弾元の位置を受け取ってダメージを計算する。
    /// hitFromPosition は吹き飛び方向を決めるために使用する（=プレイヤーの位置）。
    /// </summary>
    public void HandleHit(float impactSpeed, Vector3 hitFromPosition)
    {
        if (impactSpeed < damageSpeedThreshold) return;

        float extraSpeed = impactSpeed - damageSpeedThreshold;
        int damage = baseDamage + Mathf.FloorToInt(extraSpeed * speedDamageMultiplier);

        TakeDamage(damage, hitFromPosition);
        Debug.Log($"ヒット！ 速度:{impactSpeed:F1} -> 敵に {damage} ダメージ！ (残りHP: {currentHp})");
    }

    /// <summary>
    /// 被弾元なしでダメージを与える（吹き飛びは自分自身の位置基準でフォールバック）。
    /// </summary>
    public void TakeDamage(int damage)
    {
        TakeDamage(damage, transform.position - Vector3.right);
    }

    public void TakeDamage(int damage, Vector3 hitFromPosition)
    {
        if (currentHp <= 0) return;

        currentHp -= damage;

        if (currentHp <= 0)
        {
            Die(damage, hitFromPosition);
        }
        else if (knockback != null)
        {
            knockback.ApplyHitKnockback(hitFromPosition, damage);
        }
    }

    private void Die(int lastDamage, Vector3 hitFromPosition)
    {
        Debug.Log($"{gameObject.name} を撃破！");
        if (knockback != null)
        {
            knockback.ApplyDeathKnockback(hitFromPosition, lastDamage);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
