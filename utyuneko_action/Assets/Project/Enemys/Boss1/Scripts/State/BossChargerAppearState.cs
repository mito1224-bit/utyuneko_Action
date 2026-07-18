using UnityEngine;

/// <summary>
/// 登場演出。appearTime 待ってから Idle へ。
/// Boss2（StageSecondBossAppearState）の流儀を流用：
///   カメラをボスへズームロック → シェイク＋咆哮SE → モデルを脈動で威嚇 → 終了でカメラを戻し戦闘BGMへ。
/// 演出中は接触ダメージを一括OFF（登場即当たりを防ぐ）。シングルトンが無いテストシーンでは各演出は自動スキップ。
/// </summary>
public class BossChargerAppearState : BossChargerBaseState
{
    private float timer;
    private Vector3 baseScale;
    private CameraFollowWithZoom cam;

    public BossChargerAppearState(BossChargerController boss) : base(boss) { }

    public override void Enter()
    {
        timer = boss.appearTime;
        baseScale = boss.CaptureVisualScale();

        // 登場中は無敵演出＝当たり判定OFF
        boss.SetAllDamageSourcesEnabled(false);

        // カメラをボスへ寄せる → シェイク＋咆哮SE
        cam = boss.BeginCameraFocus(4.5f, 2.5f);
        boss.PlayShake(boss.appearShakeDuration, boss.appearShakeMagnitude);
        boss.PlaySE(SeType.EnemyRangeAttack);
    }

    public override void Update()
    {
        boss.UpdateModelFacing();
        boss.ApplyVisualPulse(baseScale, boss.appearPulseAmount);

        timer -= Time.deltaTime;
        if (timer <= 0f) boss.TransitionToState(boss.StateIdle);
    }

    public override void Exit()
    {
        // 演出で触ったスケールを戻し、当たり判定を復帰。カメラをプレイヤーへ戻して戦闘BGMへ。
        boss.RestoreVisualScale(baseScale);
        boss.SetAllDamageSourcesEnabled(true);
        boss.EndCameraFocus(cam, 1f);
        boss.PlayBattleBgm();
    }
}
