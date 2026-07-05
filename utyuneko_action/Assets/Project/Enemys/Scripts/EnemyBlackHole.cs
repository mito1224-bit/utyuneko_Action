using UnityEngine;

/// <summary>
/// ブラックホール（吸い込み）型の敵。自分を中心とした範囲に入ったプレイヤーを中心へ引き寄せる。
///
/// 役割分担（疎結合）:
///   - このスクリプトは「吸い込み（吸引力）」だけを担当する。プレイヤーの Rigidbody2D を
///     中心方向へ AddForce で引き寄せる（FixedUpdate で物理に乗せる）。
///   - 「敵に当たったらダメージ」は本体の EnemyCollision(Reflect) ＋ EnemyHealth.HandleHit が担当。
///     バースト中の無敵・被弾後の無敵は PlayerHealth 側が処理する（他エネミーと同じ流儀）。
///   - 吹き飛び中／死亡中（EnemyKnockback）は吸引を止める（EnemyMovement 等と同じ協調）。
///
/// 想定セットアップ:
///   - EnemyCollision.collisionType = Reflect ＋ 本体コライダー（吸い込まれて当たると弾かれ＆ダメージ）。
///   - 単体で付与できる吸引モジュール（RequireComponent なし）。
///
/// 可視化は EnemyBomber / EnemyAreaAttack と同じ実行時生成の塗りつぶし円メッシュ（プレハブ不要、ビルトインRP前提）。
/// 生成した Material/Mesh は OnDestroy で破棄（リーク対策の流儀どおり）。
/// </summary>
public class EnemyBlackHole : MonoBehaviour
{
    [Header("対象")]
    [Tooltip("吸い込む対象（プレイヤー）のタグ")]
    public string playerTag = "Player";

    [Header("吸い込み範囲・威力")]
    [Tooltip("吸い込みが効く範囲（半径）。この内側に入ると中心へ引き寄せられる")]
    public float pullRadius = 5f;

    [Tooltip("基準の吸引力。大きいほど強く引き寄せる")]
    public float pullForce = 20f;

    [Tooltip("中心に近いほど吸引力を強める（ブラックホールらしい加速感）")]
    public bool strongerNearCenter = true;

    [Tooltip("中心での吸引力の倍率（strongerNearCenter が ON のとき、外周=1倍→中心=この倍率）")]
    public float centerForceMultiplier = 2.5f;

    [Tooltip("引き寄せ方向を水平（X）のみに限定する。OFFなら平面全方向（X/Y）へ引き込む")]
    public bool horizontalOnly = false;

    [Header("引き込み速度の制限（任意）")]
    [Tooltip("中心へ向かう速度に上限を設ける（理不尽な加速の暴走を防ぐ）")]
    public bool limitApproachSpeed = true;

    [Tooltip("中心へ向かう速度の上限")]
    public float maxApproachSpeed = 12f;

    [Header("可視化（吸い込み範囲）")]
    [Tooltip("Gameビューで吸い込み範囲を半透明の円で表示する")]
    public bool showRuntimeRange = true;

    [Tooltip("外周の色（薄い）")]
    public Color edgeColor = new Color(0.4f, 0f, 0.6f, 0.08f);

    [Tooltip("中心付近の色（濃い）")]
    public Color coreColor = new Color(0.15f, 0f, 0.3f, 0.6f);

    [Tooltip("吸い込みの渦を感じさせる回転演出の速さ（度/秒）。0で回転なし")]
    public float swirlSpeed = 90f;

    private Transform player;
    private Rigidbody2D playerRb;
    private EnemyKnockback knockback;

    // 可視化用（実行時生成）
    private Transform rangeVisual;
    private MeshRenderer rangeRenderer;
    private Material rangeMaterial;
    private Mesh rangeMesh;
    private float swirlAngle; // 渦回転の累積角（ワールドZ回転で適用＝親の回転を継承しない）

    void Awake()
    {
        knockback = GetComponent<EnemyKnockback>();
        if (showRuntimeRange) CreateRangeVisual();
    }

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null)
        {
            player = p.transform;
            // ルートに無ければ子も探す（プレイヤーの Rigidbody2D は共有参照）
            playerRb = p.GetComponent<Rigidbody2D>();
            if (playerRb == null) playerRb = p.GetComponentInChildren<Rigidbody2D>();
        }
    }

    void Update()
    {
        // 見た目（渦の回転・範囲色）は通常フレームで更新。吸引そのものは FixedUpdate
        bool suppressed = knockback != null && (knockback.IsActive || knockback.IsDying);
        UpdateRangeVisual(suppressed);
    }

    void FixedUpdate()
    {
        if (playerRb == null) return;

        // 吹き飛び中／死亡中は吸引しない（殴って怯ませれば吸い込みが止まる）
        if (knockback != null && (knockback.IsActive || knockback.IsDying)) return;

        Vector2 toCenter = (Vector2)transform.position - playerRb.position;

        // 引き込み方向の限定（水平のみ）
        if (horizontalOnly) toCenter.y = 0f;

        float dist = toCenter.magnitude;
        if (dist > pullRadius || dist <= 0.0001f) return; // 範囲外／中心に重なっているときは何もしない

        Vector2 dir = toCenter / dist;

        // 中心に近いほど強く（t: 外周=0 → 中心=1）
        float force = pullForce;
        if (strongerNearCenter)
        {
            float t = 1f - Mathf.Clamp01(dist / pullRadius);
            force *= Mathf.Lerp(1f, Mathf.Max(1f, centerForceMultiplier), t);
        }

        // 中心へ向かう速度が上限未満のときだけ加速する（暴走防止）
        if (limitApproachSpeed)
        {
            float approachSpeed = Vector2.Dot(playerRb.linearVelocity, dir); // 中心方向への速度成分
            if (approachSpeed >= maxApproachSpeed) return;
        }

        playerRb.AddForce(dir * force, ForceMode2D.Force);
    }

    // ─── 可視化（実行時生成の塗りつぶし円。EnemyBomber と同方式） ───

    private void CreateRangeVisual()
    {
        GameObject go = new GameObject("BlackHoleRange(仮)");
        rangeVisual = go.transform;
        rangeVisual.SetParent(transform, false);
        rangeVisual.localPosition = Vector3.zero;

        MeshFilter mf = go.AddComponent<MeshFilter>();
        rangeMesh = BuildDiscMesh(48);
        mf.sharedMesh = rangeMesh;

        rangeRenderer = go.AddComponent<MeshRenderer>();
        rangeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rangeRenderer.receiveShadows = false;
        rangeRenderer.sortingOrder = -1; // プレイヤー／敵スプライトの後ろに描く

        rangeMaterial = new Material(Shader.Find("Sprites/Default"));
        rangeMaterial.renderQueue = 3000; // Transparent
        rangeRenderer.material = rangeMaterial;
    }

    // 中心が濃く外周が薄い、半径1の塗りつぶし円メッシュ（頂点カラーでグラデーション）。実スケールは localScale で合わせる
    private Mesh BuildDiscMesh(int segments)
    {
        Mesh mesh = new Mesh { name = "BlackHoleDisc(仮)" };

        Vector3[] verts = new Vector3[segments + 1];
        Color[] cols = new Color[segments + 1];
        verts[0] = Vector3.zero;
        cols[0] = Color.white;          // 中心（後で coreColor を乗算）
        for (int i = 0; i < segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            verts[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            cols[i + 1] = new Color(1f, 1f, 1f, 0f); // 外周（透明側）
        }

        int[] tris = new int[segments * 3];
        for (int i = 0; i < segments; i++)
        {
            tris[i * 3] = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = (i + 1) % segments + 1;
        }

        mesh.vertices = verts;
        mesh.colors = cols;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    private void UpdateRangeVisual(bool suppressed)
    {
        if (rangeVisual == null) return;

        // 半径が Inspector で変わっても追従（円メッシュは半径1なので半径そのものを掛ける）
        rangeVisual.localScale = Vector3.one * pullRadius;

        // 渦の回転演出（吸い込み中のみ角度を進める）。
        // 親（モデル/ルート）の回転を継承すると円が斜めに寝るので、ワールドのZ回転だけを適用して
        // 常にカメラ正面（XY平面）を保ちつつ渦だけ回す。
        if (!suppressed && swirlSpeed != 0f)
            swirlAngle += swirlSpeed * Time.deltaTime;
        rangeVisual.rotation = Quaternion.Euler(0f, 0f, swirlAngle);

        // 頂点カラー(中心=不透明/外周=透明)に乗算する基本色。吹き飛び中は薄める
        Color c = coreColor;
        if (suppressed) c.a *= 0.25f;
        rangeMaterial.color = c;
    }

    void OnDestroy()
    {
        if (rangeMaterial != null) Destroy(rangeMaterial);
        if (rangeMesh != null) Destroy(rangeMesh);
    }

    // シーンビューで吸い込み範囲を可視化（調整用）
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, pullRadius); // 吸い込み範囲
    }
}
