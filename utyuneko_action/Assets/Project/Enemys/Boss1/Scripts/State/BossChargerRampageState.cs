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
    private enum Phase { Aim, Dash }
    private Phase phase;
    private float timer;       // 現フェーズの残り時間
    private float phaseTotal;  // 現フェーズの総時間（予兆進行度の算出に使う）
    private Vector2 chargeDir;
    private int chargesDone;

    public BossChargerRampageState(BossChargerController boss) : base(boss) { }

    public override void Enter()
    {
        chargesDone = 0;
        // FixedUpdate が最初の Update より先に走っても方向を持っておく
        chargeDir = boss.DirectionToPlayer(true);
        // 1回目だけ長めの咆哮タメ
        BeginAim(boss.rampageRoarTime);
    }

    public override void Update()
    {
        switch (phase)
        {
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
            // 乱舞の締め＝壁で自滅スタン（衝撃波・弱点タイムはスタン側で処理）
            boss.TransitionToState(boss.StateStun);
            return;
        }

        // まだ残り → 短い狙い直しを挟んで、プレイヤー方向へ突進し直す（乱舞継続）
        BeginAim(boss.rampageWindupTime);
    }

    private void BeginAim(float duration)
    {
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
