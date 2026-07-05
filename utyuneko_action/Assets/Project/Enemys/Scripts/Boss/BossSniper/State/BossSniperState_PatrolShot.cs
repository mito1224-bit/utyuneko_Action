using UnityEngine;

/// <summary>
/// 出現撃ち：巡回中の瞬間移動が攻撃に化けたもの。
///   瞬間移動 → 出現した瞬間に狙いを固定（赤いロック射線） → 短いレーザー → （残弾があれば撃ち直し） → 巡回へ戻る。
///   1回のテレポートで撃つ回数は Phase.patrolShotCount（フェーズが進むと連射が増える）。
///
/// 狙いの決め方（各弾のロック開始時に1回だけ計算し、以後プレイヤーを追わない）:
///   - 偏差撃ち（確率 boss.patrolShotLeadChance）: プレイヤーの現在速度から
///     patrolShotLeadTime 秒先の位置を狙う。同じ方向へ走り続けていると当たる。
///   - 直撃狙い（残りの確率）: 出現時点のプレイヤー位置を狙う。立ち止まっていると当たる。
///   両方が混ざるので、「赤い射線が見えたら今の自分の動きを変える」のが正解の駆け引きになる。
///
/// 分身攻撃とは別の牽制なので、この間もボスは無敵（OnBurstHit を実装していない）。
/// 分身攻撃までの AttackTimer はこの間も数え続ける（出現撃ちのせいで本命の攻撃が遠のかないように）。
/// </summary>
public class BossSniperState_PatrolShot : BossSniperStateBase
{
    private enum Step { Teleporting, Locking, Firing }
    private Step step;

    private readonly BossSniperTeleport teleport = new BossSniperTeleport();
    private int shotsRemaining; // このテレポートで残っている発射回数（Phase.patrolShotCount から数える）

    public override void Enter(BossSniper boss)
    {
        base.Enter(boss);

        // プレイヤー不在なら撃てないので巡回へ戻る
        if (boss.Player == null)
        {
            boss.TransitionToState(boss.StatePatrol);
            return;
        }

        shotsRemaining = Mathf.Max(1, boss.Difficulty.patrolShotCount);

        step = Step.Teleporting;
        teleport.Begin(boss.SelfUnit, boss.RandomPatrolPoint(), boss.teleportShrinkTime, boss.teleportExpandTime);
    }

    public override void UpdateState()
    {
        boss.AttackTimer -= Time.deltaTime; // 分身攻撃までのカウントは止めない

        switch (step)
        {
            case Step.Teleporting:
                boss.SelfUnit.FaceTick();
                teleport.Update();
                if (!teleport.Running)
                {
                    BeginLock(); // 出現した瞬間に狙いを固定
                }
                break;

            case Step.Locking:
                boss.SelfUnit.LockTick(); // 固定した狙いを赤い射線で見せる（最終警告）
                if (Countdown())
                {
                    step = Step.Firing;
                    timer = Mathf.Max(0f, boss.patrolShotFireDuration);
                    boss.SelfUnit.FireTick(); // fireDuration=0 でも最低1回は判定
                }
                break;

            case Step.Firing:
                boss.SelfUnit.FireTick();
                if (Countdown())
                {
                    shotsRemaining--;
                    if (shotsRemaining > 0)
                    {
                        BeginLock(); // まだ残弾がある → 狙いを付け直して次弾（偏差／直撃も再抽選）
                    }
                    else
                    {
                        boss.TransitionToState(boss.StatePatrol);
                    }
                }
                break;
        }
    }

    // 出現した瞬間に狙いを1回だけ決めて固定する
    private void BeginLock()
    {
        Vector2 aimPoint = boss.Player.position;

        // 偏差撃ち：プレイヤーの現在速度から少し先の位置を狙う
        if (boss.PlayerRb != null && Random.value < boss.patrolShotLeadChance)
        {
            aimPoint += boss.PlayerRb.linearVelocity * boss.patrolShotLeadTime;
        }

        boss.SelfUnit.SetAimPoint(aimPoint);

        step = Step.Locking;
        timer = Mathf.Max(0.05f, boss.Difficulty.patrolShotLockTime);
    }

    public override void Exit()
    {
        boss.SelfUnit.HideBeam();
    }

    // 出現撃ち中も本物に当てれば通常ダメージ（無敵時間つき）
    public override void OnBurstHit(BossSniperBeamUnit unit, PlayerController pc)
    {
        boss.HandleNormalBurstHit(unit, pc);
    }
}