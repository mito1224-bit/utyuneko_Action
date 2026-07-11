/// <summary>
/// 帰還：分身を解除し、瞬間移動で巡回エリア内のランダム位置へ戻り、次の行動選択（ChooseNextAction）へ渡す。
/// 各攻撃の撃ち終わり・スタンからの復帰・被ダメージ後の全てで使う共通の「戻り」ステート。
/// </summary>
public class BossSniperState_Return : BossSniperStateBase
{
    private readonly BossSniperTeleport teleport = new BossSniperTeleport();

    public override void Enter(BossSniper boss)
    {
        base.Enter(boss);

        boss.DespawnClones();
        boss.SelfUnit.HideBeam();
        boss.RestoreFlightBody(); // スタン明けなら Dynamic → Kinematic に戻る
        boss.AttackTimer = boss.timeBetweenAttacks; // 攻撃サイクルの区切り：次の分身攻撃までの時間を補充

        teleport.Begin(boss.SelfUnit, boss.RandomPatrolPoint(), boss.teleportShrinkTime, boss.teleportExpandTime);
    }

    public override void UpdateState()
    {
        teleport.Update();

        if (!teleport.Running)
        {
            // 帰還完了 → 次の行動を選ぶ（巡回／全体攻撃／横一斉射のランダム。規定回数後は分身攻撃）
            boss.TransitionToState(boss.ChooseNextAction());
        }
    }
}