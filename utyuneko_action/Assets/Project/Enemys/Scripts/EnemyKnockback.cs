using UnityEngine;

/// <summary>
/// エネミーの吹き飛び挙動。
/// EnemyHealth から ApplyHitKnockback / ApplyDeathKnockback で呼ばれる。
/// Rigidbody を使わず transform で manual に動かすので、既存の EnemyMovement と物理が衝突しない。
/// XY平面で動作（Z軸は常に0）。
///
/// 死亡時:
///   - 通常の吹き飛びより deathMultiplier 倍の力で飛ぶ
///   - disableOnDeath にセットされた MonoBehaviour（EnemyMovement, EnemyAttack 等）を無効化
///   - deathDestroyDelay 秒後にオブジェクト消滅
/// </summary>
public class EnemyKnockback : MonoBehaviour
{
    [Header("吹き飛び基本設定")]
    [Tooltip("ダメージ1あたりの吹き飛び初速")]
    public float forcePerDamage = 3f;

    [Tooltip("最低吹き飛び速度（ダメージが小さくても最低限飛ぶ）")]
    public float minForce = 4f;

    [Tooltip("最大吹き飛び速度（通常時）")]
    public float maxForce = 25f;

    [Header("方向設定")]
    [Tooltip("上方向の混ぜ具合。0=完全に水平、1=完全に真上")]
    [Range(0f, 1f)] public float upwardBlend = 0.3f;

    [Header("物理")]
    [Tooltip("毎秒あたりの速度減衰係数（1.0で減衰なし、0.5で1秒で半減）")]
    [Range(0.05f, 1f)] public float decayPerSecond = 0.4f;

    [Tooltip("死亡時に適用される下向き重力")]
    public float deathGravity = 25f;

    [Header("死亡時の挙動")]
    [Tooltip("通常ダメージ時に対する死亡時の力の倍率")]
    public float deathMultiplier = 2.5f;

    [Tooltip("死亡してから GameObject 消滅までの秒数")]
    public float deathDestroyDelay = 1.5f;

    [Tooltip("死亡時に無効化するスクリプト（EnemyMovement, EnemyAttack など）")]
    public MonoBehaviour[] disableOnDeath;

    private Vector3 currentVelocity = Vector3.zero;
    private bool isDying = false;
    private bool active = false;

    public bool IsDying => isDying;

    /// <summary>
    /// 通常被弾時の吹き飛びを適用する。
    /// fromPosition から離れる方向に飛ぶ。
    /// </summary>
    public void ApplyHitKnockback(Vector3 fromPosition, int damage)
    {
        if (isDying) return;
        currentVelocity = ComputeLaunchVelocity(fromPosition, damage, false);
        active = true;
    }

    /// <summary>
    /// 死亡時の吹き飛びを適用する。動作スクリプトを止め、一定時間後に消滅させる。
    /// </summary>
    public void ApplyDeathKnockback(Vector3 fromPosition, int damage)
    {
        if (isDying) return;
        isDying = true;

        if (disableOnDeath != null)
        {
            foreach (var mb in disableOnDeath)
            {
                if (mb != null) mb.enabled = false;
            }
        }

        currentVelocity = ComputeLaunchVelocity(fromPosition, damage, true);
        active = true;

        Destroy(gameObject, deathDestroyDelay);
    }

    private Vector3 ComputeLaunchVelocity(Vector3 fromPosition, int damage, bool isDeath)
    {
        Vector3 dir = transform.position - fromPosition;
        dir.z = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.right;
        dir = dir.normalized;

        Vector3 blended = (dir * (1f - upwardBlend) + Vector3.up * upwardBlend).normalized;

        float baseForce = Mathf.Clamp(minForce + forcePerDamage * damage, minForce, maxForce);
        if (isDeath) baseForce *= deathMultiplier;

        return blended * baseForce;
    }

    void Update()
    {
        if (!active) return;

        if (isDying)
        {
            currentVelocity.y -= deathGravity * Time.deltaTime;
        }

        Vector3 delta = currentVelocity * Time.deltaTime;
        delta.z = 0f;
        transform.position += delta;

        currentVelocity *= Mathf.Pow(decayPerSecond, Time.deltaTime);

        if (!isDying && currentVelocity.sqrMagnitude < 0.04f)
        {
            currentVelocity = Vector3.zero;
            active = false;
        }
    }
}
