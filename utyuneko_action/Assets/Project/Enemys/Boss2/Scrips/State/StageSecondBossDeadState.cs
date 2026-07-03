using UnityEngine;

public class StageSecondBossDeadState : StageSecondBossBaseState
{
    public StageSecondBossDeadState(StageSecondBossController boss) : base(boss) { }

    public override void Enter()
    {
        Debug.Log("ステージ2ボス撃破！");
        SoundManager.Instance.PlaySE(SeType.EnemyDie);
        Object.Destroy(boss.gameObject, 0.5f);
    }
}