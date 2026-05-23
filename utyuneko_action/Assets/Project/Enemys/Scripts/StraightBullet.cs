using UnityEngine;

public class StraightBullet : MonoBehaviour
{
    [Header("弾の設定")]
    public float speed = 5.0f;       // 弾の飛ぶ速度
    public float lifeTime = 3.0f;    // 弾が消滅するまでの時間（秒）

    private Vector3 flyDirection = Vector3.left; // 弾が飛ぶ方向（デフォルトは左）

    // 【修正箇所】エラーを消し、飛ぶ方向をセットする処理に変更
    public void SetDirection(Vector3 direction)
    {
        flyDirection = direction.normalized;
    }

    void Start()
    {
        // 生成されてから lifeTime 秒後に自身を破壊する
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        // 毎フレーム、自身の飛行方向へ移動する
        transform.Translate(flyDirection * speed * Time.deltaTime, Space.World);
    }
}
