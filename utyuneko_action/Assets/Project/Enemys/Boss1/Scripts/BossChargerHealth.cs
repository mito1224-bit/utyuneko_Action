using UnityEngine;

/// <summary>
/// 突進×盾ボス（Boss3）のHP管理。
/// ダメージはプレイヤーのバースト体当たりのみ（requireBurstToDamage）。速度スケーリングは EnemyHealth と同じ流儀。
/// スタン中はダメージ倍率アップ（stunDamageMultiplier）、フェーズ移行の咆哮中はダメージカット。
/// 盾によるガードは物理盾（BossChargerShield）が正面を覆うことで実現するため、ここでは角度判定はしない。
/// </summary>
public class BossChargerHealth : MonoBehaviour
{
    [Header("基礎ステータス")]
    public float maxHP = 300f;
    public float currentHP;
    public GameObject damagekEffect;

    [Header("被弾条件")]
    [Tooltip("バースト中の体当たりのみダメージを受ける")]
    public bool requireBurstToDamage = true;
    [Tooltip("この速度未満の体当たりはダメージ0（かすり当て防止）")]
    public float damageSpeedThreshold = 5f;

    [Header("ダメージ計算（速度スケーリング）")]
    public float baseDamage = 4f;
    public float speedDamageMultiplier = 0.4f;

    [Header("状態別倍率")]
    [Tooltip("スタン中の被弾ダメージ倍率（弱点タイム）")]
    public float stunDamageMultiplier = 3f;
    [Tooltip("盾投げ中の被弾ダメージ倍率（全身無防備タイム）")]
    public float throwDamageMultiplier = 1.5f;
    [Tooltip("フェーズ2移行（咆哮）中の被弾ダメージ倍率。0.2なら80%カット")]
    public float phaseTransitionDamageMultiplier = 0.2f;
    [Tooltip("必殺技（乱舞突進）中の被弾ダメージ倍率。大技の見せ場を途中で潰されないよう極端に下げる。0.1なら90%カット")]
    public float rampageDamageMultiplier = 0.1f;

    [Header("被弾インターバル")]
    [Tooltip("連続ヒットを防ぐ無敵時間（秒）")]
    public float damageInterval = 0.4f;

    [Header("被弾演出")]
    [Tooltip("被弾時の白フラッシュ。未指定なら同じ GameObject の HitFlash を自動取得（無ければ演出なし）")]
    public HitFlash hitFlash;

    [Header("UI")]
    [Tooltip("HPバー（未設定でも動く。テストシーン用）。ボス2と同じ StageSecondBossHPBar を流用する＝" +
             "赤ゲージ＋白の残像ゲージ・出現アニメ・被弾シェイク・死亡フェードがそのまま使える。\n" +
             "★出現アニメ（StartAppearAnimation）は BossChargerTrigger が叩くので、" +
             "トリガー無しで直接置くテストシーンではゲージの最大値が入らず正しく表示されない")]
    public StageSecondBossHPBar hpBar;

    public float CurrentHpRatio => maxHP > 0f ? currentHP / maxHP : 0f;
    public bool IsDead { get; private set; }

    private BossChargerController controller;
    private float invincibilityTimer;

    void Awake()
    {
        currentHP = maxHP;
        controller = GetComponent<BossChargerController>();
        if (hitFlash == null) hitFlash = GetComponent<HitFlash>(); // 白フラッシュ演出（任意）
    }

    void Update()
    {
        if (invincibilityTimer > 0f) invincibilityTimer -= Time.deltaTime;
    }

    /// <summary>
    /// 体当たりヒットの受付。BossChargerController の OnCollisionEnter2D（本体）から呼ばれる。
    /// 盾に当たった分は BossChargerShield 側で吸収されるためここへ来ない。
    /// </summary>
    public void HandleHit(float impactSpeed, bool isBursting)
    {
        if (IsDead || invincibilityTimer > 0f) return;
        if (requireBurstToDamage && !isBursting) return;
        if (impactSpeed < damageSpeedThreshold) return;

        float damage = baseDamage + impactSpeed * speedDamageMultiplier;
        damage *= CurrentStateMultiplier();

        TakeDamage(damage);
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || damage <= 0f) return;

        currentHP = Mathf.Max(0f, currentHP - damage);
        invincibilityTimer = damageInterval;
        if (damagekEffect) Instantiate(damagekEffect, controller.transform.position, Quaternion.identity);


        // ボス2（StageSecondBossHealth）と同じ流儀：HP反映＋被弾シェイク。
        // HPが0になったときのフェードアウトは UpdateHP の内部が面倒を見る。
        if (hpBar != null)
        {
            hpBar.UpdateHP(currentHP);
            hpBar.ShakeBar(0.2f, 12f);
        }

        if (currentHP <= 0f)
        {
            // トドメの一撃はフラッシュしない（EnemyHealth と同じ流儀）。
            // ここでフラッシュすると、スタン/死亡フェード（BlinkFade）の一時複製マテリアルを
            // HitFlash が「元マテリアル」として掴み、フェード終了時に複製が破棄されて
            // 破棄済みマテリアルへ復帰＝ピンク（マゼンタ）化するため。死亡演出は BlinkFade に任せる。
            IsDead = true;
            controller?.TransitionToState(controller.StateDead);
        }
        else
        {
            hitFlash?.Flash(); // 生存する被弾のみ白フラッシュ
        }
    }

    /// <summary>進行中の白フラッシュを止めてマテリアルを元へ戻す。フェード（BlinkFade）と競合させたくない直前に呼ぶ。</summary>
    public void StopFlash() => hitFlash?.StopAndRestore();

    // 現在の状態に応じたダメージ倍率
    private float CurrentStateMultiplier()
    {
        if (controller == null) return 1f;
        if (controller.CurrentState == controller.StateStun) return stunDamageMultiplier;
        if (controller.CurrentState == controller.StateShieldThrow) return throwDamageMultiplier;
        if (controller.CurrentState == controller.StatePhaseTransition) return phaseTransitionDamageMultiplier;
        if (controller.CurrentState == controller.StateRampage) return rampageDamageMultiplier;
        return 1f;
    }
}
