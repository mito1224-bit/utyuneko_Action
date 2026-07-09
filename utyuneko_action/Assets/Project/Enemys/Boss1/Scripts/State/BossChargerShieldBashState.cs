using UnityEngine;

/// <summary>
/// シールドバッシュ（技③）。密着してくるプレイヤーを盾で薙ぎ払う近接攻撃。
/// 予兆（Windup）→ 発動（Active: 正面 bashRange 内のプレイヤーへダメージ＋ノックバック）→ 隙（Recover）。
/// ダメージは PlayerHealth.TakeDamage を直接呼ぶ（EnemySniper のレーザーと同じ流儀。無敵は PlayerHealth 側）。
/// </summary>
public class BossChargerShieldBashState : BossChargerBaseState
{
    private enum Phase { Windup, Active, Recover }
    private Phase phase;
    private float timer;
    private bool hasHit; // 1回のバッシュで多段ヒットさせない

    public BossChargerShieldBashState(BossChargerController boss) : base(boss) { }

    public override void Enter()
    {
        phase = Phase.Windup;
        timer = boss.bashWindupTime / boss.SpeedMultiplier;
        hasHit = false;

        // 予兆開始時点でプレイヤーの方を向いて固定（発動中は向き直さない＝背後へ回れば避けられる）
        Vector2 toPlayer = boss.DirectionToPlayer(true);
        boss.SetFacing((int)Mathf.Sign(toPlayer.x));
    }

    public override void Update()
    {
        boss.UpdateModelFacing();
        timer -= Time.deltaTime;

        switch (phase)
        {
            case Phase.Windup:
                if (timer <= 0f)
                {
                    phase = Phase.Active;
                    timer = boss.bashActiveTime;
                }
                break;

            case Phase.Active:
                TryHitPlayer();
                if (timer <= 0f)
                {
                    phase = Phase.Recover;
                    timer = boss.bashRecoverTime / boss.SpeedMultiplier;
                }
                break;

            case Phase.Recover:
                if (timer <= 0f) boss.TransitionToState(boss.StateIdle);
                break;
        }
    }

    // 正面 bashRange 内（かつ向いている側）のプレイヤーにダメージ＋ノックバック
    private void TryHitPlayer()
    {
        if (hasHit) return;

        Transform player = boss.GetPlayerTransform();
        if (player == null) return;

        Vector2 toPlayer = (Vector2)player.position - (Vector2)boss.transform.position;
        if (toPlayer.magnitude > boss.bashRange) return;
        if (Mathf.Sign(toPlayer.x) != boss.Facing) return; // 背後は当たらない

        hasHit = true;

        PlayerHealth hp = player.GetComponentInParent<PlayerHealth>();
        if (hp != null) hp.TakeDamage(boss.bashDamage);

        // 水平方向へ弾き飛ばす（EnemyCollision.ApplyKnockback と同じ考え方）
        Rigidbody2D prb = boss.GetPlayerRigidbody();
        if (prb != null)
        {
            Vector2 dir = new Vector2(Mathf.Sign(toPlayer.x), 0f);
            prb.linearVelocity = Vector2.zero;
            prb.AddForce(dir * boss.bashKnockbackForce, ForceMode2D.Impulse);
        }
    }
}
