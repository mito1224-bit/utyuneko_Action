using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ボススナイパーのHP・ダメージ・無敵時間を管理する専用コンポーネント。
/// コントローラ（BossSniper）から分離し、被弾の「数値まわり」だけをここに集約する。
///
/// 設計:
///   - HPはフェーズごと（BossSniper.PhaseSettings.phaseMaxHP）。満タンから始まり、
///     削り切ったら BossSniper.AdvancePhaseByHP() を呼んで次フェーズへ。最終フェーズで
///     削り切ると撃破（BossSniper.DefeatByHP()）。フェーズ進行のトリガーはHPゼロのみ。
///   - ダメージ計算は速度依存: basePlayerDamage + プレイヤー速度 × playerSpeedDamageMultiplier。
///   - 通常時は damageInterval の無敵時間で連続ヒットを抑制。
///   - スタン中は倍率（Phase.stunDamageMultiplier）を掛けた一撃が「1回だけ」通る。
///     一撃が通ったら stunConsumed が立ち、同じスタン中はそれ以上ダメージを受けない。
///     （復帰までの間の置き＝stunRecoverDelay は BossSniper 側のスタン処理で扱う）
///
/// 呼び出し口:
///   各ステートの被弾判定（本物へバースト体当たり）で TryApplyBurstDamage(unit, pc, isStunned) を呼ぶ。
///   戻り値は「ダメージが実際に入ったか」。スタンの一撃判定にも使える。
///
/// HPバー等への通知はすべて UnityEvent。表示方法（現フェーズHPだけを映す等）は購読側で決める。
/// </summary>
public class BossSniperHealth : MonoBehaviour
{
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
    [Tooltip("現在HPが変わった（引数: 現在HP, 現フェーズ最大HP）。現フェーズHPだけを映すHPバーはこれを購読")]
    public UnityEvent<float, float> onHPChanged = new UnityEvent<float, float>();

    [Tooltip("ダメージを受けた（引数: 与ダメージ量）。被弾フラッシュ・シェイク等に")]
    public UnityEvent<float> onDamaged = new UnityEvent<float>();

    [Tooltip("フェーズが切り替わった（引数: 新フェーズ番号 0始まり, その最大HP）。切替演出に")]
    public UnityEvent<int, float> onPhaseChanged = new UnityEvent<int, float>();

    /// <summary>現フェーズの残りHP。</summary>
    public float CurrentHP { get; private set; }

    /// <summary>現フェーズの最大HP。</summary>
    public float MaxHP { get; private set; }

    private BossSniper boss;
    private BossSniperFlash flash;
    private BossSniperHitStop hitStop;
    private float invincibilityTimer;
    private bool stunConsumed; // 現在のスタンで既に一撃を消費したか

    void Awake()
    {
        boss = GetComponent<BossSniper>();
        if (boss == null) boss = GetComponentInParent<BossSniper>();
        flash = GetComponent<BossSniperFlash>();
        hitStop = GetComponent<BossSniperHitStop>();
    }

    void Update()
    {
        if (invincibilityTimer > 0f) invincibilityTimer -= Time.deltaTime;
    }

    /// <summary>フェーズ開始時に現在HPを満タンへ。BossSniper から呼ばれる。</summary>
    public void InitPhase(float maxHP)
    {
        MaxHP = Mathf.Max(1f, maxHP);
        CurrentHP = MaxHP;
        stunConsumed = false;
        invincibilityTimer = 0f;
        onHPChanged?.Invoke(CurrentHP, MaxHP);
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
        if (isStunned)
        {
            // スタン中：無敵時間は無視するが、そのスタンで1回だけ
            if (stunConsumed) return false;
            stunConsumed = true;

            float dmg = ComputeBaseDamage(pc) * Mathf.Max(1f, boss.Phase.stunDamageMultiplier);
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

        // プレイヤーのバースト回数を回復（既存仕様）
        if (refundPlayerBurstOnDamage && pc != null) pc.OnEnemyKilledInBurst();

        CurrentHP -= damage;
        onDamaged?.Invoke(damage);
        if (autoFlashOnDamage && flash != null) flash.Flash(); // 本物だけがここを通る＝本物だけ光る

        if (CurrentHP <= 0f)
        {
            CurrentHP = 0f;
            onHPChanged?.Invoke(CurrentHP, MaxHP);

            // フェーズHPを削り切った → 次フェーズ or 撃破（判断は BossSniper に委ねる）
            if (boss.IsFinalPhase)
            {
                if (hitStop != null) hitStop.PlayDefeat(); // 撃破：全体スロー（長め）＋強いシェイク
                boss.DefeatByHP();
            }
            else
            {
                // フェーズ最後の一撃：スタン由来なら手応えを出す、通常ならボスだけ軽く止める
                TriggerHitStop(unit, fromStun);
                boss.AdvancePhaseByHP();
            }
            return;
        }

        onHPChanged?.Invoke(CurrentHP, MaxHP);

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