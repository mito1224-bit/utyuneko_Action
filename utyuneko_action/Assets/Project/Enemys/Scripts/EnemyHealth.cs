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

    [Header("状態フラグ")]
    [Tooltip("HPが0になって撃破された瞬間に true になる（読み取り専用。他スクリプトから参照可）")]
    [SerializeField] private bool isDeadFlg = false;
    public bool IsDeadFlg => isDeadFlg; // 外部からは読み取りのみ

    [Header("バースト限定ダメージ")]
    [Tooltip("プレイヤーがバースト中のときだけダメージを受ける（ジャンプで軽く当たっただけでは倒せない）。" +
             "OFF＝速度が閾値を超えれば非バーストでもダメージ")]
    public bool requireBurstToDamage = true;

    [Header("ダメージ判定設定")]
    [Tooltip("ダメージを与えるための最低スピード")]
    public float damageSpeedThreshold = 5.0f;

    [Tooltip("最低スピードを満たしたときの基本ダメージ")]
    public int baseDamage = 1;

    [Tooltip("超過スピード1ごとの追加ダメージ倍率")]
    public float speedDamageMultiplier = 1.0f;

    private EnemyKnockback knockback;
    private EnemyShield shield;
    private HitFlash hitFlash;

    void Awake()
    {
        knockback = GetComponent<EnemyKnockback>();
        shield = GetComponent<EnemyShield>(); // 盾を持つ敵のみ。無ければ null
        hitFlash = GetComponent<HitFlash>();   // 白フラッシュ演出。付いていなければ null（任意）
    }

    void Start()
    {
        currentHp = maxHp;
    }

    /// <summary>
    /// EnemyCollision から衝突速度と被弾元の位置を受け取ってダメージを計算する。
    /// hitFromPosition は吹き飛び方向を決めるために使用する（=プレイヤーの位置）。
    /// isBursting はプレイヤーがバースト攻撃中かどうか。requireBurstToDamage が ON なら
    /// バースト中でない当たり（ジャンプ接触など）はダメージ無効にする。
    /// </summary>
    public void HandleHit(float impactSpeed, Vector3 hitFromPosition, bool isBursting)
    {
        // バースト限定ダメージ：バースト中でない当たりはダメージを与えない（弾き・ノックバックは EnemyCollision が担当）
        if (requireBurstToDamage && !isBursting) return;

        if (impactSpeed < damageSpeedThreshold) return;

        // 盾を持つ敵は、前方（盾側）から当てられてもダメージを受けない。
        // 反射・ノックバックは EnemyCollision（Reflect）が担当するので、ここではダメージだけ無効化する。
        if (shield != null && shield.Blocks(hitFromPosition))
        {
            shield.PlayBlockEffect(); // 盾で防いだので盾だけを白フラッシュ（本体は光らせない）
            Debug.Log($"{gameObject.name}: 盾で防御！ ダメージ無効（盾の反対側から当てる必要あり）");
            return;
        }

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
            // 致命の一撃はフラッシュせず、死亡演出（EnemyKnockback の半透明フェード明滅）に任せる。
            // ここでフラッシュするとマテリアル差し替えがフェードの複製と競合するため。
            Die(damage, hitFromPosition);
        }
        else
        {
            hitFlash?.Flash(); // 生存する被弾のみ白フラッシュ
            if (knockback != null) knockback.ApplyHitKnockback(hitFromPosition, damage);
        }
    }

    private void Die(int lastDamage, Vector3 hitFromPosition)
    {
        isDeadFlg = true; // 撃破フラグを立てる（ノックバック処理より先に立てて同フレーム参照でも拾える）
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
