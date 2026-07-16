using UnityEngine;

public class KeyGimmick : MonoBehaviour
{
    [Header("設定")]
    [SerializeField] private Transform[] pathPoints;
    [SerializeField] private KeySocket targetSocket;

    [Header("押し込み設定")]
    [Tooltip("プレイヤーのバースト速度（レール方向成分）に掛ける倍率。\n大きいほど同じ速度でも遠くまで進む。")]
    [SerializeField] private float pushMultiplier = 1.0f;

    [Tooltip("1秒あたりの減速量（レール上スピードの減り方）。\n大きいほどすぐ止まり、小さいほど遠くまで滑る。")]
    [SerializeField] private float deceleration = 4.0f;

    [Header("プレイヤー反発設定")]
    [Tooltip("プレイヤーを弾き返す力（Impulse）")]
    [SerializeField] private float playerReflectForce = 10f;

    private Rigidbody2D myRb;

    // レールを1本の折れ線として管理するためのデータ
    private Vector2[] railPoints;      // [初期位置, pathPoints..., Socket位置]
    private float[] cumulativeLengths; // 各頂点までの始点からの累積距離
    private float totalLength;         // レール全長

    // 現在の状態
    private float distanceTraveled = 0f; // 始点(初期位置)からの累積距離
    private float railSpeed = 0f;        // レール上の符号つきスピード（+ で終点方向、- で始点方向）
    private bool hasArrived = false;     // Socketに到達済みか

    private void Start()
    {
        myRb = GetComponent<Rigidbody2D>();

        // 途中停止中はレール上に固定したいので、重力・物理挙動は殺しておく。
        // 位置は transform で直接制御する。
        myRb.bodyType = RigidbodyType2D.Kinematic;
        myRb.linearVelocity = Vector2.zero;
        myRb.angularVelocity = 0f;

        BuildRail();
    }

    /// <summary>
    /// [初期位置, pathPoints..., Socket位置] を1本の折れ線として構築し、
    /// 各頂点までの累積距離を事前計算しておく。
    /// </summary>
    private void BuildRail()
    {
        int extra = 2; // 初期位置 + Socket位置
        int pointCount = (pathPoints != null ? pathPoints.Length : 0) + extra;

        railPoints = new Vector2[pointCount];
        cumulativeLengths = new float[pointCount];

        int idx = 0;
        railPoints[idx++] = transform.position; // 始点＝Keyの初期位置

        if (pathPoints != null)
        {
            for (int i = 0; i < pathPoints.Length; i++)
            {
                railPoints[idx++] = pathPoints[i].position;
            }
        }

        railPoints[idx] = targetSocket.transform.position; // 終点＝Socket位置

        // 累積距離を計算
        cumulativeLengths[0] = 0f;
        for (int i = 1; i < railPoints.Length; i++)
        {
            cumulativeLengths[i] = cumulativeLengths[i - 1]
                + Vector2.Distance(railPoints[i - 1], railPoints[i]);
        }

        totalLength = cumulativeLengths[railPoints.Length - 1];
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasArrived || !other.CompareTag("Player")) return;

        // 1. 本当にバースト中かを状態で確認する
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;
        if (player.CurrentState != player.StateBurst) return;

        Rigidbody2D playerRb = other.GetComponent<Rigidbody2D>();
        if (playerRb == null) return;

        // 2. レール方向に投影した速度成分 × 倍率 を、符号つきスピードとして上書き
        Vector2 v = playerRb.linearVelocity;
        Vector2 railDir = GetRailDirection(distanceTraveled);
        float projected = Vector2.Dot(v, railDir); // 正面ほど大きく、真横で0、逆向きで負
        railSpeed = projected * pushMultiplier;

        // 3. プレイヤーを弾き返す（従来通り固定Impulse）
        //    向きは「Socket から Key へ」の向き。
        Vector2 reflexDirection =
            ((Vector2)transform.position - (Vector2)targetSocket.transform.position).normalized;
        playerRb.linearVelocity = Vector2.zero;
        playerRb.AddForce(reflexDirection * playerReflectForce, ForceMode2D.Impulse);
    }

    private void FixedUpdate()
    {
        if (hasArrived) return;

        // 動いていなければ何もしない（止まっている間はレール上で待機）
        if (Mathf.Abs(railSpeed) < 0.0001f) return;

        float dt = Time.fixedDeltaTime;

        // 累積距離を進める
        distanceTraveled += railSpeed * dt;

        // 減速：スピードの大きさを deceleration の割合で0へ近づける
        railSpeed = Mathf.MoveTowards(railSpeed, 0f, deceleration * dt);

        // 始点より手前へは逆走させない
        if (distanceTraveled <= 0f)
        {
            distanceTraveled = 0f;
            railSpeed = 0f;
        }

        // 終点（Socket）到達
        if (distanceTraveled >= totalLength)
        {
            distanceTraveled = totalLength;
            railSpeed = 0f;
            Arrive();
        }

        // 累積距離からワールド座標を求めて反映
        transform.position = GetPositionAtDistance(distanceTraveled);
    }

    /// <summary>
    /// 始点からの累積距離 d に対応するワールド座標を返す。
    /// </summary>
    private Vector2 GetPositionAtDistance(float d)
    {
        d = Mathf.Clamp(d, 0f, totalLength);

        // d が含まれる区間を探す
        for (int i = 1; i < railPoints.Length; i++)
        {
            if (d <= cumulativeLengths[i])
            {
                float segStart = cumulativeLengths[i - 1];
                float segLen = cumulativeLengths[i] - segStart;
                float t = (segLen > 0.0001f) ? (d - segStart) / segLen : 0f;
                return Vector2.Lerp(railPoints[i - 1], railPoints[i], t);
            }
        }
        return railPoints[railPoints.Length - 1];
    }

    /// <summary>
    /// 始点からの累積距離 d の地点における、終点方向を正とした進行方向（単位ベクトル）を返す。
    /// </summary>
    private Vector2 GetRailDirection(float d)
    {
        d = Mathf.Clamp(d, 0f, totalLength);

        for (int i = 1; i < railPoints.Length; i++)
        {
            // 境界ちょうどのときは次の区間の向きを優先したいので < で判定
            if (d < cumulativeLengths[i] || i == railPoints.Length - 1)
            {
                Vector2 dir = railPoints[i] - railPoints[i - 1];
                if (dir.sqrMagnitude > 0.0001f) return dir.normalized;
            }
        }
        return Vector2.right; // 念のためのフォールバック
    }

    /// <summary>
    /// Socket に到達した瞬間の処理。
    /// 位置をSocketにぴったり合わせ、以降の押し込みを止める。
    /// Socket側のOnTriggerEnter2Dは物理トリガーの侵入で自然に呼ばれる。
    /// </summary>
    private void Arrive()
    {
        hasArrived = true;
        transform.position = targetSocket.transform.position;
        enabled = false;
    }
}