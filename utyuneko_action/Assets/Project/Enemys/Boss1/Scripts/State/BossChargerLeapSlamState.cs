using UnityEngine;

/// <summary>
/// 飛び上がり叩きつけ（技⑧）。プレイヤーの位置へ放物線で跳び、着地の瞬間に円範囲AoE＋左右へ衝撃波を出す。
///
/// フェーズ: 予兆(Windup:狙いを定めてタメ) → 跳躍(Leap:放物線移動＋着地円の予兆表示) → 着地(Land:AoE) → 硬直(Recover) → 待機へ。
///
/// 着地円の予兆は実行時生成の半透明ディスク（Sprites/Default）で見せる（プレハブ不要）。
/// 着地ダメージは OverlapCircle → PlayerHealth.TakeDamage（無敵は PlayerHealth 側。EnemyAreaAttack と同じ疎結合）。
/// 衝撃波は既存の SpawnShockwaves を流用する（shockwavePrefab 未設定なら出ない）。
/// 間合いを一気に詰める技。密着対策のシールドバッシュと役割を分ける。
/// </summary>
public class BossChargerLeapSlamState : BossChargerBaseState
{
    private enum Phase { Windup, Leap, Recover }
    private Phase phase;
    private float timer;

    private Vector2 startPos;
    private Vector2 targetPos;
    private float leapElapsed;

    // 着地予兆の円。生成・破棄・方式（スプライト/フォールバック）は TelegraphCircle が面倒を見る
    private readonly TelegraphCircle telegraph = new TelegraphCircle();

    private static readonly Color telegraphColor = new Color(1f, 0.4f, 0.1f, 0.5f);

    public BossChargerLeapSlamState(BossChargerController boss) : base(boss) { }

    public override void Enter()
    {
        phase = Phase.Windup;
        timer = boss.leapWindupTime / boss.SpeedMultiplier;
        boss.LeapSlamTimer = boss.leapSlamCooldown; // クールダウン開始
        leapElapsed = 0f;

        Vector2 toPlayer = boss.DirectionToPlayer(true);
        boss.SetFacing(toPlayer.x == 0 ? boss.Facing : (int)Mathf.Sign(toPlayer.x));
    }

    public override void Update()
    {
        boss.UpdateModelFacing();

        switch (phase)
        {
            case Phase.Windup:
                // 跳ぶ直前までプレイヤーを向く
                Vector2 toPlayer = boss.DirectionToPlayer(true);
                boss.SetFacing(toPlayer.x == 0 ? boss.Facing : (int)Mathf.Sign(toPlayer.x));
                timer -= Time.deltaTime;
                if (timer <= 0f) BeginLeap();
                break;

            case Phase.Leap:
                // 移動は FixedUpdate。着地円の予兆を降下に合わせて濃くする
                UpdateTelegraph();
                break;

            case Phase.Recover:
                timer -= Time.deltaTime;
                if (timer <= 0f) boss.TransitionToState(boss.StateIdle);
                break;
        }
    }

    public override void FixedUpdate()
    {
        if (phase != Phase.Leap) return;

        leapElapsed += Time.fixedDeltaTime;
        float dur = Mathf.Max(0.05f, boss.leapTime / boss.SpeedMultiplier);
        float u = Mathf.Clamp01(leapElapsed / dur);

        // 水平は線形、垂直は放物線（u=0.5 で最高点）。MoveSweep せず空中を素直に飛ぶ
        Vector2 pos = Vector2.Lerp(startPos, targetPos, u);
        pos.y += boss.leapArcHeight * 4f * u * (1f - u);
        if (boss.Rb != null) boss.Rb.MovePosition(pos);

        // 突進方向へモデルを向ける（斜め成分でピッチが効く）
        boss.UpdateModelFacing((targetPos - startPos).normalized);

        if (u >= 1f) Land();
    }

    public override void Exit()
    {
        DestroyTelegraph();
    }

    private void BeginLeap()
    {
        phase = Phase.Leap;
        leapElapsed = 0f;

        startPos = boss.transform.position;
        // 着地点＝プレイヤーの真下（現在のX）。高さは跳んだ位置に戻す（同じ地面想定）
        Transform player = boss.GetPlayerTransform();
        float targetX = player != null ? player.position.x : startPos.x + boss.Facing * 4f;
        targetPos = new Vector2(targetX, startPos.y);

        CreateTelegraph(new Vector3(targetPos.x, targetPos.y, boss.transform.position.z), boss.leapLandRadius);
    }

    private void Land()
    {
        if (boss.Rb != null) boss.Rb.MovePosition(targetPos);
        DestroyTelegraph();

        // 着地の円範囲AoE
        Collider2D[] hits = Physics2D.OverlapCircleAll(targetPos, boss.leapLandRadius, boss.attackTargetLayers);
        foreach (Collider2D c in hits)
        {
            PlayerHealth hp = c.GetComponentInParent<PlayerHealth>();
            if (hp != null) { hp.TakeDamage(boss.leapDamage); break; }
        }

        // 着地の衝撃波（既存の技④資産を流用。shockwavePrefab 未設定なら出ない）
        if (boss.leapLandShockwaves) boss.SpawnShockwaves();

        phase = Phase.Recover;
        timer = boss.leapRecoverTime / boss.SpeedMultiplier;
    }

    // ─── 着地予兆の円（TelegraphCircle。爆弾・雑魚の範囲円と同じ方式） ───

    private void CreateTelegraph(Vector3 pos, float radius)
    {
        // 親を付けない（ボスが跳んでも着地点に置きっぱなしにするため）
        telegraph.Create("LeapLandTelegraph(仮)", null, boss.telegraphSprite, boss.telegraphCircleMaterial, sortingOrder: -1);
        telegraph.SetWorldPosition(pos);
        telegraph.SetRadius(radius);
        telegraph.SetColor(telegraphColor);
    }

    // 降下が進むほど濃く（着弾が近いのを見せる）
    private void UpdateTelegraph()
    {
        if (!telegraph.IsCreated) return;
        Color c = telegraphColor;
        c.a = Mathf.Lerp(0.25f, 0.75f, Mathf.Clamp01(leapElapsed / Mathf.Max(0.05f, boss.leapTime)));
        telegraph.SetColor(c);
    }

    private void DestroyTelegraph() => telegraph.Destroy();
}
