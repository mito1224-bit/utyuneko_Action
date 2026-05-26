using System.Collections;
using UnityEngine;

/// <summary>
/// 貫通ゾーン専用スクリプト。エネミー本体の子GameObjectにアタッチして使う。
///
/// 【貫通条件】
///   プレイヤーが StateBurst（バースト突進中）のときだけ貫通を許可する。
///
/// 【改善点①：Physics.IgnoreCollision】
///   Collider.enabled を切り替えるのではなく、プレイヤーと本体Colliderの間の
///   衝突だけをピンポイントで無視する。他のオブジェクトとの当たり判定を壊さない。
///
/// 【改善点②：角度補正】
///   貫通が発動したとき、プレイヤーの速度ベクトルを PierceZone の法線方向に矯正する。
///   斜めに入っても引っかからず、きれいにすり抜けられる。
///   angleCorrection を 0 にすると補正なし、1 で完全補正（デフォルト 0.7 = ほどよく矯正）。
///
/// 【Unity側のセットアップ】
///   1. エネミー本体の子GameObjectを作成（名前例: "PierceZone"）
///   2. 子オブジェクトに BoxCollider を追加 → isTrigger = true に設定
///   3. 子オブジェクトに このスクリプト をアタッチ
///   4. Sceneビューで BoxCollider をエネミー前面より外側にはみ出るよう調整
///   5. PierceZone の Transform の前方向（ローカル Z+ or X+）が貫通方向になる
///      → pierceDirection で軸を選択可能
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
    [Tooltip("貫通時にプレイヤーの速度ベクトルをどれだけ貫通方向に寄せるか\n" +
             "0 = 補正なし / 0.7 = ほどよく矯正（推奨）/ 1 = 完全に真っ直ぐ")]
    [Range(0f, 1f)]
    public float angleCorrection = 0.7f;

    [Header("貫通方向の軸")]
    [Tooltip("PierceZone の Transform のどの軸を「貫通方向」とするか")]
    public PierceAxis pierceDirection = PierceAxis.Forward;

    private EnemyHealth parentHealth;
    private EnemyCollision parentCollision;
    private Collider parentCollider;

    void Awake()
    {
        parentHealth = GetComponentInParent<EnemyHealth>();
        parentCollision = GetComponentInParent<EnemyCollision>();

        if (parentCollision != null)
            parentCollider = parentCollision.GetComponent<Collider>();

        if (parentCollision == null)
            Debug.LogWarning($"{gameObject.name}: 親に EnemyCollision が見つかりません。");
        if (parentHealth == null)
            Debug.LogWarning($"{gameObject.name}: 親に EnemyHealth が見つかりません。");

        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"{gameObject.name}: Collider の isTrigger が false です。true に設定してください。");
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        // バースト中でなければ貫通しない
        if (!IsPlayerBursting(other.gameObject))
        {
            Debug.Log("PierceZone: バースト中でないため貫通をスキップ");
            return;
        }

        // ★ 改善①：Physics.IgnoreCollision でプレイヤーと本体の衝突を無視
        if (parentCollider != null)
        {
            Collider playerCol = other.GetComponent<Collider>();
            if (playerCol != null)
                Physics.IgnoreCollision(parentCollider, playerCol, true);
        }

        // ★ 改善②：角度補正 — プレイヤーの速度を貫通方向に矯正
        if (angleCorrection > 0f)
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
            {
                CorrectAngle(rb);
            }
        }

        // ダメージ処理
        if (dealDamageOnPierce && parentHealth != null)
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
                parentHealth.HandleHit(rb.linearVelocity.magnitude);
        }

        Debug.Log("PierceZone: バースト中のため貫通を許可（IgnoreCollision + 角度補正）");
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        // 抜けたらコリジョン無視を解除（1フレーム遅延で安全に）
        StartCoroutine(RestoreCollision(other.GetComponent<Collider>()));
    }

    // =========================================================
    //  角度補正
    // =========================================================
    private void CorrectAngle(Rigidbody playerRb)
    {
        // PierceZone の Transform から貫通方向を取得
        Vector3 pierceDir = GetPierceDirection();

        // 2.5D の場合は Y 成分を無視して XZ 平面で補正
        pierceDir.y = 0f;
        pierceDir.Normalize();

        if (pierceDir.sqrMagnitude < 0.001f) return;

        // 現在の速度
        Vector3 currentVel = playerRb.linearVelocity;
        float speed = currentVel.magnitude;

        if (speed < 0.1f) return;

        // 現在の進行方向（正規化）
        Vector3 currentDir = currentVel.normalized;

        // 貫通方向と現在の方向の内積で、同じ向きかチェック
        // （逆方向から入ってきた場合は反転）
        if (Vector3.Dot(currentDir, pierceDir) < 0f)
            pierceDir = -pierceDir;

        // 現在の方向と貫通方向を angleCorrection の割合でブレンド
        // 0 = 元の方向のまま、1 = 完全に貫通方向
        Vector3 correctedDir = Vector3.Lerp(currentDir, pierceDir, angleCorrection).normalized;

        // 速度を維持したまま方向だけ変える
        playerRb.linearVelocity = correctedDir * speed;
    }

    /// <summary>
    /// Inspector で設定された軸に基づいて、貫通方向のワールドベクトルを返す。
    /// </summary>
    private Vector3 GetPierceDirection()
    {
        switch (pierceDirection)
        {
            case PierceAxis.Right: return transform.right;
            case PierceAxis.Up: return transform.up;
            case PierceAxis.Forward:
            default: return transform.forward;
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
        //  return pc.; // PlayerController に追加する public プロパティ
    }

    // =========================================================
    //  コリジョン復元
    // =========================================================
    private IEnumerator RestoreCollision(Collider playerCol)
    {
        // 1フレーム待ってから復元（同フレーム競合を防ぐ）
        yield return null;

        if (parentCollider != null && playerCol != null)
            Physics.IgnoreCollision(parentCollider, playerCol, false);
    }
}//using System.Collections;
 //using UnityEngine;

///// <summary>
///// 貫通ゾーン専用スクリプト。エネミー本体の子GameObjectにアタッチして使う。
/////
///// 【Unity側のセットアップ】
/////   1. エネミー本体の子GameObjectを作成（名前例: "PierceZone"）
/////   2. 子オブジェクトに BoxCollider を追加 → isTrigger = true に設定
/////   3. 子オブジェクトに このスクリプト をアタッチ
/////   4. Sceneビューで BoxCollider をエネミー前面より少し外側にはみ出るよう調整
/////      （プレイヤーの SphereCollider の半径分だけ余分に前に出すのが目安）
/////
///// 【isPiercing フラグの役割】
/////   SphereCollider は球体のため、PierceZone に入る前に
/////   本体 BoxCollider の角に先当たりしてしまうことがある。
/////   OnTriggerEnter で isPiercing=true を立てておくことで、
/////   本体側の OnCollisionEnter での反射処理をブロックする。
///// </summary>
//public class PierceZoneTrigger : MonoBehaviour
//{
//    [Header("判定対象")]
//    public string playerTag = "Player";

//    [Header("貫通時もダメージを与えるか")]
//    [Tooltip("true: 貫通時もダメージを与える / false: 完全にすり抜けるだけ")]
//    public bool dealDamageOnPierce = false;


//    private EnemyHealth parentHealth;
//    private EnemyCollision parentCollision;
//    private Collider parentCollider;

//    void Awake()
//    {
//        parentHealth = GetComponentInParent<EnemyHealth>();
//        parentCollision = GetComponentInParent<EnemyCollision>();

//        if (parentCollision == null)
//            Debug.LogWarning($"{gameObject.name}: 親に EnemyCollision が見つかりません。");

//        if (parentHealth == null)
//            Debug.LogWarning($"{gameObject.name}: 親に EnemyHealth が見つかりません。");

//        Collider col = GetComponent<Collider>();
//        if (col != null && !col.isTrigger)
//            Debug.LogWarning($"{gameObject.name}: Collider の isTrigger が false です。true に設定してください。");
//    }

//    void OnTriggerEnter(Collider other)
//    {

//        Debug.Log($"TriggerEnter: {other.gameObject.name} / tag: {other.tag}");
//        if (!other.CompareTag(playerTag)) return;


//        if (!IsPlayerBursting(other.gameObject))
//        {
//            // バースト中でなければ貫通しない → 本体の反射処理に任せる
//            Debug.Log("PierceZone: バースト中でないため貫通をスキップ");
//            return;
//        }

//        // 本体のCollider自体をオフにしてしまう
//        // → OnCollisionEnter が物理的に発火しなくなる
//        if (parentCollider != null)
//        {
//            Collider playerCol = other.GetComponent<Collider>();
//            if (playerCol != null)
//            {
//                Physics.IgnoreCollision(parentCollider, playerCol, true);
//            }
//            //Collider enemyCol = parentCollision.GetComponent<Collider>();
//            //if (enemyCol != null) 
//            //    enemyCol.enabled = false;
//        }

//        if (dealDamageOnPierce && parentHealth != null)
//        {
//            Rigidbody rb = other.GetComponent<Rigidbody>();
//            if (rb != null) parentHealth.HandleHit(rb.linearVelocity.magnitude);
//        }
//    }

//    void OnTriggerExit(Collider other)
//    {
//        if (!other.CompareTag(playerTag)) return;

//        // プレイヤーが抜けたら本体Colliderを戻す
//        // StartCoroutine(ReEnableCollider());

//        //抜けたらコリジョン無視を解除（1フレーム遅延で）※同フレームの競合を防ぐため    
//        StartCoroutine(RestoreCollision(other.GetComponent<Collider>()));
//    }
//    private bool IsPlayerBursting(GameObject playerObj)
//    {
//        // PlayerHealth 経由で PlayerController を取得し、ステートを確認する
//        PlayerController pc = playerObj.GetComponent<PlayerController>();
//        if (pc == null) return false;

//        return pc.CurrentState == pc.StateBurst;
//    }
//    //private IEnumerator ReEnableCollider()
//    //{
//    //    yield return null; // 1フレーム待つ
//    //    if (parentCollision != null)
//    //    {
//    //        Collider enemyCol = parentCollision.GetComponent<Collider>();
//    //        if (enemyCol != null) enemyCol.enabled = true;
//    //    }
//    //}
//    // =========================================================
//    //  コリジョン復元
//    // =========================================================
//    private IEnumerator RestoreCollision(Collider playerCol)
//    {
//        // 1フレーム待ってから復元（同フレーム競合を防ぐ）
//        yield return null;

//        if (parentCollider != null && playerCol != null)
//            Physics.IgnoreCollision(parentCollider, playerCol, false);
//    }
//}