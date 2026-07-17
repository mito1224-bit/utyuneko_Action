using UnityEngine;

/// <summary>
/// エネミーの衝突タイプを一元管理するスクリプト。
///
/// Reflect : 全方向ノックバック（バースト中は壁のように反射させ、ダメージを与える）
/// Pierce  : 全方向すり抜け（isTrigger=true。バースト中のみダメージ）
///
/// バースト判定はプレイヤーの状態機械（PlayerController.CurrentState == StateBurst）を直接参照する。
/// ※以前は PlayerBurst レイヤーで判定していたが、レイヤーは PlayerLayerSwitcher が状態から派生させた
///   物理マトリクス用の信号にすぎず、レイヤー削除で壊れるため、状態を直接見る方式に統一した。
/// </summary>
public class EnemyCollision : MonoBehaviour
{
    public enum CollisionType { Reflect, Pierce }

    [Header("衝突タイプ")]
    [Tooltip(
        "Reflect : 全方向ノックバック（バースト中は反射＋ダメージ）\n" +
        "Pierce  : 全方向すり抜け（バースト中のみダメージ）"
    )]
    public CollisionType collisionType = CollisionType.Reflect;

    [Header("共通設定")]
    public string playerTag = "Player";

    [Header("ノックバック設定")]
    public float knockbackForce = 15f;

    [Header("Pierce専用設定（2コライダー方式）")]
    [Tooltip("Pierceタイプ: 床・壁に当たるソリッドのBodyコライダー。プレイヤーとは実行時に衝突無視する。\n" +
             "未設定ならこのGameObjectの非トリガーColliderを自動採用")]
    public Collider2D pierceBodyCollider;
    [Tooltip("Pierceタイプ: プレイヤー検出用トリガーコライダー（ダメージ判定）。\n" +
             "未設定ならこのGameObjectのトリガーColliderを自動採用")]
    public Collider2D pierceDamageTrigger;

    private Collider2D myCol;
    private EnemyHealth enemyHealth;
    private EnemyShield enemyShield; // 盾を持つ敵のみ。無ければ null

    private Rigidbody2D myRB;

    private float hitStopTime = 0.1f;

    void Awake()
    {
        myRB = GetComponent<Rigidbody2D>();

        enemyHealth = GetComponent<EnemyHealth>();
        enemyShield = GetComponent<EnemyShield>(); // 盾を持つ敵のみ

        // 回転だけ固定する（Z回転フリーズ）。位置は固定しない。
        // 巡回移動は EnemyMovement が rb.MovePosition でスイープ移動させ、静的な床・壁にぶつかって
        // 止まる前提（CLAUDE.md 2026/06/25 のすり抜け修正）。ここで FreezeAll にして位置を固定すると
        // MovePosition の移動・壁衝突と干渉してしまうため、位置の拘束はかけない。
        // ※プレイヤーに押されてズレる懸念は Rigidbody2D の Mass / 衝突解決側で調整する。
        if (myRB != null) myRB.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (collisionType == CollisionType.Pierce)
        {
            // Pierce: 環境用のソリッドBody + プレイヤー検出用トリガーの2コライダー構成
            AutoAssignPierceColliders();
            if (pierceBodyCollider != null) pierceBodyCollider.isTrigger = false; // 床・壁にソリッドで当たる
            if (pierceDamageTrigger != null) pierceDamageTrigger.isTrigger = true;  // プレイヤー検出はトリガー

            // ダメージ用トリガーが無いと OnTriggerEnter2D が発火せず、HandleHit が一度も呼ばれない
            // ＝プレイヤーが当てても敵が絶対に死なない。コライダー1個のプレハブに Pierce を設定すると起きる。
            // 2コライダー方式（Body=ソリッド + もう1つ=トリガー）にして両方を割り当てること。
            if (pierceDamageTrigger == null)
                Debug.LogWarning($"{gameObject.name}: Pierce なのにダメージ用トリガーColliderがありません。" +
                                 "トリガーのCollider2Dを追加して pierceDamageTrigger に割り当てないと敵が死にません。", this);
        }
        else
        {
            // Reflect: 本体コライダーは常にソリッド
            myCol = GetComponent<Collider2D>();
            if (myCol != null) myCol.isTrigger = false;
        }
    }

    void Start()
    {
        if (collisionType != CollisionType.Pierce || pierceBodyCollider == null) return;

        // プレイヤーはすり抜けさせたいので、Bodyコライダーとプレイヤーの衝突だけ無視する。
        // （床・壁との衝突は維持されるので、すり抜けなくなる）
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null) return;

        foreach (var playerCol in player.GetComponentsInChildren<Collider2D>())
        {
            if (playerCol != null)
                Physics2D.IgnoreCollision(pierceBodyCollider, playerCol, true);
        }
    }

    // Inspector 未設定時に、自身のコライダーから Body/トリガーを自動割り当てする
    private void AutoAssignPierceColliders()
    {
        if (pierceBodyCollider != null && pierceDamageTrigger != null) return;

        foreach (var col in GetComponents<Collider2D>())
        {
            if (col.isTrigger)
            {
                if (pierceDamageTrigger == null) pierceDamageTrigger = col;
            }
            else
            {
                if (pierceBodyCollider == null) pierceBodyCollider = col;
            }
        }
    }

    // =========================================================
    //  Reflect → OnCollisionEnter2D
    // =========================================================
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (enemyHealth.IsDeadFlg) return;

        if (!collision.gameObject.CompareTag(playerTag)) return;

        // プレイヤーがバースト状態かどうかを状態機械から直接判定（レイヤーに依存しない）
        PlayerController p = collision.gameObject.GetComponent<PlayerController>();
        bool isBursting = IsBursting(p);

        // 盾（本体以外のコライダー）に当たった分は本体ダメージにしない＝物理ガード（BossChargerController と同じ流儀）。
        // 盾は正面を物理的に覆うソリッドコライダー（RefObjレイヤー）で、バーストの反射はプレイヤー側（壁扱い）が担当する。
        // ここでは弾いた演出だけ出して抜ける（本体へのダメージ・ノックバック・ヒットストップは出さない）。
        if (myCol != null && collision.otherCollider != myCol)
        {
            // 接触点から火花を出したいので、衝突の接点を渡す（接点が取れない場合は盾の位置で代用）。
            if (isBursting)
            {
                Vector3 blockPoint = collision.contactCount > 0
                    ? (Vector3)collision.GetContact(0).point
                    : collision.otherCollider.bounds.center;
                enemyShield?.PlayBlockEffect(blockPoint);
            }
            return;
        }

        float impactSpeed = collision.relativeVelocity.magnitude;
        Vector3 hitFromPos = collision.transform.position;

        // バースト中に当たったときだけバースト回数の回復を行う（ジャンプ接触では回復させない）
        if (p && isBursting) p.OnEnemyKilledInBurst();

        // Reflect: 反射そのものは PlayerState_Burst（壁と同じ Vector3.Reflect）が担当する。
        // バースト中もすり抜けさせず、壁のように跳ね返す。
        // 非バースト時はノックバックで弾く。ダメージはバースト時のみ（EnemyHealth 側でゲート）。
        if (!isBursting)
        {
            ApplyKnockback(collision.rigidbody, collision.transform.position);
        }
        enemyHealth?.HandleHit(impactSpeed, hitFromPos, isBursting);

        TimeManager.Instance.TriggerGlobalHitStop(hitStopTime);
    }

    // =========================================================
    //  Pierce → OnTriggerEnter（すり抜けつつダメージを与える）
    // =========================================================
    void OnTriggerEnter2D(Collider2D other)
    {
        if (enemyHealth.IsDeadFlg) return;

        if (collisionType != CollisionType.Pierce) return;
        if (!other.CompareTag(playerTag)) return;

        // プレイヤーがバースト状態かどうかを状態機械から直接判定（レイヤーに依存しない）
        PlayerController p = other.gameObject.GetComponent<PlayerController>();
        bool isBursting = IsBursting(p);

        // バースト中に当たったときだけバースト回数の回復を行う
        if (p && isBursting) p.OnEnemyKilledInBurst();

        Rigidbody2D rb = other.GetComponent<Rigidbody2D>();
        if (rb != null)
            enemyHealth?.HandleHit(rb.linearVelocity.magnitude, other.transform.position, isBursting);

        TimeManager.Instance.TriggerGlobalHitStop(hitStopTime);
    }

    // =========================================================
    //  共通ユーティリティ
    // =========================================================

    // プレイヤーがバースト攻撃中か。状態機械（PlayerController）を直接参照するのが最も正確。
    // タグは「プレイヤーかどうか」の識別用、レイヤーは物理マトリクス用であって、状態の判定には使わない。
    private bool IsBursting(PlayerController p)
    {
        return p != null && p.CurrentState == p.StateBurst;
    }

    private void ApplyKnockback(Rigidbody2D targetRb, Vector3 targetPos)
    {
        if (targetRb == null) return;

        // XY平面で水平方向（X）にのみ弾く（縦方向には飛ばさない）
        Vector2 dir = (Vector2)(targetPos - transform.position);
        dir.y = 0f;
        dir = dir.normalized;

        targetRb.linearVelocity = Vector2.zero;
        targetRb.AddForce(dir * knockbackForce, ForceMode2D.Impulse);
    }
}
