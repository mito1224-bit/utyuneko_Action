using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 「動くブロック」1個(単体メッシュ)を対象に、見た目に意味のある角だけを検出して
/// LineRenderer/Cylinderでネオン風に光らせるスクリプト。
/// (NeonOutlineGenerator.cs を、複数ブロック統合なし・移動&伸縮対応にした単体ブロック版)
///
/// 【今回のポイント】ブロックがローカルX/Y/Z軸方向に非均一スケール(=伸縮)される場合、
/// 「辺の長さ」は伸縮に追従して変わってよいが、「ネオンの太さ」は伸縮の影響を受けず
/// 常に一定のワールド単位を保ちたい、という要件に対応している。
///
/// なぜ単純な親子関係(Transform階層)だけでは解決できないか:
/// Unityでは、回転を持つ子オブジェクトの上流に「非均一スケールを持つ祖先」が居ると、
/// その階層の見た目には必ず歪み(シアー)が発生する(Transformが回転+均一スケールしか
/// 厳密には表現できない仕組み上の制約)。そのため、ブロックを直方体的に伸縮させながら
/// 円柱(Cylinder)の太さだけを完全に一定に保つには、「太さを描画するフレーム」自体が
/// ブロックのスケールを一切引き継がないようにする必要がある。
///
/// 解決方法(本スクリプトの設計):
/// 1. 輪郭オブジェクトのルート(_outlineRoot)は、ブロックの子にはせず、独立した
///    GameObjectとして用意する。
/// 2. 毎フレーム(LateUpdate)、_outlineRootの「位置」と「回転」だけをブロックの現在の
///    ワールド位置・回転に同期する。スケールは常に(1,1,1)のまま一切同期しない。
///    これにより_outlineRoot配下は「歪みのない剛体フレーム」になる。
/// 3. 各辺の両端は、ブロックのメッシュローカル座標から
///    meshTransform.TransformPoint()で毎フレームワールド座標を計算する。
///    ここでブロックの現在のスケール(伸縮)がそのまま反映されるため、
///    辺の長さは伸縮に正しく追従する。
/// 4. そのワールド座標を、歪みのない_outlineRootのローカル座標へ変換してから
///    LineRenderer/Cylinderに渡す。太さ(lineWidth)はこの歪みのないフレーム内で
///    一定値として扱われるため、ブロックがどれだけ非均一にスケールされても
///    常に同じワールド単位の太さを保つ。
///
/// 【重要】この仕組み上、輪郭の「位置・長さ」を常に最新の状態に保つために
/// LateUpdate()で毎フレーム再計算を行う(エッジ数に比例した軽い処理)。
/// 単体ブロック・剛体移動+軸方向の伸縮のみを前提にしているため、
/// メッシュの頂点そのものが変形する場合(頂点アニメーション等)は対象外。
/// その場合は形が変わるたびにGenerate()を呼び直すこと(辺の構成自体は再計算される)。
///
/// 使い方:
/// 1. 輪郭を光らせたい単体ブロック(MeshFilterを持つオブジェクト、またはその親)にアタッチする。
///    MeshFilterが自分自身に無い場合は子を自動検索するが、確実にしたい場合は
///    targetMeshFilterに直接ドラッグ&ドロップしておくこと。
/// 2. Neon MaterialにEmissionを有効化したマテリアルを割り当てる。
/// 3. 再生 or インスペクタの右クリックメニューから "Generate Neon Outline" を実行。
/// 4. 以後はブロックが移動・回転・伸縮(軸方向の非均一スケール含む)しても、
///    辺の長さは追従し、太さは一定のまま自動的に更新される。
/// </summary>
[DisallowMultipleComponent]
public class NeonOutlineSingleBlock : MonoBehaviour
{
    [Header("対象メッシュ")]
    [Tooltip("輪郭を計算する対象のMeshFilter。未設定の場合、まず自分自身→次に子の順で自動検索する。" +
             "確実に特定のメッシュを対象にしたい場合は直接指定すること。")]
    public MeshFilter targetMeshFilter;

    [Header("検出設定")]
    [Tooltip("ONの場合、対象メッシュのローカル座標での実寸から自動的に許容誤差を計算する。" +
             "メッシュ自体の作り(細かさ)が変わらない限り通常はONのままで良い")]
    public bool autoCalculatePositionEpsilon = true;

    [Tooltip("autoCalculatePositionEpsilonがONの場合、メッシュの最小実寸(ローカル座標)に対してこの割合" +
             "(例:0.001=0.1%)を許容誤差として使う。OFFの場合はFixed Position Epsilonの値をそのまま使う")]
    [Range(0.00001f, 0.01f)]
    public float relativeEpsilonRatio = 0.001f;

    [Tooltip("autoCalculatePositionEpsilonがOFFの場合に使う固定の許容誤差(メッシュのローカル単位)")]
    public float fixedPositionEpsilon = 0.0001f;

    [Tooltip("2つの面が「同一平面」とみなす法線の角度差(度)。これより小さければ平面分割線として除外する")]
    [Range(0.1f, 10f)]
    public float coplanarAngleThreshold = 1.0f;

    [Header("見た目設定")]
    [Tooltip("Billboard: 軽量だが断面は常にカメラ向きの平らな帯(LineRenderer・簡易版)。\n" +
             "Cylinder: 断面が本当に円形の3Dメッシュ(重いが正確、近づいたり真横から見ても破綻しない)。")]
    public NeonRenderMode renderMode = NeonRenderMode.Billboard;
    public Material neonMaterial;

    [Tooltip("輪郭の太さ(常に一定のワールド単位)。ブロックが軸方向に非均一スケール(伸縮)されても、" +
             "太さはこの値のまま変わらない(辺の長さの方はブロックの伸縮に追従して変わる)。")]
    public float lineWidth = 0.03f;
    public Color neonColor = Color.cyan;

    [Tooltip("ONにすると、CylinderモードでneonColorをマテリアルの発光色(_EmissionColor)にも反映する。\n" +
             "OFF(デフォルト)の場合、マテリアル側にあらかじめ設定したEmission(多くの場合HDRな明るい色)を" +
             "上書きせずそのまま使う。Bloomで光らせたい場合は基本OFFのままで良い。")]
    public bool tintEmissionWithNeonColor = false;

    [Tooltip("tintEmissionWithNeonColorがONの場合に使う発光強度(HDR倍率)。")]
    [Range(0f, 10f)]
    public float emissionIntensity = 2f;

    [Tooltip("Cylinderモード時、複数のエッジが集まる角(頂点)に球を置いて継ぎ目を覆うか。")]
    public bool addJointSpheres = true;

    /// <summary>ネオン線の描画方式。</summary>
    public enum NeonRenderMode
    {
        /// <summary>LineRendererによる軽量な平らな帯(ビルボード)。</summary>
        Billboard,
        /// <summary>円柱プリミティブによる本物の3Dチューブ。</summary>
        Cylinder
    }

    [Header("自動実行")]
    [Tooltip("Playモード開始時に自動生成するか")]
    public bool generateOnStart = true;

    [Header("デバッグ")]
    [Tooltip("ONにすると、メッシュ情報と判定理由をConsoleに詳細出力する(確認時のみON推奨)")]
    public bool verboseDebugLog = false;

    // _outlineRootはブロックの子ではなく独立したオブジェクトなので、スクリプトの再コンパイル等を
    // 挟んでも参照を失わないようにSerializeFieldで保持しておく(Clear()で確実に破棄するため)。
    [SerializeField, HideInInspector]
    private Transform _outlineRoot;

    private const string OutlineRootNamePrefix = "_NeonOutlineRoot_";

    private MeshFilter _meshFilter;

    /// <summary>
    /// 1本の辺の描画インスタンス(LineRenderer版 or Cylinder版)と、
    /// その元になるメッシュローカル座標を保持する。LateUpdateで毎フレーム参照する。
    /// </summary>
    private class OutlineEdgeInstance
    {
        public Transform transform;       // Cylinderモード: 円柱自体のTransform / Billboardモード: 線オブジェクトのTransform
        public LineRenderer lineRenderer; // Billboardモードのみ使用(Cylinderモードではnull)
        public Vector3 localA;
        public Vector3 localB;
    }

    /// <summary>
    /// ジョイント球1個の描画インスタンスと、元になるメッシュローカル座標を保持する。
    /// </summary>
    private class OutlineJointInstance
    {
        public Transform transform;
        public Vector3 localPosition;
    }

    private List<OutlineEdgeInstance> _edgeInstances;
    private List<OutlineJointInstance> _jointInstances;

    private void Start()
    {
        WarnIfStatic();

        if (generateOnStart)
        {
            Generate();
        }
    }

    /// <summary>
    /// 毎フレーム、_outlineRootの位置・回転をブロックに同期し、各辺・各ジョイントの
    /// 現在のワールド座標(=伸縮を反映した座標)を計算して描画オブジェクトに反映する。
    /// </summary>
    private void LateUpdate()
    {
        RefreshTransforms();
    }

    /// <summary>
    /// 対象ブロックにStaticフラグが付いている場合に警告を出す。
    /// このスクリプトは「動く・伸縮するブロック」前提のため、Staticのまま動かすと
    /// Static Batchingの仕組み上、見た目が破綻する可能性がある。
    /// </summary>
    private void WarnIfStatic()
    {
        MeshFilter mf = ResolveMeshFilter();
        if (mf != null && mf.gameObject.isStatic)
        {
            Debug.LogWarning($"[NeonOutlineSingleBlock] '{mf.gameObject.name}' はStaticフラグがONです。" +
                "このスクリプトは動く・伸縮するブロックを想定しているため、StaticはOFFにしてください" +
                "(Staticのまま実行時にTransformを動かすと、Static Batchingの仕組み上、見た目が破綻する場合があります)。");
        }
    }

    /// <summary>
    /// 外側エッジ・有意な角を検出し、ネオン線を生成するメイン処理。
    /// 辺の構成(どの辺を表示するか)はメッシュのローカル座標のみから一度だけ計算する
    /// (この構成自体はブロックの伸縮では変化しない)。
    /// 実際の表示位置・長さ・向きはLateUpdate()で毎フレーム更新される。
    /// </summary>
    [ContextMenu("Generate Neon Outline")]
    public void Generate()
    {
        Clear();

        MeshFilter mf = ResolveMeshFilter();
        if (mf == null)
        {
            Debug.LogWarning("[NeonOutlineSingleBlock] 対象のMeshFilterが見つかりませんでした。" +
                "targetMeshFilterを指定するか、このスクリプトと同じオブジェクトか子にMeshFilterを置いてください。");
            return;
        }

        Mesh mesh = mf.sharedMesh;
        if (mesh == null)
        {
            Debug.LogWarning($"[NeonOutlineSingleBlock] '{mf.gameObject.name}': sharedMeshがnullです。");
            return;
        }

        // 動く/伸縮するブロックがStatic Batching済みの状態でメッシュを読むと、結合済みメッシュ全体を
        // 読んでしまっている可能性が高い(=Staticのまま動かしてしまっているケース)。
        Renderer rend = mf.GetComponent<Renderer>();
        if (rend != null && rend.isPartOfStaticBatch)
        {
            Debug.LogError($"[NeonOutlineSingleBlock] '{mf.gameObject.name}' はStatic Batching済みのため、" +
                "MeshFilter.sharedMeshから正しいメッシュ情報を取得できません。" +
                "このスクリプトは動く・伸縮するブロック用のため、対象オブジェクトのStaticフラグをOFFにしてから再実行してください。");
            return;
        }

        _meshFilter = mf;

        Vector3[] localVerts = mesh.vertices;
        int[] tris = mesh.triangles;

        float epsilon = ResolveEpsilon(localVerts);

        // edgeNormals: エッジキーごとに、そのエッジを持つ三角形の法線(ローカル空間)を記録する
        Dictionary<EdgeKey, List<Vector3>> edgeNormals = new Dictionary<EdgeKey, List<Vector3>>();
        Dictionary<EdgeKey, (Vector3 a, Vector3 b)> edgePositions = new Dictionary<EdgeKey, (Vector3, Vector3)>();

        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 p0 = localVerts[tris[i]];
            Vector3 p1 = localVerts[tris[i + 1]];
            Vector3 p2 = localVerts[tris[i + 2]];

            // この三角形の法線(ローカル空間)
            Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0).normalized;

            AddEdge(edgeNormals, edgePositions, p0, p1, normal, epsilon);
            AddEdge(edgeNormals, edgePositions, p1, p2, normal, epsilon);
            AddEdge(edgeNormals, edgePositions, p2, p0, normal, epsilon);
        }

        // 判定: 各エッジキーについて「表示すべきか」を決める
        List<(Vector3 a, Vector3 b)> outlineEdges = new List<(Vector3, Vector3)>();
        int shownCount = 0, hiddenCoplanar = 0, hiddenOther = 0;

        foreach (var kvp in edgeNormals)
        {
            bool show = ShouldShowEdge(kvp.Value, out string reason);

            if (show)
            {
                outlineEdges.Add(edgePositions[kvp.Key]);
                shownCount++;
            }
            else if (reason.StartsWith("corner"))
            {
                hiddenCoplanar++;
            }
            else
            {
                hiddenOther++;
            }

            if (verboseDebugLog && kvp.Value.Count >= 2)
            {
                var (pa, pb) = edgePositions[kvp.Key];
                Debug.Log($"[EdgeDebug] local=({pa})-({pb}) count={kvp.Value.Count} show={show} reason={reason}");
            }
        }

        Debug.Log($"[NeonOutlineSingleBlock] '{mf.gameObject.name}' 三角形数={tris.Length / 3}, " +
                  $"集計後エッジ数={edgeNormals.Count}, 表示={shownCount}, " +
                  $"非表示(同一平面の分割線)={hiddenCoplanar}, 非表示(その他/非マニフォールド)={hiddenOther}");

        CreateOutlineRoot(mf.transform);

        _edgeInstances = new List<OutlineEdgeInstance>();
        _jointInstances = new List<OutlineJointInstance>();

        foreach (var edge in outlineEdges)
        {
            CreateLine(edge.a, edge.b);
        }

        if (renderMode == NeonRenderMode.Cylinder && addJointSpheres)
        {
            CreateJointSpheres(outlineEdges, epsilon);
        }

        // 生成直後に1回、現在のブロックの状態(位置・回転・伸縮)に合わせて
        // 正しい見た目に反映しておく(次のLateUpdateを待たずに済むようにするため)。
        RefreshTransforms();
    }

    /// <summary>
    /// _outlineRootの位置・回転をブロックの現在のワールド位置・回転に同期し、
    /// 各辺・各ジョイントの現在のワールド座標(伸縮を反映)を、歪みのない
    /// _outlineRootのローカル座標に変換してから描画オブジェクトへ反映する。
    /// </summary>
    private void RefreshTransforms()
    {
        if (_outlineRoot == null || _meshFilter == null || _edgeInstances == null)
            return;

        Transform meshTransform = _meshFilter.transform;

        // _outlineRootは「位置」と「回転」だけをブロックに同期し、スケールは常に(1,1,1)のまま。
        // これにより_outlineRoot配下は、ブロックがどれだけ非均一にスケール(伸縮)されても
        // 歪まない「剛体フレーム」として機能する。
        _outlineRoot.position = meshTransform.position;
        _outlineRoot.rotation = meshTransform.rotation;

        foreach (OutlineEdgeInstance entry in _edgeInstances)
        {
            Vector3 worldA = meshTransform.TransformPoint(entry.localA);
            Vector3 worldB = meshTransform.TransformPoint(entry.localB);

            // ワールド座標→_outlineRootのローカル座標(歪みのない剛体フレーム)に変換する。
            // ここでのa,b間の距離は、ブロックの現在のスケール(伸縮)を正しく反映した「本当の長さ」になる。
            Vector3 a = _outlineRoot.InverseTransformPoint(worldA);
            Vector3 b = _outlineRoot.InverseTransformPoint(worldB);

            UpdateEdgeTransform(entry, a, b);
        }

        foreach (OutlineJointInstance joint in _jointInstances)
        {
            Vector3 worldP = meshTransform.TransformPoint(joint.localPosition);
            joint.transform.localPosition = _outlineRoot.InverseTransformPoint(worldP);
        }
    }

    /// <summary>
    /// 1本の辺の描画インスタンスを、現在の(歪みのないフレームでの)両端座標a, bに合わせて更新する。
    /// </summary>
    private void UpdateEdgeTransform(OutlineEdgeInstance entry, Vector3 a, Vector3 b)
    {
        if (entry.lineRenderer != null)
        {
            // Billboardモード: LineRendererのuseWorldSpace=falseなので、
            // 「歪みのないフレーム」のローカル座標をそのまま渡せばよい。
            entry.lineRenderer.SetPosition(0, a);
            entry.lineRenderer.SetPosition(1, b);
            return;
        }

        // Cylinderモード
        Vector3 direction = b - a;
        float length = direction.magnitude;
        if (length < 1e-6f)
        {
            // 長さがほぼ0になった場合は見えないようにする(縮退対策)
            entry.transform.localScale = Vector3.zero;
            return;
        }

        Vector3 mid = (a + b) * 0.5f;
        entry.transform.localPosition = mid;
        entry.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);

        // 標準のCylinderプリミティブは「直径1・高さ2(半径0.5、y=-1〜1)」の形状なので、
        // それに合わせてスケールを調整する。X/ZのlineWidthは歪みのないフレーム内の値なので、
        // ブロックの伸縮の影響を受けず常に一定の太さになる。
        entry.transform.localScale = new Vector3(lineWidth, length * 0.5f, lineWidth);
    }

    /// <summary>
    /// 対象のMeshFilterを解決する。
    /// targetMeshFilterが指定されていればそれを使う。未指定の場合、まず自分自身を探し、
    /// 無ければ子階層から(生成済みのネオン輪郭自体は除外して)探す。
    /// </summary>
    private MeshFilter ResolveMeshFilter()
    {
        if (targetMeshFilter != null)
            return targetMeshFilter;

        MeshFilter self = GetComponent<MeshFilter>();
        if (self != null)
            return self;

        MeshFilter[] children = GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter mf in children)
        {
            if (mf.GetComponent<LineRenderer>() != null)
                continue; // 念のため、Billboard版の線オブジェクトを誤検出しないようにする

            return mf;
        }

        return null;
    }

    /// <summary>
    /// 頂点配列(ローカル空間)からAABBのサイズを計算する。
    /// </summary>
    private static Vector3 ComputeLocalBoundsSize(Vector3[] verts)
    {
        if (verts == null || verts.Length == 0) return Vector3.zero;

        Vector3 min = verts[0];
        Vector3 max = verts[0];
        for (int i = 1; i < verts.Length; i++)
        {
            min = Vector3.Min(min, verts[i]);
            max = Vector3.Max(max, verts[i]);
        }
        return max - min;
    }

    /// <summary>
    /// 位置の許容誤差(エッジの同一性判定に使う)を解決する。
    /// メッシュのローカル座標における実寸を基準にする(ワールドスケールには依存しない)。
    /// </summary>
    private float ResolveEpsilon(Vector3[] localVerts)
    {
        if (!autoCalculatePositionEpsilon)
            return fixedPositionEpsilon;

        Vector3 size = ComputeLocalBoundsSize(localVerts);
        float minSize = float.MaxValue;
        foreach (float s in new[] { Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z) })
        {
            if (s > 0.000001f && s < minSize)
            {
                minSize = s;
            }
        }

        float resolved = (minSize < float.MaxValue) ? minSize * relativeEpsilonRatio : fixedPositionEpsilon;

        if (verboseDebugLog)
        {
            Debug.Log($"[NeonOutlineSingleBlock] 自動計算したpositionEpsilon = {resolved} " +
                      $"(メッシュのローカル実寸の最小値={minSize} × 比率{relativeEpsilonRatio})");
        }

        return resolved;
    }

    /// <summary>
    /// エッジの出現記録(法線のリスト)から、ネオン表示すべきかどうかを判定する。
    /// </summary>
    private bool ShouldShowEdge(List<Vector3> normals, out string reason)
    {
        // 1回しか出現しない = 同一メッシュ内のどの面とも共有されていない外側の辺(オープンメッシュの境界)
        if (normals.Count == 1)
        {
            reason = "boundary";
            return true;
        }

        // 2回出現する = 隣接する2つの三角形が辺を共有している
        // 法線が大きく異なれば「立体としての本当の角」 → 表示する
        // 法線がほぼ同じなら「同一平面上のポリゴン分割線」 → 表示しない
        if (normals.Count == 2)
        {
            float angle = Vector3.Angle(normals[0], normals[1]);
            reason = $"corner angle={angle:F2}";
            return angle > coplanarAngleThreshold;
        }

        // 3回以上出現するのは通常想定外(非マニフォールドな形状)。安全側として非表示にする。
        reason = $"count={normals.Count}(non-manifold)";
        return false;
    }

    private void AddEdge(
        Dictionary<EdgeKey, List<Vector3>> edgeNormals,
        Dictionary<EdgeKey, (Vector3, Vector3)> edgePositions,
        Vector3 a, Vector3 b, Vector3 normal, float epsilon)
    {
        EdgeKey key = EdgeKey.FromPoints(a, b, epsilon);

        if (!edgeNormals.TryGetValue(key, out List<Vector3> list))
        {
            list = new List<Vector3>();
            edgeNormals[key] = list;
            edgePositions[key] = (a, b);
        }

        list.Add(normal);
    }

    /// <summary>
    /// 既存の生成物を削除する。再生成前や手動クリーンアップ用。
    /// </summary>
    [ContextMenu("Clear Neon Outline")]
    public void Clear()
    {
        if (_outlineRoot != null)
        {
            if (Application.isPlaying)
                Destroy(_outlineRoot.gameObject);
            else
                DestroyImmediate(_outlineRoot.gameObject);
        }

        _outlineRoot = null;
        _meshFilter = null;
        _edgeInstances = null;
        _jointInstances = null;
    }

    /// <summary>
    /// 輪郭オブジェクトのルートを作成する。
    ///
    /// 【重要】あえてmeshTransformの子にしない。
    /// ブロック(meshTransform)が非均一スケール(軸方向の伸縮)をされる場合、回転を持つ
    /// 子オブジェクトがその下にあるとUnityの仕組み上「歪み(シアー)」が発生してしまい、
    /// 太さが伸縮方向に応じて潰れたり伸びたりしてしまう。
    /// これを避けるため、_outlineRootは独立したGameObjectとして用意し、スケールは常に
    /// (1,1,1)のまま、位置と回転だけをRefreshTransforms()で毎フレームブロックに同期する
    /// (=歪みのない剛体フレームとして使う)。
    /// </summary>
    private void CreateOutlineRoot(Transform meshTransform)
    {
        GameObject rootObj = new GameObject(OutlineRootNamePrefix + meshTransform.gameObject.name);
        rootObj.transform.SetParent(null, worldPositionStays: false);
        rootObj.transform.position = meshTransform.position;
        rootObj.transform.rotation = meshTransform.rotation;
        rootObj.transform.localScale = Vector3.one;

        _outlineRoot = rootObj.transform;
    }

    /// <summary>
    /// 1本のネオン線を生成する。renderModeに応じて描画方式を切り替える。
    /// a, bは対象メッシュのローカル座標(初期登録用。以後はLateUpdateで再計算される)。
    /// </summary>
    private void CreateLine(Vector3 a, Vector3 b)
    {
        switch (renderMode)
        {
            case NeonRenderMode.Cylinder:
                CreateCylinderEdge(a, b);
                break;

            case NeonRenderMode.Billboard:
            default:
                CreateLineRendererEdge(a, b);
                break;
        }
    }

    /// <summary>
    /// 【簡易版】LineRendererで1本のネオン線を生成する。
    /// useWorldSpace = falseにし、_outlineRoot配下(=歪みのない剛体フレーム)に
    /// 配置することで、太さ(width)が常に一定のワールド単位を保つようにする。
    /// 実際の座標(SetPosition)はRefreshTransformsが毎フレーム更新する。
    /// </summary>
    private void CreateLineRendererEdge(Vector3 localA, Vector3 localB)
    {
        GameObject lineObj = new GameObject("NeonEdge");
        lineObj.transform.SetParent(_outlineRoot, worldPositionStays: false);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.positionCount = 2;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 2;

        if (neonMaterial != null)
        {
            lr.material = neonMaterial;
        }
        lr.startColor = neonColor;
        lr.endColor = neonColor;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        _edgeInstances.Add(new OutlineEdgeInstance
        {
            transform = lineObj.transform,
            lineRenderer = lr,
            localA = localA,
            localB = localB
        });
    }

    /// <summary>
    /// 【本物版】円柱プリミティブで1本のネオン線を生成する。
    /// _outlineRoot(=歪みのない剛体フレーム)の子として配置することで、
    /// ブロックが非均一スケール(伸縮)されても太さが歪まないようにする。
    /// 実際のlocalPosition/localRotation/localScaleはRefreshTransformsが毎フレーム更新する。
    /// </summary>
    private void CreateCylinderEdge(Vector3 localA, Vector3 localB)
    {
        GameObject cylObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylObj.name = "NeonEdge";

        // CreatePrimitiveはデフォルトでCapsuleColliderを付けてくるが、
        // ネオン線に当たり判定は不要なので削除する。
        Collider col = cylObj.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying)
                Destroy(col);
            else
                DestroyImmediate(col);
        }

        cylObj.transform.SetParent(_outlineRoot, worldPositionStays: false);

        MeshRenderer mr = cylObj.GetComponent<MeshRenderer>();
        ApplyNeonMaterialAndColor(mr);

        _edgeInstances.Add(new OutlineEdgeInstance
        {
            transform = cylObj.transform,
            lineRenderer = null,
            localA = localA,
            localB = localB
        });
    }

    /// <summary>
    /// Cylinderモード用: 複数のエッジが集まる角(頂点)に、円柱の平らな端面(切り口)を
    /// 覆い隠す小さな球を配置する。1本しかエッジが集まっていない末端には配置しない。
    /// </summary>
    private void CreateJointSpheres(List<(Vector3 a, Vector3 b)> edges, float epsilon)
    {
        Dictionary<(long, long, long), (Vector3 position, int degree)> vertexInfo =
            new Dictionary<(long, long, long), (Vector3, int)>();

        void Register(Vector3 p)
        {
            (long, long, long) key = QuantizePosition(p, epsilon);
            if (vertexInfo.TryGetValue(key, out var info))
            {
                vertexInfo[key] = (info.position, info.degree + 1);
            }
            else
            {
                vertexInfo[key] = (p, 1);
            }
        }

        foreach (var edge in edges)
        {
            Register(edge.a);
            Register(edge.b);
        }

        int jointCount = 0;
        foreach (var kvp in vertexInfo)
        {
            if (kvp.Value.degree < 2)
                continue; // 末端(行き止まり)はスキップ

            CreateJointSphere(kvp.Value.position);
            jointCount++;
        }

        if (verboseDebugLog)
        {
            Debug.Log($"[NeonOutlineSingleBlock] ジョイント球を{jointCount}個生成しました(次数2以上の頂点のみ)。");
        }
    }

    /// <summary>
    /// EdgeKey.Quantizeと同じロジックで座標をグリッド単位に量子化する。
    /// </summary>
    private static (long, long, long) QuantizePosition(Vector3 p, float epsilon)
    {
        float inv = 1.0f / epsilon;
        long qx = (long)Mathf.Round(p.x * inv);
        long qy = (long)Mathf.Round(p.y * inv);
        long qz = (long)Mathf.Round(p.z * inv);
        return (qx, qy, qz);
    }

    /// <summary>
    /// 1個のジョイント球(継ぎ目を覆う小さな球)を生成する。
    /// localPositionは対象メッシュのローカル座標(初期登録用。以後はLateUpdateで再計算される)。
    /// サイズ(lineWidth)は歪みのない剛体フレーム内の値なので、ブロックの伸縮の影響を受けない。
    /// </summary>
    private void CreateJointSphere(Vector3 localPosition)
    {
        GameObject sphereObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereObj.name = "NeonJoint";

        Collider col = sphereObj.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying)
                Destroy(col);
            else
                DestroyImmediate(col);
        }

        sphereObj.transform.SetParent(_outlineRoot, worldPositionStays: false);

        // 標準のSphereプリミティブは直径1(半径0.5)。円柱の太さ(lineWidth=直径)に合わせる。
        sphereObj.transform.localScale = new Vector3(lineWidth, lineWidth, lineWidth);

        MeshRenderer mr = sphereObj.GetComponent<MeshRenderer>();
        ApplyNeonMaterialAndColor(mr);

        _jointInstances.Add(new OutlineJointInstance
        {
            transform = sphereObj.transform,
            localPosition = localPosition
        });
    }

    /// <summary>
    /// CylinderモードのネオンオブジェクトにneonMaterialと色を適用する共通処理。
    /// sharedMaterialを直接書き換えると全エッジ/全ジョイントが同じマテリアルインスタンスを
    /// 共有して色が連動してしまうため、MaterialPropertyBlockで個体ごとに色を当てる。
    /// </summary>
    private void ApplyNeonMaterialAndColor(MeshRenderer mr)
    {
        if (neonMaterial == null)
        {
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return;
        }

        mr.sharedMaterial = neonMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        mr.GetPropertyBlock(mpb);
        if (neonMaterial.HasProperty("_Color"))
            mpb.SetColor("_Color", neonColor);
        if (neonMaterial.HasProperty("_BaseColor"))
            mpb.SetColor("_BaseColor", neonColor);

        if (tintEmissionWithNeonColor && neonMaterial.HasProperty("_EmissionColor"))
        {
            Color hdrEmission = neonColor * emissionIntensity;
            mpb.SetColor("_EmissionColor", hdrEmission);
        }

        mr.SetPropertyBlock(mpb);
    }

    /// <summary>
    /// エッジを「向きを無視した両端座標」で一意に識別するためのキー。
    /// 浮動小数点誤差を吸収するため、座標をグリッド単位で量子化してから比較する。
    /// </summary>
    private readonly struct EdgeKey
    {
        private readonly long _x1, _y1, _z1, _x2, _y2, _z2;

        private EdgeKey(long x1, long y1, long z1, long x2, long y2, long z2)
        {
            _x1 = x1; _y1 = y1; _z1 = z1;
            _x2 = x2; _y2 = y2; _z2 = z2;
        }

        public static EdgeKey FromPoints(Vector3 a, Vector3 b, float epsilon)
        {
            (long qx1, long qy1, long qz1) = Quantize(a, epsilon);
            (long qx2, long qy2, long qz2) = Quantize(b, epsilon);

            // 向き(A→B / B→A)の違いを無視するため、必ず小さい方を先頭にソートする
            if (ComparePoint(qx1, qy1, qz1, qx2, qy2, qz2) <= 0)
            {
                return new EdgeKey(qx1, qy1, qz1, qx2, qy2, qz2);
            }
            else
            {
                return new EdgeKey(qx2, qy2, qz2, qx1, qy1, qz1);
            }
        }

        private static (long, long, long) Quantize(Vector3 p, float epsilon)
        {
            float inv = 1.0f / epsilon;
            long qx = (long)Mathf.Round(p.x * inv);
            long qy = (long)Mathf.Round(p.y * inv);
            long qz = (long)Mathf.Round(p.z * inv);
            return (qx, qy, qz);
        }

        private static int ComparePoint(long x1, long y1, long z1, long x2, long y2, long z2)
        {
            if (x1 != x2) return x1.CompareTo(x2);
            if (y1 != y2) return y1.CompareTo(y2);
            return z1.CompareTo(z2);
        }

        public override bool Equals(object obj)
        {
            if (obj is not EdgeKey other) return false;
            return _x1 == other._x1 && _y1 == other._y1 && _z1 == other._z1
                && _x2 == other._x2 && _y2 == other._y2 && _z2 == other._z2;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + _x1.GetHashCode();
                hash = hash * 31 + _y1.GetHashCode();
                hash = hash * 31 + _z1.GetHashCode();
                hash = hash * 31 + _x2.GetHashCode();
                hash = hash * 31 + _y2.GetHashCode();
                hash = hash * 31 + _z2.GetHashCode();
                return hash;
            }
        }
    }
}