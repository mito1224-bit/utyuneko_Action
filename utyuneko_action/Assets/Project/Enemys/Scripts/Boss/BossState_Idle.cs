using UnityEngine;

/// <summary>
/// 攻撃と攻撃の間。短い待機後 ChooseAttack へ。
/// フェーズ2では idleBetweenAttacks * phase2IdleMultiplier に短縮される。
/// </summary>
public class BossState_Idle : IBossState
{
    private BossController b;
    private float timer;

    public void Enter(BossController boss)
    {
        b = boss;
        if (b.movement != null) b.movement.IsLocked = false;

        timer = b.idleBetweenAttacks;
        if (b.currentPhase >= 2) timer *= b.phase2IdleMultiplier;
    }

    public void UpdateState()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            b.TransitionToState(b.StateChooseAttack);
        }
    }

    public void FixedUpdateState() { }
    public void Exit() { }
}
