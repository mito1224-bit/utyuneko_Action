using UnityEngine;

/// <summary>
/// 撃破：最終フェーズのスタン中にダメージを受けた。ボスは倒れてそのまま。
/// 撃破演出（SE・エフェクト・リザルトなど）は onDefeated イベントに接続する。
/// destroyDelayOnDefeat > 0 なら、その秒数後に本体を Destroy する。
/// </summary>
public class BossSniperState_Defeated : BossSniperStateBase
{
    public override void Enter(BossSniper boss)
    {
        base.Enter(boss);

        boss.DespawnClones();
        boss.SelfUnit.HideBeam();
        boss.BeginFallBody(); // 力なく地面へ倒れたままにする

        boss.onDefeated?.Invoke();

        if (boss.destroyDelayOnDefeat > 0f)
        {
            Object.Destroy(boss.gameObject, boss.destroyDelayOnDefeat);
        }
    }

    // 何にも反応しない（無敵・行動なし）
}
