using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ボススナイパーのHP・ダメージ・無敵時間を管理する専用コンポーネント。
/// コントローラ（BossSniper）から分離し、被弾の「数値まわり」だけをここに集約する。
///
/// 設計（フェーズ制は廃止・単一HP）:
///   - HPは maxHP の1本。削り切ったら BossSniper.DefeatByHP() で撃破。
///   - HPが enragedThresholdRatio（既定0.5＝半分）以下になると「強化モード」（IsEnraged）。
///     難易度セットの切り替え（BossSniper.Difficulty）とお供分身の出現条件に使われる。
///     初めて下回った瞬間に onEnraged と BossSniper.NotifyEnraged() を1回だけ発火する。
///   - ダメージ計算は速度依存: basePlayerDamage + プレイヤー速度 × playerSpeedDamageMultiplier。
///   - 通常時は damageInterval の無敵時間で連続ヒットを抑制。
///   - スタン中は倍率（Difficulty.stunDamageMultiplier）を掛けた一撃が「1回だけ」通る。
///     一撃が通ったら stunConsumed が立ち、同じスタン中はそれ以上ダメージを受けない。
///
/// 呼び出し口:
///   各ステートの被弾判定（本物へバースト体当たり）で TryApplyBurstDamage(unit, pc, isStunned) を呼ぶ。
///   戻り値は「ダメージが実際に入ったか」。
///
/// HPバー等への通知はすべて UnityEvent。
/// </summary>
public class BossSniperHealth : MonoBehaviour
{
    [Header("基礎ステータス")]
    [Tooltip("ボスの最大HP（1本のプール）。削り切ったら撃破")]
    public float maxHP = 400f;

    [Tooltip("強化モードに入るHP比率。0.5なら半分以下で強化（難易度セット切替＋お供分身が出現）")]
    [Range(0f, 1f)] public float enragedThresholdRatio = 0.5f;

    [Header("被弾ダメージ（速度依存）")]
    [Tooltip("プレイヤーのバースト体当たりの基礎ダメージ")]
    public float basePlayerDamage = 8f;

    [Tooltip("プレイヤーの速度に掛けてダメージへ加算する係数（速いほど痛い）")]
    public float playerSpeedDamageMultiplier = 0.4f;

    [Header("無敵時間")]
    [Tooltip("被弾後の無敵時間。連続ヒットを抑制する。スタン中の一撃には別枠（stunConsumed）で対応")]
    public float damageInterval = 0.5f;

    [Header("プレイヤーへの還元")]
    [Tooltip("ダメージを与えたとき PlayerController.OnEnemyKilledInBurst を呼んでバースト回数を回復させるか")]
    public bool refundPlayerBurstOnDamage = true;

    [Header("白フラッシュ連携")]
    [Tooltip("同じ GameObject の BossSniperFlash を被弾時に自動で光らせる。onDamaged へ手動配線している場合は二重発火を避けるためオフに")]
    public bool autoFlashOnDamage = true;

    [Header("イベント（HPバー・SE・エフェクト接続用）")]
    [Tooltip("現在HPが変わった（引数: 現在HP, 最大HP）")]
    public UnityEvent<float, float> onHPChanged = new UnityEvent<float, float>();

    [Tooltip("ダメージを受けた（引数: 与ダメージ量）。被弾フラッシュ・シェイク等に")]
    public UnityEvent<float> onDamaged = new UnityEvent<float>();

    [Tooltip("強化モードに入った（HPがしきい値を下回った瞬間・1回だけ）。演出の切替などに")]
    public UnityEvent onEnraged = new UnityEvent();

    /// <summary>現在の残りHP。</summary>
    public float CurrentHP { get; private set; }

    /// <summary>最大HP（HPバー互換用のプロパティ）。</summary>
    public float MaxHP => maxHP;

    /// <summary>強化モード（HPがしきい値以下）か。</summary>
    public bool IsEnraged => CurrentHP <= maxHP * enragedThresholdRatio;

    /// <summary>
    /// ボスが被ダメージ後の無敵時間中か。
    /// この間はボスが連続ダメージを受けないだけでなく、
    /// 本物ボスへの非バースト接触ダメージも無効化する。
    /// </summary>
    public bool IsInvincible => invincibilityTimer > 0f;

    private BossSniper boss;
    private BossSniperFlash flash;
    private BossSniperHitStop hitStop;
    private float invincibilityTimer;
    private bool stunConsumed;   // 現在のスタンで既に一撃を消費したか
    private bool enragedNotified; // onEnraged を発火済みか（1回だけ）

    void Awake()
    {
        boss = GetComponent<BossSniper>();
        if (boss == null) boss = GetComponentInParent<BossSniper>();
        flash = GetComponent<BossSniperFlash>();
        hitStop = GetComponent<BossSniperHitStop>();

        CurrentHP = Mathf.Max(1f, maxHP);
    }

    void Start()
    {
        onHPChanged?.Invoke(CurrentHP, maxHP); // HPバーへ初期値を通知
    }

    void Update()
    {
        if (invincibilityTimer > 0f) invincibilityTimer -= Time.deltaTime;
    }

    /// <summary>スタンに入るときに呼ぶ。スタンの「一撃」枠をリセットする。</summary>
    public void ResetStunHit()
    {
        stunConsumed = false;
    }

    /// <summary>
    /// 本物へのバースト体当たりを受けてダメージを試みる。実際に入ったら true。
    /// isStunned=true のときはスタン倍率が乗る一撃（そのスタン中1回だけ）。
    /// </summary>
    public bool TryApplyBurstDamage(BossSniperBeamUnit unit, PlayerController pc, bool isStunned)
    {
        if (CurrentHP <= 0f) return false; // 既に撃破済み。二重撃破・onDefeated の多重発火を防ぐ

        if (isStunned)
        {
            // スタン中：無敵時間は無視するが、そのスタンで1回だけ
            if (stunConsumed) return false;
            stunConsumed = true;

            float dmg = ComputeBaseDamage(pc) * Mathf.Max(1f, boss.Difficulty.stunDamageMultiplier);
            ApplyDamage(dmg, unit, pc, fromStun: true);
            return true;
        }
        else
        {
            // 通常時：無敵時間で連続ヒットを抑制
            if (invincibilityTimer > 0f) return false;
            invincibilityTimer = damageInterval;

            float dmg = ComputeBaseDamage(pc);
            ApplyDamage(dmg, unit, pc, fromStun: false);
            return true;
        }
    }

    private float ComputeBaseDamage(PlayerController pc)
    {
        float speed = (pc != null && pc.rb2D != null) ? pc.rb2D.linearVelocity.magnitude : 0f;
        return basePlayerDamage + speed * playerSpeedDamageMultiplier;
    }

    private void ApplyDamage(float damage, BossSniperBeamUnit unit, PlayerController pc, bool fromStun)
    {
        if (damage <= 0f) return;
        if (CurrentHP <= 0f) return; // 既に撃破済み

        // プレイヤーのバースト回数を回復（既存仕様）
        if (refundPlayerBurstOnDamage && pc != null) pc.OnEnemyKilledInBurst();

        CurrentHP -= damage;

        // ダメージが実際に入った直後は、ボスの無敵時間を開始 / 延長する。
        // 通常ダメージでは TryApplyBurstDamage 側ですでに設定されているが、
        // スタン中の倍率一撃などでも「被弾直後の接触ダメージ無効」を効かせるため、ここでも保証する。
        invincibilityTimer = Mathf.Max(invincibilityTimer, damageInterval);

        onDamaged?.Invoke(damage);
        if (autoFlashOnDamage && flash != null) flash.Flash(); // 本物だけがここを通る＝本物だけ光る

        // 撃破判定
        if (CurrentHP <= 0f)
        {
            CurrentHP = 0f;
            onHPChanged?.Invoke(CurrentHP, maxHP);

            if (hitStop != null) hitStop.PlayDefeat(); // 撃破：全体スロー（長め）＋強いシェイク
            boss.DefeatByHP();
            return;
        }

        onHPChanged?.Invoke(CurrentHP, maxHP);

        // 強化モードへの移行判定（初めて下回った瞬間に1回だけ通知）
        if (!enragedNotified && IsEnraged)
        {
            enragedNotified = true;
            onEnraged?.Invoke();
            boss.NotifyEnraged();
        }

        TriggerHitStop(unit, fromStun);
    }

    // ダメージ種別に応じてヒットストップ＋シェイクを呼び分ける（撃破は呼び出し側で処理済み）
    private void TriggerHitStop(BossSniperBeamUnit unit, bool fromStun)
    {
        if (hitStop == null) return;
        if (fromStun)
        {
            hitStop.PlayStun(); // スタン一撃：全体スロー＋全方向シェイク
        }
        else
        {
            // 通常：ボスだけフリーズ＋「殴られた方向」の軸±シェイク
            Vector2 dir = unit != null ? unit.LastHitDirection : Vector2.right;
            hitStop.PlayNormal(dir);
        }
    }
}