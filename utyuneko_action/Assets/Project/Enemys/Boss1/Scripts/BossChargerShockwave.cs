using UnityEngine;

/// <summary>
/// 突進×盾ボス（Boss3）の壁ヒット衝撃波（技④）。
/// ボスが壁に自滅した瞬間に足元から左右へ地面を走る。
/// プレイヤーへのダメージはこのオブジェクトの DamageSource（トリガーコライダー）を PlayerHealth が読む。
/// ジャンプで飛び越えるのが正解の遊び。壁に当たるか寿命が切れたら消える。
/// </summary>
public class BossChargerShockwave : MonoBehaviour
{
    [Tooltip("進む速度")]
    public float speed = 8f;
    [Tooltip("寿命（秒）。壁に当たらなくてもこの時間で消える")]
    public float lifetime = 3f;
    [Tooltip("壁を検知するレイの長さ（進行方向）")]
    public float wallCheckDistance = 0.3f;

    private Vector2 dir = Vector2.right;
    private LayerMask wallLayers;
    private float timer;

    /// <summary>生成直後にコントローラから呼ばれる</summary>
    public void Init(Vector2 direction, LayerMask walls)
    {
        dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        wallLayers = walls;
    }

    void Update()
    {
        transform.position += (Vector3)(dir * speed * Time.deltaTime);

        // 壁に当たったら消滅
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, wallCheckDistance, wallLayers);
        if (hit.collider != null)
        {
            Destroy(gameObject);
            return;
        }

        timer += Time.deltaTime;
        if (timer >= lifetime) Destroy(gameObject);
    }
}
