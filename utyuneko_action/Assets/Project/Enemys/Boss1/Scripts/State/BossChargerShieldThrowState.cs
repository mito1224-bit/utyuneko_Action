using UnityEngine;

/// <summary>
/// 盾投げ（技⑤・フェーズ2限定）。盾をブーメラン投擲し、戻ってくるまで正面が無防備になる。
/// リスクとリターンの逆転タイム: ボスは throwMoveSpeed で速く動き回るが、
/// 被弾ダメージは throwDamageMultiplier 倍（BossChargerHealth）で受ける。
///
/// フェーズ: 予兆(Windup:軌道ラインを見せてタメ) → 投擲(Throwing:盾が自走で往復) → 戻ったら Idle へ。
/// 予兆中は盾の飛ぶ直線軌道を LineRenderer（突進予兆を流用）でプレイヤーへ見せる＝避ける猶予。
/// 盾の往復は BossChargerShield 側が自走する。
/// </summary>
public class BossChargerShieldThrowState : BossChargerBaseState
{
    private enum Phase { Windup, Throwing }
    private Phase phase;
    private float timer;        // 予兆の残り時間
    private float windupTotal;  // 予兆の総時間（進行度の算出に使う）
    private Vector2 aimDir;     // 投げる方向（予兆中はプレイヤーへ追従、投擲でロック）
    private float safetyTimer;  // 盾が何かの拍子に戻れなくなった場合の保険

    public BossChargerShieldThrowState(BossChargerController boss) : base(boss) { }

    public override void Enter()
    {
        boss.ShieldThrowTimer = boss.shieldThrowCooldown; // 次に投げられるまでのクールダウン

        // プレイヤーへ向けて狙う（上下も狙う＝斜め投げあり）
        aimDir = boss.DirectionToPlayer(false);
        boss.SetFacing((int)Mathf.Sign(aimDir.x));

        phase = Phase.Windup;
        windupTotal = timer = Mathf.Max(0f, boss.throwTelegraphWindupTime) / boss.SpeedMultiplier;
        if (timer <= 0f) BeginThrow(); // 予兆時間0なら即投げ（従来挙動）
    }

    public override void Update()
    {
        switch (phase)
        {
            case Phase.Windup:
                // 投げる直前までプレイヤーへ狙いを合わせ続け、軌道ラインを見せる
                aimDir = boss.DirectionToPlayer(false);
                boss.SetFacing((int)Mathf.Sign(aimDir.x));
                boss.UpdateModelFacing(aimDir);
                ShowThrowTelegraph();
                timer -= Time.deltaTime;
                if (timer <= 0f) BeginThrow();
                break;

            case Phase.Throwing:
                // 投擲中もプレイヤーの方を向く
                Vector2 toPlayer = boss.DirectionToPlayer(true);
                boss.SetFacing((int)Mathf.Sign(toPlayer.x));
                boss.UpdateModelFacing();

                // 盾が手元に戻ったら攻撃再開
                if (boss.shield == null || boss.shield.IsHeld)
                {
                    boss.TransitionToState(boss.StateIdle);
                    return;
                }

                safetyTimer -= Time.deltaTime;
                if (safetyTimer <= 0f) boss.TransitionToState(boss.StateIdle);
                break;
        }
    }

    public override void FixedUpdate()
    {
        if (phase != Phase.Throwing) return;

        // 無防備な代わりに速く動いてプレイヤーを追う（近づきすぎる手前で止まる）
        Transform player = boss.GetPlayerTransform();
        if (player == null) return;

        float dist = Vector2.Distance(player.position, boss.transform.position);
        if (dist <= boss.bashTriggerRange) return;

        Vector2 dir = boss.DirectionToPlayer(true);
        boss.MoveSweep(dir, boss.throwMoveSpeed * boss.SpeedMultiplier);
    }

    public override void Exit()
    {
        boss.HideChargeTelegraph();
    }

    // 盾の現在位置から投擲方向へ、盾の飛距離ぶんの軌道ラインを描く（予兆進行で黄→赤）
    private void ShowThrowTelegraph()
    {
        if (boss.shield == null) { boss.HideChargeTelegraph(); return; }
        Vector2 origin = boss.shield.transform.position;
        Vector2 dir = aimDir.sqrMagnitude > 0.0001f ? aimDir.normalized : Vector2.right * boss.Facing;
        float progress = windupTotal > 0f ? 1f - Mathf.Clamp01(timer / windupTotal) : 1f;
        boss.ShowThrowTelegraph(origin, dir, boss.shield.throwDistance, progress);
    }

    private void BeginThrow()
    {
        boss.HideChargeTelegraph();
        boss.SetFacing((int)Mathf.Sign(aimDir.x));
        boss.shield?.Throw(aimDir);
        safetyTimer = 8f;
        phase = Phase.Throwing;
    }
}
