using UnityEngine;

/// <summary>
/// スタン（本番）：地面に落ちて無防備な「ボーナス削りタイム」。
///   - 本物にバースト体当たり → スタン倍率（Phase.stunDamageMultiplier）が乗った一撃が「1回だけ」通る。
///     一撃が入ったら、そのまま少し間（stunRecoverDelay）を置いて復帰する（StunRecover ステートへ）。
///   - 攻撃されずに時間切れ（Difficulty.stunDuration）→ 帰還（Return）して攻撃を繰り返す。
///
/// HPを削り切った場合は Health 側から DefeatByHP が呼ばれて撃破になる（下の TryApplyBurstDamage の戻り値とは独立）。
/// </summary>
public class BossSniperState_Stunned : BossSniperStateBase
{
    public override void Enter(BossSniper boss)
    {
        base.Enter(boss);
        timer = Mathf.Max(0.5f, boss.Difficulty.stunDuration);
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
        if (!unit.IsReal) return;
        if (boss.Health == null) return;

        // スタン中の一撃（倍率つき・そのスタンで1回だけ）。入ったら間を置いて復帰へ
        bool applied = boss.Health.TryApplyBurstDamage(unit, pc, isStunned: true);
        if (applied)
        {
            // HPを削り切っていたら Health が既に撃破（Defeated）へ遷移させているので、ここでは触らない
            if (boss.CurrentState == this)
            {
                boss.TransitionToState(boss.StateStunRecover);
            }
        }
    }
}