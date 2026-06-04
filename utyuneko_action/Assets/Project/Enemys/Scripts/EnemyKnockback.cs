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
///   - Renderer を点滅させる
///   - 床・壁（groundLayers）にぶつかったら消滅
///   - 床に当たらず飛び続けた場合の保険として deathDestroyDelay 秒後にも消滅
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

    [Tooltip("床に当たらず飛び続けた場合の保険として、死亡してから消滅するまでの最大秒数")]
    public float deathDestroyDelay = 1.5f;

    [Tooltip("死亡時に無効化するスクリプト（EnemyMovement, EnemyAttack など）")]
    public MonoBehaviour[] disableOnDeath;

    [Header("点滅演出")]
    [Tooltip("死亡中に Renderer を点滅させる")]
    public bool blinkOnDeath = true;

    [Tooltip("点滅の1回あたりの間隔（秒）。小さいほど速く点滅する")]
    public float blinkInterval = 0.08f;

    [Header("床ヒットで消滅")]
    [Tooltip("死亡中に着地（床・壁ヒット）したら消滅させる")]
    public bool destroyOnGroundHit = true;

    [Tooltip("床・壁とみなすレイヤー（デフォルトは全レイヤー。プレイヤータグは常に除外）")]
    public LayerMask groundLayers = ~0;

    [Tooltip("無視する対象のタグ（プレイヤー）。このタグとの衝突では消えない")]
    public string playerTag = "Player";

    [Tooltip("着地してから消滅するまでの猶予秒数（着地後も少し点滅を見せたいとき用）")]
    public float lingerAfterLanding = 0.5f;

    private Vector3 currentVelocity = Vector3.zero;
    private bool isDying = false;
    private bool active = false;
    private bool hasLanded = false;

    private Renderer[] renderers;
    private float blinkTimer = 0f;
    private bool blinkVisible = true;

    public bool IsDying => isDying;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
    }

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
    /// 死亡時の吹き飛びを適用する。動作スクリプトを止め、点滅させ、床ヒットまたは保険時間で消滅させる。
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

        // 床に当たらず飛び続けた場合の保険。着地時はそちらが先に消滅させる。
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
        if (isDying && blinkOnDeath) UpdateBlink();

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

    /// <summary>
    /// Renderer の表示/非表示を blinkInterval ごとに切り替えて点滅させる。
    /// </summary>
    private void UpdateBlink()
    {
        if (renderers == null || renderers.Length == 0) return;

        blinkTimer += Time.deltaTime;
        if (blinkTimer < blinkInterval) return;

        blinkTimer = 0f;
        blinkVisible = !blinkVisible;
        foreach (var r in renderers)
        {
            if (r != null) r.enabled = blinkVisible;
        }
    }

    /// <summary>
    /// 死亡中に床・壁へぶつかったら消滅させる。
    /// transform で動かしているが、非キネマティック Rigidbody があるため衝突イベントは発火する。
    /// </summary>
    void OnCollisionEnter(Collision collision)
    {
        if (!isDying || !destroyOnGroundHit || hasLanded) return;

        GameObject other = collision.gameObject;

        // プレイヤーとの衝突では消えない
        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag)) return;

        // groundLayers に含まれるレイヤーのみ着地とみなす
        if ((groundLayers.value & (1 << other.layer)) == 0) return;

        hasLanded = true;
        active = false;
        currentVelocity = Vector3.zero;

        Destroy(gameObject, lingerAfterLanding);
    }
}
