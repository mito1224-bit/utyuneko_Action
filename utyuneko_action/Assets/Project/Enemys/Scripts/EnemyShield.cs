using UnityEngine;

/// <summary>
/// 盾を持つ敵の「物理盾」フォロワー。BossChargerShield と同じ思想を汎用エネミー用に薄くしたもの。
///
/// 仕組み（物理ガード）:
///   - 盾はソリッドの Collider2D を持つ子オブジェクト（RefObj レイヤー）で、敵本体の正面を物理的に覆う。
///   - プレイヤーのバーストは盾に当たると壁と同じく反射する（反射はプレイヤー側＝RefObj を壁扱い）。
///     盾に阻まれて本体コライダーへ届かないので、本体（EnemyHealth）にはダメージが入らない。
///   - 盾の反対側（背後）から当てたときだけ本体コライダーに届いてダメージが通る。
///   - ステートが Normal のプレイヤーが盾へ触れるとダメージ＝盾オブジェクトの DamageSource が担当（このスクリプト外。
///     Enemy タグにしておけばバースト中は PlayerHealth 側で免除される。BossChargerShield と同じ）。
///
/// このスクリプトの唯一の仕事は「盾の向きを本体の移動方向へ追従させる」こと。
///   - 向きは shieldPivot の localPosition.x の符号反転で切り替える。
///     ★親を回転／スケールさせて追従させると 2D コライダーが潰れる（当たり判定が崩れる）ため、
///       shieldPivot は root 直下の「回転・スケールを継承しない」位置へ置くこと（BossChargerShield と同じ理由）。
///
/// 想定セットアップ（Editor）:
///   - root直下に空アンカー → その下に盾オブジェクト（ソリッド Collider2D ＋ RefObj レイヤー ＋ DamageSource）。
///   - shieldPivot にそのアンカーを割り当てる。初期 localPosition.x に前方オフセットを持たせる（例 x=-0.5 で左構え）。
///   - EnemyCollision は Reflect（本体コライダーへのバーストだけダメージ。盾ヒットは otherCollider 判定で除外される）。
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
public class EnemyShield : MonoBehaviour
{
    [Header("盾の向き")]
    [Tooltip("EnemyMovement があれば移動方向を前方（盾の向き）として自動追従する")]
    public bool followMoveDirection = true;

    [Tooltip("EnemyMovement が無い／追従しない場合の前方向き（ワールド方向）")]
    public Vector2 facingOverride = Vector2.left;

    [Header("盾オブジェクト")]
    [Tooltip("向きを追従させる盾アンカー（root直下・非回転非スケール推奨）。localPosition.x の符号を反転して前方へ構える")]
    public Transform shieldPivot;

    [Tooltip("shieldPivot の初期 localPosition.x が 0 のときに使う前方オフセット（絶対値）")]
    public float shieldDistance = 0.5f;

    [Header("演出（任意）")]
    [Tooltip("盾でバーストを弾いた瞬間に光らせる HitFlash（未指定なら shieldPivot 配下から自動取得）。EnemyCollision から叩かれる")]
    public HitFlash shieldFlash;

    private EnemyMovement movement;
    private Vector3 heldLocalPos;   // 構え位置（右向き時。左向きは x 反転）
    private int facing = -1;        // -1=左 / +1=右

    void Awake()
    {
        movement = GetComponent<EnemyMovement>();
        if (shieldPivot != null) heldLocalPos = shieldPivot.localPosition;
        // 盾の見た目側に付いた HitFlash を自動取得（本体の HitFlash とは別物。盾だけを光らせる）
        if (shieldFlash == null && shieldPivot != null) shieldFlash = shieldPivot.GetComponentInChildren<HitFlash>();
    }

    void Update()
    {
        if (shieldPivot == null) return;

        Vector2 f = GetFacing();
        if (f.sqrMagnitude < 0.0001f) return;
        facing = f.x >= 0f ? 1 : -1;

        // localPosition.x の符号だけ切り替える（親の回転・スケールを継承しないので当たり判定が潰れない）
        float mag = Mathf.Abs(heldLocalPos.x);
        if (mag < 0.0001f) mag = Mathf.Abs(shieldDistance);

        Vector3 lp = heldLocalPos;
        lp.x = mag * facing;
        shieldPivot.localPosition = lp;
    }

    /// <summary>盾でバーストを弾いた瞬間の演出（盾のみ白フラッシュ）。EnemyCollision から呼ばれる。</summary>
    public void PlayBlockEffect() => shieldFlash?.Flash();

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

    // シーンビューで盾の構え側（前方）を可視化（調整用）
    private void OnDrawGizmosSelected()
    {
        Vector2 f = GetFacing();
        if (f.sqrMagnitude < 0.0001f) return;
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)(f.normalized * 1f));
    }
}
