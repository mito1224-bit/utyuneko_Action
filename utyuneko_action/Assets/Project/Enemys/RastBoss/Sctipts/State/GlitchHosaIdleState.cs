using UnityEngine;

/// <summary>
/// 崩壊した補佐：待機（技抽選）ステート。形態に応じてスプレッドシート準拠の技を抽選！
/// </summary>
public class GlitchHosaIdleState : GlitchHosaBaseState
{
    private float idleTimer = 0f;
    private float idleDuration = 1.2f;

    public GlitchHosaIdleState(GlitchHosaController boss) : base(boss) { }

    public override void Enter()
    {
        float speedMul = boss.attackSpeedMultiplier;
        idleDuration = Random.Range(1.0f, 1.4f) / speedMul;
        idleTimer = 0f;
        if (boss.bossAnimator != null) boss.bossAnimator.speed = 1f;
    }

    public override void Update()
    {
        idleTimer += Time.deltaTime;

        if (idleTimer >= idleDuration)
        {
            float rand = Random.value;

            if (boss.hosaCurrentPhase == 1)
            {
                //boss.TransitionToState(boss.StateP2_HackingSteal);
                // 【第1形態】4択の25%均等確率で完全動作！
                if (rand < 0.25f)
                {
                    boss.TransitionToState(boss.StateP1_Beam);       // 技①：四方回転極太ビーム
                }
                else if (rand < 0.50f)
                {
                    boss.TransitionToState(boss.StateP1_Clones);     // 技②：分身交互スナイパービーム
                }
                else if (rand < 0.75f)
                {
                    boss.TransitionToState(boss.StateP1_BackBombs);  // 技③：奥逃げ爆弾投げ（打ち返しラリー）
                }
                else
                {
                    boss.TransitionToState(boss.StateP1_WallDash);   // 技④：3往復予測ダッシュ＆壁大激突
                }
            }
            else if (boss.hosaCurrentPhase >= 2)
            {
                // 👑 【第2形態・最終形態】新技の能力強奪をローテーションに完全追加！
                if (rand < 0.3f)
                {
                    boss.TransitionToState(boss.StateP2_CloneDash);   // コンボ①：分身レーザー×ハッキング突撃
                }
                else if (rand < 0.6f)
                {
                    boss.TransitionToState(boss.StateP2_BeamBombs);   // コンボ②：4回転ビーム×予測偏差爆撃
                }
                else if (rand < 0.85f)
                {
                    boss.TransitionToState(boss.StateP2_HackingSteal); // 👑 コンボ③：新設・ランダム能力強奪ハッキング！
                }
                else
                {
                    // 不意打ちのP1大技
                    boss.TransitionToState(boss.StateP1_BackBombs);
                }
            }
        }
    }
}