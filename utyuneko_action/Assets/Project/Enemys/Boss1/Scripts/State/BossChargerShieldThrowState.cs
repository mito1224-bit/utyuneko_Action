using UnityEngine;

/// <summary>
/// 盾投げ（技⑤・フェーズ2限定）。盾をブーメラン投擲し、戻ってくるまで正面が無防備になる。
/// リスクとリターンの逆転タイム: ボスは throwMoveSpeed で速く動き回るが、
/// 被弾ダメージは throwDamageMultiplier 倍（BossChargerHealth）で受ける。
/// 盾の往復は BossChargerShield 側が自走する。戻ったら Idle へ。
/// </summary>
public class BossChargerShieldThrowState : BossChargerBaseState
{
    private float safetyTimer; // 盾が何かの拍子に戻れなくなった場合の保険

    public BossChargerShieldThrowState(BossChargerController boss) : base(boss) { }

    public override void Enter()
    {
        safetyTimer = 8f;

        // プレイヤーへ向けて投げる（上下も狙う＝斜め投げあり）
        Vector2 dir = boss.DirectionToPlayer(false);
        boss.SetFacing((int)Mathf.Sign(dir.x));
        boss.shield?.Throw(dir);

        boss.ShieldThrowTimer = boss.shieldThrowCooldown; // 次に投げられるまでのクールダウン
    }

    public override void Update()
    {
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
    }

    public override void FixedUpdate()
    {
        // 無防備な代わりに速く動いてプレイヤーを追う（近づきすぎる手前で止まる）
        Transform player = boss.GetPlayerTransform();
        if (player == null) return;

        float dist = Vector2.Distance(player.position, boss.transform.position);
        if (dist <= boss.bashTriggerRange) return;

        Vector2 dir = boss.DirectionToPlayer(true);
        boss.MoveSweep(dir, boss.throwMoveSpeed * boss.SpeedMultiplier);
    }
}
