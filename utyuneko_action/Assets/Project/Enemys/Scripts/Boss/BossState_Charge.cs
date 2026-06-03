using UnityEngine;

/// <summary>
/// 突進攻撃。Enter時にプレイヤー方向を凍結し、chargeDuration 秒間その方向へ chargeSpeed で進む。
/// 移動は Rigidbody.linearVelocity で行う（既存と同じ作法）。
/// </summary>
public class BossState_Charge : IBossState
{
    private BossController b;
    private Vector3 chargeDir;
    private float timer;
    private Vector3 prevVelocity;

    public void Enter(BossController boss)
    {
        b = boss;
        if (b.movement != null) b.movement.IsLocked = true;

        if (b.player != null)
        {
            Vector3 d = b.player.position - b.transform.position;
            d.z = 0f;
            chargeDir = d.sqrMagnitude > 0.0001f ? d.normalized : b.transform.right;
        }
        else
        {
            chargeDir = b.transform.right;
        }

        prevVelocity = b.rb.linearVelocity;
        timer = b.chargeDuration;
    }

    public void UpdateState()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            b.TransitionToState(b.StateIdle);
        }
    }

    public void FixedUpdateState()
    {
        b.rb.linearVelocity = chargeDir * b.chargeSpeed;
    }

    public void Exit()
    {
        b.rb.linearVelocity = Vector3.zero;
    }
}
