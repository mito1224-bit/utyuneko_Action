using UnityEngine;

/// <summary>
/// 着地猶予：地面に落ちた直後の短い猶予時間。まだ無敵（OnBurstHit を実装しない）。
/// 猶予が明けたら Stunned（ダメージ受付）へ。
/// </summary>
public class BossSniperState_StunGrace : BossSniperStateBase
{
    public override void Enter(BossSniper boss)
    {
        base.Enter(boss);
        timer = Mathf.Max(0f, boss.postLandingGrace);
    }

    public override void UpdateState()
    {
        if (Countdown())
        {
            boss.TransitionToState(boss.StateStunned);
        }
    }
}
