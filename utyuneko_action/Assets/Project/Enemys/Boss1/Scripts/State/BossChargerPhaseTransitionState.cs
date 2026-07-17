using UnityEngine;

/// <summary>
/// フェーズ2移行（咆哮）。phaseTransitionTime の間その場で威嚇し、被弾ダメージは大幅カット
/// （BossChargerHealth.phaseTransitionDamageMultiplier）。終わったら Idle へ。
/// IsPhase2 はコントローラ側で移行判定時に立てているので、以降は速度倍率・連続突進・盾投げが解禁される。
///
/// 演出は Boss2（StageSecondBossPhaseTransitionState）の流儀を流用：
///   カメラをボスへズームロック → シェイク＋咆哮SE → 登場より大きめの脈動で威嚇 → 終了でカメラを戻す。
///   威嚇中は自分の接触ダメージも一時OFF（棒立ち威嚇の間に事故当たりさせない）。
/// </summary>
public class BossChargerPhaseTransitionState : BossChargerBaseState
{
    private float timer;
    private Vector3 baseScale;
    private CameraFollowWithZoom cam;

    public BossChargerPhaseTransitionState(BossChargerController boss) : base(boss) { }

    public override void Enter()
    {
        timer = boss.phaseTransitionTime;
        baseScale = boss.CaptureVisualScale();

        // 咆哮中は盾を構え直しておく（スタン中に移行した場合の保険）
        boss.shield?.SetGuardEnabled(true);
        // 威嚇中は自分の接触ダメージをOFF（被弾ダメージのカットは Health の倍率が担当）
        boss.SetAllDamageSourcesEnabled(false);

        cam = boss.BeginCameraFocus(3f, 2f);
        boss.PlayShake(boss.phaseShakeDuration, boss.phaseShakeMagnitude);
        boss.PlaySE(SeType.EnemyConfusion);
    }

    public override void Update()
    {
        boss.UpdateModelFacing();
        boss.ApplyVisualPulse(baseScale, boss.phasePulseAmount);

        timer -= Time.deltaTime;
        if (timer <= 0f) boss.TransitionToState(boss.StateIdle);
    }

    public override void Exit()
    {
        boss.RestoreVisualScale(baseScale);
        boss.SetAllDamageSourcesEnabled(true);
        boss.EndCameraFocus(cam, 1f);
    }
}
