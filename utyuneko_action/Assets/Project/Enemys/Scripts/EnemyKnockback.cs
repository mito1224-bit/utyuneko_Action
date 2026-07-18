using UnityEngine;
using System.Collections.Generic;

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
///   - keepInCameraOnDeath が有効なら、画面外へ飛ばさずカメラ範囲でクランプ＆反射して画面内で吹っ飛ぶ
///   - decelerateOnDeath が OFF（既定）なら死亡吹き飛び中は減速しない（一定速度で飛び続ける）
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

    [Tooltip("死亡吹き飛び中も速度を減衰させる。OFF＝一定速度で飛び続ける（カメラ内で跳ね回らせたいとき用）")]
    public bool decelerateOnDeath = false;

    [Tooltip("死亡時に適用される下向き重力")]
    public float deathGravity = 25f;

    [Header("死亡時にカメラ内へ留める")]
    [Tooltip("死亡吹き飛び中、画面外へ飛んでいかないようカメラ表示範囲でクランプ＆反射する")]
    public bool keepInCameraOnDeath = true;

    [Tooltip("カメラ端からの余白（ビューポート比率。0＝端ぴったり / 0.05＝少し内側で跳ねる）")]
    [Range(0f, 0.4f)] public float cameraMargin = 0.03f;

    [Tooltip("カメラ端で跳ね返るときの速度保持率（1＝減速なしで跳ね返る）")]
    [Range(0f, 1f)] public float cameraBounceFactor = 1f;

    [Header("死亡時の挙動")]
    [Tooltip("通常ダメージ時に対する死亡時の力の倍率")]
    public float deathMultiplier = 2.5f;

    [Tooltip("床に当たらず飛び続けた場合の保険として、死亡してから消滅するまでの最大秒数")]
    public float deathDestroyDelay = 1.5f;

    [Tooltip("死亡時に無効化するスクリプト（EnemyMovement, EnemyAttack など）")]
    public MonoBehaviour[] disableOnDeath;

    [Header("明滅演出（半透明フェード）")]
    [Tooltip("死亡中にモデルを半透明フェードで明滅させる")]
    public bool blinkOnDeath = true;

    [Tooltip("明滅の1往復（不透明→半透明→不透明）にかける時間の目安（秒）。小さいほど速い")]
    public float blinkInterval = 0.12f;

    [Tooltip("明滅で最も薄くなるときのアルファ（0=完全透明 / 1=不透明のまま）")]
    [Range(0f, 1f)] public float blinkMinAlpha = 0.3f;

    [Tooltip("死亡（撃破）した瞬間にモデルのアウトラインを消す。" +
             "アウトラインは RenderingLayerMask で対象を絞っているため、" +
             "死亡モデルのレンダラーを既定レンダリングレイヤーへ戻してフィルタから外す")]
    public bool hideOutlineOnDeath = true;

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

    [Header("消滅パーティクル（スモーク等）")]
    [Tooltip("死亡した敵が実際に消滅する瞬間・その地点に出すパーティクル（任意）。未設定なら何も出さない。" +
             "着地消滅・保険タイマー消滅のどちらの経路でも、消えた位置に出る（親子付けせず独立生成）")]
    public GameObject despawnEffectPrefab;

    [Tooltip("despawnEffectPrefab を敵の向きに合わせて回転させる。OFF なら回転なし（Quaternion.identity）")]
    public bool matchRotationForDespawnEffect = false;

    [Tooltip("生成したパーティクルを強制的に消すまでの秒数（保険）。" +
             "プレハブ側で自壊する場合（ParticleSystem の Stop Action=Destroy / AutoDestroy 付き）は 0 でOK")]
    public float despawnEffectLifetime = 0f;

    private Vector3 currentVelocity = Vector3.zero;
    private bool isDying = false;
    private bool active = false;
    private bool hasLanded = false;
    private int bounceCount = 0;
    private int lastGroundHitFrame = -1;

    private Renderer[] renderers;
    private BlinkFade blinkFade;   // 半透明フェードの明滅
    private float blinkPhase = 0f; // 明滅の位相（累積）

    private Camera cam; // カメラ内クランプ用（死亡時のみ使用）

    public bool IsDying => isDying;

    /// <summary>
    /// 現在吹き飛び中（速度が残っていて transform を動かしている）かどうか。
    /// EnemyMovement が巡回を一時停止し、収まったら元位置へ戻る判断に使う。
    /// </summary>
    public bool IsActive => active;

    /// <summary>
    /// 死亡吹き飛び中に床・壁へぶつかった瞬間に発火する（引数＝接触面の法線）。
    /// 購読側でバウンド/消滅とは独立に処理を差し込める（例: EnemyBomber の「壁ヒットで即爆発」）。
    /// このイベントは isDying 中の HandleGroundHit からのみ呼ばれる。
    /// </summary>
    public event System.Action<Vector2> OnDeathGroundHit;

    void Awake()
    {
        renderers = CollectModelRenderers();
    }

    // 点滅対象のモデル用レンダラーを集める（Mesh/Skinned/Sprite）。
    // 視線ライン（LineRenderer）や軌跡（TrailRenderer）は点滅で復活すると困るので除外する。
    private Renderer[] CollectModelRenderers()
    {
        var all = GetComponentsInChildren<Renderer>(true);
        var list = new List<Renderer>(all.Length);
        foreach (var r in all)
        {
            if (r == null) continue;
            if (r is LineRenderer || r is TrailRenderer) continue;
            list.Add(r);
        }
        return list.ToArray();
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

        // 進行中の被弾フラッシュ（HitFlash）があればマテリアルを元へ戻してから死亡フェードに入る
        // （両者ともマテリアルを差し替えるので競合を防ぐ）。
        GetComponent<HitFlash>()?.StopAndRestore();

        // 死亡時点の最新モデルを明滅対象に取り直す（実行時に組み替え／生成されたモデル部位も確実に含める）。
        // これで「モデルの一部が明滅しないまま」になるのを防ぐ。
        renderers = CollectModelRenderers();

        // 死亡時はアウトラインを消す（明滅の有無に関わらず）。
        // アウトラインは RenderingLayerMask フィルタなので、モデルを既定レイヤーへ戻して対象外にする。
        if (hideOutlineOnDeath) BlinkFade.HideOutline(renderers);

        if (blinkOnDeath)
        {
            blinkFade = new BlinkFade(renderers);
            blinkFade.Begin(); // マテリアルを透明対応の複製へ差し替え
        }

        if (disableOnDeath != null)
        {
            foreach (var mb in disableOnDeath)
            {
                if (mb != null) mb.enabled = false;
            }
        }

        // 死亡吹き飛び中に触れてもダメージを受けないよう、接触ダメージを自動で無効化する。
        // PlayerHealth 側が source.enabled を見ているので、disableOnDeath への手動登録漏れがあっても効く。
        foreach (var ds in GetComponentsInChildren<DamageSource>(true))
        {
            ds.enabled = false;
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

        // 死亡時は画面外へ飛ばさないようカメラ範囲でクランプ＆反射する
        if (isDying && keepInCameraOnDeath) ClampToCamera();

        // 減衰。死亡時は decelerateOnDeath が OFF なら減速させない（一定速度で飛び続ける）
        if (!isDying || decelerateOnDeath)
        {
            currentVelocity *= Mathf.Pow(decayPerSecond, Time.deltaTime);
        }

        if (!isDying && currentVelocity.sqrMagnitude < 0.04f)
        {
            currentVelocity = Vector3.zero;
            active = false;
        }
    }

    /// <summary>
    /// 死亡吹き飛び中、カメラの表示範囲外へ出ないよう位置をクランプし、端で速度を反射させる。
    /// これにより敵は画面内で吹っ飛んで（跳ね回って）から消滅する。
    /// </summary>
    private void ClampToCamera()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 vp = cam.WorldToViewportPoint(transform.position);
        // カメラ後方（vp.z<0）はクランプ計算が破綻するので何もしない
        if (vp.z <= 0f) return;

        float min = cameraMargin;
        float max = 1f - cameraMargin;
        bool hitX = false, hitY = false;

        if (vp.x < min) { vp.x = min; hitX = true; }
        else if (vp.x > max) { vp.x = max; hitX = true; }

        if (vp.y < min) { vp.y = min; hitY = true; }
        else if (vp.y > max) { vp.y = max; hitY = true; }

        if (!hitX && !hitY) return;

        Vector3 clamped = cam.ViewportToWorldPoint(vp);
        clamped.z = transform.position.z; // Z平面は維持
        transform.position = clamped;

        // ぶつかった軸だけ速度を反転（端で跳ね返る）。cameraBounceFactor=1 なら減速なし
        if (hitX) currentVelocity.x = -currentVelocity.x * cameraBounceFactor;
        if (hitY) currentVelocity.y = -currentVelocity.y * cameraBounceFactor;
    }

    /// <summary>
    /// モデルのアルファを不透明↔半透明で脈動させて明滅させる（ハードな点滅ではなく半透明フェード）。
    /// アルファ制御なので Animator の m_Enabled 上書きの影響を受けず、Animator 付きモデルでも確実に効く。
    /// </summary>
    private void UpdateBlink()
    {
        if (blinkFade == null || !blinkFade.IsActive) return;

        // blinkInterval を1往復の目安時間として位相を進める（cos で 1→min→1 と滑らかに脈動）
        float speed = Mathf.PI * 2f / Mathf.Max(0.0001f, blinkInterval);
        blinkPhase += Time.deltaTime * speed;

        float t = Mathf.Cos(blinkPhase) * 0.5f + 0.5f; // 0..1
        float alpha = Mathf.Lerp(blinkMinAlpha, 1f, t);
        blinkFade.SetAlpha(alpha);
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

        // 死亡吹き飛び中の壁・床ヒットを購読側へ通知（EnemyBomber の壁ヒット即爆発など。バウンド/消滅とは独立）
        OnDeathGroundHit?.Invoke(normal);

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

    /// <summary>
    /// 敵が実際に破棄される瞬間に、消滅地点へスモーク等のパーティクルを出す。
    /// 着地消滅（lingerAfterLanding）・保険タイマー消滅（deathDestroyDelay）のどちらの経路でも
    /// この一点で発火するため、消えた位置に確実に一致する。敵本体は消えるので親子付けせず独立生成する。
    /// </summary>
    void OnDestroy()
    {
        // 死亡消滅のときだけ出す（生存中に別要因で破棄された場合は出さない）
        if (!isDying) return;
        if (despawnEffectPrefab == null) return;
        // シーン遷移・アプリ終了によるアンロード時の破棄では出さない（実際の撃破消滅のみ）
        if (!gameObject.scene.isLoaded) return;

        Quaternion rot = matchRotationForDespawnEffect ? transform.rotation : Quaternion.identity;
        GameObject fx = Instantiate(despawnEffectPrefab, transform.position, rot);

        // 保険：プレハブが自壊しない場合に備えて任意秒で消す（0 なら何もしない＝プレハブ任せ）
        if (despawnEffectLifetime > 0f) Destroy(fx, despawnEffectLifetime);
    }
}
