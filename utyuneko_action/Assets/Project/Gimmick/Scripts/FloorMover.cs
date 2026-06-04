using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class FloorMover : MonoBehaviour
{
    [Header("── 移動設定 ───────────────────")]
    public float moveWidthX = 1f;
    public float moveWidthY = 0f;
    public float moveSpeed = 1f;

    private float _phase = 0f;
    private Vector2 _startPosition;
    private Rigidbody2D _rb;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        // 物理設定を床用に最適化
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.useFullKinematicContacts = true;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate; // 動きを滑らかにする
    }

    void Start()
    {
        _startPosition = transform.position;
    }

    // 動く床は FixedUpdate で計算するのが鉄則です
    void FixedUpdate()
    {
        // 1. フェーズを更新
        _phase += moveSpeed * Time.fixedDeltaTime * Mathf.PI * 2f;

        // 2. 次の目標地点を計算
        float wave = Mathf.Sin(_phase);
        Vector2 targetPosition = _startPosition + new Vector2(moveWidthX * wave, moveWidthY * wave);

        // 3. 【重要】「現在の位置」から「目標位置」へ行くための速度を計算
        // これにより、物理エンジンが「この床は今この速度で動いている」と正しく認識します
        Vector2 velocity = (targetPosition - (Vector2)transform.position) / Time.fixedDeltaTime;

        // 4. 速度をセット
        _rb.linearVelocity = velocity;
    }

    public void ResetStartPosition()
    {
        _startPosition = transform.position;
        _phase = 0f;
    }

    public void SetSpeed(float speed)
    {
        moveSpeed = Mathf.Max(0f, speed);
    }
}