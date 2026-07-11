using UnityEngine;

/// <summary>
/// 突進×盾ボス（Boss3）の物理盾。ボスの子オブジェクトに置く（ソリッドの Collider2D 必須）。
///
/// 役割:
///   - 正面ガード: 盾がボス本体の正面を物理的に覆う。バーストは盾に当たると壁と同じように反射し、
///     本体（BossChargerHealth）にはダメージが入らない。
///   - カウンター検知: バースト中のプレイヤーが盾に当たったら controller.OnShieldBurstHit() を呼ぶ（技⑥）。
///   - 投擲（技⑤）: ブーメラン式に「往路→復路」で飛ぶ。投擲中はボス正面が無防備になる。
///     投擲中の接触ダメージはこのオブジェクトの DamageSource（PlayerHealth が読む）。
///
/// 向きの追従は SetFacing（コントローラから毎フレーム）で localPosition.x の符号を切り替える。
/// 親の回転で追従させると2Dコライダーが潰れるため使わない（EnemyMovement の注意書きと同じ理由）。
/// </summary>
public class BossChargerShield : MonoBehaviour
{
    [Tooltip("親のボスコントローラ（未設定なら親から自動取得）")]
    public BossChargerController controller;

    [Header("投擲（技⑤）")]
    [Tooltip("投げる距離（壁があれば手前で折り返す）")]
    public float throwDistance = 10f;
    [Tooltip("往路の速度")]
    public float throwSpeed = 14f;
    [Tooltip("復路（戻り）の速度")]
    public float returnSpeed = 10f;
    [Tooltip("投擲中の回転速度（度/秒・見た目）")]
    public float spinSpeed = 720f;

    public enum ShieldMode { Held, ThrowOut, ThrowReturn }
    public ShieldMode Mode { get; private set; } = ShieldMode.Held;
    public bool IsHeld => Mode == ShieldMode.Held;

    private Collider2D col;
    private Vector3 heldLocalPosition;   // 構え位置（右向き時。左向きは x 反転）
    private Quaternion heldLocalRotation;
    private Vector2 throwDir;
    private Vector2 throwStart;
    private float throwTargetDistance;

    void Awake()
    {
        if (controller == null) controller = GetComponentInParent<BossChargerController>();
        col = GetComponent<Collider2D>();

        heldLocalPosition = transform.localPosition;
        heldLocalRotation = transform.localRotation;

        // 接触ダメージ（DamageSource）は常時ON。突進中に盾から当たってもダメージが出るようにする。
        // バースト中のプレイヤーは PlayerHealth 側の既存チェック（Enemyタグ×Burst）で免除される。
    }

    void Update()
    {
        switch (Mode)
        {
            case ShieldMode.ThrowOut:
                UpdateThrow(throwSpeed);
                if (Vector2.Distance(throwStart, transform.position) >= throwTargetDistance)
                    Mode = ShieldMode.ThrowReturn;
                break;

            case ShieldMode.ThrowReturn:
                // ボス本体の構え位置へ帰ってくる（ボスが動いていても追従）
                Vector3 home = HeldWorldPosition();
                transform.position = Vector3.MoveTowards(transform.position, home, returnSpeed * Time.deltaTime);
                Spin();
                if ((transform.position - home).sqrMagnitude < 0.04f) CatchShield();
                break;
        }
    }

    // ─── 正面ガード・カウンター検知 ───

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (controller == null) return;
        if (!collision.gameObject.CompareTag(controller.playerTag)) return;

        // バースト中に盾へ突っ込んできた → 反射はプレイヤー側（壁と同じ扱い）、こちらはカウンターを起動
        PlayerController p = collision.gameObject.GetComponent<PlayerController>();
        if (p != null && p.CurrentState == p.StateBurst && IsHeld)
        {
            controller.OnShieldBurstHit();
        }
    }

    /// <summary>向き（-1=左 / +1=右）に合わせて構え位置を反転する。構え中のみ有効</summary>
    public void SetFacing(int facing)
    {
        if (Mode != ShieldMode.Held) return;
        Vector3 pos = heldLocalPosition;
        pos.x = Mathf.Abs(heldLocalPosition.x) * facing;
        transform.localPosition = pos;
    }

    /// <summary>スタン中などにガードを無効化する（コライダーごとOFF＝正面が開く）</summary>
    public void SetGuardEnabled(bool on)
    {
        if (col != null) col.enabled = on;
        // 見た目も消したい場合は Renderer も同期させる（半透明化などは後で演出側で調整）
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            if (r != null) r.enabled = on;
    }

    // ─── 投擲（技⑤） ───

    /// <summary>盾を投げる。往路→復路で自動で戻り、戻ったら IsHeld に復帰する</summary>
    public void Throw(Vector2 dir)
    {
        if (Mode != ShieldMode.Held) return;

        throwDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right * (controller != null ? controller.Facing : 1);
        throwStart = transform.position;

        // 壁があれば手前で折り返す距離に丸める
        throwTargetDistance = throwDistance;
        if (controller != null)
        {
            RaycastHit2D hit = Physics2D.Raycast(throwStart, throwDir, throwDistance, controller.wallLayers);
            if (hit.collider != null) throwTargetDistance = Mathf.Max(0.5f, hit.distance - 0.5f);
        }

        Mode = ShieldMode.ThrowOut;
    }

    private void UpdateThrow(float speed)
    {
        transform.position += (Vector3)(throwDir * speed * Time.deltaTime);
        Spin();
    }

    private void Spin()
    {
        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
    }

    private Vector3 HeldWorldPosition()
    {
        if (controller == null) return transform.position;
        Vector3 local = heldLocalPosition;
        local.x = Mathf.Abs(heldLocalPosition.x) * controller.Facing;
        return controller.transform.TransformPoint(local);
    }

    private void CatchShield()
    {
        Mode = ShieldMode.Held;
        transform.localRotation = heldLocalRotation;
        SetFacing(controller != null ? controller.Facing : 1);
    }
}
