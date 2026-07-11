using UnityEngine;

/// <summary>
/// ボスステートの基底クラス。
/// 全メソッドを空実装（virtual）にしてあるので、各ステートは必要なものだけ override すればよい。
/// EnemySniper の Countdown と同じ流儀のタイマー小道具も持つ。
/// </summary>
public abstract class BossSniperStateBase : IBossSniperState
{
    protected BossSniper boss;

    // 各ステートが使い回す汎用タイマー。Enter で設定し、Countdown() で消化する
    protected float timer;

    public virtual void Enter(BossSniper boss)
    {
        this.boss = boss;
    }

    public virtual void UpdateState() { }
    public virtual void FixedUpdateState() { }
    public virtual void Exit() { }
    public virtual void OnBurstHit(BossSniperBeamUnit unit, PlayerController pc) { }
    public virtual void OnGroundHit() { }

    /// <summary>タイマーを進め、0になったら true を返す。</summary>
    protected bool Countdown()
    {
        timer -= Time.deltaTime;
        return timer <= 0f;
    }
}
