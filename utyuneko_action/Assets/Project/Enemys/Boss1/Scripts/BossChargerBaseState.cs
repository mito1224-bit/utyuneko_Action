using UnityEngine;

/// <summary>
/// 突進×盾ボス（Boss3）の状態基底クラス。
/// StageSecondBossBaseState と同じ流儀（コントローラ参照を持つプレーンクラス）。
/// </summary>
public abstract class BossChargerBaseState
{
    protected BossChargerController boss;
    public BossChargerBaseState(BossChargerController boss) { this.boss = boss; }

    public virtual void Enter() { }
    public virtual void Update() { }
    public virtual void FixedUpdate() { }
    public virtual void Exit() { }

    /// <summary>コントローラの OnCollisionEnter2D から転送される（突進の壁ヒット保険など）</summary>
    public virtual void OnCollisionEnter(Collision2D collision) { }
}
