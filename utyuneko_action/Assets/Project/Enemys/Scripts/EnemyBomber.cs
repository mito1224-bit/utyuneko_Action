using UnityEngine;

/// <summary>
/// 自爆型の敵。プレイヤーが射程に入るとカウントダウン（導火線）が始まり、時間が経つと自分中心に爆発する。
///
/// フェーズ:
///   待機(Idle) → カウントダウン(Countdown:残り時間が減るほど点滅が加速＝危険表示)
///        → 爆発(Exploding:範囲ダメージ＋フラッシュ) → 自滅(destroyOnExplode) または待機へリセット
///
/// 爆発判定は Physics2D.OverlapCircleAll。バースト中無敵・被弾後無敵は PlayerHealth 側が処理（疎結合）。
/// 吹き飛び中／死亡中（EnemyKnockback）はカウントダウンを中断する＝殴って爆発を止められる。
///
/// chasePlayer が ON なら、射程に入ってからカウントダウン（fuseTime）のあいだプレイヤーを追尾してから爆発する
/// （OFF＝その場で爆発）。追尾は FixedUpdate + rb.MovePosition（壁すり抜け防止）で行い、その間は巡回
/// （EnemyMovement）を一時停止して rb.MovePosition の二重制御を避ける。
///
/// explodeOnDeathWallHit が ON なら、プレイヤーに倒されて吹き飛んだあと壁・床にぶつかった瞬間に即爆発する
/// （EnemyKnockback.OnDeathGroundHit を購読）。消滅そのものは EnemyKnockback（バウンド/着地）が担当。
///
/// 可視化は EnemyAreaAttack と同じ実行時生成の塗りつぶし円メッシュ（プレハブ不要、ビルトインRP前提）。
/// 生成した Material/Mesh は OnDestroy で破棄（リーク対策の流儀どおり）。
/// </summary>
public class EnemyBomber : MonoBehaviour
{
    [Header("索敵")]
    [Tooltip("プレイヤーのタグ")]
    public string playerTag = "Player";

    [Tooltip("カウントダウンを始める射程（半径）")]
    public float detectionRange = 5f;

    [Header("カウントダウン")]
    [Tooltip("導火線の長さ。射程に入ってからこの秒数で爆発する")]
    public float fuseTime = 2f;

    [Tooltip("カウントダウン中にプレイヤーが射程外へ出たらリセットする（false＝一度始まったら爆発まで止まらない）")]
    public bool resetIfPlayerLeaves = false;

    [Header("追尾（任意）")]
    [Tooltip("射程に入ったら、カウントダウン中（＝爆発までの数秒）プレイヤーを追いかける。OFF＝その場で爆発。" +
             "追う秒数は fuseTime に一致する")]
    public bool chasePlayer = false;

    [Tooltip("追尾速度")]
    public float chaseSpeed = 2.5f;

    [Tooltip("追尾を水平方向のみに限定する（地上の敵向け。OFF＝平面全方向）")]
    public bool chaseHorizontalOnly = false;

    [Tooltip("この距離まで近づいたら追尾を止める（プレイヤーへのめり込み防止）")]
    public float chaseStopDistance = 0.3f;

    [Header("爆発・威力")]
    [Tooltip("爆発の半径")]
    public float explosionRadius = 3f;

    [Tooltip("爆発でプレイヤーに与えるダメージ量")]
    public int explosionDamage = 1;

    [Tooltip("ダメージ対象として走査するレイヤー（プレイヤーのレイヤーを含めること）")]
    public LayerMask targetLayers = ~0;

    [Tooltip("爆発したら自分を消す（自爆）。false なら爆発後に待機へ戻る")]
    public bool destroyOnExplode = true;

    [Tooltip("プレイヤーに倒されて吹き飛んだあと、壁・床にぶつかったら即座に爆発する（カウントダウン中でなくても爆発）。" +
             "消滅そのものは EnemyKnockback が担当。即消えさせたいなら EnemyKnockback の bounceOnDeath を OFF に")]
    public bool explodeOnDeathWallHit = true;

    [Header("演出（任意）")]
    [Tooltip("爆発の瞬間に出すエフェクト（任意）。半径に合わせて自動スケールする")]
    public GameObject explosionEffectPrefab;

    [Tooltip("エフェクトを爆発範囲に合わせて自動スケールする（1x1ユニット＝直径1のプレハブ想定）")]
    public bool autoScaleEffect = true;

    [Tooltip("自動スケール時の微調整倍率（スケール＝半径×2×この値）。" +
             "ボス2の爆弾（StageSecondBossTimedBomb）と同じ式・同じ既定値なので、同じ P_Ex を挿せば見た目が揃う")]
    public float explosionEffectScaleMultiplier = 0.25f;

    [Tooltip("爆発フラッシュの表示時間（自滅前の見せ時間）")]
    public float explodeFlashTime = 0.2f;

    [Header("導火線の明滅（半透明フェード）")]
    [Tooltip("カウントダウン開始時の明滅の1往復時間（遅い）")]
    public float blinkIntervalStart = 0.3f;

    [Tooltip("爆発直前の明滅の1往復時間（速い）")]
    public float blinkIntervalEnd = 0.05f;

    [Tooltip("明滅で最も薄くなるときのアルファ（0=完全透明 / 1=不透明のまま）")]
    [Range(0f, 1f)] public float blinkMinAlpha = 0.3f;

    [Header("可視化（爆発範囲）")]
    [Tooltip("Gameビューで爆発範囲を半透明の円で表示する")]
    public bool showRuntimeRange = true;

    [Tooltip("待機・カウントダウン序盤の色（薄い）")]
    public Color idleColor = new Color(1f, 0.3f, 0f, 0.08f);

    [Tooltip("爆発直前の色（濃い）")]
    public Color dangerColor = new Color(1f, 0.2f, 0f, 0.55f);

    [Tooltip("爆発フラッシュの色")]
    public Color explodeColor = new Color(1f, 1f, 0.6f, 0.9f);

    [Tooltip("予兆円のマテリアル。ボス2の爆弾と同じ Boss_Area を割り当てると見た目が揃う。\n" +
             "★スプライト前提シェーダーなので telegraphSprite とセットで指定すること。両方未指定なら従来の Sprites/Default")]
    public Material telegraphMaterial;

    [Tooltip("予兆円のスプライト。ボス2の爆弾と同じ WhiteCircle2 を想定（スケール1＝直径1ユニット）")]
    public Sprite telegraphSprite;

    private enum Phase { Idle, Countdown, Exploding }
    private Phase phase = Phase.Idle;
    private float timer;
    private BlinkFade blinkFade;  // 導火線の半透明フェード明滅
    private float blinkPhase;     // 明滅の位相（累積）

    private Transform player;
    private EnemyKnockback knockback;
    private HitFlash hitFlash;
    private Renderer[] renderers;

    // 追尾用
    private Rigidbody2D rb;
    private EnemyMovement movement;      // 追尾中は巡回を止める（rb.MovePosition の二重制御を防ぐ）
    private bool patrolSuspended = false;

    private bool hasExplodedOnDeath = false; // 死亡吹き飛び中の壁ヒット爆発は1回だけ

    // 可視化用（実行時生成）。生成物の破棄は TelegraphCircle.Destroy() が面倒を見る
    private Transform rangeVisual;
    private readonly TelegraphCircle rangeCircle = new TelegraphCircle();

    void Awake()
    {
        knockback = GetComponent<EnemyKnockback>();
        rb = GetComponent<Rigidbody2D>();
        movement = GetComponent<EnemyMovement>();
        hitFlash = GetComponent<HitFlash>();
        renderers = GetComponentsInChildren<Renderer>(); // 可視化メッシュ生成前に取得（自分の見た目のみ）
        if (showRuntimeRange) CreateRangeVisual();

        // 死亡吹き飛び中の壁ヒットで即爆発するために購読する。
        // OnEnable ではなく Awake で購読するのは、死亡時に disableOnDeath でこのスクリプトが
        // enabled=false にされても購読を維持する（OnDisable で外れないようにする）ため。
        if (knockback != null) knockback.OnDeathGroundHit += HandleDeathGroundHit;
    }

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null) player = p.transform;
    }

    void Update()
    {
        // 吹き飛び中／死亡中はカウントダウンを中断（殴って止められる）。爆発中は止めない
        if (phase != Phase.Exploding && knockback != null && (knockback.IsActive || knockback.IsDying))
        {
            if (phase == Phase.Countdown) CancelCountdown();
            UpdateRangeVisual(true);
            return;
        }

        switch (phase)
        {
            case Phase.Idle:
                if (PlayerInRange()) BeginCountdown();
                break;

            case Phase.Countdown:
                if (resetIfPlayerLeaves && !PlayerInRange())
                {
                    CancelCountdown();
                    break;
                }
                UpdateFuseBlink();
                timer -= Time.deltaTime;
                if (timer <= 0f) Explode();
                break;

            case Phase.Exploding:
                timer -= Time.deltaTime;
                if (timer <= 0f) FinishExplosion();
                break;
        }

        UpdateRangeVisual(false);
    }

    // カウントダウン中にプレイヤーを追尾する（物理ステップで MovePosition＝壁すり抜け防止）。
    // 追尾は fuseTime のあいだだけ＝爆発までプレイヤーを追う。EnemyMovement は SuspendPatrol で止めてある。
    void FixedUpdate()
    {
        if (!chasePlayer || phase != Phase.Countdown || player == null) return;
        // 吹き飛び中／死亡中は追尾しない（Update 側で中断済みだが FixedUpdate との順序に備えて二重ガード）
        if (knockback != null && (knockback.IsActive || knockback.IsDying)) return;

        Vector2 pos = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 to = (Vector2)player.position - pos;
        if (chaseHorizontalOnly) to.y = 0f;

        float dist = to.magnitude;
        if (dist <= chaseStopDistance) return; // 近づきすぎたら止まる（めり込み防止）

        Vector2 dir = to / dist;
        // 行き過ぎ防止：停止距離を割り込まないように移動量をクランプ
        float step = Mathf.Min(chaseSpeed * Time.fixedDeltaTime, dist - chaseStopDistance);
        Vector2 next = pos + dir * step;

        if (rb != null) rb.MovePosition(next);
        else transform.position = next;
    }

    // 追尾中は巡回（EnemyMovement）を止める。両者が rb.MovePosition を書くと実行順で競合するため。
    private void SuspendPatrol()
    {
        if (movement != null && movement.enabled)
        {
            movement.enabled = false;
            patrolSuspended = true;
        }
    }

    // 止めていた巡回を元に戻す（カウントダウン中断・爆発後リセット時）。
    private void ResumePatrol()
    {
        if (patrolSuspended && movement != null)
        {
            movement.enabled = true;
            patrolSuspended = false;
        }
    }

    // ─── フェーズ遷移 ─────────────────────────────

    private void BeginCountdown()
    {
        phase = Phase.Countdown;
        timer = Mathf.Max(0.0001f, fuseTime);
        blinkPhase = 0f;
        blinkFade = new BlinkFade(renderers);
        blinkFade.Begin(); // マテリアルを透明対応の複製へ差し替え
        if (chasePlayer) SuspendPatrol(); // 追尾するので巡回を止める（rb.MovePosition の競合防止）
    }

    private void CancelCountdown()
    {
        phase = Phase.Idle;
        // 死亡中は死亡フェード（EnemyKnockback）にマテリアルを譲る（ここで戻すと競合する）
        EndFade(knockback != null && knockback.IsDying);
        ResumePatrol();
    }

    private void Explode()
    {
        EndFade(false); // 爆発前にモデルを元へ戻す
        DoExplosionDamage();

        phase = Phase.Exploding;
        timer = Mathf.Max(0f, explodeFlashTime);
    }

    // 自分中心の範囲へダメージ＋演出（カウントダウン爆発と壁ヒット即爆発で共用）。
    private void DoExplosionDamage()
    {
        // 範囲内のプレイヤーへダメージ
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius, targetLayers);
        foreach (Collider2D c in hits)
        {
            PlayerHealth hp = c.GetComponentInParent<PlayerHealth>();
            if (hp != null)
            {
                hp.TakeDamage(explosionDamage);
                break; // プレイヤーは1体想定（多重ヒット防止）
            }
        }

        if (explosionEffectPrefab != null)
        {
            GameObject fx = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
            if (autoScaleEffect)
            {
                float diameter = explosionRadius * 2f * explosionEffectScaleMultiplier;
                fx.transform.localScale = new Vector3(diameter, diameter, 1f);
            }
        }
    }

    // プレイヤーに倒されて吹き飛び中、壁・床にぶつかった瞬間に EnemyKnockback から呼ばれる。
    // その場で即爆発（範囲ダメージ＋演出）する。消滅そのものは EnemyKnockback が担当（バウンド/着地）。
    private void HandleDeathGroundHit(Vector2 normal)
    {
        if (!explodeOnDeathWallHit || hasExplodedOnDeath) return;
        hasExplodedOnDeath = true;
        DoExplosionDamage();
    }

    private void FinishExplosion()
    {
        if (destroyOnExplode)
        {
            Destroy(gameObject);
        }
        else
        {
            phase = Phase.Idle; // 待機へ戻して再利用
            ResumePatrol();
        }
    }

    // ─── 索敵・点滅 ───────────────────────────────

    private bool PlayerInRange()
    {
        if (player == null) return false;
        return ((Vector2)player.position - (Vector2)transform.position).sqrMagnitude
               <= detectionRange * detectionRange;
    }

    // 残り時間が減るほど明滅を速くする（導火線の演出）。半透明フェードで脈動させる。
    // アルファ制御なので Animator の m_Enabled 上書きの影響を受けない。
    private void UpdateFuseBlink()
    {
        if (blinkFade == null || !blinkFade.IsActive) return;

        float progress = 1f - Mathf.Clamp01(timer / fuseTime);
        float interval = Mathf.Lerp(blinkIntervalStart, blinkIntervalEnd, progress);

        float speed = Mathf.PI * 2f / Mathf.Max(0.0001f, interval);
        blinkPhase += Time.deltaTime * speed;

        float t = Mathf.Cos(blinkPhase) * 0.5f + 0.5f; // 0..1
        blinkFade.SetAlpha(Mathf.Lerp(blinkMinAlpha, 1f, t));
    }

    // 導火線フェードを終了する。leaveForDeathFade=true なら元へ戻さず放棄（死亡フェードに任せる）
    private void EndFade(bool leaveForDeathFade)
    {
        if (blinkFade == null) return;
        if (!leaveForDeathFade)
        {
            hitFlash?.StopAndRestore(); // 進行中のフラッシュを確定（破棄する複製を後で参照しないように）
            blinkFade.End();
        }
        blinkFade = null;
    }

    // ─── 可視化（予兆円。TelegraphCircle に集約。EnemyAreaAttack / ボスの着地円と同じ） ───

    private void CreateRangeVisual()
    {
        rangeCircle.Create("ExplosionRange(仮)", transform, telegraphSprite, telegraphMaterial, sortingOrder: -1);
        rangeVisual = rangeCircle.Transform;
        if (rangeVisual != null) rangeVisual.localPosition = Vector3.zero;
    }

    private void UpdateRangeVisual(bool hidden)
    {
        if (rangeVisual == null) return;

        rangeCircle.SetRadius(explosionRadius);

        // 親（モデル/ルート）が進行方向へ回転しても、範囲円は常にカメラ正面（XY平面）を向かせる。
        rangeVisual.rotation = Quaternion.identity;

        Color c;
        if (hidden || phase == Phase.Idle)
        {
            c = idleColor;
            if (hidden) c.a = 0f;
        }
        else if (phase == Phase.Exploding)
        {
            c = explodeColor;
        }
        else // Countdown：残り時間が減るほど濃く
        {
            float progress = 1f - Mathf.Clamp01(timer / fuseTime);
            c = Color.Lerp(idleColor, dangerColor, progress);
        }

        rangeCircle.SetColor(c);
    }

    void OnDestroy()
    {
        if (knockback != null) knockback.OnDeathGroundHit -= HandleDeathGroundHit;
        rangeCircle.Destroy();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange); // 射程
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius); // 爆発範囲
    }
}
