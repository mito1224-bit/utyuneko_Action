using UnityEngine;

/// <summary>
/// 一定時間ごとに、自分を中心とした設定範囲（円）へ攻撃を出す敵。
///
/// 仕組み:
///   - クールダウン（attackInterval）→ 予兆（telegraphTime）→ 発動（範囲内のプレイヤーへダメージ）を繰り返す。
///   - 予兆を挟むことでプレイヤーに回避猶予を与える（即着弾は理不尽なので避ける）。
///   - 発動の瞬間に Physics2D.OverlapCircle で範囲内を走査し、PlayerHealth.TakeDamage を呼ぶ。
///     バースト中の無敵・被弾後の無敵は PlayerHealth 側が処理するのでここでは判定しない（疎結合）。
///   - 吹き飛び中／死亡中（EnemyKnockback）は攻撃を中断する（EnemyMovement と同じ協調）。
///
/// 想定セットアップ:
///   - どのエネミーにも単体で付与できる攻撃モジュール（EnemyHealth 等の有無は問わない）。
///   - telegraphEffectPrefab / strikeEffectPrefab は任意。半径に合わせて自動スケールする。
/// </summary>
public class EnemyAreaAttack : MonoBehaviour
{
    [Header("攻撃タイミング")]
    [Tooltip("起動してから最初の攻撃までの待ち時間（秒）")]
    public float startDelay = 1f;

    [Tooltip("攻撃と攻撃の間隔＝クールダウン（秒）")]
    public float attackInterval = 3f;

    [Tooltip("発動（攻撃判定）を残す秒数。0なら瞬間判定。値を大きくすると判定が一定時間残り続ける")]
    public float activeTime = 0.3f;

    [Header("予兆（テレグラフ）")]
    [Tooltip("発動の前にこの秒数だけ予兆を出す。プレイヤーが範囲外へ逃げる猶予")]
    public float telegraphTime = 0.8f;

    [Tooltip("予兆中に表示するエフェクト（任意）。攻撃範囲に追従し、半径に合わせて自動スケールする")]
    public GameObject telegraphEffectPrefab;

    [Header("攻撃範囲・威力")]
    [Tooltip("自分を中心とした攻撃の半径")]
    public float attackRadius = 3f;

    [Tooltip("範囲内のプレイヤーに与えるダメージ量")]
    public int attackDamage = 1;

    [Tooltip("ダメージ対象として走査するレイヤー（プレイヤーのレイヤーを含めること）")]
    public LayerMask targetLayers = ~0;

    [Header("演出（任意）")]
    [Tooltip("発動の瞬間に出すエフェクト（任意）。半径に合わせて自動スケールする")]
    public GameObject strikeEffectPrefab;

    [Tooltip("エフェクトを攻撃範囲に合わせて自動スケールする（1x1ユニット＝直径1のプレハブ想定）")]
    public bool autoScaleEffect = true;

    [Header("実行時の可視化（仮）")]
    [Tooltip("Gameビューでも攻撃範囲を半透明の円で表示する（プレハブ未用意でも見えるようにする仮実装）")]
    public bool showRuntimeRange = true;

    [Tooltip("クールダウン中（常時表示）の色。危険範囲を薄く示す")]
    public Color idleColor = new Color(1f, 0f, 0f, 0.12f);

    [Tooltip("予兆中の色。着弾が近いほど濃くなる")]
    public Color telegraphColor = new Color(1f, 0.5f, 0f, 0.6f);

    [Tooltip("発動中（攻撃判定が出ている間）の色")]
    public Color strikeColor = new Color(1f, 1f, 1f, 0.85f);

    private enum Phase { Cooldown, Telegraph, Active }
    private Phase phase = Phase.Cooldown;
    private float timer;

    private EnemyKnockback knockback;
    private GameObject telegraphInstance;

    // 実行時可視化用（仮）。OnDestroy で破棄する
    private Transform rangeVisual;
    private MeshRenderer rangeRenderer;
    private Material rangeMaterial;
    private Mesh rangeMesh;

    void Awake()
    {
        knockback = GetComponent<EnemyKnockback>();
        if (showRuntimeRange) CreateRangeVisual();
    }

    void Start()
    {
        timer = startDelay;
    }

    void Update()
    {
        // 吹き飛び中／死亡中は攻撃しない（出していた予兆も消す）
        if (knockback != null && (knockback.IsActive || knockback.IsDying))
        {
            if (phase != Phase.Cooldown) CancelAttack(); // 予兆中・発動中なら中断
            UpdateRangeVisual(true); // 吹き飛び中は範囲表示を隠す
            return;
        }

        // 発動中は判定が出ている間ずっと範囲内を走査する（無敵が切れれば再ヒット）
        if (phase == Phase.Active) ApplyDamageInRange();

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            switch (phase)
            {
                case Phase.Cooldown:
                    BeginTelegraph();
                    break;
                case Phase.Telegraph:
                    BeginActive();
                    break;
                case Phase.Active:
                    EndActive();
                    break;
            }
        }

        UpdateRangeVisual(false);
    }

    // 予兆を開始：エフェクトを自分の子として出し、半径に追従させる
    private void BeginTelegraph()
    {
        phase = Phase.Telegraph;
        timer = Mathf.Max(0f, telegraphTime);

        if (telegraphEffectPrefab != null)
        {
            telegraphInstance = Instantiate(telegraphEffectPrefab, transform.position, Quaternion.identity, transform);
            telegraphInstance.transform.localPosition = Vector3.zero;
            ApplyEffectScale(telegraphInstance.transform);
        }
    }

    // 攻撃を中断（吹き飛び等）：予兆・発動を打ち切ってクールダウンに戻す
    private void CancelAttack()
    {
        CleanupTelegraph();
        phase = Phase.Cooldown;
        timer = attackInterval;
    }

    // 発動開始：判定を出し、発動エフェクトを出す。発動中は activeTime のあいだ判定が残る
    private void BeginActive()
    {
        CleanupTelegraph();

        phase = Phase.Active;
        timer = Mathf.Max(0f, activeTime);

        if (strikeEffectPrefab != null)
        {
            GameObject fx = Instantiate(strikeEffectPrefab, transform.position, Quaternion.identity);
            ApplyEffectScale(fx.transform);
        }

        // activeTime=0 でも最低1回は判定する（瞬間攻撃と同じ挙動）
        ApplyDamageInRange();
    }

    // 範囲内のプレイヤーへダメージ。発動中は毎フレーム呼ばれる（無敵時間は PlayerHealth 側が処理）
    private void ApplyDamageInRange()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRadius, targetLayers);
        foreach (Collider2D c in hits)
        {
            PlayerHealth hp = c.GetComponentInParent<PlayerHealth>();
            if (hp != null)
            {
                hp.TakeDamage(attackDamage);
                break; // プレイヤーは1体想定（複数コライダーでの多重ヒットを防ぐ）
            }
        }
    }

    // 発動終了：次のクールダウンへ
    private void EndActive()
    {
        phase = Phase.Cooldown;
        timer = attackInterval;
    }

    private void CleanupTelegraph()
    {
        if (telegraphInstance != null)
        {
            Destroy(telegraphInstance);
            telegraphInstance = null;
        }
    }

    // エフェクト（直径1のプレハブ想定）を攻撃範囲＝直径 attackRadius*2 に合わせる
    private void ApplyEffectScale(Transform t)
    {
        if (autoScaleEffect) t.localScale = Vector3.one * (attackRadius * 2f);
    }

    // ─── 実行時の範囲可視化（仮）────────────────────────────────
    // プレハブを用意しなくても Game ビューで攻撃範囲が見えるよう、
    // 半径1の塗りつぶし円メッシュを子として生成し、フェーズで色を変える。

    private void CreateRangeVisual()
    {
        GameObject go = new GameObject("AreaAttackRange(仮)");
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

        // ビルトインRP前提。Sprites/Default は _Color で色付け＆半透明ブレンドできる
        rangeMaterial = new Material(Shader.Find("Sprites/Default"));
        rangeMaterial.renderQueue = 3000; // Transparent
        rangeRenderer.material = rangeMaterial;
    }

    // 中心＋外周の扇メッシュ（半径1の単位円）。実スケールは transform.localScale で合わせる
    private Mesh BuildDiscMesh(int segments)
    {
        Mesh mesh = new Mesh { name = "AreaAttackDisc(仮)" };

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

    // フェーズに応じて範囲の色／濃さを更新する
    private void UpdateRangeVisual(bool hidden)
    {
        if (rangeVisual == null) return;

        // 半径が Inspector で変わっても追従（円メッシュは半径1なので半径そのものを掛ける）
        rangeVisual.localScale = Vector3.one * attackRadius;

        // 親（モデル/ルート）が進行方向へ回転しても、範囲円は常にカメラ正面（XY平面）を向かせる。
        // ワールド回転を無回転に固定＝親のY軸回転を継承して円が斜めに寝るのを防ぐ。
        rangeVisual.rotation = Quaternion.identity;

        Color c;
        if (hidden)
        {
            c = idleColor;
            c.a = 0f;
        }
        else if (phase == Phase.Active)
        {
            // 発動中：判定が出ている間は強い色で塗る
            c = strikeColor;
        }
        else if (phase == Phase.Telegraph)
        {
            // 予兆：待機色→予兆色へ、着弾が近いほど濃く
            float progress = (telegraphTime > 0f) ? 1f - Mathf.Clamp01(timer / telegraphTime) : 1f;
            c = Color.Lerp(idleColor, telegraphColor, progress);
        }
        else
        {
            c = idleColor;
        }

        rangeMaterial.color = c;
    }

    void OnDestroy()
    {
        // 実行時に生成したマテリアル／メッシュを破棄（リーク対策）
        if (rangeMaterial != null) Destroy(rangeMaterial);
        if (rangeMesh != null) Destroy(rangeMesh);
    }

    // シーンビューで攻撃範囲を可視化（調整用）
    private void OnDrawGizmosSelected()
    {
        // 予兆中はオレンジ、それ以外は赤で攻撃範囲の円を描く
        Gizmos.color = (phase == Phase.Telegraph) ? new Color(1f, 0.5f, 0f, 0.9f) : Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRadius);
    }
}
