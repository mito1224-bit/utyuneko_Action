using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AfterImageEffect : MonoBehaviour
{
    [Header("残像専用の半透明マテリアル（SurfaceTypeをTransparentにしたもの）")]
    public Material afterImageMaterial;

    [Header("残像を出す間隔（秒）")]
    public float spawnInterval = 0.05f;

    [Header("消える速度（値が大きいほど早く消える）")]
    public float fadeSpeed = 3.0f;

    [Header("アルファの初期値")]
    public float alpha = 0.6f;

    [Header("プレイヤーへの追従強度（0でその場に固定）")]
    [SerializeField] private float followSpeed = 5.0f;

    private float timer;

    // 💡【大改造】単一ではなく、プレイヤーが持つすべてのパーツを記憶するリストに変更！
    private List<SkinnedMeshRenderer> skinnedRenderers = new List<SkinnedMeshRenderer>();
    private List<MeshFilter> meshFilters = new List<MeshFilter>();

    void Start()
    {
        // 💡 プレイヤーの子オブジェクトにある「すべてのアニメーションパーツ」を根こそぎ取得！
        GetComponentsInChildren<SkinnedMeshRenderer>(true, skinnedRenderers);

        // 💡 アニメーションしない純粋な3Dパーツ（回転するドリル単体など）もすべて取得！
        MeshFilter[] allFilters = GetComponentsInChildren<MeshFilter>(true);
        foreach (var filter in allFilters)
        {
            // スキンメッシュ（体など）と重複しないパーツだけを抽出
            if (filter.GetComponent<SkinnedMeshRenderer>() == null)
            {
                meshFilters.Add(filter);
            }
        }
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            Spawn3DGhost();
            timer = 0f;
        }
    }

    void Spawn3DGhost()
    {
        if (skinnedRenderers.Count == 0 && meshFilters.Count == 0) return;

        // 1. 📂 残像全体の「親玉（グループの箱）」を今のプレイヤーの位置に生成
        GameObject ghostParent = new GameObject("[AfterImage_3D_Group]");
        ghostParent.transform.position = transform.position;
        ghostParent.transform.rotation = transform.rotation;
        ghostParent.transform.localScale = transform.lossyScale;

        // メモリ解放用の一時保存リスト
        List<Mesh> bakedMeshes = new List<Mesh>();

        // ===================================================================
        // 🧬 2.【パーツ巡回①】アニメーションで動く手足や体などをすべて複製・焼き付け！
        // ===================================================================
        foreach (var smr in skinnedRenderers)
        {
            if (smr == null || !smr.enabled || smr.gameObject.activeInHierarchy == false) continue;

            // パーツ用の空オブジェクトを作り、プレイヤーのそのパーツの「世界座標」に完璧に合わせる
            GameObject part = new GameObject(smr.name + "_GhostPiece");
            part.transform.position = smr.transform.position;
            part.transform.rotation = smr.transform.rotation;
            part.transform.localScale = smr.transform.lossyScale * 0.95f; // ほんの少し小さく
            part.transform.SetParent(ghostParent.transform); // 親玉の箱に入れる

            MeshFilter filter = part.AddComponent<MeshFilter>();
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            if (afterImageMaterial != null) renderer.material = afterImageMaterial;

            // そのパーツの「今この瞬間」の変形ポーズをバチッと焼き付ける
            Mesh bakedMesh = new Mesh();
            smr.BakeMesh(bakedMesh);
            filter.mesh = bakedMesh;
            bakedMeshes.Add(bakedMesh);
        }

        // ===================================================================
        // ⚙️ 3.【パーツ巡回②】アニメーションしない独立パーツ（ドリルなど）をすべて複製！
        // ===================================================================
        foreach (var mf in meshFilters)
        {
            if (mf == null || mf.gameObject.activeInHierarchy == false) continue;
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || !mr.enabled) continue;

            GameObject part = new GameObject(mf.name + "_GhostPiece");
            part.transform.position = mf.transform.position;
            part.transform.rotation = mf.transform.rotation;
            part.transform.localScale = mf.transform.lossyScale * 0.95f;
            part.transform.SetParent(ghostParent.transform);

            MeshFilter filter = part.AddComponent<MeshFilter>();
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            if (afterImageMaterial != null) renderer.material = afterImageMaterial;

            filter.mesh = mf.sharedMesh;
        }

        // 全パーツを詰め込んだ親玉ごとフェードアウト＆追従開始！
        StartCoroutine(FadeOutAndDestroy3D(ghostParent, bakedMeshes));
    }

    IEnumerator FadeOutAndDestroy3D(GameObject ghostParent, List<Mesh> bakedMeshes)
    {
        if (ghostParent == null) yield break;

        // グループ内にあるすべてのパーツのRendererからマテリアルを一斉に集める
        MeshRenderer[] renderers = ghostParent.GetComponentsInChildren<MeshRenderer>();
        List<Material> mats = new List<Material>();
        foreach (var r in renderers)
        {
            if (r.material != null) mats.Add(r.material);
        }

        float currentAlpha = alpha;

        while (currentAlpha > 0)
        {
            if (ghostParent == null) yield break;

            // ⛓️【親玉丸ごと追従】
            // バラバラのパーツを入れた「親の箱」ごと、プレイヤーの後ろを Lerp で追いかけさせる！
            if (followSpeed > 0f)
            {
                ghostParent.transform.position = Vector3.Lerp(ghostParent.transform.position, transform.position, Time.deltaTime * followSpeed);
                ghostParent.transform.rotation = Quaternion.Lerp(ghostParent.transform.rotation, transform.rotation, Time.deltaTime * followSpeed);
            }

            // アルファ値（透明度）を減算
            currentAlpha -= fadeSpeed * Time.deltaTime;

            // 集めたすべてのパーツのマテリアルの透明度を同時に下げる
            foreach (var mat in mats)
            {
                if (mat != null && mat.HasProperty("_BaseColor"))
                {
                    Color color = mat.GetColor("_BaseColor");
                    color.a = currentAlpha;
                    mat.SetColor("_BaseColor", color);
                }
            }

            yield return null;
        }

        // 🧹【メモリ解放】焼き付けたすべてのメッシュのゴミを一斉に掃除
        foreach (var mesh in bakedMeshes)
        {
            if (mesh != null) Destroy(mesh);
        }

        if (ghostParent != null) Destroy(ghostParent);
    }
}