using UnityEngine;

/// <summary>
/// 攻撃抽選ステート。フェーズに応じて利用可能な攻撃から1つランダムに選び、即その攻撃ステートへ遷移する。
/// このステート自身は1フレームで抜ける。
///
/// フェーズ1: HomingShot, SpreadShot
/// フェーズ2: HomingShot, SpreadShot, Charge, AreaImpact （全部解放）
/// </summary>
public class BossState_ChooseAttack : IBossState
{
    private BossController b;

    public void Enter(BossController boss)
    {
        b = boss;
    }

    public void UpdateState()
    {
        IBossState next = PickAttack();
        b.TransitionToState(next);
    }

    public void FixedUpdateState() { }
    public void Exit() { }

    private IBossState PickAttack()
    {
        if (b.currentPhase >= 2)
        {
            int r = Random.Range(0, 4);
            switch (r)
            {
                case 0: return b.StateHomingShot;
                case 1: return b.StateSpreadShot;
                case 2: return b.StateCharge;
                default: return b.StateAreaImpact;
            }
        }
        else
        {
            int r = Random.Range(0, 2);
            return r == 0 ? (IBossState)b.StateHomingShot : b.StateSpreadShot;
        }
    }
}
