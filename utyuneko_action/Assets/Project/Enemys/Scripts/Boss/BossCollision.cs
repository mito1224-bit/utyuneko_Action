using UnityEngine;

/// <summary>
/// ボス本体の衝突ハンドラ。プレイヤーがBurstで突っ込んできたときに BossHealth.HandleHit を呼ぶ。
/// 反射そのものは PlayerState_Burst 側が EnemyCollision(Reflect) を見て担当するため、
/// ボスのGameObjectには EnemyCollision(Reflect, EnemyHealth無し) も併用してアタッチする。
/// このスクリプトは「Burstヒットからのダメージ伝達」だけを担当。
/// </summary>
[RequireComponent(typeof(BossHealth))]
public class BossCollision : MonoBehaviour
{
    [Header("判定対象")]
    public string playerTag = "Player";

    private BossHealth health;

    void Awake()
    {
        health = GetComponent<BossHealth>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag(playerTag)) return;

        PlayerController pc = collision.gameObject.GetComponentInParent<PlayerController>();
        bool isBursting = pc != null && pc.CurrentState == pc.StateBurst;
        if (!isBursting) return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        health.HandleHit(impactSpeed);
    }
}
