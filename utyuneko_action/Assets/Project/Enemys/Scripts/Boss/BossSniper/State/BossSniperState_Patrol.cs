using UnityEngine;

/// <summary>
/// 通常状態：巡回ポイントが作る多角形エリアの内側を、一定間隔でランダムに瞬間移動する。
/// 瞬間移動のタイミングで、確率（Phase.patrolShotChance）により「出現撃ち」（PatrolShot）に化ける。
/// 分身攻撃までの残り時間（boss.AttackTimer）が満ちたら分身展開（Split）へ。
/// AttackTimer はボス側が持つので、出現撃ちを挟んでもリセットされない。
/// この間ボスは無敵（OnBurstHit を実装していないので、当たっても何も起きない）。
/// </summary>
public class BossSniperState_Patrol : BossSniperStateBase
{
    private readonly BossSniperTeleport teleport = new BossSniperTeleport();
    private float teleportTimer;

    public override void Enter(BossSniper boss)
    {
        base.Enter(boss);
        boss.RestoreFlightBody();
        boss.SelfUnit.HideBeam();
        teleportTimer = boss.teleportInterval;
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

        // 分身攻撃の間隔が満ちたら（テレポ中でないときに）次の攻撃へ。
        // 行き先はフェーズにより異なる：通常は 全体攻撃→分身、最終フェーズは全体攻撃を挟むかランダム
        boss.AttackTimer -= Time.deltaTime;
        if (boss.AttackTimer <= 0f)
        {
            boss.TransitionToState(boss.NextAttackAfterPatrol());
            return;
        }

        // 一定間隔で瞬間移動。確率で「出現撃ち」に化ける（テレポート自体は PatrolShot 側が行う）
        teleportTimer -= Time.deltaTime;
        if (teleportTimer <= 0f)
        {
            teleportTimer = boss.teleportInterval;

            if (Random.value < boss.Difficulty.patrolShotChance)
            {
                boss.TransitionToState(boss.StatePatrolShot);
            }
            else
            {
                teleport.Begin(boss.SelfUnit, boss.RandomPatrolPoint(), boss.teleportShrinkTime, boss.teleportExpandTime);
            }
        }
    }

    // 巡回中も本物に当てれば通常ダメージ（無敵時間つき）。テレポで消えている間は当たり判定が無効
    public override void OnBurstHit(BossSniperBeamUnit unit, PlayerController pc)
    {
        boss.HandleNormalBurstHit(unit, pc);
    }
}