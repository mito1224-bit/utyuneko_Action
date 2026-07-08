using UnityEngine;

/// <summary>
/// 突進のみの敵。プレイヤーへ一直線に突進し、壁に当たると自らスタンを食らう（攻撃チャンスになる）。
///
/// フェーズ:
///   待機(Idle) → 予兆(Windup:突進方向を固定した最終警告) → 突進(Charge)
///        → 壁ヒットでスタン(Stun:停止＋点滅) → クールダウン(Cooldown) → 待機
///
/// 移動は EnemyMovement / EnemyKnockback と同じく transform で手動制御（物理解決に頼らない）。
/// 壁検知は衝突コールバックではなく進行方向への Physics2D.Raycast（高速突進でのトンネリング回避）。
/// プレイヤーへの接触ダメージ・反射は EnemyCollision(Reflect) が担当する（疎結合）。
/// 吹き飛び中／死亡中（EnemyKnockback）は突進を中断する。
/// </summary>
public class EnemyCharger : MonoBehaviour
{
    [Header("索敵")]
    [Tooltip("プレイヤーのタグ")]
    public string playerTag = "Player";

    [Tooltip("突進を始める索敵半径（円形索敵時のみ使用）")]
    public float detectionRange = 8f;

    [Tooltip("射線が壁に遮られていたら突進しない（円形索敵時のみ使用）")]
    public bool requireLineOfSight = true;

    [Tooltip("壁とみなすレイヤー。射線遮断＆突進の停止＆スタンの原因になる（自分の敵レイヤーは含めない）")]
    public LayerMask wallLayers;

    [Header("索敵（レイ）")]
    [Tooltip("ONにすると索敵を「進行方向への直線レイ」に切り替える。レイがプレイヤーに当たった瞬間、予兆(Windup)なしで即突進する。\n" +
             "OFF＝従来通りの円形索敵＋Windupを挟んでから突進する")]
    public bool useRayDetection = false;

    [Tooltip("レイの届く距離")]
    public float rayDetectionRange = 8f;

    [Tooltip("レイが衝突判定するレイヤー。プレイヤーと壁の両方を含めること（壁が手前にあれば遮られて検知しない）。自分の敵レイヤーは含めない")]
    public LayerMask rayDetectionLayers;

    [Header("突進")]
    [Tooltip("突進前の予兆（最終警告）時間。この間に方向が固定される＝避ける猶予")]
    public float windupTime = 0.5f;

    [Tooltip("突進速度")]
    public float chargeSpeed = 14f;

    [Tooltip("壁に当たらなかった場合に突進を打ち切る保険時間（秒）")]
    public float maxChargeTime = 2f;

    [Tooltip("突進方向を水平（左右）のみにする。OFFならプレイヤーの実際の位置（斜め上・下含む）へ一直線に突進する")]
    public bool horizontalOnly = false;

    [Tooltip("予兆中もプレイヤーを追い続け、突進開始の瞬間に方向を確定する。\n" +
             "OFF＝予兆開始時の方向で固定（以降プレイヤーが逃げれば避けられる＝避けゲー寄り）")]
    public bool trackDuringWindup = false;

    [Header("スタン")]
    [Tooltip("壁に当たって自滅したときのスタン時間（停止＝攻撃チャンス）")]
    public float stunDuration = 1.5f;

    [Tooltip("スタン明けから次の突進を始められるまでのクールダウン")]
    public float cooldown = 1f;

    [Tooltip("スタン中に Renderer を点滅させる")]
    public bool stunBlink = true;

    [Tooltip("スタン点滅の間隔（秒）")]
    public float blinkInterval = 0.1f;

    [Header("検知の詰め")]
    [Tooltip("壁の手前で止めるための余白。突進が壁にめり込まないよう少し手前で停止する")]
    public float wallSkin = 0.05f;

    [Header("可視化（レイ索敵）")]
    [Tooltip("索敵レイを実行時に線で表示する（スナイパーの射線と同じ LineRenderer 方式）。useRayDetection がONのときのみ意味を持つ")]
    public bool showRayDetectionLine = true;

    [Tooltip("索敵レイの色")]
    public Color rayDetectionColor = new Color(1f, 0.2f, 0.2f, 0.6f);

    [Tooltip("索敵レイの太さ")]
    public float rayDetectionWidth = 0.05f;

    [Header("デバッグ")]
    [Tooltip("レイ索敵の判定内容（何に当たったか・プレイヤー参照の有無）をConsoleに出力する")]
    public bool debugRayDetection = false;

    private enum Phase { Idle, Windup, Charge, Stun, Cooldown }
    private Phase phase = Phase.Idle;
    private float timer;

    private Vector2 chargeDir = Vector2.left; // 予兆開始時に固定する突進方向
    private float blinkTimer;

    private Transform player;
    private EnemyKnockback knockback;
    private EnemyMovement movement;
    private Collider2D col;
    private Rigidbody2D rb;
    private Renderer[] renderers;

    // 索敵レイの可視化用（実行時生成）。OnDestroy で破棄
    private LineRenderer rayLine;
    private Material rayLineMaterial;

    public bool IsStunned => phase == Phase.Stun;

    void Awake()
    {
        knockback = GetComponent<EnemyKnockback>();
        movement = GetComponent<EnemyMovement>();
        col = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        renderers = GetComponentsInChildren<Renderer>();
        CreateRayVisual();
    }

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null) player = p.transform;
    }

    void Update()
    {
        // 吹き飛び中／死亡中は突進を中断（スタン点滅の残りも戻す）
        if (knockback != null && (knockback.IsActive || knockback.IsDying))
        {
            if (phase != Phase.Idle) AbortToCooldown();
            HideRayLine();
            return;
        }

        switch (phase)
        {
            case Phase.Idle:
                if (useRayDetection)
                {
                    bool found = RayDetectPlayer(out float rayLength);
                    DrawRayLine(rayLength); // 遮られた位置まで描く（線が途中で止まれば何かが遮っている）
                    if (found) BeginChargeImmediately();
                }
                else if (CanSeePlayer()) BeginWindup();
                break;

            case Phase.Windup:
                // trackDuringWindup なら予兆中も追従し、突進開始の瞬間に方向が確定する
                if (trackDuringWindup) chargeDir = DirectionToPlayer();
                HideRayLine();
                Countdown(BeginCharge);
                break;

            case Phase.Charge:
                // 突進の移動と壁検知は FixedUpdate（物理）側で行う（rb.MovePosition でスイープ衝突させるため）
                break;

            case Phase.Stun:
                if (stunBlink) UpdateBlink();
                Countdown(BeginCooldown);
                break;

            case Phase.Cooldown:
                Countdown(() => phase = Phase.Idle);
                break;
        }
    }

    // 突進の移動は物理ステップで行う。rb.MovePosition なら静的な床・壁にスイープ衝突で止まり、
    // transform 直書きのようにすり抜けない（高速突進では Collision Detection を Continuous 推奨）。
    void FixedUpdate()
    {
        if (phase != Phase.Charge) return;
        // 吹き飛び中／死亡中は突進移動しない（中断は Update 側で処理）
        if (knockback != null && (knockback.IsActive || knockback.IsDying)) return;
        ChargeStep();
    }

    // 突進中に wallLayers の壁へ実際に接触したらスタン。先読み（CircleCast）が取りこぼしたときの保険。
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (phase != Phase.Charge) return;
        if ((wallLayers.value & (1 << collision.gameObject.layer)) == 0) return;
        BeginStun();
    }

    private void Countdown(System.Action onElapsed)
    {
        timer -= Time.deltaTime;
        if (timer <= 0f) onElapsed();
    }

    // ─── フェーズ遷移 ─────────────────────────────

    private void BeginWindup()
    {
        SetPatrolEnabled(false); // 攻撃中は巡回（EnemyMovement）を止める。同じ Rigidbody2D への MovePosition が競合して突進が上書きされるため
        phase = Phase.Windup;
        timer = Mathf.Max(0f, windupTime);
        // 初期方向を決定。trackDuringWindup=false ならこのまま固定（以降プレイヤーが逃げても追わない＝避けられる）。
        // trackDuringWindup=true なら予兆中も Update で更新され、突進開始の瞬間に確定する。
        chargeDir = DirectionToPlayer();
    }

    private void BeginCharge()
    {
        // 突進に入ったら索敵の視線ラインは消す（Idle→即突進の経路でも確実に消えるよう、
        // 突進開始の共通チョークポイントであるここで隠す。突進・スタン・クールダウン中は再描画されない）
        HideRayLine();
        phase = Phase.Charge;
        timer = Mathf.Max(0f, maxChargeTime);
    }

    // レイ索敵がプレイヤーを捉えた瞬間、Windup（予兆）を挟まずそのまま突進を開始する
    private void BeginChargeImmediately()
    {
        SetPatrolEnabled(false); // 攻撃中は巡回（EnemyMovement）を止める（BeginWindup と同じ理由）
        chargeDir = RayDirection();
        BeginCharge();
    }

    private void BeginStun()
    {
        if (rb != null) rb.linearVelocity = Vector2.zero; // 突進終了。衝突で得た残留速度を消す
        phase = Phase.Stun;
        timer = Mathf.Max(0f, stunDuration);
        blinkTimer = 0f;
    }

    private void BeginCooldown()
    {
        if (rb != null) rb.linearVelocity = Vector2.zero; // 突進終了。衝突で得た残留速度を消す
        SetRenderersEnabled(true); // スタン点滅から確実に表示へ戻す
        SetPatrolEnabled(true);    // 攻撃終了。巡回を再開する
        phase = Phase.Cooldown;
        timer = Mathf.Max(0f, cooldown);
    }

    // 中断時：点滅を戻してクールダウンへ
    private void AbortToCooldown()
    {
        SetRenderersEnabled(true);
        SetPatrolEnabled(true); // 攻撃終了。巡回を再開する
        phase = Phase.Cooldown;
        timer = Mathf.Max(0f, cooldown);
    }

    // 攻撃（予兆〜突進〜スタン）中は EnemyMovement を無効化して MovePosition の競合を防ぐ。
    // EnemyKnockback とは違い EnemyMovement 側は EnemyCharger を知らないため、こちらから止める
    private void SetPatrolEnabled(bool on)
    {
        if (movement != null) movement.enabled = on;
    }

    // このコンポーネントが攻撃中に無効化された場合も巡回を確実に戻す
    void OnDisable()
    {
        SetPatrolEnabled(true);
    }

    // ─── 突進の1フレーム ───────────────────────────

    private void ChargeStep()
    {
        if (rb == null) return;

        float step = chargeSpeed * Time.fixedDeltaTime;

        // 進行方向の壁を CircleCast で先読み。当たれば壁手前まで進んで自滅スタン。
        // 今フレームの移動量＋余白の距離だけ先読みしてトンネリングも回避する。
        RaycastHit2D hit = Physics2D.CircleCast(rb.position, BodyRadius(), chargeDir, step + wallSkin, wallLayers);

        if (hit.collider != null)
        {
            // 壁手前（本体が wallSkin だけ残る位置）まで進んで自滅スタン
            float travel = Mathf.Max(0f, hit.distance - wallSkin);
            rb.MovePosition(rb.position + chargeDir * travel);
            BeginStun();
            return;
        }

        // 物理移動。MovePosition なので静的な床・壁にはスイープ衝突で止まり transform 直書きのようにすり抜けない。
        // 先読みが取りこぼしても物理で止まり、OnCollisionEnter2D 側でスタンする。
        rb.MovePosition(rb.position + chargeDir * step);

        // 壁に当たらず保険時間を過ぎたら突進終了（スタンはしない）
        timer -= Time.fixedDeltaTime;
        if (timer <= 0f) BeginCooldown();
    }

    // ─── 索敵・方向 ───────────────────────────────

    private Vector2 DirectionToPlayer()
    {
        if (player == null) return chargeDir;

        Vector2 d = (Vector2)player.position - (Vector2)transform.position;
        if (horizontalOnly)
        {
            float sx = Mathf.Sign(d.x);
            return new Vector2(sx == 0 ? (chargeDir.x >= 0 ? 1f : -1f) : sx, 0f);
        }
        return d.sqrMagnitude > 0.0001f ? d.normalized : chargeDir;
    }

    private bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector2 origin = transform.position;
        Vector2 toPlayer = (Vector2)player.position - origin;
        if (toPlayer.magnitude > detectionRange) return false;

        if (requireLineOfSight)
        {
            RaycastHit2D wall = Physics2D.Raycast(origin, toPlayer.normalized, toPlayer.magnitude, wallLayers);
            if (wall.collider != null) return false;
        }
        return true;
    }

    // EnemyMovement の向いている方向（左右の巡回方向）にレイを飛ばす。EnemyMovement が無ければ直前の突進方向を使う
    private Vector2 RayDirection()
    {
        if (movement != null)
        {
            Vector2 d = movement.moveDirection;
            if (d.sqrMagnitude > 0.0001f) return d.normalized;
        }
        return chargeDir;
    }

    // 進行方向へレイを飛ばし、プレイヤーが見えたら索敵成立（壁が手前にあれば遮られる）。
    // Defaultレイヤーにはチェックポイント等のトリガーが多数あるため、トリガーは透過して
    // 「最初に当たった実体」がプレイヤーか壁かで判定する（トリガーでもプレイヤー所属なら検知）。
    // blockedLength: 実体に遮られた位置までの距離（可視化用。遮られなければレイ全長）
    private bool RayDetectPlayer(out float blockedLength)
    {
        blockedLength = rayDetectionRange;

        if (player == null)
        {
            if (debugRayDetection) Debug.Log($"[EnemyCharger] player参照がnull（タグ'{playerTag}'のオブジェクトが見つかっていない）", this);
            return false;
        }

        Vector2 origin = transform.position;
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, RayDirection(), rayDetectionRange, rayDetectionLayers);

        foreach (RaycastHit2D hit in hits) // RaycastAll は距離順
        {
            if (hit.collider.isTrigger)
            {
                if (IsPlayerCollider(hit.collider))
                {
                    if (debugRayDetection) Debug.Log($"[EnemyCharger] プレイヤー検知（トリガー経由）: {hit.collider.name}", this);
                    return true; // プレイヤーの足元センサー等
                }
                if (debugRayDetection) Debug.Log($"[EnemyCharger] トリガーを透過: {hit.collider.name} (layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}, dist={hit.distance:F2})", this);
                continue; // 他のトリガー（チェックポイント等）は素通し
            }

            // 最初の実体。壁ならここで遮断
            blockedLength = hit.distance;
            bool isPlayer = IsPlayerCollider(hit.collider);
            if (debugRayDetection) Debug.Log($"[EnemyCharger] 最初の実体ヒット: {hit.collider.name} (layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}, dist={hit.distance:F2}) → {(isPlayer ? "プレイヤー！突進" : "プレイヤーではないので遮断")}", this);
            return isPlayer;
        }

        if (debugRayDetection) Debug.Log($"[EnemyCharger] レイは何にも当たらず（向き={RayDirection()}, 距離={rayDetectionRange}, マスク={rayDetectionLayers.value}）", this);
        return false;
    }

    // タグ判定。コライダーが子（HoverSensor等）の場合はぶら下がる Rigidbody2D 側のタグも見る
    private bool IsPlayerCollider(Collider2D c)
    {
        if (c.CompareTag(playerTag)) return true;
        Rigidbody2D body = c.attachedRigidbody;
        return body != null && body.CompareTag(playerTag);
    }

    // ─── 可視化（LineRenderer 実行時生成、EnemySniper の射線と同じ方式） ──────────

    private void CreateRayVisual()
    {
        GameObject go = new GameObject("ChargerDetectionRay");
        go.transform.SetParent(transform, false);

        rayLine = go.AddComponent<LineRenderer>();
        rayLine.useWorldSpace = true;
        rayLine.positionCount = 2;
        rayLine.numCapVertices = 2;
        rayLine.textureMode = LineTextureMode.Stretch;
        rayLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rayLine.receiveShadows = false;
        rayLine.sortingOrder = 10; // 敵・プレイヤーより前に描く

        // ビルトインRP前提。Sprites/Default は頂点カラー＆半透明ブレンド対応
        rayLineMaterial = new Material(Shader.Find("Sprites/Default"));
        rayLineMaterial.renderQueue = 3000; // Transparent
        rayLine.material = rayLineMaterial;

        HideRayLine();
    }

    // length: 描画する長さ。実体に遮られた位置で線を止める（線が途中で止まれば遮蔽物の存在が見える）
    private void DrawRayLine(float length)
    {
        if (rayLine == null || !useRayDetection || !showRayDetectionLine) { HideRayLine(); return; }

        Vector2 origin = transform.position;
        Vector2 dir = RayDirection();

        rayLine.enabled = true;
        rayLine.startWidth = rayDetectionWidth;
        rayLine.endWidth = rayDetectionWidth;
        rayLine.startColor = rayDetectionColor;
        rayLine.endColor = rayDetectionColor;

        rayLine.SetPosition(0, new Vector3(origin.x, origin.y, transform.position.z));
        Vector2 end = origin + dir * length;
        rayLine.SetPosition(1, new Vector3(end.x, end.y, transform.position.z));
    }

    private void HideRayLine()
    {
        if (rayLine != null) rayLine.enabled = false;
    }

    void OnDestroy()
    {
        if (rayLineMaterial != null) Destroy(rayLineMaterial);
    }

    // 本体のおおよその半径（CircleCast 用）。コライダーの小さい方の半幅を採用し、
    // 円が本体内に収まるようにする（壁へ正面から当たれば前面で止まる）。
    private float BodyRadius()
    {
        if (col == null) return 0.5f;
        Vector2 e = col.bounds.extents;
        return Mathf.Max(0.01f, Mathf.Min(e.x, e.y));
    }

    // ─── スタン点滅 ───────────────────────────────

    private void UpdateBlink()
    {
        blinkTimer += Time.deltaTime;
        if (blinkTimer >= blinkInterval)
        {
            blinkTimer = 0f;
            foreach (Renderer r in renderers)
                if (r != null) r.enabled = !r.enabled;
        }
    }

    private void SetRenderersEnabled(bool on)
    {
        foreach (Renderer r in renderers)
            if (r != null) r.enabled = on;
    }

    // シーンビューで索敵範囲（またはレイ）と突進方向を可視化
    private void OnDrawGizmosSelected()
    {
        if (useRayDetection)
        {
            Gizmos.color = Color.red;
            Vector2 dir = Application.isPlaying ? RayDirection() : (Vector2)transform.right;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)(dir * rayDetectionRange));
        }
        else
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)(chargeDir * 2f));
    }
}