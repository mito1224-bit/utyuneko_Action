using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 待機。プレイヤーの方を向き、ゆっくり間合いを詰めながら idleTime 後に次の行動を選ぶ。
///
/// 行動選択:
///   - フェーズ2かつ盾投げクールダウン明け → 盾投げ（技⑤）
///   - プレイヤーが bashTriggerRange 内   → シールドバッシュ（技③・密着対策）
///   - それ以外                           → 遠距離技からランダム
///       突進（技①／フェーズ2は連続突進②） / 地面叩き（技⑦） / 飛び上がり叩きつけ（技⑧）
///       ※地面叩き・飛びつきはクールダウン明けのときだけ候補に入る。
/// </summary>
public class BossChargerIdleState : BossChargerBaseState
{
    private float timer;

    public BossChargerIdleState(BossChargerController boss) : base(boss) { }

    public override void Enter()
    {
        // フェーズ2は行動間隔も短くなる
        timer = boss.idleTime / boss.SpeedMultiplier;
    }

    public override void Update()
    {
        // 常にプレイヤーの方を向く
        Vector2 toPlayer = boss.DirectionToPlayer(true);
        boss.SetFacing((int)Mathf.Sign(toPlayer.x));
        boss.UpdateModelFacing();

        timer -= Time.deltaTime;
        if (timer <= 0f) ChooseNextAction();
    }

    public override void FixedUpdate()
    {
        // ゆっくり間合いを詰める（盾を構えたまま歩く圧）。
        // idleApproach が OFF のときは待機中は動かない＝移動は攻撃（突進・地面叩き等）のときだけになる。
        if (!boss.idleApproach || boss.idleApproachSpeed <= 0f) return;
        Transform player = boss.GetPlayerTransform();
        if (player == null) return;

        float dist = Vector2.Distance(player.position, boss.transform.position);
        if (dist <= boss.bashTriggerRange * 0.8f) return; // 密着まで来たら押し込まない

        Vector2 dir = boss.DirectionToPlayer(true);
        boss.MoveSweep(dir, boss.idleApproachSpeed * boss.SpeedMultiplier);
    }

    private void ChooseNextAction()
    {
        Transform player = boss.GetPlayerTransform();
        float dist = player != null ? Vector2.Distance(player.position, boss.transform.position) : float.MaxValue;

        // 盾投げ（フェーズ2限定・クールダウン明け）
        if (boss.IsPhase2 && boss.ShieldThrowTimer <= 0f && boss.shield != null && boss.shield.IsHeld)
        {
            boss.TransitionToState(boss.StateShieldThrow);
            return;
        }

        // 密着されていたらバッシュで引き剥がす
        if (dist <= boss.bashTriggerRange)
        {
            boss.TransitionToState(boss.StateShieldBash);
            return;
        }

        // 遠距離技：クールダウン明けの候補からランダムに選ぶ（突進は常に候補）
        var candidates = new List<BossChargerBaseState> { boss.StateCharge };
        if (boss.enableGroundSlam && boss.GroundSlamTimer <= 0f) candidates.Add(boss.StateGroundSlam);
        if (boss.enableLeapSlam && boss.LeapSlamTimer <= 0f) candidates.Add(boss.StateLeapSlam);

        boss.TransitionToState(candidates[Random.Range(0, candidates.Count)]);
    }
}
