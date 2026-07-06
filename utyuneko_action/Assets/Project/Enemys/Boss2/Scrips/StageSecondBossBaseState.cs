using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ===================================================================
// 🤖 ステージ2ボスの状態（State）の基底クラス
// ===================================================================
public abstract class StageSecondBossBaseState
{
    protected StageSecondBossController boss;
    public StageSecondBossBaseState(StageSecondBossController boss) { this.boss = boss; }

    public virtual void Enter() { }
    public virtual void Update() { }
    public virtual void FixedUpdate() { }
    public virtual void Exit() { }
}