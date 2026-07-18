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

    [Header("エフェクト（RastBossの衝撃波と同じ見た目を流用）")]
    [Tooltip("進行しながら一定間隔で置いていく爆発エフェクト（P_Ex 等のワンショット自己破棄型を想定）。未指定なら出さない")]
    public GameObject effectPrefab;
    [Tooltip("エフェクトを置く間隔（秒）。移動する波の軌跡になる。0以下なら生成時の1発だけ")]
    public float effectSpawnInterval = 0.1f;
    [Tooltip("置いたエフェクトのローカルスケール倍率（P_Ex基準。RastBoss衝撃波は0.25）")]
    public float effectScaleMultiplier = 0.25f;

    private Vector2 dir = Vector2.right;
    private LayerMask wallLayers;
    private float timer;
    private float effectTimer;

    /// <summary>生成直後にコントローラから呼ばれる</summary>
    public void Init(Vector2 direction, LayerMask walls)
    {
        dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        wallLayers = walls;
    }

    void Start()
    {
        // 1発目を足元へ（Init 後に走るので方向・位置は確定済み）
        SpawnEffect();
    }

    void Update()
    {
        transform.position += (Vector3)(dir * speed * Time.deltaTime);

        // 進行に沿って一定間隔でエフェクトを置いていく＝爆発の軌跡（移動する波を可視化）
        if (effectPrefab != null && effectSpawnInterval > 0f)
        {
            effectTimer += Time.deltaTime;
            while (effectTimer >= effectSpawnInterval)
            {
                effectTimer -= effectSpawnInterval;
                SpawnEffect();
            }
        }

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

    // 現在位置にワンショットのエフェクトを生成。親子付けしない（波が壁で消えても再生中の爆発は残す）。
    // P_Ex は再生後に自己 Destroy されるので寿命管理は不要。
    private void SpawnEffect()
    {
        if (effectPrefab == null) return;
        GameObject fx = Instantiate(effectPrefab, transform.position, Quaternion.identity);
        fx.transform.localScale = Vector3.one * effectScaleMultiplier;
    }
}
