using UnityEngine;

/// <summary>
/// ボスの移動。プレイヤーを追跡しつつ、希望距離 keepDistance を保つ。
/// 攻撃ステート中は IsLocked を true にされ、移動を停止する。
/// </summary>
[RequireComponent(typeof(BossController))]
public class BossMovement : MonoBehaviour
{
    [Header("追跡設定")]
    public float moveSpeed = 3.0f;

    [Tooltip("プレイヤーから保ちたい距離。これ以上近いと離れる、これ以下なら近づく")]
    public float keepDistance = 6.0f;

    [Tooltip("距離調整の許容幅。keepDistance ± deadZone の範囲では動かない")]
    public float deadZone = 1.0f;

    [Header("フェーズ2時の補正")]
    [Tooltip("フェーズ2での速度倍率")]
    public float phase2SpeedMultiplier = 1.4f;

    /// <summary>攻撃中など、外部から移動を止めたいときに true にする</summary>
    public bool IsLocked { get; set; } = false;

    private BossController boss;

    void Awake()
    {
        boss = GetComponent<BossController>();
    }

    void Update()
    {
        if (IsLocked) return;
        if (boss.player == null) return;

        Vector3 toPlayer = boss.player.position - transform.position;
        toPlayer.z = 0f;
        float dist = toPlayer.magnitude;
        if (dist < 0.01f) return;

        float speed = moveSpeed * (boss.currentPhase >= 2 ? phase2SpeedMultiplier : 1f);
        Vector3 dir;

        if (dist > keepDistance + deadZone)
        {
            dir = toPlayer.normalized;
        }
        else if (dist < keepDistance - deadZone)
        {
            dir = -toPlayer.normalized;
        }
        else
        {
            return;
        }

        Vector3 delta = dir * speed * Time.deltaTime;
        delta.z = 0f;
        transform.position += delta;
    }
}
