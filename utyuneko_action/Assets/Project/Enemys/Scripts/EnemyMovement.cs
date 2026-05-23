using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    [Header("移動設定")]
    public float moveSpeed = 2.0f;               // 移動速度
    public Vector3 moveDirection = Vector3.left; // 現在の移動方向

    [Header("方向転換設定")]
    public float changeInterval = 3.0f;          // 方向を反転するまでの時間（秒）
    private float timer = 0f;                    // 時間を計測するためのタイマー

    void Update()
    {
        Move();
        HandleDirectionChange();
    }

    private void Move()
    {
        // 指定した方向へ速度を掛けて移動
        transform.Translate(moveDirection.normalized * moveSpeed * Time.deltaTime);
    }

    private void HandleDirectionChange()
    {
        // 毎フレームの経過時間をタイマーに加算
        timer += Time.deltaTime;

        // タイマーが設定した間隔（秒）に達したか判定
        if (timer >= changeInterval)
        {
            // 移動方向を反転させる（例: 左なら右へ、上なら下へ）
            moveDirection = -moveDirection;

            // タイマーを0にリセットして再計測を開始
            timer = 0f;
        }
    }
}
