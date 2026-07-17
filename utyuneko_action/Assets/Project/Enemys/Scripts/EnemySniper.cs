using UnityEngine;

/// <summary>
/// スナイパー型の敵。
///
/// 仕様:
///   - プレイヤーが射程に入り射線が通る（壁に遮られない）と「照準」を開始し、射線がプレイヤーを追う。
///   - 一定時間（aimTime）経つと射線がその方向に固定（ロック）され、最終警告（lockTime）のあと
///     その線上に「レーザー」を発射してダメージ判定（fireDuration）。
///   - 一度照準に入るとプレイヤーが逃げても固定方向に撃つ（避けるゲーム性）。
///
/// 物理は2D前提（Physics2D）。プレイヤーは Rigidbody2D/Collider2D。
/// 吹き飛び中／死亡中（EnemyKnockback）は攻撃を中断する（EnemyAreaAttack と同じ協調）。
///
/// 可視化は LineRenderer を実行時生成（プレハブ不要）。射線=細い／レーザー=太い。
/// 生成した Material は OnDestroy で破棄（リーク対策の流儀どおり）。
/// </summary>
public class EnemySniper : MonoBehaviour
{
    [Header("索敵")]
    [Tooltip("プレイヤーのタグ")]
    public string playerTag = "Player";

    [Tooltip("プレイヤーを見つける半径")]
    public float detectionRange = 12f;

    [Tooltip("射線を遮る壁などのレイヤー。レーザーもここで止まる")]
    public LayerMask obstacleLayer;

    [Tooltip("ダメージ判定の対象レイヤー（プレイヤーのレイヤーを含めること）")]
    public LayerMask targetLayers = ~0;

    [Tooltip("射線・レーザーを出す原点（未指定なら自分の位置）")]
    public Transform firePoint;

    [Header("タイミング")]
    [Tooltip("照準（射線がプレイヤーを追う）時間。経つと射線が固定される")]
    public float aimTime = 1.2f;

    [Tooltip("射線固定後、レーザー発射までの最終警告時間")]
    public float lockTime = 0.3f;

    [Tooltip("レーザー（攻撃判定）が出ている時間")]
    public float fireDuration = 0.25f;

    [Tooltip("発射後、次の照準を始められるまでのクールダウン")]
    public float cooldown = 2f;

    [Header("レーザー・威力")]
    [Tooltip("レーザーの最大長（壁があればそこで止まる）")]
    public float maxBeamLength = 30f;

    [Tooltip("レーザーがプレイヤーに与えるダメージ量")]
    public int beamDamage = 1;

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

    [Tooltip("射線・ロック・レーザーを描く LineRenderer のマテリアル。未指定なら従来どおり Sprites/Default を実行時生成する。\n" +
             "★ラインの形・色（aim/lock/fireColor）のラープはそのまま。色は LineRenderer の頂点カラーで乗るので、" +
             "Boss_Area（URPHologram）のように頂点カラーを乗算するシェーダーならそのまま効く（ボス2の予兆と見た目が揃う）")]
    public Material beamMaterial;

    [Header("レーザー演出（任意／Particle フォルダの Lazer）")]
    [Tooltip("発射の瞬間に再生するレーザーパーティクル。Lazer プレハブを子に置いて LaserVisualController を付けたものを割り当てる。\n" +
             "未指定なら従来どおり LineRenderer のビームだけで動く")]
    public LaserVisualController laserVisual;

    [Tooltip("レーザーパーティクルの太さ倍率（1＝プレハブそのまま）。beamWidth（当たり判定の太さ）とは別物なので見た目だけここで合わせる")]
    public float laserVisualWidthMultiplier = 1f;

    [Tooltip("パーティクルを出す間は LineRenderer のビームを隠す（線とパーティクルが二重に見えるのを防ぐ）。射線・ロックの予兆は隠さない")]
    public bool hideLineWhileLaserFX = true;

    [Header("銃口（照準追従）")]
    [Tooltip("照準方向に合わせて回転・配置する銃口オブジェクト（バレル等）。firePoint をこの子にすると射線原点も追従する")]
    public Transform aimPivot;

    [Tooltip("0なら最初に置いた位置を基準に照準方向へオービット（反対方向を狙うと銃口も反対側へ回り込む）。0より大きいと敵中心からその距離の純粋な放射状配置で上書き")]
    public float aimPivotDistance = 0f;

    [Tooltip("銃口スプライトの基準向きの補正角（度）。既定は右(+X)向きが照準方向に一致。上向きの絵なら-90など")]
    public float aimAngleOffset = 0f;

    [Header("モデルの向き（弾を打つ方向へ左右だけ向ける／Y軸）")]
    [Tooltip("弾を打つ方向(lockedDir)の左右に合わせて向けるモデル＝Rotation層の子（プレイヤーに倣った入れ子の回転層）。" +
             "EnemyMovement を付けない前提でスナイパー自身が回す。未指定なら何もしない。" +
             "Rigidbody2D/Collider2D と同居するルートではなく、コライダーを持たない子を割り当てること")]
    public Transform visualTransform;

    [Tooltip("モデルを左右に向ける回転軸（追加された3Dモデルの振り向きは通常Y軸）")]
    public RotationAxis visualRotationAxis = RotationAxis.Y;

    [Tooltip("左向き（弾の方向が-x）のときの角度（度）。EnemyMovement と同じ既定")]
    public float leftAngle = 50f;

    [Tooltip("右向き（弾の方向が+x）のときの角度（度）。EnemyMovement と同じ既定")]
    public float rightAngle = -50f;

    [Tooltip("角度を変える速さ（度/秒）。0以下なら即時に切り替え")]
    public float visualRotationSpeed = 360f;

    [Header("銃のピッチ（照準の上下に合わせてX軸で傾ける／3Dモデル用）")]
    [Tooltip("照準方向の上下に合わせてローカルX軸で傾ける銃のTransform（Sh_Gun_03 など）。" +
             "左右の振り向きは visualTransform（Y軸）が担当し、こちらは上下だけを受け持つ。未指定なら何もしない")]
    public Transform gunPitchTransform;

    [Tooltip("銃モデルの基準向きの補正角（度）。銃身が水平を向く姿勢が0になるように調整する")]
    public float gunPitchOffset = 0f;

    [Tooltip("ピッチの回転方向が逆になる場合はオンにする")]
    public bool invertGunPitch = false;

    [Tooltip("ピッチの可動範囲（度）")]
    public float minGunPitch = -80f;
    public float maxGunPitch = 80f;

    [Tooltip("ピッチを変える速さ（度/秒）。0以下なら即時")]
    public float gunPitchSpeed = 360f;

    [Header("体のピッチ（照準の上下に合わせて体全体もX軸で傾ける）")]
    [Tooltip("visualTransform（rot）を照準の上下に合わせてX軸でも傾けるか")]
    public bool tiltBodyToAim = true;

    [Tooltip("仰角のうち体が担う割合（0〜1）。残りは銃（gunPitchTransform）が担う。" +
             "1=体だけで狙う／0.5=体と銃で半分ずつ／0=銃だけ")]
    [Range(0f, 1f)]
    public float bodyPitchWeight = 0.5f;

    [Tooltip("体のピッチの回転方向が逆になる場合はオンにする")]
    public bool invertBodyPitch = false;

    [Tooltip("体のピッチの可動範囲（度）。傾けすぎると不自然になるので狭めに")]
    public float minBodyPitch = -45f;
    public float maxBodyPitch = 45f;

    public enum RotationAxis { X, Y, Z }
    private float currentVisualAngle;     // 左右の角度間を補間する現在角
    private bool hasVisualAngleInit;      // 初期角を設定済みか

    private float currentGunPitch;        // ピッチの補間用現在角
    private bool hasGunPitchInit;
    private Quaternion gunPitchRestLocalRotation = Quaternion.identity; // 銃の初期ローカル回転（階層の姿勢を保つ）

    private float currentBodyPitch;       // 体のピッチの補間用現在角
    private bool hasBodyPitchInit;

    private enum Phase { Idle, Aim, Lock, Fire, Cooldown }
    private Phase phase = Phase.Idle;
    private float timer;

    private Transform player;
    private EnemyKnockback knockback;

    private Vector2 lockedDir = Vector2.left; // ロック時に固定する発射方向

    // 銃口の初期配置（敵中心からのオフセット）。照準方向に合わせてこれをオービットさせる
    private Vector2 pivotRestOffset;
    private float pivotRestAngle;
    private bool hasPivotRest;

    // 可視化用（実行時生成）。OnDestroy で破棄
    private LineRenderer line;
    private Material lineMaterial;

    void Awake()
    {
        knockback = GetComponent<EnemyKnockback>();
        CreateBeamVisual();
    }

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null) player = p.transform;

        // 銃の初期ローカル回転を記録（ピッチはこの姿勢に対する差分として掛ける）
        if (gunPitchTransform != null)
        {
            gunPitchRestLocalRotation = gunPitchTransform.localRotation;
        }

        // 銃口の初期配置を記録（この位置を基準に照準方向へオービットさせる）
        if (aimPivot != null)
        {
            pivotRestOffset = (Vector2)(aimPivot.position - transform.position);
            if (pivotRestOffset.sqrMagnitude > 0.0001f)
            {
                pivotRestAngle = Mathf.Atan2(pivotRestOffset.y, pivotRestOffset.x);
                hasPivotRest = true;
            }
        }
    }

    void Update()
    {
        // 銃口とモデルは常に現在の照準方向へ向ける（照準中は lockedDir がプレイヤーを追う）
        UpdateAimPivot();
        UpdateModelFacing();
        UpdateGunPitch();

        // 吹き飛び中／死亡中は攻撃を中断
        if (knockback != null && (knockback.IsActive || knockback.IsDying))
        {
            if (phase != Phase.Idle && phase != Phase.Cooldown)
            {
                BeginCooldown(); // 中断時もレーザーを止める（BeginCooldown内でStopLaserVisual）
            }
            HideBeam();
            return;
        }

        switch (phase)
        {
            case Phase.Idle:
                if (CanSeePlayer()) BeginAim();
                HideBeam();
                break;

            case Phase.Aim:
                // プレイヤーが見えている間は射線を追従させる（見失っても最後の方向を保持）
                if (CanSeePlayer()) lockedDir = AimDirectionToPlayer();
                DrawBeam(lockedDir, aimColor, sightWidth);
                Countdown(BeginLock);
                break;

            case Phase.Lock:
                // 射線を固定したまま最終警告
                DrawBeam(lockedDir, lockColor, sightWidth);
                Countdown(BeginFire);
                break;

            case Phase.Fire:
                // レーザー判定＋表示。発射中は毎フレーム当たり判定（無敵は PlayerHealth 側）
                // パーティクルを出しているなら線は隠す（二重に見えるため）
                if (laserVisual != null && hideLineWhileLaserFX) HideBeam();
                else DrawBeam(lockedDir, fireColor, beamWidth);
                ApplyBeamDamage();
                Countdown(BeginCooldown);
                break;

            case Phase.Cooldown:
                HideBeam();
                Countdown(() => phase = Phase.Idle);
                break;
        }
    }

    // タイマーを進め、0になったら次の処理を呼ぶ
    private void Countdown(System.Action onElapsed)
    {
        timer -= Time.deltaTime;
        if (timer <= 0f) onElapsed();
    }

    private void BeginAim()
    {
        phase = Phase.Aim;
        timer = Mathf.Max(0f, aimTime);
        lockedDir = AimDirectionToPlayer();
    }

    private void BeginLock()
    {
        phase = Phase.Lock;
        timer = Mathf.Max(0f, lockTime);
    }

    private void BeginFire()
    {
        phase = Phase.Fire;
        timer = Mathf.Max(0f, fireDuration);
        PlayLaserVisual();  // 発射の瞬間に1回だけ再生（毎フレーム呼ぶと再生し直しになる）
        ApplyBeamDamage(); // fireDuration=0 でも最低1回は判定
    }

    // レーザーパーティクルを射線に合わせて配置・再生する。
    // 当たり判定は従来どおり ApplyBeamDamage の CircleCast が担当＝これは見た目だけ。
    // （Lazer プレハブに付く LaserParticleTrigger は使わない。使うとダメージが二重になる）
    private void PlayLaserVisual()
    {
        if (laserVisual == null) return;

        Vector2 origin = FireOrigin();
        float len = ComputeBeamLength(origin, lockedDir);

        // レーザーも銃口と同じ z 平面に置く（DrawBeam と同じ理由＝視差ズレ防止）
        float z = firePoint != null ? firePoint.position.z : transform.position.z;

        Transform t = laserVisual.transform;
        t.position = new Vector3(origin.x, origin.y, z);

        // Lazer プレハブの Shape は Cone＝粒はローカル +Z へ飛ぶ。
        // そのため Z軸回転ではなく、+Z が射線方向を向くように LookRotation で合わせる。
        t.rotation = Quaternion.LookRotation(new Vector3(lockedDir.x, lockedDir.y, 0f), Vector3.up);

        laserVisual.Configure(len, laserVisualWidthMultiplier);
    }

    private void BeginCooldown()
    {
        phase = Phase.Cooldown;
        timer = Mathf.Max(0f, cooldown);
        // Fire を抜けたらレーザーパーティクルを止める（Lazer は looping なので放置すると出っぱなしになる）。
        // 正常終了（fireDuration 経過）・中断のどちらもここを通る。
        StopLaserVisual();
    }

    // ─── 索敵・方向 ───────────────────────────────

    private Vector2 FireOrigin()
    {
        return firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
    }

    private Vector2 AimDirectionToPlayer()
    {
        if (player == null) return lockedDir;
        Vector2 d = (Vector2)player.position - FireOrigin();
        return d.sqrMagnitude > 0.0001f ? d.normalized : lockedDir;
    }

    // 射程内かつ射線が壁に遮られていないか
    private bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector2 origin = FireOrigin();
        Vector2 toPlayer = (Vector2)player.position - origin;
        float dist = toPlayer.magnitude;
        if (dist > detectionRange) return false;

        // 壁に当たったら射線は通っていない
        RaycastHit2D wall = Physics2D.Raycast(origin, toPlayer.normalized, dist, obstacleLayer);
        return wall.collider == null;
    }

    // 銃口オブジェクトを照準方向に合わせて回転・配置する。
    // 位置は「最初に置いた配置」を基準に、照準方向へ敵の周りをオービット（回り込み）させる。
    // これにより反対方向を狙うと銃口も反対側へ移動する。
    private void UpdateAimPivot()
    {
        if (aimPivot == null) return;

        float aimAngleRad = Mathf.Atan2(lockedDir.y, lockedDir.x);

        // 向き：照準方向に絵の基準向き補正を加える
        aimPivot.rotation = Quaternion.Euler(0f, 0f, aimAngleRad * Mathf.Rad2Deg + aimAngleOffset);

        // 位置
        Vector2 offset;
        if (aimPivotDistance != 0f)
        {
            // distance>0：敵中心から照準方向へその距離だけ離して配置（純粋な放射状）
            offset = lockedDir * aimPivotDistance;
        }
        else if (hasPivotRest)
        {
            // distance=0：最初に置いたオフセットを、照準方向との差分だけ回転させてオービット
            float delta = aimAngleRad - pivotRestAngle;
            offset = Rotate2D(pivotRestOffset, delta);
        }
        else
        {
            return; // 基準配置が無く距離も0なら回転のみ（位置はそのまま）
        }

        Vector3 pos = transform.position + (Vector3)offset;
        pos.z = transform.position.z; // Zは敵と同じ平面を維持
        aimPivot.position = pos;
    }

    // モデル（Rotation層）を弾を打つ方向(lockedDir)の左右に合わせて向ける。
    // 砲台のように射線へ正確に向ける（Z軸）のではなく、左右だけ（Y軸）に振り向く＝3Dモデルが寝ない。
    // ルートは回さず子の visualTransform だけ回すことで、ルートのコライダーをつぶさない。
    private void UpdateModelFacing()
    {
        if (visualTransform == null) return;

        float target = TargetVisualAngle();

        // 初回は即座に目標角へ合わせる（最初の1フレームから正しい向き）
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
            currentVisualAngle = target; // 0以下なら即時切り替え
        }

        // 体のピッチ（照準の上下）。仰角のうち bodyPitchWeight 分を体が担う
        float bodyPitch = 0f;
        if (tiltBodyToAim)
        {
            float elevation = AimElevationDeg() * bodyPitchWeight;
            float targetPitch = Mathf.Clamp(invertBodyPitch ? elevation : -elevation, minBodyPitch, maxBodyPitch);

            if (!hasBodyPitchInit)
            {
                currentBodyPitch = targetPitch;
                hasBodyPitchInit = true;
            }
            else if (visualRotationSpeed > 0f)
            {
                currentBodyPitch = Mathf.MoveTowards(currentBodyPitch, targetPitch, visualRotationSpeed * Time.deltaTime);
            }
            else
            {
                currentBodyPitch = targetPitch;
            }
            bodyPitch = currentBodyPitch;
        }

        switch (visualRotationAxis)
        {
            case RotationAxis.X:
                visualTransform.localRotation = Quaternion.Euler(currentVisualAngle, 0f, 0f);
                break;
            case RotationAxis.Y:
                // 先にY軸で左右を向き、その後の「向いた先」に対してX軸で上下に傾ける
                // （順序が逆だと、左右を向いたとき傾きが横倒れになる）
                visualTransform.localRotation =
                    Quaternion.Euler(0f, currentVisualAngle, 0f) * Quaternion.Euler(bodyPitch, 0f, 0f);
                break;
            default:
                visualTransform.localRotation = Quaternion.Euler(0f, 0f, currentVisualAngle);
                break;
        }
    }

    // 照準方向 lockedDir の仰角（水平からの上下角・度）。左右どちら向きでも上下量は同じなので x は絶対値
    private float AimElevationDeg()
    {
        return Mathf.Atan2(lockedDir.y, Mathf.Abs(lockedDir.x)) * Mathf.Rad2Deg;
    }

    // 銃（gunPitchTransform）を照準方向の上下に合わせてローカルX軸で傾ける。
    // 体（tiltBodyToAim）が仰角の bodyPitchWeight 分を担うので、銃は残りの分だけ傾ける。
    // 初期ローカル回転 gunPitchRestLocalRotation に差分を掛けるので、階層内での元の姿勢は崩れない。
    private void UpdateGunPitch()
    {
        if (gunPitchTransform == null) return;

        float remainder = tiltBodyToAim ? (1f - bodyPitchWeight) : 1f;
        float elevation = AimElevationDeg() * remainder;

        // UnityのX軸回転は「＋で下を向く」ため既定は反転。モデルによって逆なら invertGunPitch で切替
        float target = (invertGunPitch ? elevation : -elevation) + gunPitchOffset;
        target = Mathf.Clamp(target, minGunPitch, maxGunPitch);

        if (!hasGunPitchInit)
        {
            currentGunPitch = target;
            hasGunPitchInit = true;
        }
        else if (gunPitchSpeed > 0f)
        {
            currentGunPitch = Mathf.MoveTowards(currentGunPitch, target, gunPitchSpeed * Time.deltaTime);
        }
        else
        {
            currentGunPitch = target;
        }

        gunPitchTransform.localRotation = gunPitchRestLocalRotation * Quaternion.Euler(currentGunPitch, 0f, 0f);
    }

    // 弾を打つ方向(lockedDir)の左右成分から目標角を決める。真上・真下（x≈0）のときは現在角を維持。
    private float TargetVisualAngle()
    {
        if (lockedDir.x < 0f) return leftAngle;   // 左を狙う
        if (lockedDir.x > 0f) return rightAngle;  // 右を狙う
        return currentVisualAngle;
    }

    // 2DベクトルをZ軸回りに回す（ラジアン）
    private static Vector2 Rotate2D(Vector2 v, float radians)
    {
        float c = Mathf.Cos(radians);
        float s = Mathf.Sin(radians);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    // ─── レーザー判定 ─────────────────────────────

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

        // 太さを考慮してプレイヤーを拾う（細いレイだと避けにくさが理不尽になるため CircleCast）
        float radius = Mathf.Max(0.01f, beamWidth * 0.5f);
        RaycastHit2D hit = Physics2D.CircleCast(origin, radius, lockedDir, len, targetLayers);
        if (hit.collider != null)
        {
            PlayerHealth hp = hit.collider.GetComponentInParent<PlayerHealth>();
            if (hp != null) hp.TakeDamage(beamDamage);
        }
    }

    // ─── 可視化（LineRenderer 実行時生成） ──────────

    private void CreateBeamVisual()
    {
        GameObject go = new GameObject("SniperBeam");
        go.transform.SetParent(transform, false);

        line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.numCapVertices = 2;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = 10; // 敵・プレイヤーより前に描く

        if (beamMaterial != null)
        {
            // 割り当てがあれば共有マテリアルをそのまま使う（複製しないので破棄も不要＝OnDestroyでlineMaterialはnullのまま）
            line.sharedMaterial = beamMaterial;
        }
        else
        {
            // ビルトインRP前提。Sprites/Default は頂点カラー＆半透明ブレンド対応
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            lineMaterial.renderQueue = 3000; // Transparent
            line.material = lineMaterial;
        }

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

        // モデルが z≠0 にあるため、レーザーも firePoint と同じ z 平面に描く（銃口とレーザーの視差ズレ防止）
        float z = firePoint != null ? firePoint.position.z : transform.position.z;
        line.SetPosition(0, new Vector3(origin.x, origin.y, z));
        Vector2 end = origin + dir * len;
        line.SetPosition(1, new Vector3(end.x, end.y, z));
    }

    private void HideBeam()
    {
        if (line != null) line.enabled = false;
    }

    // 再生中のレーザーパーティクルを止めて消す（中断用）
    private void StopLaserVisual()
    {
        if (laserVisual == null) return;
        var ps = laserVisual.GetComponent<ParticleSystem>();
        if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void OnDestroy()
    {
        if (lineMaterial != null) Destroy(lineMaterial);
    }

    // シーンビューで索敵範囲と現在の射線方向を可視化
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Vector3 origin = Application.isPlaying ? (Vector3)FireOrigin() : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin + (Vector3)(lockedDir * 2f));
    }
}