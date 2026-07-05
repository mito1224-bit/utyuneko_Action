using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 分身展開：本体がその場でXZ収縮して消え、縮んでいる間に配置ポイントを計算。
/// 本体の移動＋偽物の生成（収縮状態）を行い、全ユニットが同時に「にゅっ」と出現する。
/// 出現し終えたら照準（Aim）へ。
/// 収縮〜出現中は全員の当たり判定が無効なので、この間に本物を殴られることはない。
/// 「本物を見破ってスタンさせられる」時間は Aim / Lock ステートで受け付ける。
/// </summary>
public class BossSniperState_Split : BossSniperStateBase
{
    private enum Step { Shrinking, Expanding }
    private Step step;

    public override void Enter(BossSniper boss)
    {
        base.Enter(boss);

        // プレイヤー不在・プレハブ未設定なら攻撃できないので巡回へ戻る
        if (boss.Player == null || boss.clonePrefab == null)
        {
            boss.AttackTimer = boss.timeBetweenAttacks; // すぐ再突入してループしないよう仕切り直す
            boss.TransitionToState(boss.StatePatrol);
            return;
        }

        boss.onSplit?.Invoke();

        boss.SelfUnit.SetHitboxEnabled(false);
        boss.SelfUnit.BeginShrink(boss.teleportShrinkTime);
        step = Step.Shrinking;
    }

    public override void UpdateState()
    {
        switch (step)
        {
            case Step.Shrinking:
                if (boss.SelfUnit.IsScaleAnimating) return;

                // 縮み切った → 「この瞬間のプレイヤー位置」を中心に配置ポイントを計算し、
                // 本体を移動・偽物を収縮状態で生成して、全員同時に出現を始める
                int count = Mathf.Max(2, boss.Phase.totalUnits);
                List<Vector2> slots = boss.BuildFormationSlots(count, boss.Player.position);
                int realIndex = Random.Range(0, slots.Count);
                boss.DeployFormation(slots, realIndex);

                foreach (BossSniperBeamUnit u in boss.Units)
                {
                    u.BeginExpand(boss.teleportExpandTime);
                }
                step = Step.Expanding;
                break;

            case Step.Expanding:
                foreach (BossSniperBeamUnit u in boss.Units)
                {
                    u.FaceTick(); // 出現しながらプレイヤーの方を向く
                }
                if (!AllUnitsExpanded()) return;

                // 出現完了。ここから当たり判定を有効にして照準へ（見破りの受付は Aim / Lock 側）
                foreach (BossSniperBeamUnit u in boss.Units)
                {
                    u.SetHitboxEnabled(true);
                }
                boss.TransitionToState(boss.StateAim);
                break;
        }
    }

    private bool AllUnitsExpanded()
    {
        foreach (BossSniperBeamUnit u in boss.Units)
        {
            if (u.IsScaleAnimating) return false;
        }
        return true;
    }

    // 展開中（収縮〜出現）は当たり判定が無効なので実際には当たりにくいが、
    // 万一当たったら本物のみ通常ダメージ・偽物は消える扱いに統一
    public override void OnBurstHit(BossSniperBeamUnit unit, PlayerController pc)
    {
        if (unit.IsReal) boss.HandleNormalBurstHit(unit, pc);
        else boss.DestroyClone(unit);
    }
}