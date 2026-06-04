using System.Runtime.CompilerServices;
using UnityEngine;

public class KeyGimmick : MonoBehaviour
{
    [Header("プレイヤーの速度に対する倍率")]
    [SerializeField] private float keySpeedRate = 1.2f;      // プレイヤーの速度をカギの勢いにどれくらい反映するか
    [SerializeField] private float reflexRate = 1.2f;        // プレイヤーの跳ね返り速度への反映率
    [SerializeField] private float minImpact = 3.0f;         // 最低限の勢い

    [Header("連続衝突を防ぐためのクールタイム")]
    [SerializeField] private float hitCooldown = 0.2f;       // 次に当たれるようになるまでの時間（秒）

    private Rigidbody2D myRb;
    private float cooldownTimer = 0f;

    private void Start()
    {
        // カギ自身のRigidbody2Dを取得
        myRb = GetComponent<Rigidbody2D>();

        if (myRb == null)
        {
            Debug.LogError("カギに Rigidbody2D がついていません！アタッチしてください。");
        }
    }

    private void Update()
    {
        // クールタイムのカウントを進める
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // プレイヤーかつ、クールタイムが終わっている場合
        if (other.CompareTag("Player") && cooldownTimer <= 0f)
        {
            Rigidbody2D playerRb = other.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                // 次の衝突まで猶予時間を設定（課題1の解決）
                cooldownTimer = hitCooldown;

                // 1. プレイヤーが当たった瞬間のスピードを計算
                float playerSpeed = playerRb.linearVelocity.magnitude;
                if (playerSpeed < minImpact)
                {
                    playerSpeed = minImpact;
                }

                // 2. ぶつかった方向のベクトルを計算（課題3の解決）
                // プレイヤーからカギへ向かう方向 ＝ カギがすっ飛ぶ方向
                Vector2 pushDirection = (transform.position - other.transform.position).normalized;
                // その真逆 ＝ プレイヤーが跳ね返る方向
                Vector2 reflexDirection = -pushDirection;

                // 3. プレイヤーを反射させる
                playerRb.linearVelocity = Vector2.zero;
                playerRb.linearVelocity = reflexDirection * (playerSpeed * reflexRate);

                // 4. カギ自身に物理的な勢い（速度）を与える（課題2・4の解決）
                myRb.linearVelocity = Vector2.zero; // 前の速度をリセット
                myRb.linearVelocity = pushDirection * (playerSpeed * keySpeedRate);
            }
        }
    }
}