using UnityEngine;

/// <summary>
/// 必殺技「憤怒の乱舞突進」（技⑨・フェーズ2中にHPが低下したとき一度きり確定発動）。
///
/// 壁から壁へ高速突進を rampageChargeCount 回くり返すピンボール式の乱舞。
///   - 突入時に一度だけ長めの咆哮タメ（rampageRoarTime）＝「大技が来る」の見せ場
///   - 各突進の前に短い予兆ライン（ShowChargeTelegraph）を出す＝避ける方向が読める
///   - 壁に激突しても止まらず、すぐ次の狙い（プレイヤー方向）へ突進し直す＝乱舞
///   - 最後の1回だけ壁で自滅スタン（StateStun）＝反撃チャンス（通常突進の技②と同じ着地点）
///
/// 移動・壁検知は通常突進（BossChargerChargeState）と同じ MoveSweep のスイープ衝突を流用。
/// 接触ダメージは本体・盾の DamageSource が担当（このステートでは扱わない）。
/// </summary>
public class BossChargerRampageState : BossChargerBaseState
{
    private enum Phase { Settle, Aim, Dash }
    private Phase phase;
    private float timer;       // 現フェーズの残り時間
    private float phaseTotal;  // 現フェーズの総時間（予兆進行度の算出に使う）
    private Vector2 chargeDir;
    private int chargesDone;
    private float settleTimer; // 着地待ちの保険時間

    public BossChargerRampageState(BossChargerController boss) : base(boss) { }

    public override void Enter()
    {
        chargesDone = 0;
        // FixedUpdate が最初の Update より先に走っても方向を持っておく
        chargeDir = boss.DirectionToPlayer(true);

        // 乱舞の突進は水平（DirectionToPlayer(true) が Vector2(±1, 0) を返す）なので、
        // 空中で始めるとプレイヤーの頭上を延々と往復するだけになり、棒立ちで避けられてしまう。
        // このボスは GravityScale=0 で自然落下しないため、ここで自前で地面まで降ろしてから始める。
        if (boss.IsGroundedBelow()) BeginAim(boss.rampageRoarTime);
        else BeginSettle();
    }

    public override void Update()
    {
        switch (phase)
        {
            case Phase.Settle:
                // 降下そのものは FixedUpdate。ここでは向きの維持と保険時間だけ見る。
                boss.SetFacing((int)Mathf.Sign(boss.DirectionToPlayer(true).x));
                boss.UpdateModelFacing();
                settleTimer -= Time.deltaTime;
                // 真下に床が無い（奈落・場外）と永久に落ち続けるので、保険時間で打ち切って始める
                if (settleTimer <= 0f) BeginAim(boss.rampageRoarTime);
                break;

            case Phase.Aim:
                // 狙い続け、向き・視線を突進方向へ見せる（予兆進行度で黄→赤）
                chargeDir = boss.DirectionToPlayer(true);
                boss.SetFacing((int)Mathf.Sign(chargeDir.x));
                boss.UpdateModelFacing(chargeDir);
                float aimProgress = phaseTotal > 0f ? 1f - Mathf.Clamp01(timer / phaseTotal) : 1f;
                boss.ShowChargeTelegraph(chargeDir, aimProgress);
                timer -= Time.deltaTime;
                if (timer <= 0f) BeginDash();
                break;

            case Phase.Dash:
                // 移動と壁検知は FixedUpdate。ここでは向き維持と保険時間だけ
                boss.UpdateModelFacing(chargeDir);
                timer -= Time.deltaTime;
                if (timer <= 0f) CompleteCharge(); // 壁に当たらないまま時間切れ → 1回分として扱う
                break;
        }
    }

    public override void FixedUpdate()
    {
        if (phase == Phase.Settle)
        {
            // MoveSweep は wallLayers（＝壁・床）へのスイープなので、下向きに使えばそのまま着地処理になる
            bool landed = boss.MoveSweep(Vector2.down, boss.rampageSettleSpeed * boss.SpeedMultiplier);
            if (landed) BeginAim(boss.rampageRoarTime);
            return;
        }

        if (phase != Phase.Dash) return;

        bool hitWall = boss.MoveSweep(chargeDir, boss.rampageSpeed * boss.SpeedMultiplier);
        if (hitWall) CompleteCharge();
    }

    public override void OnCollisionEnter(Collision2D collision)
    {
        // 先読みが取りこぼして物理衝突した場合の保険（通常突進と同じ二段構え）
        if (phase != Phase.Dash) return;
        if ((boss.wallLayers.value & (1 << collision.gameObject.layer)) == 0) return;
        CompleteCharge();
    }

    public override void Exit()
    {
        boss.HideChargeTelegraph();
    }

    // 1回の突進が終わった（壁ヒット or 時間切れ）。最後なら自滅スタン、まだ残っていれば次の突進へ。
    private void CompleteCharge()
    {
        chargesDone++;

        if (chargesDone >= Mathf.Max(1, boss.rampageChargeCount))
        {
            // 乱舞の締め＝壁で自滅スタン（衝撃波・弱点タイムはスタン側で処理）。
            // 必殺技後なのでスタンを通常の ultimateStunMultiplier 倍に延長する印を立てる（StunState が消費）
            boss.NextStunIsUltimate = true;
            boss.TransitionToState(boss.StateStun);
            return;
        }

        // まだ残り → 短い狙い直しを挟んで、プレイヤー方向へ突進し直す（乱舞継続）
        BeginAim(boss.rampageWindupTime);
    }

    // 空中で発動したときに地面まで降りるフェーズ。着地したら咆哮タメへ進む。
    private void BeginSettle()
    {
        phase = Phase.Settle;
        settleTimer = Mathf.Max(0.05f, boss.rampageSettleMaxTime);
        boss.HideChargeTelegraph(); // 降下中は予兆を出さない（狙いは着地後に定める）
    }

    private void BeginAim(float duration)
    {
        // 最初の狙い＝咆哮タメ。「大技が来る」の見せ場としてシェイク＋咆哮SEを一度だけ出す
        // （2回目以降の狙い直しでは鳴らさない）。被弾ダメージのカットは Health の rampageDamageMultiplier が担当。
        if (chargesDone == 0)
        {
            boss.PlayShake(boss.rampageShakeDuration, boss.rampageShakeMagnitude);
            boss.PlaySE(SeType.EnemyConfusion);
        }

        phase = Phase.Aim;
        phaseTotal = timer = Mathf.Max(0f, duration) / boss.SpeedMultiplier;
    }

    private void BeginDash()
    {
        boss.HideChargeTelegraph();
        phase = Phase.Dash;
        timer = boss.rampageMaxChargeTime;
    }
}
