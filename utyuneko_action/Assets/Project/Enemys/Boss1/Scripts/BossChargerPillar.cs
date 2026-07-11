using UnityEngine;

/// <summary>
/// 地面叩き（技⑦）で地面から隆起する衝撃柱。BossChargerGroundSlamState が地面に沿って一列に生成する。
///
/// フェーズ: 待機(Wait:波及の時間差) → 予兆(Telegraph:足元に印) → 隆起(Rise/Active:縦の当たり判定) → 消滅。
///
/// プレイヤーへのダメージは Active 中に OverlapBox で走査して PlayerHealth.TakeDamage を呼ぶだけ
/// （バースト中・被弾後の無敵は PlayerHealth 側が処理する疎結合。EnemyAreaAttack と同じ流儀）。
/// 見た目は実行時生成の縦バー（Sprites/Default）。プレハブ不要で単体動作する。OnDestroy で複製を破棄。
/// </summary>
public class BossChargerPillar : MonoBehaviour
{
    private float waitTime;       // 生成～予兆開始までの遅延（隣の柱との時間差＝波及感）
    private float telegraphTime;
    private float riseTime;       // 隆起にかける時間（0→全高）
    private float activeTime;     // 全高で判定を残す時間
    private float width;
    private float height;
    private int damage;
    private LayerMask targetLayers;

    private enum Phase { Wait, Telegraph, Rise, Active }
    private Phase phase;
    private float timer;
    private float currentHeightScale; // 見た目の現在高さ（0..height）

    // 実行時生成の見た目（下端を軸に上へ伸びる単位クアッド）
    private Transform barVisual;
    private MeshRenderer barRenderer;
    private Material barMaterial;
    private Mesh barMesh;
    private bool usingCustomMaterial; // 割り当てマテリアルを使っているか（描画順を勝手に上書きしない用）

    /// <summary>柱の見た目設定（GroundSlamState がコントローラ値から組んで渡す）。</summary>
    public struct VisualConfig
    {
        public Material material;      // 未指定なら実行時生成の Sprites/Default 板
        public bool tint;              // マテリアルを予兆色で色付けするか
        public Color telegraphStart;   // 予兆の出始め（黄）
        public Color telegraphEnd;     // 予兆の隆起直前（赤）
        public Color active;           // 隆起中（赤・危険）
    }
    private VisualConfig visual;

    /// <summary>生成直後に GroundSlamState から呼ばれる。startDelay で波及の時間差を作る。</summary>
    public void Init(float waitTime, float telegraphTime, float riseTime, float activeTime,
                     float width, float height, int damage, LayerMask targetLayers, VisualConfig visual)
    {
        this.waitTime = Mathf.Max(0f, waitTime);
        this.telegraphTime = Mathf.Max(0f, telegraphTime);
        this.riseTime = Mathf.Max(0.01f, riseTime);
        this.activeTime = Mathf.Max(0f, activeTime);
        this.width = Mathf.Max(0.05f, width);
        this.height = Mathf.Max(0.05f, height);
        this.damage = damage;
        this.targetLayers = targetLayers;
        this.visual = visual;

        CreateVisual();
        phase = Phase.Wait;
        timer = this.waitTime;
        currentHeightScale = 0f;
        UpdateVisual(visual.telegraphStart, 0f);
    }

    void Update()
    {
        timer -= Time.deltaTime;

        switch (phase)
        {
            case Phase.Wait:
                if (timer <= 0f) { phase = Phase.Telegraph; timer = telegraphTime; }
                UpdateVisual(visual.telegraphStart, 0f); // 予兆前は薄い足元印だけ
                break;

            case Phase.Telegraph:
                // 予兆：足元に低い印を出しつつ、隆起が近づくほど 黄→赤 へ変える（いつ来るか分かりやすく）
                float p = telegraphTime > 0f ? 1f - Mathf.Clamp01(timer / telegraphTime) : 1f;
                Color teleCol = Color.Lerp(visual.telegraphStart, visual.telegraphEnd, p);
                UpdateVisual(teleCol, height * 0.08f);
                if (timer <= 0f) { phase = Phase.Rise; timer = riseTime; }
                break;

            case Phase.Rise:
                // 隆起：0→全高へ伸びる。伸びている間から当たり判定を出す
                float t = 1f - Mathf.Clamp01(timer / riseTime);
                currentHeightScale = Mathf.Lerp(0f, height, t);
                UpdateVisual(visual.active, currentHeightScale);
                ApplyDamage();
                if (timer <= 0f) { phase = Phase.Active; timer = activeTime; currentHeightScale = height; }
                break;

            case Phase.Active:
                UpdateVisual(visual.active, height);
                ApplyDamage();
                if (timer <= 0f) Destroy(gameObject);
                break;
        }
    }

    // 縦の帯（下端＝足元、上へ height）へ OverlapBox。範囲内のプレイヤーへダメージ
    private void ApplyDamage()
    {
        float h = Mathf.Max(0.05f, currentHeightScale);
        Vector2 center = (Vector2)transform.position + Vector2.up * (h * 0.5f);
        Vector2 size = new Vector2(width, h);

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, targetLayers);
        foreach (Collider2D c in hits)
        {
            PlayerHealth hp = c.GetComponentInParent<PlayerHealth>();
            if (hp != null)
            {
                hp.TakeDamage(damage); // 無敵時間は PlayerHealth 側で処理
                break;                 // プレイヤーは1体想定
            }
        }
    }

    // ─── 実行時生成の見た目（下端を軸に上へ伸びる単位クアッド） ───

    private void CreateVisual()
    {
        GameObject go = new GameObject("PillarBar(仮)");
        barVisual = go.transform;
        barVisual.SetParent(transform, false);
        barVisual.localPosition = Vector3.zero;
        barVisual.rotation = Quaternion.identity; // 親の向きに寝ないようワールド無回転で立てる

        MeshFilter mf = go.AddComponent<MeshFilter>();
        barMesh = BuildBottomAnchoredQuad();
        mf.sharedMesh = barMesh;

        barRenderer = go.AddComponent<MeshRenderer>();
        barRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        barRenderer.receiveShadows = false;
        barRenderer.sortingOrder = -1; // プレイヤー／敵スプライトの後ろ

        if (visual.material != null)
        {
            // 割り当てマテリアルを複製して使う（隆起アニメは localScale で効かせる）。描画順はマテリアル任せ。
            usingCustomMaterial = true;
            barMaterial = new Material(visual.material);
        }
        else
        {
            // 従来どおり実行時生成の半透明板でフォールバック
            usingCustomMaterial = false;
            barMaterial = new Material(Shader.Find("Sprites/Default"));
            barMaterial.renderQueue = 3000; // Transparent
        }
        barRenderer.material = barMaterial;
    }

    // 幅1・高さ1で、下端(y=0)を原点に上(y=1)へ伸びるクアッド。実スケールは localScale で合わせる。
    // 割り当てマテリアルのテクスチャが正しく貼れるよう UV を、Lit マテリアルが暗くならないよう法線(-Z)を持たせる。
    private Mesh BuildBottomAnchoredQuad()
    {
        Mesh mesh = new Mesh { name = "PillarQuad(仮)" };
        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, 0f, 0f),
            new Vector3( 0.5f, 0f, 0f),
            new Vector3( 0.5f, 1f, 0f),
            new Vector3(-0.5f, 1f, 0f),
        };
        mesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
        };
        mesh.normals = new Vector3[]
        {
            new Vector3(0f, 0f, -1f),
            new Vector3(0f, 0f, -1f),
            new Vector3(0f, 0f, -1f),
            new Vector3(0f, 0f, -1f),
        };
        mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private void UpdateVisual(Color color, float heightScale)
    {
        if (barVisual == null) return;
        barVisual.position = transform.position; // 足元に固定
        barVisual.rotation = Quaternion.identity;
        barVisual.localScale = new Vector3(width, Mathf.Max(0.001f, heightScale), 1f);

        if (barMaterial == null) return;
        // 割り当てマテリアルは tint OFF なら色付けせず素の見た目を出す。
        // フォールバック板（Sprites/Default）は常に予兆色で塗る。
        if (usingCustomMaterial && !visual.tint) return;
        SetMaterialColor(color);
    }

    // マテリアルの主色へ着色（Sprites/Standard=_Color、URP Lit=_BaseColor の両対応）
    private void SetMaterialColor(Color color)
    {
        if (barMaterial.HasProperty("_BaseColor")) barMaterial.SetColor("_BaseColor", color);
        if (barMaterial.HasProperty("_Color")) barMaterial.SetColor("_Color", color);
    }

    void OnDestroy()
    {
        if (barMaterial != null) Destroy(barMaterial);
        if (barMesh != null) Destroy(barMesh);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f);
        Vector3 c = transform.position + Vector3.up * (height * 0.5f);
        Gizmos.DrawWireCube(c, new Vector3(width, height, 0.1f));
    }
}
