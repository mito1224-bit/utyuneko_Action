using UnityEngine;

/// <summary>
/// スタン復帰待ち：スタン中に倍率付きの一撃を受けたあと、少し間（stunRecoverDelay）を置いてから復帰する。
/// この間は無敵（OnBurstHit を実装しない）。地面に倒れたまま待ち、間が明けたら帰還（Return）へ。
/// HPを削り切ってフェーズが進む場合は Stunned から直接 Return に遷移するので、このステートには来ない。
/// </summary>
public class BossSniperState_StunRecover : BossSniperStateBase
{
    public override void Enter(BossSniper boss)
    {
        base.Enter(boss);
        timer = Mathf.Max(0f, boss.stunRecoverDelay);
    }

    public override void UpdateState()
    {
        if (Countdown())
        {
            boss.TransitionToState(boss.StateReturn);
        }
    }

    // OnBurstHit は実装しない ＝ 一撃を入れたあとの復帰待ちは無敵
}