using UnityEngine;

/// <summary>
/// スタン（本番）：地面に落ちて無防備。ボスがダメージを受けられるのは、このステートの間だけ。
///   - 本物にバースト体当たり → ダメージ（フェーズ進行 or 撃破。処理は BossSniper.ApplyDamage）
///   - 時間切れ → 帰還（Return）して同じフェーズで攻撃を繰り返す
/// </summary>
public class BossSniperState_Stunned : BossSniperStateBase
{
    public override void Enter(BossSniper boss)
    {
        base.Enter(boss);
        timer = Mathf.Max(0.5f, boss.Phase.stunDuration);
    }

    public override void UpdateState()
    {
        if (Countdown())
        {
            boss.TransitionToState(boss.StateReturn); // 攻撃されずに時間切れ → 復帰
        }
    }

    public override void OnBurstHit(BossSniperBeamUnit unit, PlayerController pc)
    {
        if (unit.IsReal)
        {
            boss.ApplyDamage(pc);
        }
    }
}
