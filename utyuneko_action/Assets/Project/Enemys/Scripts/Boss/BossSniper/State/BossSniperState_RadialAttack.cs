using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全体攻撃：ステージ中央（巡回ポイントの重心）へ瞬間移動し、
/// 溜め（初弾のロック射線を提示）を挟んでから、回転しながら四方八方へ1本ずつスナイパー連射する。
///   瞬間移動 → 溜め（初弾の射線を赤く提示） → [ロック（赤い射線） → レーザー] を繰り返し → 分身攻撃へ。
///
/// 回転の仕方:
///   - 初弾はプレイヤーの方向を狙う。回転方向は「プレイヤーに近い側から回り込む」ように選ぶ
///     （初弾の次がプレイヤー正面をなぞる向き）。
///   - 1発ごとに radialAngleStep 度ずつ回転する。全弾の射角は Enter で先に決めておく。
///
/// 溜め（radialWindupTime・全フェーズ共通）:
///   - 中央到着後、初弾のロック射線（赤・細）を出したまま待ち、プレイヤーに避ける準備をさせる。
///   - 予告線（先読みの複数線）は使わない。表示するのは常に「今まさに狙っている1本」だけ。
///
/// 発射回数・ロック時間はフェーズ設定（Phase.radialShotCount / radialLockTime）でスケール。
///
/// 牽制フェーズの一部なので、この間もボスは無敵（OnBurstHit を実装していない）。
/// </summary>
public class BossSniperState_RadialAttack : BossSniperStateBase
{
    private enum Step { Teleporting, PreparingWindup, Windup, PreparingLock, Locking, Firing }
    private Step step;

    private readonly BossSniperTeleport teleport = new BossSniperTeleport();

    private List<Vector2> shotDirs;   // 全弾の射線方向（撃つ順）。Enter で確定
    private int shotIndex;            // 次に撃つ弾のインデックス

    public override void Enter(BossSniper boss)
    {
        base.Enter(boss);

        int count = Mathf.Max(1, boss.Difficulty.radialShotCount);
        float stepMag = boss.radialAngleStep;

        // 初弾はプレイヤー方向。中央（発射原点）から見たプレイヤーの角度を初弾の射角にする
        Vector2 origin = boss.StageCenter();
        Vector2 toPlayer = boss.Player != null ? ((Vector2)boss.Player.position - origin) : Vector2.right;
        float startAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;

        // 回転方向は「プレイヤーに近い側から回り込む」向き。
        // = 初弾（プレイヤー正面）の“次の弾”がプレイヤーへ寄っていく回り方を選ぶ。
        // プレイヤーの左右どちらへ回るかは、プレイヤー速度の横成分で決める（止まっていればランダム）。
        float sign = PickRotationSign(boss, origin, toPlayer);
        float stepDeg = stepMag * sign;

        shotDirs = new List<Vector2>(count);
        float angle = startAngle;
        for (int i = 0; i < count; i++)
        {
            float rad = angle * Mathf.Deg2Rad;
            shotDirs.Add(new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)));
            angle += stepDeg;
        }
        shotIndex = 0;

        step = Step.Teleporting;
        teleport.Begin(boss.SelfUnit, origin, boss.teleportShrinkTime, boss.teleportExpandTime);
    }

    // 回転方向（+1/-1）を「プレイヤーに近い側から」の観点で選ぶ
    private float PickRotationSign(BossSniper boss, Vector2 origin, Vector2 toPlayer)
    {
        // プレイヤーが動いているなら、進行方向を追いかける向きに回る（近い側を舐め続ける）。
        // toPlayer と player速度 の外積(z)符号で、時計回り/反時計回りを決める。
        if (boss.PlayerRb != null)
        {
            Vector2 v = boss.PlayerRb.linearVelocity;
            if (v.sqrMagnitude > 0.01f)
            {
                float cross = toPlayer.x * v.y - toPlayer.y * v.x;
                if (Mathf.Abs(cross) > 0.0001f) return Mathf.Sign(cross);
            }
        }
        return Random.value < 0.5f ? 1f : -1f; // 止まっていれば左右ランダム
    }

    public override void UpdateState()
    {
        switch (step)
        {
            case Step.Teleporting:
                teleport.Update();
                if (!teleport.Running)
                {
                    // 出現したら溜めへ。初弾の射線を固定してロック射線を出しておく
                    boss.SelfUnit.SetAimDirection(shotDirs[shotIndex]);
                    step = Step.PreparingWindup;
                }
                break;

            case Step.PreparingWindup:
                boss.SelfUnit.AimVisualOnlyTick();
                if (boss.SelfUnit.IsVisualAlignedToAim())
                {
                    step = Step.Windup;
                    timer = Mathf.Max(0f, boss.radialWindupTime);
                }
                break;

            case Step.Windup:
                boss.SelfUnit.LockTick(); // 溜め中は初弾のロック射線（赤・細）を出して待つ
                if (Countdown())
                {
                    step = Step.Firing;
                    timer = Mathf.Max(0f, boss.radialFireDuration);
                    boss.SelfUnit.FireTick();
                }
                break;

            case Step.PreparingLock:
                boss.SelfUnit.AimVisualOnlyTick();
                if (boss.SelfUnit.IsVisualAlignedToAim())
                {
                    step = Step.Locking;
                    timer = Mathf.Max(0.05f, boss.Difficulty.radialLockTime);
                }
                break;

            case Step.Locking:
                boss.SelfUnit.LockTick();
                if (Countdown())
                {
                    step = Step.Firing;
                    timer = Mathf.Max(0f, boss.radialFireDuration);
                    boss.SelfUnit.FireTick(); // fireDuration=0 でも最低1回は判定
                }
                break;

            case Step.Firing:
                boss.SelfUnit.FireTick();
                if (Countdown())
                {
                    shotIndex++;
                    if (shotIndex < shotDirs.Count)
                    {
                        BeginLock(); // 回転して次弾へ
                    }
                    else
                    {
                        boss.TransitionToState(boss.StateReturn); // 撃ち切ったら帰還→次の行動選択へ
                    }
                }
                break;
        }
    }

    // 次に撃つ弾の射線でロック開始
    private void BeginLock()
    {
        boss.SelfUnit.SetAimDirection(shotDirs[shotIndex]);
        step = Step.PreparingLock;
    }

    public override void Exit()
    {
        boss.SelfUnit.HideBeam();
    }

    // 全体攻撃中も本物に当てれば通常ダメージ（無敵時間つき）
    public override void OnBurstHit(BossSniperBeamUnit unit, PlayerController pc)
    {
        boss.HandleNormalBurstHit(unit, pc);
    }
}