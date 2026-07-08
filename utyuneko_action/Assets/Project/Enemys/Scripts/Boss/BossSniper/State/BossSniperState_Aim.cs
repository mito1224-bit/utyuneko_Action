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
        timer = Mathf.Max(0f, boss.Difficulty.aimTime);
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
            // 本物を見破った：通常ダメージを入れたうえでスタン落下へ（スタン中は倍率一撃のボーナス帯）
            bool wasEnraged = boss.IsEnraged; // 殴る前の強化状態を記録
            boss.HandleNormalBurstHit(unit, pc);

            // この一撃でHPを削り切った場合、ダメージ処理の中で既に撃破（Defeated）へ遷移している。
            // その場合はスタン落下で上書きしない（撃破後の復活バグ防止）。
            // また、この一撃でHPが半分を下回って強化に入った場合は、スタン落下を重ねない。
            // 強化演出（中央テレポート〜巡回復帰）が始まるので、スタンを重ねると演出後に
            // スタンが持ち越される／落下が先に走るなどの衝突が起きる。ダメージは既に入っている。
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