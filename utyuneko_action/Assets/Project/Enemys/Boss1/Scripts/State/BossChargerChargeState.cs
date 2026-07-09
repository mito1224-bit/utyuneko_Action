using UnityEngine;

/// <summary>
/// 突進（技①②）。予兆（Windup）で狙いを定めてから直進し、壁に当たると自滅スタンへ。
///
/// 予兆の見せ方（分かりやすさ重視）:
///   - 予兆の前半で「一瞬後ろへ下がる」タメ（chargeBackstep*）→ 溜めてから撃ち出す印象を作る
///   - 突進方向へモデルを向ける（左右=Y軸、上下=X軸ピッチ。斜め突進時に効く）
///   - 突進予定の視線ライン（ShowChargeTelegraph）をプレイヤーへ見せる
///
/// フェーズ1: 1回突進 → 壁ヒットで即スタン。
/// フェーズ2: multiChargeCount 回まで連続突進（壁ヒット後に短い狙い直しを挟む）。
///            最後の1回だけ自滅スタンする＝「1回避けて安心」を崩す（技②）。
///
/// 移動は EnemyCharger と同じ rb.MovePosition のスイープ（コントローラの MoveSweep）。
/// プレイヤーへの接触ダメージは本体・盾の DamageSource が担当（このステートでは扱わない）。
/// </summary>
public class BossChargerChargeState : BossChargerBaseState
{
    private enum Phase { Windup, Dash, ReAim }
    private Phase phase;
    private float timer;       // 現フェーズの残り時間
    private float phaseTotal;  // 現フェーズの総時間（バックステップの経過判定に使う）
    private Vector2 chargeDir;
    private int chargesDone;

    public BossChargerChargeState(BossChargerController boss) : base(boss) { }

    public override void Enter()
    {
        chargesDone = 0;
        // FixedUpdate が最初の Update より先に走っても方向を持っておく
        chargeDir = boss.DirectionToPlayer(boss.chargeHorizontalOnly);
        BeginAim(Phase.Windup, boss.chargeWindupTime / boss.SpeedMultiplier);
    }

    public override void Update()
    {
        switch (phase)
        {
            case Phase.Windup:
            case Phase.ReAim:
                // 狙い続け、向き・ピッチ・視線を突進方向へ見せる
                chargeDir = boss.DirectionToPlayer(boss.chargeHorizontalOnly);
                boss.SetFacing((int)Mathf.Sign(chargeDir.x));
                boss.UpdateModelFacing(chargeDir);
                boss.ShowChargeTelegraph(chargeDir);
                timer -= Time.deltaTime;
                if (timer <= 0f) BeginDash();
                break;

            case Phase.Dash:
                // 移動と壁検知は FixedUpdate。ここでは向き維持と保険時間だけ
                boss.UpdateModelFacing(chargeDir);
                timer -= Time.deltaTime;
                if (timer <= 0f) EndCharge(); // 壁に当たらないまま時間切れ → スタンせず終了
                break;
        }
    }

    public override void FixedUpdate()
    {
        // 予兆の前半でタメとして後ろへ下がる（一瞬バックステップ）
        if (phase == Phase.Windup || phase == Phase.ReAim)
        {
            DoBackstep();
            return;
        }

        if (phase != Phase.Dash) return;

        bool hitWall = boss.MoveSweep(chargeDir, boss.chargeSpeed * boss.SpeedMultiplier);
        if (hitWall) OnWallHit();
    }

    public override void OnCollisionEnter(Collision2D collision)
    {
        // 先読みが取りこぼして物理衝突した場合の保険（EnemyCharger と同じ二段構え）
        if (phase != Phase.Dash) return;
        if ((boss.wallLayers.value & (1 << collision.gameObject.layer)) == 0) return;
        OnWallHit();
    }

    public override void Exit()
    {
        boss.HideChargeTelegraph();
    }

    // 予兆の前半 chargeBackstepTime の間だけ、突進方向の逆へ下がる（壁があれば下がりきらないだけ）
    private void DoBackstep()
    {
        if (boss.chargeBackstepDistance <= 0f || boss.chargeBackstepTime <= 0f) return;
        float elapsed = phaseTotal - timer;
        if (elapsed > boss.chargeBackstepTime) return;
        float backSpeed = boss.chargeBackstepDistance / boss.chargeBackstepTime;
        boss.MoveSweep(-chargeDir, backSpeed);
    }

    private void OnWallHit()
    {
        chargesDone++;

        int maxCharges = boss.IsPhase2 ? Mathf.Max(1, boss.multiChargeCount) : 1;
        if (chargesDone < maxCharges)
        {
            // まだ連続突進が残っている → 短い狙い直しを挟んで再突進（自滅しない）
            BeginAim(Phase.ReAim, boss.reAimTime);
            return;
        }

        // 最後の突進 → 自滅スタン（衝撃波はスタン側で出す）
        boss.TransitionToState(boss.StateStun);
    }

    private void BeginAim(Phase aimPhase, float duration)
    {
        phase = aimPhase;
        phaseTotal = duration;
        timer = duration;
    }

    private void BeginDash()
    {
        boss.HideChargeTelegraph();
        phase = Phase.Dash;
        timer = boss.maxChargeTime;
    }

    private void EndCharge()
    {
        boss.TransitionToState(boss.StateIdle);
    }
}
