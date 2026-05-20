using UnityEngine;

public class PlayerState_Burst : IPlayerState
{
    private PlayerController p;

    private float burstDuration = 5.0f;
    private float burstTimer;
    private Vector3 burstDirection;
    private float currentSpeed;
    private Vector3 lastVelocity; // ★以前のコードにあった「激突直前の速度」を記憶する箱

    public void Enter(PlayerController player)
    {
        p = player;
        Debug.Log("ステート変更：完璧な物理バースト開始！");

        p.OnCollisionEnterEvent += OnCollisionEnter;

        // エイム方向の取得
        if (p.aimPivot != null) burstDirection = p.aimPivot.up.normalized;
        else burstDirection = Vector3.right;

        burstTimer = burstDuration;

        // ★【最重要】Unityの標準重力をONにする！
        // これにより、手動で重力を計算しなくても、Unityが自然で完璧な放物線を描いてくれます
        p.rb.useGravity = true;

        // 初速をガツンと与える
        currentSpeed = p.burstSpeed;
        p.rb.linearVelocity = burstDirection * currentSpeed;
    }

    public void UpdateState()
    {
        burstTimer -= Time.deltaTime;

        // 現在の速度の絶対値（ magnitude ）を計算
        float speed = p.rb.linearVelocity.magnitude;

        // スピードが指定（moveSpeed）以下になり、かつ地面にタッチしていたら終了
        bool isSpeedTooLowAndGrounded = (speed <= p.moveSpeed) && p.IsGrounded();
        bool isTimeOut = burstTimer <= 0f;
        bool isJumpPressed = p.inputActions.Player.Jump.triggered;

        if (isTimeOut || isSpeedTooLowAndGrounded || isJumpPressed)
        {
            p.TransitionToState(p.StateNormal);
        }
    }

    public void FixedUpdateState()
    {
        // ★【大修正】以前のコードの知恵をそのまま採用！
        // 激突した瞬間に、衝突前の本当の速度で計算できるように、毎フレームの速度を記録するだけにする。
        // リジッドボディの速度を無理に上書きしないので、Unityの物理と100%シンクロします。
        lastVelocity = p.rb.linearVelocity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (((1 << collision.gameObject.layer) & p.GetGroundLayerMask()) != 0)
        {
            // 激突直前のリアルな速度ベクトルを取り出す
            Vector3 incomingVector = lastVelocity;
            if (incomingVector.magnitude < 1f) return;

            // 壁・床の向き（法線）を取得
            Vector3 wallNormal = collision.contacts[0].normal;

            // ★激突直前の進行方向から、完璧な反射角を計算！
            Vector3 reflectedDirection = Vector3.Reflect(incomingVector.normalized, wallNormal);
            reflectedDirection.z = 0f;

            // 反射方向の更新
            burstDirection = reflectedDirection.normalized;

            // スピードの減衰
            currentSpeed = incomingVector.magnitude * p.reflectEfficiency;

            // 新しい反射速度を Rigidbody に直接ガツンと代入！
            p.rb.linearVelocity = burstDirection * currentSpeed;

            burstTimer = burstDuration;

            Debug.Log($"床・壁で完璧な反射！ 速度: {currentSpeed}");
        }
    }

    public void Exit()
    {
        Debug.Log("バースト終了");
        p.OnCollisionEnterEvent -= OnCollisionEnter;

        // 終わる時は少し減速させて通常状態へバトンタッチ
        p.rb.linearVelocity = p.rb.linearVelocity * 0.2f;
    }
}