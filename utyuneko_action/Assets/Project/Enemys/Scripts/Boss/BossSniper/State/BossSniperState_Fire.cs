using UnityEngine;

/// <summary>
/// 発射：全ユニットが固定した射線上にレーザーを撃つ。
/// 発射が始まった時点で「攻撃される前に見破る」ウィンドウは終了している
/// （OnBurstHit を実装していないので、本物・偽物とも当たっても何も起きない）。
/// 撃ち終えたら Return（瞬間移動で巡回へ帰還）へ。
/// </summary>
public class BossSniperState_Fire : BossSniperStateBase
{
    public override void Enter(BossSniper boss)
    {
        base.Enter(boss);
        timer = Mathf.Max(0f, boss.Phase.fireDuration);

        // fireDuration=0 でも最低1回は判定
        foreach (BossSniperBeamUnit u in boss.Units)
        {
            u.FireTick();
        }
    }

    public override void UpdateState()
    {
        foreach (BossSniperBeamUnit u in boss.Units)
        {
            u.FireTick();
        }

        if (Countdown())
        {
            boss.TransitionToState(boss.StateReturn);
        }
    }
}
