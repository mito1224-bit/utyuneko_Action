using System.Collections;
using UnityEngine;

/// <summary>
/// 貫通ゾーン専用スクリプト。エネミー本体の子GameObjectにアタッチして使う。
///
/// 【Unity側のセットアップ】
///   1. エネミー本体の子GameObjectを作成（名前例: "PierceZone"）
///   2. 子オブジェクトに BoxCollider を追加 → isTrigger = true に設定
///   3. 子オブジェクトに このスクリプト をアタッチ
///   4. Sceneビューで BoxCollider をエネミー前面より少し外側にはみ出るよう調整
///      （プレイヤーの SphereCollider の半径分だけ余分に前に出すのが目安）
///
/// 【isPiercing フラグの役割】
///   SphereCollider は球体のため、PierceZone に入る前に
///   本体 BoxCollider の角に先当たりしてしまうことがある。
///   OnTriggerEnter で isPiercing=true を立てておくことで、
///   本体側の OnCollisionEnter での反射処理をブロックする。
/// </summary>
public class PierceZoneTrigger : MonoBehaviour
{
    [Header("判定対象")]
    public string playerTag = "Player";

    [Header("貫通時もダメージを与えるか")]
    [Tooltip("true: 貫通時もダメージを与える / false: 完全にすり抜けるだけ")]
    public bool dealDamageOnPierce = false;

    private EnemyHealth parentHealth;
    private EnemyCollision parentCollision;

    void Awake()
    {
        parentHealth = GetComponentInParent<EnemyHealth>();
        parentCollision = GetComponentInParent<EnemyCollision>();

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

        // 本体の OnCollisionEnter での反射をブロック
        if (parentCollision != null)
            parentCollision.isPiercing = true;

        // ダメージあり設定の場合のみ EnemyHealth に通知
        if (dealDamageOnPierce && parentHealth != null)
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
                parentHealth.HandleHit(rb.linearVelocity.magnitude);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        // プレイヤーが抜けたらフラグを戻す
        // 少し遅延させることで、Trigger と Collision の
        // 同フレーム発火によるフラグの取り消しを防ぐ
        StartCoroutine(ResetPiercingFlag());
    }

    private IEnumerator ResetPiercingFlag()
    {
        // 1フレーム待ってからリセット
        yield return null;

        if (parentCollision != null)
            parentCollision.isPiercing = false;
    }
}