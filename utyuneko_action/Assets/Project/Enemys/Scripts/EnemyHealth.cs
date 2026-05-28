using UnityEngine;

/// <summary>
/// エネミーのHP管理・ダメージ計算・撃破のみを担当する。
/// 衝突タイプの判定は EnemyCollision に分離済み。
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

    void Start()
    {
        currentHp = maxHp;
    }

    /// <summary>
    /// EnemyCollision から衝突速度を受け取ってダメージを計算する。
    /// </summary>
    public void HandleHit(float impactSpeed)
    {
        if (impactSpeed < damageSpeedThreshold) return;

        float extraSpeed = impactSpeed - damageSpeedThreshold;
        int damage = baseDamage + Mathf.FloorToInt(extraSpeed * speedDamageMultiplier);

        TakeDamage(damage);
        Debug.Log($"ヒット！ 速度:{impactSpeed:F1} -> 敵に {damage} ダメージ！ (残りHP: {currentHp})");
    }

    public void TakeDamage(int damage)
    {
        currentHp -= damage;
        if (currentHp <= 0) Die();
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} を撃破！");
        Destroy(gameObject);
    }
}
