using UnityEngine;

public class StageSecondBossIdleState : StageSecondBossBaseState
{
    private float idleTimer = 0f;
    private float idleDuration = 1.3f; // 💡 技の合間のボスのフワフワ待機時間

    public StageSecondBossIdleState(StageSecondBossController boss) : base(boss) { }

    public override void Enter()
    {
        idleTimer = 0f;
        if (boss.bossAnimator != null) boss.bossAnimator.speed = 1f;
    }

    public override void Update()
    {
        idleTimer += Time.deltaTime;

        if (idleTimer >= idleDuration)
        {
            // ===================================================================
            // 🛠️【レベルデザインの要】優先ウルトフラグが立っていた場合の割り込み処理！
            // 時間が経ちすぎたか、HPが一定量減っている時は、通常技を完全に拒否してウルトを撃ちます。
            // ===================================================================
            if (boss.shouldForceUltimate)
            {
                boss.TransitionToState(boss.StateUltimate);
                return; // 💡 移行したので、下の通常のランダム抽選は実行せずに終了！
            }

            // -------------------------------------------------------------------
            // 通常時：4つの通常技をランダムに25%ずつの確率で繰り出すAIルーチン
            // -------------------------------------------------------------------
            float rand = Random.value;

            if (rand < 0.25f)
            {
                boss.TransitionToState(boss.StateBombTimed); // 技①：逆サイド逃げクロス爆撃
            }
            else if (rand < 0.50f)
            {
                boss.TransitionToState(boss.StateBombInstantLine); // 技②：天井グリッド爆撃
            }
            else if (rand < 0.75f)
            {
                boss.TransitionToState(boss.StateBombMine); // 技③：ステージ中央地雷バラ撒き
            }
            else
            {
                boss.TransitionToState(boss.StateBombFollow); // 技④：中空スナイプ追従4連撃
            }
        }
    }

    public override void Exit() { }
}