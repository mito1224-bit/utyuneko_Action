using UnityEngine;

/// <summary>
/// 崩壊した補佐専用：状態（State）の基底クラス
/// </summary>
public abstract class GlitchHosaBaseState
{
    protected GlitchHosaController boss;

    public GlitchHosaBaseState(GlitchHosaController boss)
    {
        this.boss = boss;
    }

    public virtual void Enter() { }
    public virtual void Update() { }
    public virtual void FixedUpdate() { }
    public virtual void Exit() { }
}