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

    [Header("爆発・威力")]
    [Tooltip("爆発の半径")]
    public float explosionRadius = 3f;

    [Tooltip("爆発でプレイヤーに与えるダメージ量")]
    public int explosionDamage = 1;

    [Tooltip("ダメージ対象として走査するレイヤー（プレイヤーのレイヤーを含めること）")]
    public LayerMask targetLayers = ~0;

    [Tooltip("爆発したら自分を消す（自爆）。false なら爆発後に待機へ戻る")]
    public bool destroyOnExplode = true;

    [Header("演出（任意）")]
    [Tooltip("爆発の瞬間に出すエフェクト（任意）。半径に合わせて自動スケールする")]
    public GameObject explosionEffectPrefab;

    [Tooltip("エフェクトを爆発範囲に合わせて自動スケールする（1x1ユニット＝直径1のプレハブ想定）")]
    public bool autoScaleEffect = true;

    [Tooltip("爆発フラッシュの表示時間（自滅前の見せ時間）")]
    public float explodeFlashTime = 0.2f;

    [Header("導火線の点滅")]
    [Tooltip("カウントダウン開始時の点滅間隔（遅い）")]
    public float blinkIntervalStart = 0.3f;

    [Tooltip("爆発直前の点滅間隔（速い）")]
    public float blinkIntervalEnd = 0.05f;

    [Header("可視化（爆発範囲）")]
    [Tooltip("Gameビューで爆発範囲を半透明の円で表示する")]
    public bool showRuntimeRange = true;

    [Tooltip("待機・カウントダウン序盤の色（薄い）")]
    public Color idleColor = new Color(1f, 0.3f, 0f, 0.08f);

    [Tooltip("爆発直前の色（濃い）")]
    public Color dangerColor = new Color(1f, 0.2f, 0f, 0.55f);

    [Tooltip("爆発フラッシュの色")]
    public Color explodeColor = new Color(1f, 1f, 0.6f, 0.9f);

    private enum Phase { Idle, Countdown, Exploding }
    private Phase phase = Phase.Idle;
    private float timer;
    private float blinkTimer;

    private Transform player;
    private EnemyKnockback knockback;
    private Renderer[] renderers;

    // 可視化用（実行時生成）
    private Transform rangeVisual;
    private MeshRenderer rangeRenderer;
    private Material rangeMaterial;
    private Mesh rangeMesh;

    void Awake()
    {
        knockback = GetComponent<EnemyKnockback>();
        renderers = GetComponentsInChildren<Renderer>(); // 可視化メッシュ生成前に取得（自分の見た目のみ）
        if (showRuntimeRange) CreateRangeVisual();
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

    // ─── フェーズ遷移 ─────────────────────────────

    private void BeginCountdown()
    {
        phase = Phase.Countdown;
        timer = Mathf.Max(0.0001f, fuseTime);
        blinkTimer = 0f;
    }

    private void CancelCountdown()
    {
        phase = Phase.Idle;
        SetRenderersEnabled(true); // 点滅から確実に表示へ戻す
    }

    private void Explode()
    {
        SetRenderersEnabled(true);

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
            if (autoScaleEffect) fx.transform.localScale = Vector3.one * (explosionRadius * 2f);
        }

        phase = Phase.Exploding;
        timer = Mathf.Max(0f, explodeFlashTime);
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
        }
    }

    // ─── 索敵・点滅 ───────────────────────────────

    private bool PlayerInRange()
    {
        if (player == null) return false;
        return ((Vector2)player.position - (Vector2)transform.position).sqrMagnitude
               <= detectionRange * detectionRange;
    }

    // 残り時間が減るほど点滅を速くする（導火線の演出）
    private void UpdateFuseBlink()
    {
        float progress = 1f - Mathf.Clamp01(timer / fuseTime);
        float interval = Mathf.Lerp(blinkIntervalStart, blinkIntervalEnd, progress);

        blinkTimer += Time.deltaTime;
        if (blinkTimer >= interval)
        {
            blinkTimer = 0f;
            foreach (Renderer r in renderers)
                if (r != null) r.enabled = !r.enabled;
        }
    }

    private void SetRenderersEnabled(bool on)
    {
        foreach (Renderer r in renderers)
            if (r != null) r.enabled = on;
    }

    // ─── 可視化（実行時生成の塗りつぶし円。EnemyAreaAttack と同方式） ───

    private void CreateRangeVisual()
    {
        GameObject go = new GameObject("ExplosionRange(仮)");
        rangeVisual = go.transform;
        rangeVisual.SetParent(transform, false);
        rangeVisual.localPosition = Vector3.zero;

        MeshFilter mf = go.AddComponent<MeshFilter>();
        rangeMesh = BuildDiscMesh(48);
        mf.sharedMesh = rangeMesh;

        rangeRenderer = go.AddComponent<MeshRenderer>();
        rangeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rangeRenderer.receiveShadows = false;
        rangeRenderer.sortingOrder = -1;

        rangeMaterial = new Material(Shader.Find("Sprites/Default"));
        rangeMaterial.renderQueue = 3000; // Transparent
        rangeRenderer.material = rangeMaterial;
    }

    private Mesh BuildDiscMesh(int segments)
    {
        Mesh mesh = new Mesh { name = "ExplosionDisc(仮)" };

        Vector3[] verts = new Vector3[segments + 1];
        verts[0] = Vector3.zero;
        for (int i = 0; i < segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            verts[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
        }

        int[] tris = new int[segments * 3];
        for (int i = 0; i < segments; i++)
        {
            tris[i * 3] = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = (i + 1) % segments + 1;
        }

        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    private void UpdateRangeVisual(bool hidden)
    {
        if (rangeVisual == null) return;

        rangeVisual.localScale = Vector3.one * explosionRadius;

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

        rangeMaterial.color = c;
    }

    void OnDestroy()
    {
        if (rangeMaterial != null) Destroy(rangeMaterial);
        if (rangeMesh != null) Destroy(rangeMesh);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange); // 射程
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius); // 爆発範囲
    }
}
