using UnityEngine;

/// <summary>
/// ボス本体・分身の両方に付ける「スナイパー銃座」コンポーネント。
///BossSniper
///BossController
/// 役割:
///   - 照準（射線がプレイヤーを追う）／ロック（固定）／発射（レーザー判定）の描画と当たり判定。
///     ※タイマーは持たない。いつ照準するか・撃つかはボスのステートが全ユニット同期で指示する。
///   - プレイヤーの「バースト体当たり」を受けたことを通知する（OnBurstHit）。
///     本物かどうかの判断・スタン処理などはボスのステート側が行う。
///   - 瞬間移動用の収縮演出：モデルのXZスケールを縮めて縦線状に消え、復元して出現する。
///     （BeginShrink / BeginExpand / SetShrunkenImmediate / IsScaleAnimating）
///   - 3Dモデルを弾を打つ方向の左右に合わせて向ける（EnemySniper の visualTransform と同じ流儀）。
///
/// 物理は2D前提（Physics2D）。可視化は LineRenderer を実行時生成し、Material は OnDestroy で破棄。
///
/// セットアップ:
///   - ボス本体のルート、および分身プレハブのルートに付ける。
///   - 分身プレハブは「Collider2D（IsTrigger 推奨）＋ 見た目の子オブジェクト」の構成でOK。
///     ビーム設定はボスが自分の値を分身へコピーするので、プレハブ側で個別調整は不要。
/// </summary>
public class BossSniperBeamUnit : MonoBehaviour
{
    [Header("射線・レーザー")]
    [Tooltip("射線・レーザーを出す原点（未指定なら自分の位置）")]
    public Transform firePoint;

    [Tooltip("射線を遮る壁などのレイヤー。レーザーもここで止まる")]
    public LayerMask obstacleLayer;

    [Tooltip("ダメージ判定の対象レイヤー（プレイヤーのレイヤーを含めること）")]
    public LayerMask targetLayers = ~0;

    [Tooltip("レーザーの最大長（壁があればそこで止まる）")]
    public float maxBeamLength = 30f;

    [Tooltip("レーザーがプレイヤーに与えるダメージ量")]
    public int beamDamage = 1;

    [Tooltip("バースト以外でプレイヤーが本体に接触したとき、プレイヤーに与える接触ダメージ量（本物のみ・偽物は無害）")]
    public int contactDamage = 1;

    [Header("可視化")]
    [Tooltip("射線（照準中）の太さ")]
    public float sightWidth = 0.06f;

    [Tooltip("レーザー（発射中）の太さ")]
    public float beamWidth = 0.4f;

    [Tooltip("照準中の射線の色（追従）")]
    public Color aimColor = new Color(1f, 1f, 0f, 0.5f);

    [Tooltip("ロック中（最終警告）の射線の色")]
    public Color lockColor = new Color(1f, 0f, 0f, 0.8f);

    [Tooltip("レーザー発射中の色")]
    public Color fireColor = new Color(1f, 0.2f, 0.2f, 0.95f);

    [Header("モデルの向き（Y軸で左右だけ振り向く）")]
    [Tooltip("照準方向の左右に合わせて向ける3Dモデル（コライダーを持たない子を割り当てる）。瞬間移動の収縮演出もこのTransformのスケールで行う")]
    public Transform visualTransform;

    [Tooltip("左向き（弾の方向が-x）のときの角度（度）")]
    public float leftAngle = 50f;

    [Tooltip("右向き（弾の方向が+x）のときの角度（度）")]
    public float rightAngle = -50f;

    [Tooltip("角度を変える速さ（度/秒）。0以下なら即時に切り替え")]
    public float visualRotationSpeed = 360f;

    [Tooltip("照準方向への傾き（Z回転）の速さ（度/秒）。全ての攻撃（照準・ロック・発射）で銃口が射線の方向を向くときの追従の速さ")]
    public float tiltRotationSpeed = 720f;

    [Header("瞬間移動の収縮演出")]
    [Tooltip("収縮しきったときのXZスケール倍率。0だと描画やスケール復元が壊れることがあるので僅かに残す")]
    public float shrunkenScale = 0.02f;

    /// <summary>
    /// プレイヤーのバースト体当たりを受けたときの通知。
    /// 引数は (このユニット, 当たってきたプレイヤー)。ボスが購読し、現在のステートへ渡す。
    /// どう反応するか（スタン／ダメージ／無視）はステート側が決める。
    /// </summary>
    public System.Action<BossSniperBeamUnit, PlayerController> OnBurstHit;

    /// <summary>本物（ボス本体）かどうか。BossSniper が設定する。</summary>
    public bool IsReal { get; set; }

    /// <summary>
    /// 破壊不可の設置ユニット（横一斉射の左右砲台など）か。BossSniper が生成時に設定する。
    /// true の間はバーストで壊せず、バーストかどうかに関わらず触れたプレイヤーが接触ダメージを受ける。
    /// </summary>
    public bool IsHazardUnit { get; set; }

    /// <summary>
    /// 直近のバースト体当たりの方向（プレイヤー→このユニット、正規化）。
    /// 通常ダメージのシェイクを「殴られた方向の軸」に沿わせるために使う。
    /// </summary>
    public Vector2 LastHitDirection { get; private set; } = Vector2.right;

    /// <summary>収縮／復元アニメが進行中か。</summary>
    public bool IsScaleAnimating => !Mathf.Approximately(scaleFactor, scaleTarget);

    private BossSniperHealth bossHealth;

    private Transform player;
    private Vector2 lockedDir = Vector2.left; // 現在の照準／固定方向
    private float currentVisualAngle;
    private bool hasVisualAngleInit;

    // 照準方向への傾き（射線の方向に銃口を合わせる）。ビームを出しているフレームだけ有効になり、
    // 攻撃していない間（巡回移動中など）は滑らかに水平へ戻る
    private bool aimTiltActive;   // このフレームでビームを出したか（各Tickが立てる）
    private float currentTilt;

    // 収縮演出（XZのみ縮めて縦線状に消える）
    private Vector3 baseScale = Vector3.one;
    private bool hasBaseScale;
    private float scaleFactor = 1f;   // 1=通常, shrunkenScale=収縮しきり
    private float scaleTarget = 1f;
    private float scaleSpeed;         // factor/秒

    private Collider2D[] hitboxes;

    private LineRenderer line;
    private Material lineMaterial;

    void Awake()
    {
        hitboxes = GetComponentsInChildren<Collider2D>(true);

        bossHealth = GetComponentInParent<BossSniperHealth>();

        if (visualTransform != null)
        {
            baseScale = visualTransform.localScale;
            hasBaseScale = true;
        }

        CreateBeamVisual();
    }

    void Update()
    {
        // 収縮／復元のスケールアニメーション
        if (IsScaleAnimating)
        {
            scaleFactor = Mathf.MoveTowards(scaleFactor, scaleTarget, scaleSpeed * Time.deltaTime);
            ApplyScale();
        }
    }

    /// <summary>ボスから呼ばれる初期化。追うプレイヤーを受け取る。</summary>
    public void Init(Transform playerTransform)
    {
        player = playerTransform;
    }

    /// <summary>別ユニット（＝ボス本体）のビーム設定をコピーする。分身生成時に使用。</summary>
    public void CopySettingsFrom(BossSniperBeamUnit src)
    {
        obstacleLayer = src.obstacleLayer;
        targetLayers = src.targetLayers;
        maxBeamLength = src.maxBeamLength;
        beamDamage = src.beamDamage;
        contactDamage = src.contactDamage;
        sightWidth = src.sightWidth;
        beamWidth = src.beamWidth;
        aimColor = src.aimColor;
        lockColor = src.lockColor;
        fireColor = src.fireColor;
        leftAngle = src.leftAngle;
        rightAngle = src.rightAngle;
        visualRotationSpeed = src.visualRotationSpeed;
        tiltRotationSpeed = src.tiltRotationSpeed;
        shrunkenScale = src.shrunkenScale;
    }

    // ─── 瞬間移動の収縮演出 ─────────────────────────

    /// <summary>XZスケールを縮めて縦線状に消えていく。</summary>
    public void BeginShrink(float duration)
    {
        SetScaleTarget(shrunkenScale, duration);
    }

    /// <summary>XZスケールを元に戻して出現する。</summary>
    public void BeginExpand(float duration)
    {
        SetScaleTarget(1f, duration);
    }

    /// <summary>収縮しきった状態に即座にする（分身の初期出現用）。当たり判定も無効化する。</summary>
    public void SetShrunkenImmediate()
    {
        scaleFactor = scaleTarget = shrunkenScale;
        ApplyScale();
        SetHitboxEnabled(false);
    }

    /// <summary>自分の Collider2D 群をまとめて有効／無効にする（消えている間は殴れない）。</summary>
    public void SetHitboxEnabled(bool enabled)
    {
        foreach (Collider2D c in hitboxes)
        {
            if (c != null) c.enabled = enabled;
        }
    }

    private void SetScaleTarget(float target, float duration)
    {
        scaleTarget = target;
        scaleSpeed = Mathf.Abs(scaleFactor - target) / Mathf.Max(0.01f, duration);

        // モデル未設定なら演出できないので即完了扱いにする（進行が詰まらないように）
        if (visualTransform == null || !hasBaseScale)
        {
            scaleFactor = target;
        }
    }

    private void ApplyScale()
    {
        if (visualTransform == null || !hasBaseScale) return;
        float f = Mathf.Max(shrunkenScale, scaleFactor);
        visualTransform.localScale = new Vector3(baseScale.x * f, baseScale.y, baseScale.z * f);
    }

    // ─── ボスのステートが毎フレーム呼ぶ描画・判定 ──────────

    /// <summary>照準中：射線がプレイヤーを追従する。</summary>
    public void AimTick()
    {
        Vector2 d = DirectionToPlayer();
        if (d.sqrMagnitude > 0.0001f) lockedDir = d;
        DrawBeam(lockedDir, aimColor, sightWidth);
        aimTiltActive = true; // 照準中は銃口を射線方向へ傾ける
        UpdateModelFacing();
    }

    /// <summary>ロック中：射線を固定したまま最終警告。</summary>
    public void LockTick()
    {
        DrawBeam(lockedDir, lockColor, sightWidth);
        aimTiltActive = true; // ロック中も射線方向へ傾ける
        UpdateModelFacing();
    }

    /// <summary>発射中：レーザー表示＋毎フレーム当たり判定（無敵時間は PlayerHealth 側）。</summary>
    public void FireTick()
    {
        DrawBeam(lockedDir, fireColor, beamWidth);
        ApplyBeamDamage();
        aimTiltActive = true; // 発射中も射線方向へ傾ける
        UpdateModelFacing();
    }

    /// <summary>ビームを消す。</summary>
    public void HideBeam()
    {
        if (line != null) line.enabled = false;
    }

    /// <summary>ビームを出していない間も、モデルだけプレイヤーの方へ向けたいときに呼ぶ。</summary>
    public void FaceTick()
    {
        Vector2 d = DirectionToPlayer();
        if (d.sqrMagnitude > 0.0001f) lockedDir = d;
        UpdateModelFacing();
    }

    /// <summary>
    /// 現在の lockedDir 方向に、見た目の左右向きがほぼ追いついているか。
    /// FaceTick() の後に呼ぶ想定。
    /// </summary>
    public bool IsVisualFacingCurrentDirection(float angleTolerance = 2f)
    {
        if (visualTransform == null) return true;

        // まだ初期化されていないなら、向き合わせ未完了扱い
        if (!hasVisualAngleInit) return false;

        float target = TargetVisualAngle();
        return Mathf.Abs(Mathf.DeltaAngle(currentVisualAngle, target)) <= angleTolerance;
    }

    /// <summary>
    /// 線は出さず、現在の lockedDir 方向へモデルだけ向ける。
    /// 予測線・ロック線を出す前の「構え」用。
    /// </summary>
    public void AimVisualOnlyTick()
    {
        if (visualTransform == null) return;

        // ビームを出している扱いにして、Z傾きも lockedDir に合わせる
        aimTiltActive = true;
        UpdateModelFacing();
    }

    /// <summary>
    /// 線は出さず、プレイヤー方向へモデルだけ向ける。
    /// 分身攻撃の Aim に入る前など、まだ狙いを固定しない場面用。
    /// </summary>
    public void FacePlayerVisualOnlyTick()
    {
        Vector2 d = DirectionToPlayer();
        if (d.sqrMagnitude > 0.0001f) lockedDir = d;

        aimTiltActive = true;
        UpdateModelFacing();
    }

    /// <summary>
    /// 現在の lockedDir 方向へ、見た目の左右向きと傾きがほぼ追いついているか。
    /// AimVisualOnlyTick() / FacePlayerVisualOnlyTick() の後に呼ぶ想定。
    /// </summary>
    public bool IsVisualAlignedToAim(float angleTolerance = 2f)
    {
        if (visualTransform == null) return true;
        if (!hasVisualAngleInit) return false;

        float targetVisual = TargetVisualAngle();

        float dirAngle = Mathf.Atan2(lockedDir.y, lockedDir.x) * Mathf.Rad2Deg;
        float targetTilt = lockedDir.x >= 0f
            ? dirAngle
            : Mathf.DeltaAngle(180f, dirAngle);

        bool visualOk =
            Mathf.Abs(Mathf.DeltaAngle(currentVisualAngle, targetVisual)) <= angleTolerance;

        bool tiltOk =
            Mathf.Abs(Mathf.DeltaAngle(currentTilt, targetTilt)) <= angleTolerance;

        return visualOk && tiltOk;
    }

    /// <summary>
    /// 線は出さず、現在のプレイヤー方向へモデルを即座に向ける。
    /// テレポート先へ座標を変えた直後、出現アニメを始める前に使う。
    /// </summary>
    public void SnapVisualToPlayerImmediate()
    {
        Vector2 d = DirectionToPlayer();

        if (d.sqrMagnitude > 0.0001f)
        {
            lockedDir = d.normalized;
        }

        SnapVisualToCurrentAimImmediate();
    }

    /// <summary>
    /// 線は出さず、現在の lockedDir 方向へモデルを即座に向ける。
    /// </summary>
    public void SnapVisualToCurrentAimImmediate()
    {
        if (visualTransform == null) return;

        currentVisualAngle = TargetVisualAngle();
        hasVisualAngleInit = true;

        float dirAngle = Mathf.Atan2(lockedDir.y, lockedDir.x) * Mathf.Rad2Deg;
        currentTilt = lockedDir.x >= 0f
            ? dirAngle
            : Mathf.DeltaAngle(180f, dirAngle);

        visualTransform.localRotation =
            Quaternion.AngleAxis(currentTilt, Vector3.forward) *
            Quaternion.Euler(0f, currentVisualAngle, 0f);

        aimTiltActive = false;
    }

    /// <summary>
    /// 指定したワールド座標の方向へ照準を固定する（偏差撃ちなど、プレイヤーの現在位置以外を狙うとき用）。
    /// 以後 LockTick / FireTick はこの方向を使う（AimTick / FaceTick を呼ぶと上書きされるので注意）。
    /// </summary>
    public void SetAimPoint(Vector2 worldPoint)
    {
        Vector2 d = worldPoint - FireOrigin();
        if (d.sqrMagnitude > 0.0001f) lockedDir = d.normalized;
    }

    /// <summary>
    /// 指定した方向へ照準を固定する（全体攻撃の回転連射など、角度で狙いを決めるとき用）。
    /// 以後 LockTick / FireTick はこの方向を使う（AimTick / FaceTick を呼ぶと上書きされるので注意）。
    /// </summary>
    public void SetAimDirection(Vector2 dir)
    {
        if (dir.sqrMagnitude > 0.0001f) lockedDir = dir.normalized;
    }

    // ─── 方向・判定 ──────────────────────────────

    private Vector2 FireOrigin()
    {
        return firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
    }

    private Vector2 DirectionToPlayer()
    {
        if (player == null) return lockedDir;
        Vector2 d = (Vector2)player.position - FireOrigin();
        return d.sqrMagnitude > 0.0001f ? d.normalized : lockedDir;
    }

    // 壁で止まることを考慮したレーザーの長さ
    private float ComputeBeamLength(Vector2 origin, Vector2 dir)
    {
        RaycastHit2D wall = Physics2D.Raycast(origin, dir, maxBeamLength, obstacleLayer);
        return wall.collider != null ? wall.distance : maxBeamLength;
    }

    private void ApplyBeamDamage()
    {
        Vector2 origin = FireOrigin();
        float len = ComputeBeamLength(origin, lockedDir);

        // 太さを考慮してプレイヤーを拾う（EnemySniper と同じ CircleCast 方式）
        float radius = Mathf.Max(0.01f, beamWidth * 0.5f);
        RaycastHit2D hit = Physics2D.CircleCast(origin, radius, lockedDir, len, targetLayers);
        if (hit.collider != null)
        {
            PlayerHealth hp = hit.collider.GetComponentInParent<PlayerHealth>();
            if (hp != null) hp.TakeDamage(beamDamage);
        }
    }

    // ─── プレイヤーのバースト体当たり検知 ─────────────
    // 本体（非Trigger）と分身（Trigger）のどちらの構成でも拾えるよう両方受ける。
    // 受けた通知にどう反応するか（無敵扱いを含む）は、ボスの現在のステートが決める。

    void OnCollisionEnter2D(Collision2D collision)
    {
        HandleContact(collision.collider);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        HandleContact(other);
    }

    private void HandleContact(Collider2D col)
    {
        PlayerController pc = col.GetComponentInParent<PlayerController>();
        if (pc == null) return;

        if (bossHealth.CurrentHP <= 0) return;

        // 破壊不可の設置ユニット：バーストかどうかに関わらず、触れたプレイヤーが接触ダメージを受けるだけ。
        // OnBurstHit は通知しない＝バーストで壊せない
        if (IsHazardUnit)
        {
            if (contactDamage > 0)
            {
                PlayerHealth hazardHp = col.GetComponentInParent<PlayerHealth>();
                if (hazardHp != null) hazardHp.TakeDamage(contactDamage);
            }
            return;
        }

        if (pc.CurrentState == pc.StateBurst)
        {
            // 殴られた方向（プレイヤー→このユニット）を記録。通常ダメージのシェイク軸に使う
            Vector2 dir = (Vector2)transform.position - (Vector2)pc.transform.position;
            if (dir.sqrMagnitude > 0.0001f) LastHitDirection = dir.normalized;

            // 「バースト状態の体当たり」は攻撃としてボスのステートへ通知（無敵扱いはステートが判断）
            if (OnBurstHit != null) OnBurstHit.Invoke(this, pc);
        }
        else
        {
            // バースト以外で本物に触れたら、プレイヤーがダメージを受ける（偽物は無害）。
            // スタン中も含めて常に有害。ただし瞬間移動で消えている間は当たり判定自体が無効。
            if (IsReal && contactDamage > 0)
            {
                if (bossHealth != null && bossHealth.IsInvincible)
                {
                    return;
                }

                PlayerHealth hp = col.GetComponentInParent<PlayerHealth>();
                if (hp != null) hp.TakeDamage(contactDamage);
            }
        }
    }

    // ─── モデルの向き（EnemySniper と同じY軸振り向き） ───

    private void UpdateModelFacing()
    {
        if (visualTransform == null) return;

        float target = TargetVisualAngle();

        if (!hasVisualAngleInit)
        {
            currentVisualAngle = target;
            hasVisualAngleInit = true;
        }
        else if (visualRotationSpeed > 0f)
        {
            currentVisualAngle = Mathf.MoveTowards(currentVisualAngle, target, visualRotationSpeed * Time.deltaTime);
        }
        else
        {
            currentVisualAngle = target;
        }

        // 照準方向への傾き（Z回転）。ビームを出しているフレーム（aimTiltActive）だけ射線の角度へ傾け、
        // 出していない間は 0（水平）へ戻る。右向きなら射線の角度そのまま、
        // 左向きなら「左（180度）からのずれ」を傾きにする（左右の振り向きと矛盾しないように）
        float targetTilt = 0f;
        if (aimTiltActive)
        {
            float dirAngle = Mathf.Atan2(lockedDir.y, lockedDir.x) * Mathf.Rad2Deg;
            targetTilt = lockedDir.x >= 0f ? dirAngle : Mathf.DeltaAngle(180f, dirAngle);
        }
        currentTilt = Mathf.MoveTowardsAngle(currentTilt, targetTilt, tiltRotationSpeed * Time.deltaTime);

        // まず左右へ振り向き（Y回転）、その上から射線の角度へ傾ける（画面の回転軸＝Z回転）
        visualTransform.localRotation =
            Quaternion.AngleAxis(currentTilt, Vector3.forward) * Quaternion.Euler(0f, currentVisualAngle, 0f);

        aimTiltActive = false; // 毎フレームの終わりに倒す。次フレームも攻撃中なら各Tickが立て直す
    }

    private float TargetVisualAngle()
    {
        if (lockedDir.x < 0f) return leftAngle;
        if (lockedDir.x > 0f) return rightAngle;
        return currentVisualAngle;
    }

    // ─── 可視化（LineRenderer 実行時生成） ──────────

    private void CreateBeamVisual()
    {
        GameObject go = new GameObject("BossBeam");
        go.transform.SetParent(transform, false);

        line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.numCapVertices = 2;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = 10;

        // ビルトインRP前提。Sprites/Default は頂点カラー＆半透明ブレンド対応
        lineMaterial = new Material(Shader.Find("Sprites/Default"));
        lineMaterial.renderQueue = 3000; // Transparent
        line.material = lineMaterial;

        HideBeam();
    }

    private void DrawBeam(Vector2 dir, Color color, float width)
    {
        if (line == null) return;

        Vector2 origin = FireOrigin();
        float len = ComputeBeamLength(origin, dir);

        line.enabled = true;
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.SetPosition(0, origin);
        line.SetPosition(1, origin + dir * len);
    }

    private void OnDestroy()
    {
        if (lineMaterial != null) Destroy(lineMaterial);
    }
}