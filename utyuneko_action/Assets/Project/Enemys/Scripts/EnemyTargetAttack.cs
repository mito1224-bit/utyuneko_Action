using UnityEngine;

public class EnemyTargetAttack : MonoBehaviour
{
    [Header("射撃設定")]
    public GameObject bulletPrefab;          // 弾のプレハブ
    public Transform firePoint;              // 弾を発射する位置
    public float fireRate = 1.0f;            // 1秒間に何発撃つか
    private float nextFireTime = 0f;

    [Header("索敵（見つける）設定")]
    public string playerTag = "Player";      // プレイヤーのタグ
    public float detectionRange = 10.0f;     // プレイヤーを見つける範囲（半径）
    public LayerMask obstacleLayer;          // 射線を遮る「壁」などのレイヤー
    public float loseTargetTime = 3.0f;      // 見失ってから完全に諦めるまでの時間（秒）

    private Transform player;                // プレイヤーの場所
    private bool isAware = false;            // 現在プレイヤーに気づいているか？
    private float lostSightTimer = 0f;       // 見失っている時間のタイマー

    void Start()
    {
        // 最初にシーン内からプレイヤーを探しておく
        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null)
        {
            player = p.transform;
        }
    }

    void Update()
    {
        if (player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        Vector3 directionToPlayer = (player.position - transform.position).normalized;

        // 【変更点】3D用のPhysics.Raycastに変更
        // Physics.Raycastは「壁に当たったらtrue」を返すので、当たらない（!）＝射線が通っている、と判定します
        bool hasLineOfSight = !Physics.Raycast(transform.position, directionToPlayer, distanceToPlayer, obstacleLayer);

        if (distanceToPlayer <= detectionRange && hasLineOfSight)
        {
            isAware = true;
            lostSightTimer = 0f;
        }
        else
        {
            if (isAware)
            {
                lostSightTimer += Time.deltaTime;
                if (lostSightTimer >= loseTargetTime) isAware = false;
            }
        }

        if (isAware)
        {
            if (Time.time >= nextFireTime)
            {
                Shoot(directionToPlayer);
                nextFireTime = Time.time + (1.0f / fireRate);
            }
        }
    }

    private void Shoot(Vector3 direction)
    {
        if (bulletPrefab != null && firePoint != null)
        {
            // 弾を生成
            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

            // 発射SE（テストシーンに SoundManager が無ければスキップ）
            if (SoundManager.Instance != null) SoundManager.Instance.PlaySE(SeType.EnemyRangeAttack);

            // StraightBulletスクリプトを取得して、プレイヤーの方向をセットする
            StraightBullet straightBullet = bullet.GetComponent<StraightBullet>();
            if (straightBullet != null)
            {
                straightBullet.SetDirection(direction);
            }
        }
    }

    // 開発用の便利機能：シーンビューに索敵範囲の円を描画する
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
