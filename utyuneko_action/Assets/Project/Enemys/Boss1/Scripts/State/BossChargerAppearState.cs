using UnityEngine;

/// <summary>
/// 登場演出。appearTime 待ってから Idle へ。
/// 演出（アニメ・カメラ等）は後からここに足す。
/// </summary>
public class BossChargerAppearState : BossChargerBaseState
{
    private float timer;

    public BossChargerAppearState(BossChargerController boss) : base(boss) { }

    public override void Enter()
    {
        timer = boss.appearTime;
    }

    public override void Update()
    {
        boss.UpdateModelFacing();
        timer -= Time.deltaTime;
        if (timer <= 0f) boss.TransitionToState(boss.StateIdle);
    }
}
