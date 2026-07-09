using UnityEngine;

/// <summary>
/// フェーズ2移行（咆哮）。phaseTransitionTime の間その場で威嚇し、被弾ダメージは大幅カット
/// （BossChargerHealth.phaseTransitionDamageMultiplier）。終わったら Idle へ。
/// IsPhase2 はコントローラ側で移行判定時に立てているので、以降は速度倍率・連続突進・盾投げが解禁される。
/// </summary>
public class BossChargerPhaseTransitionState : BossChargerBaseState
{
    private float timer;

    public BossChargerPhaseTransitionState(BossChargerController boss) : base(boss) { }

    public override void Enter()
    {
        timer = boss.phaseTransitionTime;
        // 咆哮中は盾を構え直しておく（スタン中に移行した場合の保険）
        boss.shield?.SetGuardEnabled(true);
    }

    public override void Update()
    {
        boss.UpdateModelFacing();
        timer -= Time.deltaTime;
        if (timer <= 0f) boss.TransitionToState(boss.StateIdle);
    }
}
