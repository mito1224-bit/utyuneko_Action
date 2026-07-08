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
        timer = Mathf.Max(0f, boss.Difficulty.lockTime);
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
            bool wasEnraged = boss.IsEnraged; // 殴る前の強化状態を記録
            boss.HandleNormalBurstHit(unit, pc);

            // 撃破済み（Defeated）ならスタン落下で上書きしない（復活バグ防止）。
            // この一撃で強化に入った場合もスタン落下を重ねない（演出後にスタンが持ち越される／
            // 落下が先に走るのを防ぐ）。ダメージは既に入っている。
            // 判定は EventPaused ではなく IsEnraged の変化で見る（演出のポーズは遅れて立つことがあるため）。
            bool enteredEnrageThisHit = !wasEnraged && boss.IsEnraged;
            if (boss.CurrentState == this && !enteredEnrageThisHit)
            {
                boss.TransitionToState(boss.StateStunFall);
            }
        }
        else
        {
            boss.DestroyClone(unit, pc); // 偽物は消えるだけ
        }
    }
}