using UnityEngine;

/// <summary>
/// 反射可能なボスの弾。
/// - 通常: StraightBullet と同じく flyDirection に直進。
/// - Burst中プレイヤーに触れる: 方向を反転、所有者を「プレイヤー」に切り替え（reflected = true）。
/// - reflected 状態でボスに当たる: BossBarrier を Break() する。
/// - 直接プレイヤーに当たる（非Burst時）: プレイヤーにダメージを与える前提のフックを呼ぶ（ここではログのみ。
///   既存のプレイヤーダメージ処理に接続したい場合は ApplyDamageToPlayer を編集）。
///
/// セットアップ:
///   - Collider を isTrigger = true でアタッチ。
///   - Rigidbody は付けない（transform.Translate で動かすため）。
///     （3D物理に乗せたい場合は Kinematic Rigidbody でも可）
/// </summary>
[RequireComponent(typeof(Collider))]
public class ReflectableBullet : MonoBehaviour
{
    [Header("弾の設定")]
    public float speed = 8.0f;
    public float lifeTime = 4.0f;

    [Header("タグ")]
    public string playerTag = "Player";
    public string bossTag = "Boss";

    [Header("反射時の速度倍率")]
    [Tooltip("プレイヤーの攻撃を跳ね返したときに弾速をどれだけ増やすか")]
    public float reflectSpeedMultiplier = 1.5f;

    [Header("プレイヤーへの被弾扱い")]
    [Tooltip("反射されていない弾がプレイヤーに当たったときのダメージ（プレイヤー側の被弾実装と接続するためのフック用）")]
    public int playerDamage = 1;

    [Header("バリアへのダメージ")]
    [Tooltip("反射済みの弾がバリアに当たったときに削る量")]
    public int barrierDamage = 1;

    private Vector3 flyDirection = Vector3.right;
    private bool reflected = false;

    public bool IsReflected => reflected;

    public void SetDirection(Vector3 direction)
    {
        direction.z = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        flyDirection = direction.normalized;
    }

    void Start()
    {
        Collider c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        Vector3 delta = flyDirection * speed * Time.deltaTime;
        delta.z = 0f;
        transform.Translate(delta, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            // プレイヤーのCollider子オブジェクトでもステート判定が取れるよう、親階層をたどる
            PlayerController pc = other.GetComponentInParent<PlayerController>();
            bool playerIsBursting = pc != null && pc.CurrentState == pc.StateBurst;

            if (playerIsBursting && !reflected)
            {
                ReflectBack();
                return;
            }

            if (!reflected)
            {
                ApplyDamageToPlayer(other.gameObject);
                Destroy(gameObject);
            }
            return;
        }

        if (other.CompareTag(bossTag))
        {
            if (!reflected) return;

            BossBarrier barrier = other.GetComponentInParent<BossBarrier>();
            if (barrier != null && barrier.IsActive)
            {
                barrier.ApplyDamage(barrierDamage);
                Destroy(gameObject);
                return;
            }

            BossHealth health = other.GetComponentInParent<BossHealth>();
            if (health != null)
            {
                health.HandleHit(speed);
            }
            Destroy(gameObject);
        }
    }

    private void ReflectBack()
    {
        flyDirection = -flyDirection;
        flyDirection.z = 0f;
        flyDirection = flyDirection.normalized;
        speed *= reflectSpeedMultiplier;
        reflected = true;
        Debug.Log("ボスの弾を反射！");
    }

    /// <summary>
    /// プレイヤーへのダメージ適用フック。既存のプレイヤー被弾処理に合わせて拡張する。
    /// </summary>
    private void ApplyDamageToPlayer(GameObject playerObj)
    {
        Debug.Log($"プレイヤーに {playerDamage} ダメージ（被弾処理の接続先は要追加）");
    }
}
