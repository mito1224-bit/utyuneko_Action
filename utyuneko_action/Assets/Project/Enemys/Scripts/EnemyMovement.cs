using UnityEngine;

/// <summary>
/// エネミーの左右巡回移動。
/// transform で manual に動かす（EnemyKnockback と同じ方式で物理が衝突しない）。XY平面で動作。
///
/// 追加挙動:
///   - 斜め下へレイを飛ばし、進行方向の先に床が無ければ反転（崖から落ちない）
///   - 進行方向へレイを飛ばし、壁に当たったら反転（Pierceソリッド化での壁際詰まり対策）
///   - プレイヤーに吹き飛ばされたあと、収まったら時間経過で元の位置へ戻る
/// </summary>
public class EnemyMovement : MonoBehaviour
{
    [Header("移動設定")]
    public float moveSpeed = 2.0f;               // 移動速度
    public Vector3 moveDirection = Vector3.left; // 現在の移動方向

    [Header("時間での方向転換（任意）")]
    [Tooltip("一定時間で方向を反転させる。崖・壁検知だけで十分なら false でよい")]
    public bool useTimedReversal = true;
    public float changeInterval = 3.0f;          // 方向を反転するまでの時間（秒）
    private float timer = 0f;                     // 時間を計測するためのタイマー

    [Header("壁・崖検知（2D Raycast）")]
    [Tooltip("床・壁とみなすレイヤー（自分自身の敵レイヤーは含めないこと）")]
    public LayerMask groundLayers = ~0;
    [Tooltip("進行方向の壁を検知して反転する")]
    public bool detectWall = true;
    [Tooltip("進行方向の先に床が無ければ反転する（崖落ち防止）")]
    public bool detectEdge = true;
    [Tooltip("壁検知レイの長さ（コライダー端からの距離）")]
    public float wallCheckDistance = 0.15f;
    [Tooltip("崖検知レイ（斜め下）の長さ")]
    public float edgeCheckDistance = 1.0f;
    [Tooltip("崖検知レイの下向き比率。1=真下 / 0=真横。0.7前後で斜め下")]
    [Range(0.1f, 1f)] public float edgeRayDownBias = 0.7f;
    [Tooltip("反転後、再び反転できるまでの待ち時間（崖・壁際でのバタつき防止）")]
    public float reverseCooldown = 0.2f;
    private float reverseTimer = 0f;

    [Header("元位置へ戻る（吹き飛ばし後）")]
    [Tooltip("吹き飛ばされたあと、収まったら元の位置へ戻す")]
    public bool returnToStart = true;
    [Tooltip("吹き飛びが収まってから戻り始めるまでの待ち時間（秒）")]
    public float returnDelay = 0.5f;
    [Tooltip("元位置へ戻る速度")]
    public float returnSpeed = 1.5f;
    [Tooltip("この距離まで近づいたら戻り完了として巡回を再開する")]
    public float returnThreshold = 0.05f;

    private Vector3 startPosition;   // 巡回の中心＝復帰先
    private bool displaced = false;  // 吹き飛ばされて元位置から離れているか
    private float returnTimer = 0f;

    private EnemyKnockback knockback;
    private Collider2D col;

    void Awake()
    {
        knockback = GetComponent<EnemyKnockback>();
        col = GetComponent<Collider2D>();
    }

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        // 吹き飛び中（被弾ノックバック／死亡）は EnemyKnockback が transform を制御するので巡回しない
        if (knockback != null && (knockback.IsActive || knockback.IsDying))
        {
            displaced = true;
            returnTimer = 0f;
            return;
        }

        // 吹き飛びが収まっていて元位置から離れているなら、待ってから戻る
        if (returnToStart && displaced)
        {
            ReturnToStart();
            return;
        }

        Move();
        HandleWallAndEdge();
        if (useTimedReversal) HandleDirectionChange();
    }

    private void Move()
    {
        // 指定した方向へ速度を掛けて移動
        transform.Translate(moveDirection.normalized * moveSpeed * Time.deltaTime);
    }

    private void HandleDirectionChange()
    {
        // 毎フレームの経過時間をタイマーに加算
        timer += Time.deltaTime;

        // タイマーが設定した間隔（秒）に達したか判定
        if (timer >= changeInterval)
        {
            Reverse();
            timer = 0f;
        }
    }

    /// <summary>
    /// 壁・崖を検知して必要なら反転する。連続反転を防ぐためクールダウンを設ける。
    /// </summary>
    private void HandleWallAndEdge()
    {
        if (reverseTimer > 0f)
        {
            reverseTimer -= Time.deltaTime;
            return;
        }

        bool needReverse = false;
        if (detectWall && IsWallAhead()) needReverse = true;
        else if (detectEdge && !IsGroundAhead()) needReverse = true;

        if (needReverse)
        {
            Reverse();
            reverseTimer = reverseCooldown;
            timer = 0f; // 時間反転のタイマーもリセットして直後の二重反転を防ぐ
        }
    }

    // 進行方向に壁があるか（コライダー端の少し外から前方へレイ）
    private bool IsWallAhead()
    {
        Vector2 dir = ((Vector2)moveDirection).normalized;
        Vector2 origin = (Vector2)transform.position + dir * (HalfWidth() + 0.02f);
        return Physics2D.Raycast(origin, dir, wallCheckDistance, groundLayers).collider != null;
    }

    // 進行方向の先に床があるか（斜め下へレイ）。無ければ崖とみなす。
    private bool IsGroundAhead()
    {
        Vector2 dir = ((Vector2)moveDirection).normalized;
        Vector2 origin = (Vector2)transform.position + dir * (HalfWidth() + 0.02f);
        Vector2 rayDir = (dir * (1f - edgeRayDownBias) + Vector2.down * edgeRayDownBias).normalized;
        return Physics2D.Raycast(origin, rayDir, edgeCheckDistance, groundLayers).collider != null;
    }

    // コライダーの半幅（進行方向のレイ原点を自分の外へ出すため）
    private float HalfWidth()
    {
        return col != null ? col.bounds.extents.x : 0.5f;
    }

    private void Reverse()
    {
        // 移動方向を反転させる（例: 左なら右へ）
        moveDirection = -moveDirection;
    }

    /// <summary>
    /// 吹き飛びが収まったあと、待ち時間を経て元の位置へ滑らかに戻す。
    /// </summary>
    private void ReturnToStart()
    {
        returnTimer += Time.deltaTime;
        if (returnTimer < returnDelay) return;

        Vector3 target = new Vector3(startPosition.x, startPosition.y, transform.position.z);
        transform.position = Vector3.MoveTowards(transform.position, target, returnSpeed * Time.deltaTime);

        if ((transform.position - target).sqrMagnitude <= returnThreshold * returnThreshold)
        {
            transform.position = target;
            displaced = false;
            returnTimer = 0f;
        }
    }

    // シーンビューで検知レイを可視化（調整用）
    private void OnDrawGizmosSelected()
    {
        Vector2 dir = ((Vector2)moveDirection).normalized;
        float half = Application.isPlaying ? HalfWidth() : 0.5f;
        Vector2 origin = (Vector2)transform.position + dir * (half + 0.02f);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, origin + dir * wallCheckDistance);

        Vector2 rayDir = (dir * (1f - edgeRayDownBias) + Vector2.down * edgeRayDownBias).normalized;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, origin + rayDir * edgeCheckDistance);
    }
}
