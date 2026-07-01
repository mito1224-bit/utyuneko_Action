using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ボスのHP管理。
/// - バリアが有効な間は HandleHit を受けてもダメージ0（バリアが先に吸収する）。
/// - HPが phase2HpThreshold を下回ったタイミングで OnEnterPhase2 を発火し、BossController.currentPhase を 2 にする。
/// - 撃破時は OnDefeated を発火（演出/シーン遷移などはイベント側で接続）。
/// </summary>
[RequireComponent(typeof(BossController))]
public class BossHealth : MonoBehaviour
{
    [Header("HP設定")]
    public int maxHp = 30;
    private int currentHp;

    [Header("ダメージ判定")]
    [Tooltip("ダメージを与える最低スピード（Burst速度がこれ以上で有効）")]
    public float damageSpeedThreshold = 5f;

    [Tooltip("最低スピード到達時の基礎ダメージ")]
    public int baseDamage = 1;

    [Tooltip("超過スピード1ごとの追加ダメージ倍率")]
    public float speedDamageMultiplier = 0.5f;

    [Header("フェーズ閾値")]
    [Tooltip("HPがこの割合（0-1）を下回ったらフェーズ2へ")]
    [Range(0.05f, 0.95f)] public float phase2HpRatio = 0.5f;

    [Header("イベント")]
    public UnityEvent OnEnterPhase2;
    public UnityEvent OnDefeated;

    private BossController boss;
    private BossBarrier barrier;
    private bool phase2Fired = false;

    public int CurrentHp => currentHp;
    public float HpRatio => maxHp > 0 ? (float)currentHp / maxHp : 0f;

    void Awake()
    {
        boss = GetComponent<BossController>();
        barrier = GetComponent<BossBarrier>();
    }

    void Start()
    {
        currentHp = maxHp;
    }

    /// <summary>
    /// 衝突スピードからダメージを算出して適用。バリア有効時はバリアに吸収される。
    /// </summary>
    public void HandleHit(float impactSpeed)
    {
        if (impactSpeed < damageSpeedThreshold) return;

        // バリア有効中はダメージが通らない（バリアが先に潰される必要がある）
        if (barrier != null && barrier.IsActive)
        {
            Debug.Log("バリアに弾かれた！本体にはダメージなし");
            return;
        }

        float extra = impactSpeed - damageSpeedThreshold;
        int damage = baseDamage + Mathf.FloorToInt(extra * speedDamageMultiplier);
        TakeDamage(damage);
        Debug.Log($"ボスに {damage} ダメージ！（残りHP: {currentHp}/{maxHp}）");
    }

    public void TakeDamage(int damage)
    {
        if (currentHp <= 0) return;

        currentHp -= damage;

        if (!phase2Fired && HpRatio <= phase2HpRatio)
        {
            phase2Fired = true;
            boss.currentPhase = 2;
            Debug.Log("ボス：フェーズ2へ移行！");
            OnEnterPhase2?.Invoke();
        }

        if (currentHp <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("ボス撃破！");
        OnDefeated?.Invoke();
        Destroy(gameObject);
    }
}
