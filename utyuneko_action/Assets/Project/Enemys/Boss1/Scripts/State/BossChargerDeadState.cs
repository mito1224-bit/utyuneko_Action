using UnityEngine;

/// <summary>
/// 死亡。接触ダメージを全て無効化し、盾を落とし、onDefeated（扉開放・イベント起動など）を発火して
/// deathDestroyDelay 後に消滅する。倒れ演出・エフェクトは後からここに足す。
/// </summary>
public class BossChargerDeadState : BossChargerBaseState
{
    private float timer;

    public BossChargerDeadState(BossChargerController boss) : base(boss) { }

    public override void Enter()
    {
        timer = boss.deathDestroyDelay;

        // 死体に触れてもダメージを受けないように（EnemyKnockback の死亡処理と同じ流儀）
        boss.SetAllDamageSourcesEnabled(false);
        boss.shield?.SetGuardEnabled(false);

        boss.onDefeated?.Invoke();
    }

    public override void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f) Object.Destroy(boss.gameObject);
    }
}
