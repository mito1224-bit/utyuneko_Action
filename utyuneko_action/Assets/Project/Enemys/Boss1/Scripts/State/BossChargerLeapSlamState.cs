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

    private GameObject telegraph;      // 着地予兆の円（実行時生成）
    private Material telegraphMaterial;
    private Mesh telegraphMesh;
    private Transform telegraphVisual;

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

    // ─── 着地予兆の円（実行時生成の半透明ディスク） ───

    private void CreateTelegraph(Vector3 pos, float radius)
    {
        telegraph = new GameObject("LeapLandTelegraph(仮)");
        telegraph.transform.position = pos;
        telegraphVisual = telegraph.transform;

        MeshFilter mf = telegraph.AddComponent<MeshFilter>();
        telegraphMesh = BuildDiscMesh(40);
        mf.sharedMesh = telegraphMesh;

        MeshRenderer mr = telegraph.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.sortingOrder = -1; // プレイヤー／敵スプライトの後ろ

        telegraphMaterial = new Material(Shader.Find("Sprites/Default"));
        telegraphMaterial.renderQueue = 3000; // Transparent
        mr.material = telegraphMaterial;

        telegraphVisual.localScale = Vector3.one * radius;
        telegraphMaterial.color = telegraphColor;
    }

    // 降下が進むほど濃く（着弾が近いのを見せる）
    private void UpdateTelegraph()
    {
        if (telegraphMaterial == null) return;
        Color c = telegraphColor;
        c.a = Mathf.Lerp(0.25f, 0.75f, Mathf.Clamp01(leapElapsed / Mathf.Max(0.05f, boss.leapTime)));
        telegraphMaterial.color = c;
    }

    private void DestroyTelegraph()
    {
        if (telegraphMaterial != null) { Object.Destroy(telegraphMaterial); telegraphMaterial = null; }
        if (telegraphMesh != null) { Object.Destroy(telegraphMesh); telegraphMesh = null; }
        if (telegraph != null) { Object.Destroy(telegraph); telegraph = null; }
        telegraphVisual = null;
    }

    // 中心＋外周の扇メッシュ（半径1の単位円）
    private Mesh BuildDiscMesh(int segments)
    {
        Mesh mesh = new Mesh { name = "LeapTelegraphDisc(仮)" };
        Vector3[] verts = new Vector3[segments + 1];
        verts[0] = Vector3.zero;
        for (int i = 0; i < segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            verts[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
        }
        int[] tris = new int[segments * 3];
        for (int i = 0; i < segments; i++)
        {
            tris[i * 3] = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = (i + 1) % segments + 1;
        }
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }
}
