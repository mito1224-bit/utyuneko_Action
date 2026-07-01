using System.Collections;
using UnityEngine;

/// <summary>
/// 貫通ゾーン専用スクリプト。エネミー本体の子GameObjectにアタッチして使う。
///
/// 【貫通条件】
///   プレイヤーが StateBurst（バースト突進中）のときだけ貫通を許可する。
///
/// 【コリジョン無視の復元タイミング】
///   OnTriggerExit ではなく、一定時間（ignoreCollisionDuration）後に復元する。
///   これにより「PierceZoneを抜けたがまだ本体内にいる」問題を回避する。
///   さらに安全策として、復元前にプレイヤーと本体が重なっていないか確認する。
/// </summary>
public class PierceZoneTrigger : MonoBehaviour
{
    public enum PierceAxis { Forward, Right, Up }

    [Header("判定対象")]
    public string playerTag = "Player";

    [Header("貫通時もダメージを与えるか")]
    [Tooltip("true: 貫通時もダメージを与える / false: 完全にすり抜けるだけ")]
    public bool dealDamageOnPierce = false;

    [Header("角度補正")]
    [Tooltip("貫通時にプレイヤーの速度をどれだけ貫通方向に寄せるか\n" +
             "0 = 補正なし / 0.7 = ほどよく矯正 / 1 = 完全に真っ直ぐ")]
    [Range(0f, 1f)]
    public float angleCorrection = 0.7f;

    [Header("貫通方向の軸")]
    [Tooltip("PierceZone の Transform のどの軸を「貫通方向」とするか")]
    public PierceAxis pierceDirection = PierceAxis.Forward;

    [Header("コリジョン無視の設定")]
    [Tooltip("Physics.IgnoreCollision を維持する秒数。\n" +
             "プレイヤーが本体を完全に通過するのに十分な時間を設定する。\n" +
             "目安: 0.3〜0.8秒（バースト速度に応じて調整）")]
    public float ignoreCollisionDuration = 0.5f;

    private EnemyHealth parentHealth;
    private EnemyCollision parentCollision;
    private Collider2D parentCollider;

    void Awake()
    {
        parentHealth = GetComponentInParent<EnemyHealth>();
        parentCollision = GetComponentInParent<EnemyCollision>();

        if (parentCollision != null)
            parentCollider = parentCollision.GetComponent<Collider2D>();

        if (parentCollision == null)
            Debug.LogWarning($"{gameObject.name}: 親に EnemyCollision が見つかりません。");
        if (parentHealth == null)
            Debug.LogWarning($"{gameObject.name}: 親に EnemyHealth が見つかりません。");

        Collider2D col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"{gameObject.name}: Collider2D の isTrigger が false です。");
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        // バースト中でなければ貫通しない
        if (!IsPlayerBursting(other.gameObject))
        {
            Debug.Log("PierceZone: バースト中でないため貫通をスキップ");
            return;
        }

        Collider2D playerCol = other.GetComponent<Collider2D>();

        // ★ Physics2D.IgnoreCollision でプレイヤーと本体の衝突を無視
        if (parentCollider != null && playerCol != null)
        {
            Physics2D.IgnoreCollision(parentCollider, playerCol, true);

            // 一定時間後に復元（OnTriggerExitではなくタイマーで管理）
            StartCoroutine(RestoreAfterDelay(playerCol, ignoreCollisionDuration));
        }

        // ★ 角度補正
        if (angleCorrection > 0f)
        {
            Rigidbody2D rb = other.GetComponent<Rigidbody2D>();
            if (rb != null) CorrectAngle(rb);
        }

        // ダメージ処理
        if (dealDamageOnPierce && parentHealth != null)
        {
            Rigidbody2D rb = other.GetComponent<Rigidbody2D>();
            if (rb != null) parentHealth.HandleHit(rb.linearVelocity.magnitude, other.transform.position);
        }

        Debug.Log($"PierceZone: 貫通を許可（{ignoreCollisionDuration}秒間コリジョン無視）");
    }

    // =========================================================
    //  タイマー式のコリジョン復元
    // =========================================================
    private IEnumerator RestoreAfterDelay(Collider2D playerCol, float delay)
    {
        yield return new WaitForSeconds(delay);

        // 安全策：復元前にまだ重なっていないか確認
        // 重なっていたら追加で待つ（最大5回リトライ）
        int retries = 0;
        while (retries < 5 && parentCollider != null && playerCol != null)
        {
            // 2つのColliderのBoundsが重なっているか簡易チェック
            if (parentCollider.bounds.Intersects(playerCol.bounds))
            {
                // まだ重なっている → もう少し待つ
                yield return new WaitForSeconds(0.1f);
                retries++;
            }
            else
            {
                // 離れた → 復元OK
                break;
            }
        }

        if (parentCollider != null && playerCol != null)
        {
            Physics2D.IgnoreCollision(parentCollider, playerCol, false);
            Debug.Log("PierceZone: コリジョン復元");
        }
    }

    // =========================================================
    //  角度補正
    // =========================================================
    private void CorrectAngle(Rigidbody2D playerRb)
    {
        Vector2 pierceDir = GetPierceDirection();
        if (pierceDir.sqrMagnitude < 0.001f) return;
        pierceDir.Normalize();

        Vector2 currentVel = playerRb.linearVelocity;
        float speed = currentVel.magnitude;
        if (speed < 0.1f) return;

        Vector2 currentDir = currentVel.normalized;

        // 逆方向から入ってきた場合は反転
        if (Vector2.Dot(currentDir, pierceDir) < 0f)
            pierceDir = -pierceDir;

        Vector2 correctedDir = Vector2.Lerp(currentDir, pierceDir, angleCorrection).normalized;
        playerRb.linearVelocity = correctedDir * speed;
    }

    // 2D（XY平面）での貫通方向。Forward(=Z)は奥行き軸なので2Dでは無効になり、補正はスキップされる。
    // XY平面では Right か Up を指定すること。
    private Vector2 GetPierceDirection()
    {
        switch (pierceDirection)
        {
            case PierceAxis.Right: return transform.right;
            case PierceAxis.Up: return transform.up;
            case PierceAxis.Forward:
            default: return transform.forward; // (Vector2)transform.forward = (0,0) → 補正スキップ
        }
    }

    // =========================================================
    //  バースト状態チェック
    // =========================================================
    private bool IsPlayerBursting(GameObject playerObj)
    {
        PlayerController pc = playerObj.GetComponent<PlayerController>();
        if (pc == null) return false;

        return pc.CurrentState == pc.StateBurst;
    }
}