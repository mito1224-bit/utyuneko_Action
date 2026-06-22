using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 子オブジェクトとして配置されたブロック群(立方体・三角柱など任意の形状)を走査し、
/// 「他のブロックと共有していない外側の輪郭」と「同一メッシュ内の見た目に意味のある角」だけを
/// 検出してLineRendererでネオン風に光らせるスクリプト。
///
/// 立方体決め打ちではなく、各ブロックのMeshFilter.sharedMeshの実データ(三角形)から
/// 辺を抽出するため、キューブ・三角柱(対角カットしたプリズム)など形状を問わず動作する。
///
/// 判定の考え方:
/// 1. 全ブロックの全三角形から辺を列挙し、「ワールド座標キー」+「どの面の法線を持つか」を記録する
/// 2. 同じ位置の辺が何回・どんな法線の組み合わせで出現したかを集計する
///    - 1回だけ出現        → 他のブロックに接していない外側の辺 → 表示する
///    - 同一メッシュ内に2回出現し、法線が異なる(角がある) → 立体としての本当の角 → 表示する
///    - 同一メッシュ内に2回出現し、法線が同じ(同一平面の分割線) → ポリゴン分割用の対角線 → 表示しない
///    - 異なるブロック間で出現(ブロック同士が接している) → 共有面の境界 → 表示しない
///
/// 【重要】Unityの「Static」フラグとStatic Batchingについて:
/// 対象ブロックのInspectorで「Static」にチェックを入れると、Unityは描画最適化のため
/// Static Batchingを行い、同じマテリアルを使う複数のレンダラーのメッシュを
/// 「ワールド座標に変換済みの1つの結合メッシュ(Combined Mesh)」へまとめてしまう。
/// このとき各オブジェクトのMeshFilter.sharedMeshは「結合済みメッシュ全体への参照」に
/// 置き換わり、元の単体メッシュの頂点・三角形情報は実行時にスクリプトから正しく読み取れなくなる
/// (Unity公式ドキュメント・Discussionsでも既知の挙動として説明されている)。
///
/// さらにこのバッチ生成はビルド時だけでなく、Unity Editor上でもPlayモードに入った
/// タイミングで行われるため、「Scene上で事前にStaticへチェックを入れておく」だけでも、
/// Play開始後にこのスクリプトのStart()が読むメッシュ情報は既に壊れている可能性がある。
///
/// この問題を回避するため、本スクリプトは以下の2系統の動作をサポートする:
///   A) 事前ベイクなし(従来通り): Static Batchingの影響を受けない通常のオブジェクトに対して、
///      実行時にMeshFilter.sharedMeshを直接読んで輪郭を計算する。
///   B) 事前ベイクあり(Static対応): Editモード(Play前)でメニューから
///      "Step1: Bake Mesh Data" を実行し、各ブロックの生メッシュ情報(ローカル頂点・三角形)を
///      コンポーネントに保存しておく。実行時はこの保存済みデータのみを使うため、
///      対象ブロックにStaticチェックが入っていてもStatic Batchingの影響を受けない。
///
/// 使い方:
/// 1. Tilemapを持つ各オブジェクト(ブロック群の親)にこのスクリプトをアタッチ
/// 2. Neon MaterialにEmissionを有効化したマテリアルを割り当てる
/// 3. 対象ブロックをStaticにする場合は、必ずPlayする前にEditモードのまま
///    右クリックメニューから "Step1: Bake Mesh Data" を実行しておく
/// 4. 再生 or インスペクタの右クリックメニューから "Step2: Generate Neon Outline" を実行
/// </summary>
[DisallowMultipleComponent]
public class NeonOutlineGenerator : MonoBehaviour
{
    [Header("検出設定")]
    [Tooltip("ONの場合、各ブロックの実寸(ワールド座標換算)から自動的に許容誤差を計算する。" +
             "Scaleが1でも100でも0.01でも自動的に適切な値になるため、通常はONのままで良い")]
    public bool autoCalculatePositionEpsilon = true;

    [Tooltip("autoCalculatePositionEpsilonがONの場合、ブロックの最小実寸に対してこの割合(例:0.001=0.1%)を" +
             "許容誤差として使う。OFFの場合はFixed Position Epsilonの値をそのまま使う")]
    [Range(0.00001f, 0.01f)]
    public float relativeEpsilonRatio = 0.001f;

    [Tooltip("autoCalculatePositionEpsilonがOFFの場合に使う固定の許容誤差(ワールド単位)")]
    public float fixedPositionEpsilon = 0.0001f;

    [Tooltip("2つの面が「同一平面」とみなす法線の角度差(度)。これより小さければ平面分割線として除外する")]
    [Range(0.1f, 10f)]
    public float coplanarAngleThreshold = 1.0f;

    [Header("見た目設定")]
    [Tooltip("Billboard: 軽量だが断面は常にカメラ向きの平らな帯(LineRenderer・簡易版)。\n" +
             "Cylinder: 断面が本当に円形の3Dメッシュ(重いが正確、近づいたり真横から見ても破綻しない)。\n" +
             "ブロック数・エッジ数が多い場合、Cylinderはドローコール/頂点数が増えるため負荷に注意。")]
    public NeonRenderMode renderMode = NeonRenderMode.Billboard;
    public Material neonMaterial;
    public float lineWidth = 0.03f;
    public Color neonColor = Color.cyan;

    [Tooltip("ONにすると、CylinderモードでneonColorをマテリアルの発光色(_EmissionColor)にも反映する。\n" +
             "OFF(デフォルト)の場合、マテリアル側にあらかじめ設定したEmission(多くの場合HDRな明るい色)を" +
             "上書きせずそのまま使う。Bloomで光らせたい場合は基本OFFのままで良い" +
             "(neonColorは普通の色なので、ONにすると逆に発光が暗くなって光らなくなることが多い)。")]
    public bool tintEmissionWithNeonColor = false;

    [Tooltip("tintEmissionWithNeonColorがONの場合に使う発光強度(HDR倍率)。" +
             "Bloomで光らせるには1.0より十分大きい値が必要なことが多い。")]
    [Range(0f, 10f)]
    public float emissionIntensity = 2f;

    [Tooltip("Cylinderモード時、複数のエッジが集まる角(頂点)に球を置いて継ぎ目を覆うか。\n" +
             "OFFにすると、各円柱の平らな端面(切り口)がそのまま角で突き出して見える。")]
    public bool addJointSpheres = true;

    /// <summary>
    /// ネオン線の描画方式。
    /// </summary>
    public enum NeonRenderMode
    {
        /// <summary>LineRendererによる軽量な平らな帯(ビルボード)。エッジ数が多い場合に向く。</summary>
        Billboard,
        /// <summary>円柱プリミティブによる本物の3Dチューブ。近距離・斜め視点でも破綻しない。</summary>
        Cylinder
    }

    [Header("自動実行")]
    [Tooltip("Playモード開始時に自動生成するか")]
    public bool generateOnStart = true;

    [Header("デバッグ")]
    [Tooltip("ONにすると、各ブロックのメッシュ情報と判定理由をConsoleに詳細出力する(確認時のみON推奨)")]
    public bool verboseDebugLog = false;

    // --- Static Batching対応: 事前ベイクしたメッシュ情報 ------------------------------
    // Editモード(Play前)で BakeMeshData() を実行すると、ここに各ブロックの
    // 「ローカル頂点・三角形・対応するTransform」が保存される。
    // 値が入っている場合、実行時(Generate)はこのデータのみを使い、
    // MeshFilter.sharedMeshを直接読まないため、Static Batchingの影響を受けない。
    [SerializeField, HideInInspector]
    private List<BakedBlock> _bakedBlocks = new List<BakedBlock>();

    [System.Serializable]
    private class BakedBlock
    {
        public Transform transform;
        public Vector3[] localVertices;
        public int[] triangles;
    }

    /// <summary>
    /// 事前にベイクされたメッシュデータが保存されているか。
    /// </summary>
    public bool HasBakedMeshData => _bakedBlocks != null && _bakedBlocks.Count > 0;

    private Transform _outlineRoot;
    private const string OutlineRootName = "_NeonOutlineRoot";

    private void Start()
    {
        if (generateOnStart)
        {
            Generate();
        }
    }

    /// <summary>
    /// 【Static対応の本体】Editモード(Play前)で実行する。
    /// 対象ブロックの生メッシュ(ローカル頂点・三角形)をStatic Batchingが走る前に読み取り、
    /// コンポーネントにシリアライズして保存する。
    ///
    /// 一度ベイクしておけば、対象ブロックにStaticチェックを入れてPlayしても、
    /// 実行時はこの保存済みデータのみを使うためメッシュが壊れて見える問題が起きない。
    ///
    /// ブロックの形状やレイアウトを変更した場合は、再度このメニューを実行して
    /// ベイクし直すこと(古いデータが残っていると食い違いの原因になる)。
    /// </summary>
    [ContextMenu("Step1: Bake Mesh Data (Editモードで実行)")]
    public void BakeMeshData()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[NeonOutlineGenerator] Bake処理はPlayする前のEditモードでの実行を推奨します。" +
                "Play中(特にStaticチェック済みオブジェクト)では、既にStatic Batchingにより" +
                "メッシュが結合済みメッシュへ置き換わっている可能性があり、" +
                "そのままBakeすると壊れたデータを保存してしまう恐れがあります。");
        }

        MeshFilter[] meshFilters = CollectBlockMeshFilters();
        if (meshFilters.Length == 0)
        {
            Debug.LogWarning("[NeonOutlineGenerator] Bake対象のブロックが見つかりませんでした。子オブジェクトの構成を確認してください。");
            return;
        }

        _bakedBlocks.Clear();
        int staticBatchWarnCount = 0;

        foreach (MeshFilter mf in meshFilters)
        {
            Mesh mesh = mf.sharedMesh;
            if (mesh == null)
            {
                if (verboseDebugLog)
                    Debug.LogWarning($"[NeonOutlineGenerator] {mf.gameObject.name}: sharedMeshがnullです。スキップします。");
                continue;
            }

            // Renderer.isPartOfStaticBatchは「すでにStatic Batchingが行われたか」を示すフラグ。
            // これがtrueの状態でBakeすると、結合済みメッシュ全体を読んでしまっている可能性が高い。
            Renderer rend = mf.GetComponent<Renderer>();
            if (rend != null && rend.isPartOfStaticBatch)
            {
                staticBatchWarnCount++;
                Debug.LogWarning($"[NeonOutlineGenerator] '{mf.gameObject.name}' は既にStatic Batching済みの状態でした。" +
                    "このタイミングで取得したメッシュ情報は結合済みメッシュ全体を指している可能性があります。" +
                    "Playを停止し、Editモードのまま再度Bakeしてください。");
            }

            // mesh.vertices / mesh.triangles は呼び出すたびに新しい配列のコピーを返すAPIなので、
            // ここで読んだ値はそのまま保存してよい(元のメッシュアセットを変更するわけではない)。
            _bakedBlocks.Add(new BakedBlock
            {
                transform = mf.transform,
                localVertices = mesh.vertices,
                triangles = mesh.triangles
            });
        }

        Debug.Log($"[NeonOutlineGenerator] Bake完了: {_bakedBlocks.Count}ブロック分のメッシュデータを保存しました。" +
                  (staticBatchWarnCount > 0
                      ? $" ※ うち{staticBatchWarnCount}件はStatic Batching済みの状態で取得されたため要再確認です。"
                      : " このデータはStatic Batchingの影響を受けません。") +
                  " ※ シーン(またはPrefab)の保存を忘れずに行ってください。");

        MarkDirtyIfNeeded();
    }

    /// <summary>
    /// ベイク済みデータを削除する。ブロックのレイアウトを変更した後、
    /// 古いデータが残らないようにするためのクリーンアップ用。
    /// </summary>
    [ContextMenu("Clear Baked Mesh Data")]
    public void ClearBakedMeshData()
    {
        _bakedBlocks.Clear();
        MarkDirtyIfNeeded();
        Debug.Log("[NeonOutlineGenerator] ベイク済みメッシュデータをクリアしました。");
    }

    /// <summary>
    /// ベイクデータを変更した直後に呼ぶ。Unityに「このオブジェクトは保存が必要」と明示的に伝える。
    ///
    /// [ContextMenu]からスクリプトでフィールドを書き換えただけでは、Unityが自動的に
    /// 「未保存の変更」として認識してくれない場合がある(特にPrefabインスタンス上での変更)。
    /// これを忘れると、Bakeした直後は動いていても、シーンやPrefabを保存せずに
    /// Unityを閉じたり再生したりした際にベイクデータが消えてしまうことがある。
    /// Editorビルドにのみ関係するため、プレイヤービルドには含まれない(#if UNITY_EDITORで除外)。
    /// </summary>
    private void MarkDirtyIfNeeded()
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);

        // Prefabインスタンス上での変更は、Override(上書き)として明示的に記録する必要がある
        if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this))
        {
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        }

        // シーンに置かれている場合は、シーン自体も「未保存」としてマークする
        if (gameObject.scene.IsValid())
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    /// <summary>
    /// 外側エッジ・有意な角を検出し、ネオン線を生成するメイン処理。
    /// ベイク済みデータがあればそれを使い(Static対応)、なければ従来通り
    /// MeshFilter.sharedMeshから直接読む。
    /// </summary>
    [ContextMenu("Step2: Generate Neon Outline")]
    public void Generate()
    {
        Clear();

        List<BlockGeometrySource> sources = GetBlockGeometrySources();
        if (sources.Count == 0)
        {
            Debug.LogWarning("[NeonOutlineGenerator] ブロックが見つかりませんでした。" +
                "子オブジェクトの構成、またはベイク済みデータの有無を確認してください。");
            return;
        }

        // edgeRecords: エッジキーごとに、そのエッジを持つ三角形の法線(ワールド空間)とブロックIDを記録
        Dictionary<EdgeKey, List<EdgeRecord>> edgeRecords = new Dictionary<EdgeKey, List<EdgeRecord>>();
        Dictionary<EdgeKey, (Vector3 a, Vector3 b)> edgePositions = new Dictionary<EdgeKey, (Vector3, Vector3)>();

        // 動的にpositionEpsilonを決定する: 全ブロックのワールド実寸のうち最小のものを基にする
        float resolvedEpsilon = fixedPositionEpsilon;
        if (autoCalculatePositionEpsilon)
        {
            float minWorldSize = float.MaxValue;
            foreach (BlockGeometrySource src in sources)
            {
                Vector3 localSize = ComputeLocalBoundsSize(src.localVertices);
                Vector3 lossy = src.transform.lossyScale;

                // ワールド座標での実寸(各軸)
                float worldSizeX = Mathf.Abs(localSize.x * lossy.x);
                float worldSizeY = Mathf.Abs(localSize.y * lossy.y);
                float worldSizeZ = Mathf.Abs(localSize.z * lossy.z);

                // 0は無視(平面メッシュなどで1軸が0になる場合があるため)
                foreach (float s in new[] { worldSizeX, worldSizeY, worldSizeZ })
                {
                    if (s > 0.000001f && s < minWorldSize)
                    {
                        minWorldSize = s;
                    }
                }
            }

            if (minWorldSize < float.MaxValue)
            {
                resolvedEpsilon = minWorldSize * relativeEpsilonRatio;
            }

            if (verboseDebugLog)
            {
                Debug.Log($"[NeonOutlineGenerator] 自動計算したpositionEpsilon = {resolvedEpsilon} " +
                          $"(最小ブロック実寸={minWorldSize} × 比率{relativeEpsilonRatio})");
            }
        }

        int blockId = 0;
        foreach (BlockGeometrySource src in sources)
        {
            Transform t = src.transform;
            Vector3[] localVerts = src.localVertices;
            int[] tris = src.triangles;

            if (verboseDebugLog)
            {
                Debug.Log($"[NeonOutlineGenerator] blockId={blockId} name={src.debugName} " +
                          $"頂点数={localVerts.Length} 三角形数={tris.Length / 3} " +
                          $"localScale={t.localScale} lossyScale={t.lossyScale} " +
                          $"source={(src.fromBakedData ? "baked" : "live")}");
            }

            // ローカル→ワールド変換済みの頂点配列を作っておく
            Vector3[] worldVerts = new Vector3[localVerts.Length];
            for (int i = 0; i < localVerts.Length; i++)
            {
                worldVerts[i] = t.TransformPoint(localVerts[i]);
            }

            // 三角形ごとに3辺を列挙
            for (int i = 0; i < tris.Length; i += 3)
            {
                int i0 = tris[i];
                int i1 = tris[i + 1];
                int i2 = tris[i + 2];

                Vector3 p0 = worldVerts[i0];
                Vector3 p1 = worldVerts[i1];
                Vector3 p2 = worldVerts[i2];

                // この三角形の法線(ワールド空間)
                Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0).normalized;

                AddEdge(edgeRecords, edgePositions, p0, p1, normal, blockId, resolvedEpsilon);
                AddEdge(edgeRecords, edgePositions, p1, p2, normal, blockId, resolvedEpsilon);
                AddEdge(edgeRecords, edgePositions, p2, p0, normal, blockId, resolvedEpsilon);
            }

            blockId++;
        }

        // 判定: 各エッジキーについて「表示すべきか」を決める
        List<(Vector3 a, Vector3 b)> outlineEdges = new List<(Vector3, Vector3)>();
        int shownCount = 0, hiddenSameBlockCoplanar = 0, hiddenCrossBlock = 0, hiddenOther = 0;

        foreach (var kvp in edgeRecords)
        {
            EdgeKey key = kvp.Key;
            List<EdgeRecord> records = kvp.Value;

            bool show = ShouldShowEdge(records, out string reason);

            if (show)
            {
                outlineEdges.Add(edgePositions[key]);
                shownCount++;
            }
            else
            {
                if (reason.StartsWith("cross-block")) hiddenCrossBlock++;
                else if (reason.StartsWith("same-block")) hiddenSameBlockCoplanar++;
                else hiddenOther++;
            }

            if (verboseDebugLog && records.Count >= 2)
            {
                var (pa, pb) = edgePositions[key];
                Debug.Log($"[EdgeDebug] pos=({pa})-({pb}) count={records.Count} show={show} reason={reason}");
            }
        }

        Debug.Log($"[NeonOutlineGenerator] ブロック数: {blockId}, 総エッジ数(集計後): {edgeRecords.Count}, " +
                  $"表示={shownCount}, 非表示(ブロック間共有)={hiddenCrossBlock}, 非表示(同一平面)={hiddenSameBlockCoplanar}, 非表示(その他)={hiddenOther}");

        CreateOutlineRoot();
        foreach (var edge in outlineEdges)
        {
            CreateLine(edge.a, edge.b);
        }

        // Cylinderモードでは、角(複数エッジが集まる頂点)に球を置いて
        // 円柱の平らな端面(切り口)が突き出して見える問題を解消する。
        if (renderMode == NeonRenderMode.Cylinder && addJointSpheres)
        {
            CreateJointSpheres(outlineEdges, resolvedEpsilon);
        }
    }

    /// <summary>
    /// 1ブロック分のジオメトリ取得元を表す内部用データ。
    /// ベイク済みデータ・ライブ読み取りのどちらから来たかを問わず、
    /// 以降の処理(Generate本体)は同じコードパスで扱えるようにするための抽象化。
    /// </summary>
    private struct BlockGeometrySource
    {
        public Transform transform;
        public Vector3[] localVertices;
        public int[] triangles;
        public string debugName;
        public bool fromBakedData;
    }

    /// <summary>
    /// 輪郭計算に使うブロックジオメトリの一覧を取得する。
    ///
    /// ・ベイク済みデータ(_bakedBlocks)が存在する場合は、それのみを使う。
    ///   MeshFilter.sharedMeshには一切アクセスしないため、対象ブロックがStatic化されて
    ///   Static Batchingが行われていても影響を受けない。
    ///
    /// ・ベイク済みデータが無い場合は、従来通りMeshFilter.sharedMeshから直接読む(ライブ読み取り)。
    ///   このとき、Static Batching済みのレンダラーを検出した場合は明確な警告を出す
    ///   (この状態で取得したメッシュ情報は壊れている可能性が高いため)。
    /// </summary>
    private List<BlockGeometrySource> GetBlockGeometrySources()
    {
        List<BlockGeometrySource> list = new List<BlockGeometrySource>();

        if (HasBakedMeshData)
        {
            foreach (BakedBlock b in _bakedBlocks)
            {
                if (b.transform == null || b.localVertices == null || b.triangles == null)
                {
                    if (verboseDebugLog)
                        Debug.LogWarning("[NeonOutlineGenerator] ベイク済みデータに無効なエントリがあります(対象オブジェクトが削除された可能性)。スキップします。再Bakeを推奨します。");
                    continue;
                }

                list.Add(new BlockGeometrySource
                {
                    transform = b.transform,
                    localVertices = b.localVertices,
                    triangles = b.triangles,
                    debugName = b.transform.name,
                    fromBakedData = true
                });
            }

            if (verboseDebugLog)
                Debug.Log($"[NeonOutlineGenerator] ベイク済みメッシュデータを使用します(ブロック数={list.Count})。Static Batchingの影響を受けません。");

            return list;
        }

        // --- ベイクデータが無い場合: ライブでMeshFilterから読む(従来動作) ---
        MeshFilter[] meshFilters = CollectBlockMeshFilters();
        foreach (MeshFilter mf in meshFilters)
        {
            Mesh mesh = mf.sharedMesh;
            if (mesh == null)
            {
                if (verboseDebugLog)
                    Debug.LogWarning($"[NeonOutlineGenerator] {mf.gameObject.name}: sharedMeshがnullです。スキップします。");
                continue;
            }

            Renderer rend = mf.GetComponent<Renderer>();
            if (rend != null && rend.isPartOfStaticBatch)
            {
                Debug.LogError($"[NeonOutlineGenerator] '{mf.gameObject.name}' はStatic Batching済みのため、" +
                    "MeshFilter.sharedMeshから個別ブロックの正しいメッシュ情報を取得できません" +
                    "(Static Batchingで結合された全体メッシュが返ってきています)。" +
                    "Editモード(Play前)で「Step1: Bake Mesh Data」を実行してから、再度お試しください。" +
                    "このまま続行するとネオンの輪郭が破綻して表示されます。");
            }

            list.Add(new BlockGeometrySource
            {
                transform = mf.transform,
                localVertices = mesh.vertices,
                triangles = mesh.triangles,
                debugName = mf.gameObject.name,
                fromBakedData = false
            });
        }

        return list;
    }

    /// <summary>
    /// 頂点配列(ローカル空間)からAABBのサイズを計算する。
    /// Mesh.boundsに依存せず、ベイク済みデータ(Meshオブジェクトを保持していない)でも使えるようにするため。
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
    /// エッジの出現記録から、ネオン表示すべきかどうかを判定する。
    /// reasonには判定理由を返す(デバッグ用)。
    /// </summary>
    private bool ShouldShowEdge(List<EdgeRecord> records, out string reason)
    {
        // 1回しか出現しない = どのブロックとも、同一メッシュ内のどの面とも共有されていない外側の辺
        if (records.Count == 1)
        {
            reason = "boundary";
            return true;
        }

        // 2回出現する場合
        if (records.Count == 2)
        {
            EdgeRecord r0 = records[0];
            EdgeRecord r1 = records[1];

            // 異なるブロック間で共有されているなら、接合面の境界 → 表示しない
            if (r0.blockId != r1.blockId)
            {
                reason = "cross-block";
                return false;
            }

            // 同一ブロック内で2回出現 = 隣接する2つの三角形が辺を共有している
            // 法線が大きく異なれば「立体としての本当の角」 → 表示する
            // 法線がほぼ同じなら「同一平面上のポリゴン分割線」 → 表示しない
            float angle = Vector3.Angle(r0.normal, r1.normal);
            reason = $"same-block angle={angle:F2}";
            return angle > coplanarAngleThreshold;
        }

        // 3回以上出現するのは通常想定外(非マニフォールドな形状)だが、
        // 安全側として「表示しない」にしておく(内部に埋もれている可能性が高いため)
        reason = $"count={records.Count}(non-manifold)";
        return false;
    }

    private void AddEdge(
        Dictionary<EdgeKey, List<EdgeRecord>> edgeRecords,
        Dictionary<EdgeKey, (Vector3, Vector3)> edgePositions,
        Vector3 a, Vector3 b, Vector3 normal, int blockId, float epsilon)
    {
        EdgeKey key = EdgeKey.FromPoints(a, b, epsilon);

        if (!edgeRecords.TryGetValue(key, out List<EdgeRecord> list))
        {
            list = new List<EdgeRecord>();
            edgeRecords[key] = list;
            edgePositions[key] = (a, b);
        }

        list.Add(new EdgeRecord { normal = normal, blockId = blockId });
    }

    /// <summary>
    /// 既存の生成物を削除する。再生成前や手動クリーンアップ用。
    /// </summary>
    [ContextMenu("Clear Neon Outline")]
    public void Clear()
    {
        Transform existing = transform.Find(OutlineRootName);
        if (existing != null)
        {
            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);
        }
        _outlineRoot = null;
    }

    private void CreateOutlineRoot()
    {
        GameObject rootObj = new GameObject(OutlineRootName);
        rootObj.transform.SetParent(transform, worldPositionStays: true);
        _outlineRoot = rootObj.transform;
    }

    /// <summary>
    /// 子階層からブロックのMeshFilterを収集する。
    /// 生成済みのネオン線自体は除外する(Billboard版・Cylinder版どちらも除外対象)。
    ///
    /// Cylinderモードで生成したネオン線は、円柱プリミティブなのでMeshFilterを持つ。
    /// 通常Generate()は冒頭でClear()を呼ぶため問題は起きないが、BakeMeshData()は
    /// Clear()を呼ばないため、何らかの理由で_outlineRootが残っている状態で実行されると
    /// ネオン線自体を「ブロック」として誤って読み込んでしまう恐れがある。
    /// それを防ぐため、_outlineRoot配下の子は名前ベースで明示的に除外する。
    /// </summary>
    private MeshFilter[] CollectBlockMeshFilters()
    {
        MeshFilter[] all = GetComponentsInChildren<MeshFilter>();
        List<MeshFilter> result = new List<MeshFilter>(all.Length);

        foreach (MeshFilter mf in all)
        {
            if (mf.GetComponent<LineRenderer>() != null)
                continue;

            if (IsUnderOutlineRoot(mf.transform))
                continue;

            result.Add(mf);
        }

        return result.ToArray();
    }

    /// <summary>
    /// 指定したtransformが、このコンポーネントが生成したネオン線のルート(_NeonOutlineRoot)
    /// 配下にあるかどうかを判定する。
    /// </summary>
    private bool IsUnderOutlineRoot(Transform t)
    {
        Transform current = t.parent;
        while (current != null)
        {
            if (current.name == OutlineRootName)
                return true;
            current = current.parent;
        }
        return false;
    }

    /// <summary>
    /// 1本のネオン線を生成する。renderModeに応じて描画方式を切り替える。
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
    /// 断面は常にカメラ方向を向く平らな帯(ビルボード)。軽量だが、近距離・斜め視点では
    /// 平らさが見えてしまうことがある。
    /// </summary>
    private void CreateLineRendererEdge(Vector3 a, Vector3 b)
    {
        GameObject lineObj = new GameObject("NeonEdge");
        lineObj.transform.SetParent(_outlineRoot, worldPositionStays: true);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
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
    }

    /// <summary>
    /// 【本物版】円柱プリミティブで1本のネオン線を生成する。
    /// 断面が本当に円形の3Dメッシュなので、近距離や斜め・真横からの視点でも破綻しない。
    /// 一方で、エッジ1本につきGameObject+MeshRenderer+ドローコールが1つ増えるため、
    /// エッジ数が非常に多い(数百〜数千)場合はLineRenderer版より負荷が高くなる点に注意。
    ///
    /// 円柱の分割面数はUnity標準のCylinderプリミティブメッシュに固定されており、
    /// このメソッドからは調整できない(調整したい場合は専用の管状メッシュを
    /// 自前で生成する実装に置き換える必要がある)。
    /// </summary>
    private void CreateCylinderEdge(Vector3 a, Vector3 b)
    {
        Vector3 direction = b - a;
        float length = direction.magnitude;
        if (length < 1e-6f)
            return; // 長さがほぼ0のエッジは作らない(縮退三角形などのノイズ対策)

        GameObject cylObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylObj.name = "NeonEdge";

        // CreatePrimitiveはデフォルトでCapsuleColliderを付けてくるが、
        // ネオン線に当たり判定は不要なので削除する(レイキャストや物理に干渉させないため)。
        Collider col = cylObj.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying)
                Destroy(col);
            else
                DestroyImmediate(col);
        }

        // 親に入れる前にワールド空間でTransformを確定させる(親のスケール/回転に影響されないようにするため)
        Vector3 mid = (a + b) * 0.5f;
        cylObj.transform.position = mid;
        cylObj.transform.up = direction.normalized; // 標準Cylinderはローカルy軸方向に伸びている

        // 標準のCylinderプリミティブは「直径1・高さ2(半径0.5、y=-1〜1)」の形状なので、
        // それに合わせてスケールを調整する。
        cylObj.transform.localScale = new Vector3(lineWidth, length * 0.5f, lineWidth);

        cylObj.transform.SetParent(_outlineRoot, worldPositionStays: true);

        MeshRenderer mr = cylObj.GetComponent<MeshRenderer>();
        ApplyNeonMaterialAndColor(mr);
    }

    /// <summary>
    /// Cylinderモード用: 複数のエッジが集まる角(頂点)に、円柱の平らな端面(切り口)を
    /// 覆い隠す小さな球を配置する。これによりパイプの継ぎ目のように滑らかに見える。
    ///
    /// 1本しかエッジが集まっていない末端(行き止まり)には配置しない。
    /// (円柱自体の丸い端面でも見た目上は気にならないことが多いため、無駄なオブジェクトを増やさない)
    /// </summary>
    private void CreateJointSpheres(List<(Vector3 a, Vector3 b)> edges, float epsilon)
    {
        // 頂点位置(量子化キー)ごとに「代表座標」と「そこに集まるエッジの本数(次数)」を集計する
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
            Debug.Log($"[NeonOutlineGenerator] ジョイント球を{jointCount}個生成しました(次数2以上の頂点のみ)。");
        }
    }

    /// <summary>
    /// EdgeKey.Quantizeと同じロジックでワールド座標をグリッド単位に量子化する
    /// (頂点の同一性判定に使う浮動小数点誤差吸収用)。
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
    /// 1個のジョイント球(継ぎ目を覆う小さな球)を指定座標に生成する。
    /// </summary>
    private void CreateJointSphere(Vector3 position)
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

        sphereObj.transform.position = position;

        // 標準のSphereプリミティブは直径1(半径0.5)。円柱の太さ(lineWidth=直径)に合わせる。
        // 球の直径が円柱の直径と同じであれば、球面が円柱の側面とちょうど面一(つらいち)になり、
        // 余分に膨らんで見えることなく継ぎ目だけを覆える。
        sphereObj.transform.localScale = new Vector3(lineWidth, lineWidth, lineWidth);

        sphereObj.transform.SetParent(_outlineRoot, worldPositionStays: true);

        MeshRenderer mr = sphereObj.GetComponent<MeshRenderer>();
        ApplyNeonMaterialAndColor(mr);
    }

    /// <summary>
    /// CylinderモードのネオンオブジェクトにneonMaterialと色を適用する共通処理。
    /// CreateCylinderEdge・CreateJointSphereの両方から呼ばれる。
    ///
    /// sharedMaterialを直接書き換えると全エッジ/全ジョイントが同じマテリアルインスタンスを
    /// 共有して色が連動してしまうため、MaterialPropertyBlockで個体ごとに色(ベースカラー)を当てる。
    /// シェーダーによってプロパティ名が異なる(Built-in:_Color / URP・HDRP:_BaseColor)ため、
    /// 存在するものだけ設定する。
    ///
    /// 【重要】発光色(_EmissionColor)はデフォルトでは触らない。
    /// neonColorは普通の(HDRでない)色なので、これをそのまま_EmissionColorに上書きすると、
    /// マテリアル側でBloomが光るように設定していた明るいHDR発光色を踏みつぶしてしまい、
    /// 「メッシュ表示にすると光らなくなる」という症状になる。
    /// 発光色もneonColorで揃えたい場合のみ、tintEmissionWithNeonColorをONにして、
    /// emissionIntensityで十分明るく(HDR的に)補ったうえで上書きする。
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
    /// 1本のエッジについて、それを含む三角形の情報(法線・所属ブロックID)。
    /// </summary>
    private struct EdgeRecord
    {
        public Vector3 normal;
        public int blockId;
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