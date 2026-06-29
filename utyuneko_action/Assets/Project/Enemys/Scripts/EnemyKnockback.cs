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
///   - 床・壁（groundLayers）にぶつかったら、bounceOnDeath が有効なら数回バウンドしてから消滅
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

    [Header("床バウンド（死亡時）")]
    [Tooltip("死亡中に床・壁へぶつかったとき、消滅する前に少しバウンドさせる")]
    public bool bounceOnDeath = true;

    [Tooltip("消滅するまでにバウンドできる最大回数")]
    public int maxBounceCount = 2;

    [Tooltip("1回のバウンドで保持する速度の割合（反発係数）。0=跳ねない / 1=減衰なし")]
    [Range(0f, 1f)] public float bounceFactor = 0.45f;

    [Tooltip("バウンド時に水平方向の速度へ掛ける係数（1=減衰なし、小さいほど横移動が止まる）")]
    [Range(0f, 1f)] public float bounceHorizontalKeep = 0.8f;

    [Tooltip("この速度未満で着地した場合はバウンドせず、そのまま着地扱いにする")]
    public float minBounceSpeed = 2f;

    [Tooltip("バウンド時に接触面からめり込まないよう押し出す距離")]
    public float bouncePushOut = 0.02f;

    private Vector3 currentVelocity = Vector3.zero;
    private bool isDying = false;
    private bool active = false;
    private bool hasLanded = false;
    private int bounceCount = 0;
    private int lastGroundHitFrame = -1;

    private Renderer[] renderers;
    private float blinkTimer = 0f;
    private bool blinkVisible = true;

    public bool IsDying => isDying;

    /// <summary>
    /// 現在吹き飛び中（速度が残っていて transform を動かしている）かどうか。
    /// EnemyMovement が巡回を一時停止し、収まったら元位置へ戻る判断に使う。
    /// </summary>
    public bool IsActive => active;

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
        // 上から殴られたとき真下へ飛ばすと、transform 移動のため床・壁をすり抜けて落下していく。
        // 下向き成分は消して水平のみ残し、必ず upwardBlend の上向きが効くようにする。
       // if (dir.y < 0f) dir.y = 0f;
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
    /// 死亡中に床・壁へぶつかったときの処理（Reflect など非トリガーのエネミー用）。
    /// 接触点から正確な法線が取れる。
    /// transform で動かしているが、Dynamic な Rigidbody2D があるため衝突イベントは発火する。
    /// </summary>
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!ShouldHandleGroundHit(collision.gameObject)) return;

        // 接触面の法線（床なら上向き）。複数接点があれば先頭を使う。
        Vector2 normal = collision.contactCount > 0 ? collision.GetContact(0).normal : Vector2.up;
        HandleGroundHit(normal);
    }

    /// <summary>
    /// Pierce タイプなど isTrigger=true のエネミーは衝突イベントが出ないため、
    /// トリガーで床・壁を検知する。法線は相手コライダー上の最近接点から推定する。
    /// </summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!ShouldHandleGroundHit(other.gameObject)) return;

        HandleGroundHit(EstimateNormalFrom(other));
    }

    // 床・壁ヒットとして処理すべきか（死亡中／対象レイヤー／プレイヤー除外）の共通判定
    private bool ShouldHandleGroundHit(GameObject other)
    {
        if (!isDying || hasLanded) return false;
        // 着地消滅もバウンドも無効なら、床ヒットには反応しない（保険タイマーに任せる）
        if (!destroyOnGroundHit && !bounceOnDeath) return false;
        // プレイヤーとの接触では消えない／跳ねない
        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag)) return false;
        // groundLayers に含まれるレイヤーのみ床・壁とみなす
        if ((groundLayers.value & (1 << other.layer)) == 0) return false;
        return true;
    }

    // トリガーには接触点が無いので、相手コライダー上の最近接点から法線を推定する
    private Vector2 EstimateNormalFrom(Collider2D other)
    {
        Vector2 closest = other.ClosestPoint(transform.position);
        Vector2 n = (Vector2)transform.position - closest;

        // 重なって最近接点が中心と一致する場合は、進行方向の逆を法線とする
        if (n.sqrMagnitude < 1e-4f) n = -(Vector2)currentVelocity;
        if (n.sqrMagnitude < 1e-4f) n = Vector2.up;
        return n.normalized;
    }

    // バウンド or 着地消滅を行う共通処理
    private void HandleGroundHit(Vector2 normal)
    {
        // 同フレーム内に衝突(Body)とトリガー(ダメージ判定)の両方から呼ばれても1回だけ処理する
        if (Time.frameCount == lastGroundHitFrame) return;
        lastGroundHitFrame = Time.frameCount;

        // バウンド条件：有効＆残り回数あり＆十分な速度で当たっている
        if (bounceOnDeath && bounceCount < maxBounceCount && currentVelocity.magnitude >= minBounceSpeed)
        {
            bounceCount++;

            // 接触面で反射し、反発係数で減衰。水平成分はさらに別途減衰させる。
            Vector2 reflected = Vector2.Reflect((Vector2)currentVelocity, normal) * bounceFactor;
            reflected.x *= bounceHorizontalKeep;
            currentVelocity = new Vector3(reflected.x, reflected.y, 0f);

            // 接触面にめり込んだまま再ヒットしないよう少し押し出す
            transform.position += (Vector3)(normal * bouncePushOut);
            return; // まだ消滅しない
        }

        // バウンド終了（回数切れ or 失速）→ 着地して消滅
        if (!destroyOnGroundHit) return;

        hasLanded = true;
        active = false;
        currentVelocity = Vector3.zero;

        Destroy(gameObject, lingerAfterLanding);
    }
}
