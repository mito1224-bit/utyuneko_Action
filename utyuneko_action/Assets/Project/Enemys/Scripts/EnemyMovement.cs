using UnityEngine;

/// <summary>
/// エネミーの左右巡回移動。
/// 移動は Rigidbody2D.MovePosition で行う（物理ステップでスイープ移動するので静的な床・壁にぶつかって止まる）。
/// ※ Dynamic ボディを transform.Translate で動かすと「テレポート」になり壁の衝突判定をすり抜けるため使わない。
/// Rigidbody2D が無いオブジェクトでは従来どおり transform.Translate にフォールバックする。XY平面で動作。
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

    [Header("進行方向に合わせた回転")]
    [Tooltip("向き回転を当てる対象（プレイヤーに倣った Rotation 層の子。未割り当てなら従来どおりルート＝後方互換）。" +
             "Rigidbody2D/Collider2D と同居するルートに回転を当てると2Dの当たり判定がつぶれて壁すり抜けの原因になるため、" +
             "コライダーを持たない子（Model の親）を割り当てること")]
    public Transform visualTransform;
    [Tooltip("進行方向に合わせてモデルの角度を変える")]
    public bool rotateToMoveDirection = true;
    [Tooltip("回転させる軸（追加された3Dモデルの振り向きは通常Y軸）")]
    public RotationAxis rotationAxis = RotationAxis.Y;
    [Tooltip("左向き（-x）へ移動中の角度（度）")]
    public float leftAngle = 50f;
    [Tooltip("右向き（+x）へ移動中の角度（度）")]
    public float rightAngle = -50f;
    [Tooltip("振り向くときに必ず通過させる角度（度）。前面を通したいなら前面の角度（既定180）、背面を通したいなら0などを指定")]
    public float turnViaAngle = 0f;
    [Tooltip("角度を変える速さ（度/秒）。急変させず自然に振り向かせる")]
    public float rotationSpeed = 360f;
    private float currentAngle;  // 現在の角度。左右の角度間を線形に補間する（turnViaAngle を経由して行き来）

    public enum RotationAxis { X, Y, Z }

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
    private Rigidbody2D rb;

    void Awake()
    {
        knockback = GetComponent<EnemyKnockback>();
        col = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        startPosition = transform.position;
        // 初期の進行方向に合わせて角度を初期化（最初の1フレームから正しい向きにする）
        currentAngle = TargetAngle();
        ApplyRotation();
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

        // 進行方向に合わせて滑らかに振り向く（戻り中も含め、吹き飛び中以外は常に更新）
        if (rotateToMoveDirection) UpdateFacing();

        // 復帰中は巡回の検知・反転を行わない（実移動は FixedUpdate）
        if (returnToStart && displaced) return;

        // 壁・崖検知と時間反転は moveDirection を更新するだけ（実移動は FixedUpdate の MovePosition）
        HandleWallAndEdge();
        if (useTimedReversal) HandleDirectionChange();
    }

    // 実際の位置移動は物理ステップで rb.MovePosition で行う。
    // Dynamic な Rigidbody2D を transform で動かすとテレポート扱いになり静的な壁をすり抜けるため、
    // MovePosition でスイープ移動させて床・壁にぶつかって止まるようにする。
    void FixedUpdate()
    {
        // 吹き飛び中／死亡中は EnemyKnockback が transform を制御するので動かさない
        if (knockback != null && (knockback.IsActive || knockback.IsDying)) return;

        if (returnToStart && displaced)
        {
            ReturnToStart();
            return;
        }

        Move();
    }

    private void Move()
    {
        // ワールド座標での移動量（回転に依らずワールド軸で動かす。Y回転で奥/手前へ流れるのを防ぐ）。
        Vector2 delta = (Vector2)(moveDirection.normalized) * moveSpeed * Time.fixedDeltaTime;

        if (rb != null)
        {
            // Dynamic ボディをスイープ移動。静的な床・壁に当たると止まる（transform 直書きと違いすり抜けない）
            rb.MovePosition(rb.position + delta);
        }
        else
        {
            transform.Translate(delta, Space.World);
        }
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
    /// 進行方向に応じた目標角度へ向けて、毎フレーム少しずつ角度を変える。
    /// 右目標は turnViaAngle（既定180=前面）を必ず通る表現に変換しているため、
    /// Mathf.MoveTowards の線形補間で前面を経由して自然に振り向く（背面=0度側を通らない）。
    /// </summary>
    private void UpdateFacing()
    {
        float target = TargetAngle();
        currentAngle = Mathf.MoveTowards(currentAngle, target, rotationSpeed * Time.deltaTime);
        ApplyRotation();
    }

    // 進行方向（x成分）から目標角度を決める。横移動が無いときは現在角度を維持。
    private float TargetAngle()
    {
        if (moveDirection.x < 0f) return leftAngle;        // 左向き
        if (moveDirection.x > 0f) return RightTargetAngle(); // 右向き（前面を通る表現に変換）
        return currentAngle;
    }

    /// <summary>
    /// rightAngle を「leftAngle から turnViaAngle を通って到達できる」角度表現に変換して返す。
    /// 例: leftAngle=50, rightAngle=-50, turnViaAngle=180 のとき 310 を返す（50→180→310 と前面を経由）。
    /// rightAngle が -50 でも 310 でも同じ結果になるので、保存値に依らず必ず前面を通る。
    /// </summary>
    private float RightTargetAngle()
    {
        // leftAngle を基準に [leftAngle, leftAngle+360) の範囲へ正規化
        float r0 = leftAngle + Mathf.Repeat(rightAngle - leftAngle, 360f);
        float via = leftAngle + Mathf.Repeat(turnViaAngle - leftAngle, 360f);
        // via が左→右（増加方向）の経路上にあるならそのまま、無ければ逆回り（360引く）にして via を通す
        return (via <= r0) ? r0 : r0 - 360f;
    }

    // 選択した軸へ currentAngle を適用する（他の軸は0）。
    // 回転は visualTransform（プレイヤーに倣った Rotation 層の子）へ当てる。
    // 未割り当てなら従来どおりルートへ当てる（後方互換）。ただしルートに Rigidbody2D/Collider2D が
    // 同居している場合、ルートを回すと2Dの当たり判定がつぶれて壁すり抜けの原因になる点に注意。
    private void ApplyRotation()
    {
        Transform t = visualTransform != null ? visualTransform : transform;
        switch (rotationAxis)
        {
            case RotationAxis.X: t.localRotation = Quaternion.Euler(currentAngle, 0f, 0f); break;
            case RotationAxis.Y: t.localRotation = Quaternion.Euler(0f, currentAngle, 0f); break;
            default:             t.localRotation = Quaternion.Euler(0f, 0f, currentAngle); break;
        }
    }

    /// <summary>
    /// 吹き飛びが収まったあと、待ち時間を経て元の位置へ滑らかに戻す。
    /// </summary>
    private void ReturnToStart()
    {
        returnTimer += Time.fixedDeltaTime;
        if (returnTimer < returnDelay) return;

        Vector3 target = new Vector3(startPosition.x, startPosition.y, transform.position.z);
        Vector3 next = Vector3.MoveTowards(transform.position, target, returnSpeed * Time.fixedDeltaTime);

        if (rb != null) rb.MovePosition(next);
        else transform.position = next;

        if ((next - target).sqrMagnitude <= returnThreshold * returnThreshold)
        {
            if (rb != null) rb.MovePosition(target);
            else transform.position = target;
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
