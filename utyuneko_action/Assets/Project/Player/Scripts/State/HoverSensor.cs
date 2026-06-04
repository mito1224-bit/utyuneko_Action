using UnityEngine;

public class HoverSensor : MonoBehaviour
{
    private PlayerController p;
    private Collider2D sensorCollider;

    [Header("物理バネ（クッション）設定")]
    [SerializeField] private float baseHoverForce = 55f;   // 浮かせるバネの強さ（少し強めにすると目標の波に綺麗に追従します）
    [SerializeField] private float hoverDamping = 7f;     // ガタガタ震えるのを抑えるブレーキ（ダンパー）

    [Header("目標高度の sin 波揺らぎ設定")]
    [SerializeField] private float targetOverlap = 0.15f; // 基準となる浮遊高度（センサーのめり込み目標値）
    [SerializeField] private float bobbingAmount = 0.05f;  // どれくらい上下にプカプカさせるか
    [SerializeField] private float bobbingSpeed = 4.0f;   // プカプカするスピード

    private bool isGrounded;
    private float overlapDistance;

    void Start()
    {
        p = GetComponentInParent<PlayerController>();
        sensorCollider = GetComponent<Collider2D>();
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & p.GetGroundLayerMask()) == 0) return;
        isGrounded = true;

        ColliderDistance2D dist = sensorCollider.Distance(other);
        if (dist.isValid)
        {
            overlapDistance = Mathf.Abs(dist.distance);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & p.GetGroundLayerMask()) == 0) return;
        isGrounded = false;
        overlapDistance = 0f;
    }

    void FixedUpdate()
    {
        // バースト中や空中にいる時は物理（重力）に任せる
        if (!sensorCollider.enabled || !isGrounded) return;

        // ① 【ここがポイント！】バネが目指す「目標のめり込み量」自体をサイン波でなめらかに変化させる
        // チャージ中の空中スロー時にも同じ速度でフワフワさせたい場合は Time.fixedTime を Time.unscaledTime に変更してください
        float currentTargetOverlap = targetOverlap + Mathf.Sin(Time.fixedTime * bobbingSpeed) * bobbingAmount;

        // ② 目標の高さに対して、今どれくらい余分に地面に近づいているか（ズレ）を計算
        float heightError = overlapDistance - currentTargetOverlap;

        // ③ バネの力を計算（目標より沈み込むほど強く押し返し、浮き上がると滑らかに力を弱める）
        float springForce = heightError * baseHoverForce;

        // ④ ダンパーの力（上下速度に応じたブレーキ。これでガタつきを消し去る）
        float damperForce = p.rb2D.linearVelocity.y * hoverDamping;

        // 最終的なホバー力（上向きの押し返し力）
        float totalHoverForce = springForce - damperForce;

        // 地面から離れすぎて力がマイナス（下向き）にならないように制御して AddForce
        if (totalHoverForce > 0f)
        {
            p.rb2D.AddForce(Vector2.up * totalHoverForce, ForceMode2D.Force);
        }
    }

    public bool IsGrounded() => isGrounded;
}