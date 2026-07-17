using UnityEngine;

/// <summary>
/// 攻撃予兆の円。ボス2の爆弾（MineBomb / TimedBomb）の IndicatorRoot と同じ SpriteRenderer 方式に揃えたもの。
/// EnemyBomber / EnemyAreaAttack / BossChargerLeapSlamState に同じコードが3重複していたのでここへ集約した。
///
/// ■なぜスプライト方式か
///   予兆用マテリアル Boss_Area（URPHologram = Custom/URPMistyGlowSprite_Thick）は
///   _MainTex が [PerRendererData] のスプライト前提シェーダー。SpriteRenderer がスプライトを流し込まないと、
///   フチ検出（テクスチャのアルファを周囲32箇所サンプリング）もUVベースのもわもわも働かない。
///   従来の「実行時生成ディスクメッシュ（UVなし）」では絵にならないため、爆弾と同じ方式に合わせている。
///
/// ■色の出し方
///   シェーダーが最後に頂点カラーを乗算する（finalColor *= input.color）ので、SpriteRenderer.color が
///   そのまま濃さ・色の駆動になる。マテリアルは sharedMaterial のまま共有＝複製しないので破棄も不要。
///
/// ■フォールバック
///   sprite / material が未指定なら、従来どおり Sprites/Default の実行時ディスクメッシュで描く。
///   既存シーン・テストシーンの見た目を壊さないための保険（この場合だけ Material/Mesh の破棄が要る）。
///   ★Destroy() の呼び忘れはリークになるので、持ち主の OnDestroy / Exit で必ず呼ぶこと。
/// </summary>
public class TelegraphCircle
{
    private GameObject go;
    private Transform tr;

    // スプライト方式
    private SpriteRenderer sr;

    // フォールバック（実行時メッシュ）方式。破棄が必要なのはこちらだけ
    private MeshRenderer meshRenderer;
    private Material fallbackMaterial;
    private Mesh fallbackMesh;

    private bool spriteMode;

    public bool IsCreated => go != null;
    public Transform Transform => tr;

    /// <summary>
    /// 予兆円を生成する。sprite と material の両方が指定されていればスプライト方式、
    /// どちらか欠けていれば従来の Sprites/Default メッシュ方式になる。
    /// </summary>
    public void Create(string name, Transform parent, Sprite sprite, Material material, int sortingOrder = -1)
    {
        Destroy(); // 二重生成の保険

        go = new GameObject(name);
        tr = go.transform;
        if (parent != null) tr.SetParent(parent, false);

        spriteMode = sprite != null && material != null;

        if (spriteMode)
        {
            sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = material; // 複製しない（.material だとインスタンス化してリークする）
            sr.sortingOrder = sortingOrder;
        }
        else
        {
            MeshFilter mf = go.AddComponent<MeshFilter>();
            fallbackMesh = BuildDiscMesh(48);
            mf.sharedMesh = fallbackMesh;

            meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sortingOrder = sortingOrder;

            fallbackMaterial = new Material(Shader.Find("Sprites/Default"));
            fallbackMaterial.renderQueue = 3000; // Transparent
            meshRenderer.material = fallbackMaterial;
        }
    }

    /// <summary>半径（ワールド単位）を設定する。方式によってスケールの意味が違うのでここで吸収する。</summary>
    public void SetRadius(float radius)
    {
        if (tr == null) return;

        // スプライト方式: スケール1＝直径1ユニット（爆弾の IndicatorRoot と同じ前提）なので直径を入れる。
        // メッシュ方式  : BuildDiscMesh が半径1の単位円なので半径をそのまま入れる。
        float scale = spriteMode ? radius * 2f : radius;
        tr.localScale = new Vector3(scale, scale, 1f);
    }

    /// <summary>色・濃さを設定する。スプライト方式は頂点カラー、メッシュ方式はマテリアル色。</summary>
    public void SetColor(Color c)
    {
        if (spriteMode) { if (sr != null) sr.color = c; }
        else { if (fallbackMaterial != null) fallbackMaterial.color = c; }
    }

    public void SetVisible(bool visible)
    {
        if (spriteMode) { if (sr != null) sr.enabled = visible; }
        else { if (meshRenderer != null) meshRenderer.enabled = visible; }
    }

    public void SetWorldPosition(Vector3 pos)
    {
        if (tr != null) tr.position = pos;
    }

    /// <summary>生成物を破棄する。持ち主の OnDestroy / Exit から必ず呼ぶこと。</summary>
    public void Destroy()
    {
        if (fallbackMaterial != null) { Object.Destroy(fallbackMaterial); fallbackMaterial = null; }
        if (fallbackMesh != null) { Object.Destroy(fallbackMesh); fallbackMesh = null; }
        if (go != null) { Object.Destroy(go); go = null; }
        tr = null;
        sr = null;
        meshRenderer = null;
    }

    // 中心＋外周の扇メッシュ（半径1の単位円）。フォールバック専用。
    private static Mesh BuildDiscMesh(int segments)
    {
        Mesh mesh = new Mesh { name = "TelegraphDisc(仮)" };
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
}
