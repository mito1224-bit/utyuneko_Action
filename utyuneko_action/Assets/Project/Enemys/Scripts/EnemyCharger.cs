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

    [Tooltip("突進を始める索敵半径")]
    public float detectionRange = 8f;

    [Tooltip("射線が壁に遮られていたら突進しない")]
    public bool requireLineOfSight = true;

    [Tooltip("壁とみなすレイヤー。射線遮断＆突進の停止＆スタンの原因になる（自分の敵レイヤーは含めない）")]
    public LayerMask wallLayers;

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

    private enum Phase { Idle, Windup, Charge, Stun, Cooldown }
    private Phase phase = Phase.Idle;
    private float timer;

    private Vector2 chargeDir = Vector2.left; // 予兆開始時に固定する突進方向
    private float blinkTimer;

    private Transform player;
    private EnemyKnockback knockback;
    private Collider2D col;
    private Renderer[] renderers;

    public bool IsStunned => phase == Phase.Stun;

    void Awake()
    {
        knockback = GetComponent<EnemyKnockback>();
        col = GetComponent<Collider2D>();
        renderers = GetComponentsInChildren<Renderer>();
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
            return;
        }

        switch (phase)
        {
            case Phase.Idle:
                if (CanSeePlayer()) BeginWindup();
                break;

            case Phase.Windup:
                // trackDuringWindup なら予兆中も追従し、突進開始の瞬間に方向が確定する
                if (trackDuringWindup) chargeDir = DirectionToPlayer();
                Countdown(BeginCharge);
                break;

            case Phase.Charge:
                ChargeStep();
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

    private void Countdown(System.Action onElapsed)
    {
        timer -= Time.deltaTime;
        if (timer <= 0f) onElapsed();
    }

    // ─── フェーズ遷移 ─────────────────────────────

    private void BeginWindup()
    {
        phase = Phase.Windup;
        timer = Mathf.Max(0f, windupTime);
        // 初期方向を決定。trackDuringWindup=false ならこのまま固定（以降プレイヤーが逃げても追わない＝避けられる）。
        // trackDuringWindup=true なら予兆中も Update で更新され、突進開始の瞬間に確定する。
        chargeDir = DirectionToPlayer();
    }

    private void BeginCharge()
    {
        phase = Phase.Charge;
        timer = Mathf.Max(0f, maxChargeTime);
    }

    private void BeginStun()
    {
        phase = Phase.Stun;
        timer = Mathf.Max(0f, stunDuration);
        blinkTimer = 0f;
    }

    private void BeginCooldown()
    {
        SetRenderersEnabled(true); // スタン点滅から確実に表示へ戻す
        phase = Phase.Cooldown;
        timer = Mathf.Max(0f, cooldown);
    }

    // 中断時：点滅を戻してクールダウンへ
    private void AbortToCooldown()
    {
        SetRenderersEnabled(true);
        phase = Phase.Cooldown;
        timer = Mathf.Max(0f, cooldown);
    }

    // ─── 突進の1フレーム ───────────────────────────

    private void ChargeStep()
    {
        float step = chargeSpeed * Time.deltaTime;

        // 進行方向の壁を Raycast。今フレームの移動量＋余白の範囲で検知（トンネリング回避）
        Vector2 origin = (Vector2)transform.position + chargeDir * (HalfWidth() + 0.02f);
        RaycastHit2D hit = Physics2D.Raycast(origin, chargeDir, step + wallSkin, wallLayers);

        if (hit.collider != null)
        {
            // 壁手前まで進んで自滅スタン
            float travel = Mathf.Max(0f, hit.distance - wallSkin);
            transform.position += (Vector3)(chargeDir * travel);
            BeginStun();
            return;
        }

        transform.position += (Vector3)(chargeDir * step);

        // 壁に当たらず保険時間を過ぎたら突進終了（スタンはしない）
        timer -= Time.deltaTime;
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

    private float HalfWidth()
    {
        return col != null ? col.bounds.extents.x : 0.5f;
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

    // シーンビューで索敵範囲と突進方向を可視化
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)(chargeDir * 2f));
    }
}
