using UnityEngine;

/// <summary>
/// 照準：全ユニットの射線がプレイヤーを追従する。
/// このステートと Lock の間が「本物を見破ってスタンさせられる」時間。
///   - 本物にバースト体当たり → StunFall（スタン落下）へ
///   - 偽物にバースト体当たり → その偽物が消えるだけ（ペナルティなし）
/// </summary>
public class BossSniperState_Aim : BossSniperStateBase
{
    public override void Enter(BossSniper boss)
    {
        base.Enter(boss);
        timer = Mathf.Max(0f, boss.Phase.aimTime);
    }

    public override void UpdateState()
    {
        foreach (BossSniperBeamUnit u in boss.Units)
        {
            u.AimTick();
        }

        if (Countdown())
        {
            boss.TransitionToState(boss.StateLock);
        }
    }

    public override void OnBurstHit(BossSniperBeamUnit unit, PlayerController pc)
    {
        if (unit.IsReal)
        {
            boss.TransitionToState(boss.StateStunFall); // 本物を見破られた！
        }
        else
        {
            boss.DestroyClone(unit);
        }
    }
}
