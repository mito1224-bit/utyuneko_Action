using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ボスのバリア。
/// - 有効中は BossHealth が受けるダメージを吸収（HandleHit内でチェックされる）。
/// - 反射された ReflectableBullet が当たると ApplyDamage() でHPを削り、0になったら Break()。
/// - 破壊後 regenDelay 秒で自動的に Restore() で再生（HPは最大値に戻る）。
/// - 視覚表現は子オブジェクト barrierVisual の SetActive で切替。
/// </summary>
public class BossBarrier : MonoBehaviour
{
    [Header("バリア設定")]
    [Tooltip("バリアの最大HP。これだけ反射弾を当てるとバリア破壊")]
    public int maxBarrierHp = 3;

    [Tooltip("破壊されてから自動再生するまでの秒数。0以下なら再生しない")]
    public float regenDelay = 30f;

    [Header("表示用")]
    [Tooltip("バリアの見た目（Sphereなど）。あれば有効/無効に合わせて SetActive される")]
    public GameObject barrierVisual;

    [Header("イベント")]
    public UnityEvent OnBroken;
    public UnityEvent OnRestored;
    public UnityEvent OnDamaged;

    public bool IsActive { get; private set; } = true;
    public int CurrentBarrierHp { get; private set; }

    private float regenTimer = 0f;

    void Start()
    {
        CurrentBarrierHp = maxBarrierHp;
        ApplyVisual();
    }

    void Update()
    {
        if (IsActive) return;
        if (regenDelay <= 0f) return;

        regenTimer += Time.deltaTime;
        if (regenTimer >= regenDelay)
        {
            Restore();
        }
    }

    /// <summary>
    /// バリアにダメージを与える。HPが0以下になったら Break() を呼ぶ。
    /// 反射された ReflectableBullet がボス本体に当たったときに呼ばれる。
    /// </summary>
    public void ApplyDamage(int damage)
    {
        if (!IsActive) return;
        if (damage <= 0) return;

        CurrentBarrierHp -= damage;
        Debug.Log($"バリアにダメージ！ 残り: {CurrentBarrierHp}/{maxBarrierHp}");
        OnDamaged?.Invoke();

        if (CurrentBarrierHp <= 0)
        {
            Break();
        }
    }

    /// <summary>
    /// バリアを即時破壊する（HPを0にする）。
    /// </summary>
    public void Break()
    {
        if (!IsActive) return;
        IsActive = false;
        CurrentBarrierHp = 0;
        regenTimer = 0f;
        Debug.Log("バリア破壊！本体にダメージが通る状態になった");
        ApplyVisual();
        OnBroken?.Invoke();
    }

    public void Restore()
    {
        if (IsActive) return;
        IsActive = true;
        CurrentBarrierHp = maxBarrierHp;
        regenTimer = 0f;
        Debug.Log("バリア再生！");
        ApplyVisual();
        OnRestored?.Invoke();
    }

    private void ApplyVisual()
    {
        if (barrierVisual != null) barrierVisual.SetActive(IsActive);
    }
}
