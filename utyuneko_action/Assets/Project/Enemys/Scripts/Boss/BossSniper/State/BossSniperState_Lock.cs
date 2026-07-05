using UnityEngine;

/// <summary>
/// ロック：射線を固定したまま最終警告。まだ「見破り」を受け付けている（Aim と同じ反応）。
/// 時間が来たら発射（Fire）へ。
/// </summary>
public class BossSniperState_Lock : BossSniperStateBase
{
    public override void Enter(BossSniper boss)
    {
        base.Enter(boss);
        timer = Mathf.Max(0f, boss.Phase.lockTime);
    }

    public override void UpdateState()
    {
        foreach (BossSniperBeamUnit u in boss.Units)
        {
            u.LockTick();
        }

        if (Countdown())
        {
            boss.TransitionToState(boss.StateFire);
        }
    }

    public override void OnBurstHit(BossSniperBeamUnit unit, PlayerController pc)
    {
        if (unit.IsReal)
        {
            // 発射直前まで見破りは有効。通常ダメージを入れてスタン落下へ
            boss.HandleNormalBurstHit(unit, pc);
            boss.TransitionToState(boss.StateStunFall);
        }
        else
        {
            boss.DestroyClone(unit); // 偽物は消えるだけ
        }
    }
}