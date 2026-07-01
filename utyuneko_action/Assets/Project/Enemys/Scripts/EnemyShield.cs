using UnityEngine;

/// <summary>
/// 盾を持つ敵。向いている方向（前方）からの攻撃を盾で防ぎ、ダメージを無効化する。
/// 盾の反対側（背後）から当てたときだけ EnemyHealth がダメージを通す。
///
/// 仕組み:
///   - 前方（盾の向き）は EnemyMovement.moveDirection に追従する（巡回反転で盾の向きも反転）。
///   - EnemyHealth.HandleHit がダメージ適用の前に Blocks() を問い合わせ、前方からの攻撃なら無効化する。
///   - 反射／ノックバックは EnemyCollision の Reflect ソリッドコライダーが担当（盾はダメージのみゲートする）。
///
/// 想定セットアップ:
///   - EnemyCollision の collisionType は Reflect（前から当てても背後から当てても、いったん弾く）。
///   - 盾の見た目（子オブジェクト）を shieldPivot に割り当てると、前方を向くよう自動回転する。
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
public class EnemyShield : MonoBehaviour
{
    [Header("盾の向き")]
    [Tooltip("EnemyMovement があれば移動方向を前方（盾の向き）として自動追従する")]
    public bool followMoveDirection = true;

    [Tooltip("EnemyMovement が無い／追従しない場合の前方向き（ワールド方向）")]
    public Vector2 facingOverride = Vector2.left;

    [Header("盾の防御範囲")]
    [Tooltip("前方からこの角度（度）以内の攻撃を盾で防ぐ。90で前方半円すべてを防御")]
    [Range(0f, 180f)] public float shieldHalfAngle = 90f;

    [Header("演出（任意）")]
    [Tooltip("盾の見た目。設定すると前方（右=+X）を向くよう自動回転し、前方へ配置する")]
    public Transform shieldPivot;

    [Tooltip("盾を敵の中心から前方へどれだけ離して配置するか")]
    public float shieldDistance = 0.5f;

    private EnemyMovement movement;

    void Awake()
    {
        movement = GetComponent<EnemyMovement>();
    }

    void Update()
    {
        if (shieldPivot == null) return;

        Vector2 facing = GetFacing();
        if (facing.sqrMagnitude < 0.0001f) return;

        shieldPivot.right = facing; // 盾の見た目を前方へ向ける
        // 前方へオフセットした位置に配置する（敵が反転すると盾も反対側へ移動）
        shieldPivot.position = transform.position + (Vector3)(facing * shieldDistance);
    }

    /// <summary>
    /// 攻撃元（プレイヤー位置）が盾の防御範囲（前方）にあるなら true ＝ ダメージを防ぐ。
    /// EnemyHealth.HandleHit から呼ばれる。
    /// </summary>
    public bool Blocks(Vector3 hitFromPosition)
    {
        Vector2 facing = GetFacing();
        if (facing.sqrMagnitude < 0.0001f) return false;
        facing.Normalize();

        Vector2 toAttacker = (Vector2)(hitFromPosition - transform.position);
        if (toAttacker.sqrMagnitude < 0.0001f) return false;
        toAttacker.Normalize();

        // 前方となす角が shieldHalfAngle 以内なら盾で防ぐ
        float cosThreshold = Mathf.Cos(shieldHalfAngle * Mathf.Deg2Rad);
        return Vector2.Dot(toAttacker, facing) >= cosThreshold;
    }

    // 現在の前方向き（盾の向き）。移動方向に追従するか、固定値を使う。
    private Vector2 GetFacing()
    {
        if (followMoveDirection && movement != null)
        {
            Vector2 d = movement.moveDirection;
            if (d.sqrMagnitude > 0.0001f) return d.normalized;
        }
        return facingOverride.sqrMagnitude > 0.0001f ? facingOverride.normalized : Vector2.left;
    }

    // シーンビューで盾の防御範囲を可視化（調整用）
    private void OnDrawGizmosSelected()
    {
        Vector2 facing = GetFacing();
        if (facing.sqrMagnitude < 0.0001f) return;

        Vector3 pos = transform.position;
        const float r = 1f;

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(pos, pos + (Vector3)facing * r); // 前方（盾の正面）

        // 防御範囲の端（前方 ± shieldHalfAngle）
        Vector3 edgeA = Quaternion.Euler(0f, 0f, shieldHalfAngle) * (Vector3)facing * r;
        Vector3 edgeB = Quaternion.Euler(0f, 0f, -shieldHalfAngle) * (Vector3)facing * r;
        Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.7f);
        Gizmos.DrawLine(pos, pos + edgeA);
        Gizmos.DrawLine(pos, pos + edgeB);
    }
}
