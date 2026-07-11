using UnityEngine;

/// <summary>
/// バースト反射カウンター（技⑥）。プレイヤーが正面の盾へバーストで突っ込んだ罰。
/// 予兆なしの短い反撃突進をプレイヤーへ放つ（counterDuration で打ち切り。壁に当たってもスタンしない）。
/// 「正面から突っ込んではいけない」を体で学習させるための技なので、威力より速度と即時性を重視する。
/// 接触ダメージは本体・盾の DamageSource が担当。
/// </summary>
public class BossChargerCounterState : BossChargerBaseState
{
    private float timer;
    private Vector2 dashDir;

    public BossChargerCounterState(BossChargerController boss) : base(boss) { }

    public override void Enter()
    {
        timer = boss.counterDuration;
        dashDir = boss.DirectionToPlayer(true); // 反射で離れていくプレイヤーを追い打ち
        boss.SetFacing((int)Mathf.Sign(dashDir.x));
    }

    public override void Update()
    {
        boss.UpdateModelFacing();
        timer -= Time.deltaTime;
        if (timer <= 0f) boss.TransitionToState(boss.StateIdle);
    }

    public override void FixedUpdate()
    {
        // 壁に当たったら追撃打ち切り（自滅スタンはしない＝カウンターはローリスク技）
        bool hitWall = boss.MoveSweep(dashDir, boss.counterSpeed);
        if (hitWall) boss.TransitionToState(boss.StateIdle);
    }
}
