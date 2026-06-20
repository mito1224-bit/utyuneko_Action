using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 子オブジェクトとして配置されたブロック群（立方体・三角柱など任意の形状）を走査し、
/// 「他のブロックと共有していない外側の稜線」と「同一メッシュ内の見た目に意味のある角」だけを
/// 検出してLineRendererでネオン風に光らせるスクリプト。
///
/// 立方体決め打ちではなく、各ブロックのMeshFilter.sharedMeshの実データ（三角形）から
/// 辺を抽出するため、キューブ・三角柱（対角カットしたプリズム）など形状を問わず動作する。
///
/// 判定の考え方:
/// 1. 全ブロックの全三角形から辺を列挙し、「ワールド座標キー」+「どの面の法線を持つか」を記録する
/// 2. 同じ位置の辺が何回・どんな法線の組み合わせで出現したかを集計する
///    - 1回だけ出現        → 他のブロックに接していない外側の辺 → 表示する
///    - 同一メッシュ内で2回出現し、法線が異なる（角がある） → 立体としての本当の角 → 表示する
///    - 同一メッシュ内で2回出現し、法線が同じ（同一平面の分割線） → ポリゴン分割用の対角線 → 表示しない
///    - 異なるブロック間で出現（ブロック同士が接している） → 共有面の境界 → 表示しない
///
/// 使い方:
/// 1. Tilemapを持つ親オブジェクト（ブロック群の親）にこのスクリプトをアタッチ
/// 2. Neon Material に Emission を有効化したマテリアルを割り当てる
/// 3. 再生 or インスペクタの右クリックメニューから "Generate Neon Outline" を実行
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
    public Material neonMaterial;
    public float lineWidth = 0.03f;
    public Color neonColor = Color.cyan;

    [Header("自動実行")]
    [Tooltip("Playモード開始時に自動生成するか")]
    public bool generateOnStart = true;

    [Header("デバッグ")]
    [Tooltip("ONにすると、各ブロックのメッシュ情報と判定理由をConsoleに詳細出力する(確認時のみON推奨)")]
    public bool verboseDebugLog = false;

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
    /// 外側エッジ・有意な角を検出し、ネオン線を生成するメイン処理。
    /// </summary>
    [ContextMenu("Generate Neon Outline")]
    public void Generate()
    {
        Clear();

        MeshFilter[] meshFilters = CollectBlockMeshFilters();
        if (meshFilters.Length == 0)
        {
            Debug.LogWarning("[NeonOutlineGenerator] ブロックが見つかりませんでした。子オブジェクトの構成を確認してください。");
            return;
        }

        // edgeRecords: エッジキーごとに、そのエッジを持つ三角形の法線(ワールド空間)とブロックIDを記録
        Dictionary<EdgeKey, List<EdgeRecord>> edgeRecords = new Dictionary<EdgeKey, List<EdgeRecord>>();
        Dictionary<EdgeKey, (Vector3 a, Vector3 b)> edgePositions = new Dictionary<EdgeKey, (Vector3, Vector3)>();

        // 動的にpositionEpsilonを決定する: 全ブロックのワールド実寸のうち最小のものを基準にする
        float resolvedEpsilon = fixedPositionEpsilon;
        if (autoCalculatePositionEpsilon)
        {
            float minWorldSize = float.MaxValue;
            foreach (MeshFilter mf in meshFilters)
            {
                Mesh m = mf.sharedMesh;
                if (m == null) continue;

                Vector3 localSize = m.bounds.size;
                Vector3 lossy = mf.transform.lossyScale;

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
        foreach (MeshFilter mf in meshFilters)
        {
            Mesh mesh = mf.sharedMesh;
            if (mesh == null)
            {
                if (verboseDebugLog)
                    Debug.LogWarning($"[NeonOutlineGenerator] {mf.gameObject.name}: sharedMeshがnullです。スキップします。");
                continue;
            }

            Transform t = mf.transform;
            Vector3[] localVerts = mesh.vertices;
            int[] tris = mesh.triangles;

            if (verboseDebugLog)
            {
                Debug.Log($"[NeonOutlineGenerator] blockId={blockId} name={mf.gameObject.name} " +
                          $"頂点数={localVerts.Length} 三角形数={tris.Length / 3} " +
                          $"localScale={t.localScale} lossyScale={t.lossyScale} " +
                          $"subMeshCount={mesh.subMeshCount}");
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
    }

    /// <summary>
    /// エッジの出現記録から、ネオン表示すべきかどうかを判定する。
    /// reason には判定理由を返す(デバッグ用)。
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
    /// 生成済みのネオン線自体（LineRenderer用オブジェクト）は除外する。
    /// </summary>
    private MeshFilter[] CollectBlockMeshFilters()
    {
        MeshFilter[] all = GetComponentsInChildren<MeshFilter>();
        List<MeshFilter> result = new List<MeshFilter>(all.Length);

        foreach (MeshFilter mf in all)
        {
            if (mf.GetComponent<LineRenderer>() != null)
                continue;

            result.Add(mf);
        }

        return result.ToArray();
    }

    /// <summary>
    /// 1本のネオン線(LineRenderer)を生成する。
    /// </summary>
    private void CreateLine(Vector3 a, Vector3 b)
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