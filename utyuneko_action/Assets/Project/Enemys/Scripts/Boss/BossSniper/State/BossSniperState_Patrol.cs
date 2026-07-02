using UnityEngine;

/// <summary>
/// 通常状態：巡回ポイントが作る多角形エリアの内側を、一定間隔でランダムに瞬間移動する。
/// 攻撃の間隔（timeBetweenAttacks）が満ちたら分身展開（Split）へ。
/// この間ボスは無敵（OnBurstHit を実装していないので、当たっても何も起きない）。
/// </summary>
public class BossSniperState_Patrol : BossSniperStateBase
{
    private readonly BossSniperTeleport teleport = new BossSniperTeleport();
    private float teleportTimer;
    private float attackTimer;

    public override void Enter(BossSniper boss)
    {
        base.Enter(boss);
        boss.RestoreFlightBody();
        boss.SelfUnit.HideBeam();
        teleportTimer = boss.teleportInterval;
        attackTimer = boss.timeBetweenAttacks;
    }

    public override void UpdateState()
    {
        boss.SelfUnit.FaceTick(); // 待機中もプレイヤーの方を向く

        // 瞬間移動の進行中はそれを待つ
        if (teleport.Running)
        {
            teleport.Update();
            return;
        }

        // 一定間隔でエリア内のランダム位置へ瞬間移動
        teleportTimer -= Time.deltaTime;
        if (teleportTimer <= 0f)
        {
            teleportTimer = boss.teleportInterval;
            teleport.Begin(boss.SelfUnit, boss.RandomPatrolPoint(), boss.teleportShrinkTime, boss.teleportExpandTime);
            return;
        }

        // 攻撃間隔が満ちたら（テレポ中でないときに）分身展開へ
        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            boss.TransitionToState(boss.StateSplit);
        }
    }
}
