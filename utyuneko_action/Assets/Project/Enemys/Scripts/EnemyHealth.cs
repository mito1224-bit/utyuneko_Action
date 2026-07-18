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

    [Header("撃破演出")]
    [Tooltip("撃破時、まず白フラッシュ（HitFlash）を見せてから、終わったらアルファ点滅（死亡フェード）を始める。" +
             "白とフェードを同時に出すとマテリアルを奪い合ってピンク化するため直列に流す。" +
             "HitFlash が無い／OFF のときは即フェード（従来動作）")]
    public bool deathFlashThenBlink = true;

    [Header("撃破パーティクル")]
    [Tooltip("撃破された瞬間（Die）に敵の位置へ出すパーティクル（任意）。未設定なら何も出さない。" +
             "敵本体は消えるので親子付けせず独立生成する")]
    public GameObject deathEffectPrefab;

    [Tooltip("deathEffectPrefab を敵の向きに合わせて回転させる。OFF なら回転なし（Quaternion.identity＝カメラ正面向き想定）")]
    public bool matchEnemyRotationForDeathEffect = false;

    [Tooltip("生成したパーティクルを強制的に消すまでの秒数（保険）。" +
             "プレハブ側で自壊する場合（ParticleSystem の Stop Action=Destroy / AutoDestroy 付き）は 0 でOK")]
    public float deathEffectLifetime = 0f;

    private EnemyKnockback knockback;
    private HitFlash hitFlash;

    void Awake()
    {
        knockback = GetComponent<EnemyKnockback>();
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

        // ※盾によるダメージ無効化は物理（EnemyShield の盾コライダーが正面を覆う）で実現する。
        //   盾に当たったバーストは本体コライダーへ届かず、EnemyCollision 側で otherCollider 判定により
        //   HandleHit まで来ない。よってここでの角度ブロック判定は不要（撤去済み）。

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

        // 撃破SE（テストシーンに SoundManager が無ければスキップ）
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE(SeType.EnemyDie);

        // 撃破パーティクル（吹き飛ばし演出と同時に、死んだ瞬間の位置へ出す）
        SpawnDeathEffect();

        // 白フラッシュ → 終わってから死亡フェード（アルファ点滅）へ。
        // 白とフェードは両方マテリアルを差し替えるので、同時に出さず HitFlash 完了コールバックで直列に繋ぐ
        // （同時実行すると復帰時に破棄済みマテリアルを掴んでピンク化する）。
        if (hitFlash != null && deathFlashThenBlink)
        {
            hitFlash.Flash(() => StartDeathSequence(lastDamage, hitFromPosition));
        }
        else
        {
            StartDeathSequence(lastDamage, hitFromPosition);
        }
    }

    // 撃破された瞬間にパーティクルを生成する（敵本体は消えるので親子付けせず独立生成）
    private void SpawnDeathEffect()
    {
        if (deathEffectPrefab == null) return;

        Quaternion rot = matchEnemyRotationForDeathEffect ? transform.rotation : Quaternion.identity;
        GameObject fx = Instantiate(deathEffectPrefab, transform.position, rot);

        // 保険：プレハブが自壊しない場合に備えて任意秒で消す（0 なら何もしない＝プレハブ任せ）
        if (deathEffectLifetime > 0f) Destroy(fx, deathEffectLifetime);
    }

    // 死亡フェード＋吹き飛びを開始する（HitFlash が無い/OFF なら即時、有りなら白フラッシュ完了後に呼ばれる）
    private void StartDeathSequence(int lastDamage, Vector3 hitFromPosition)
    {
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
