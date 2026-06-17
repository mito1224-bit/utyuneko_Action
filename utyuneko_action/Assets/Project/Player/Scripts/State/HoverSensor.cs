using UnityEngine;

public class HoverSensor : MonoBehaviour
{
    private Rigidbody2D rb;
    private Collider2D sensorCollider;

    [Header("地面と判定するレイヤー")]
    [SerializeField] private LayerMask groundLayer;

    [Header("物理バネ（クッション）設定")]
    [SerializeField] private float baseHoverForce = 55f;
    [SerializeField] private float hoverDamping = 7f;

    [Header("目標高度の sin 波揺らぎ設定")]
    [SerializeField] private float targetOverlap = 0.15f;
    [SerializeField] private float bobbingAmount = 0.05f;
    [SerializeField] private float bobbingSpeed = 4.0f;

    private bool isGrounded;
    private float overlapDistance;

    void Start()
    {
        // 親オブジェクトから物理ボディ（Rigidbody2D）を自動取得
        rb = GetComponentInParent<Rigidbody2D>();
        sensorCollider = GetComponent<Collider2D>();

        if (rb == null)
        {
            Debug.LogError($"{gameObject.name} の親オブジェクトに Rigidbody2D が見つかりません！");
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & groundLayer) == 0) return;
        isGrounded = true;

        ColliderDistance2D dist = sensorCollider.Distance(other);
        if (dist.isValid)
        {
            overlapDistance = Mathf.Abs(dist.distance);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & groundLayer) == 0) return;
        isGrounded = false;
        overlapDistance = 0f;
    }

    void FixedUpdate()
    {
        // rb の有無チェックと、コライダーが無効化されている時は処理をスキップ
        if (rb == null || !sensorCollider.enabled || !isGrounded) return;

        // バネが目指す「目標のめり込み量」をサイン波でなめらかに変化させる
        float currentTargetOverlap = targetOverlap + Mathf.Sin(Time.fixedTime * bobbingSpeed) * bobbingAmount; //

        // 目標の高さに対して、今どれくらい余分に地面に近づいているか（ズレ）を計算
        float heightError = overlapDistance - currentTargetOverlap; //

        // バネの力を計算
        float springForce = heightError * baseHoverForce; //

        // ダンパーの力
        float damperForce = rb.linearVelocity.y * hoverDamping;

        // 最終的なホバー力
        float totalHoverForce = springForce - damperForce; //

        // 地面から離れすぎて力がマイナスにならないように制御して AddForce
        if (totalHoverForce > 0f) //
        {
            rb.AddForce(Vector2.up * totalHoverForce, ForceMode2D.Force);
        }
    }

    public bool IsGrounded() => isGrounded; //

    // 外部（PlayerControllerなど）から地面レイヤーをセットし直すための窓口
    public void SetGroundLayer(LayerMask layer)
    {
        groundLayer = layer;
    }
}