using UnityEngine;

public class EnemyAttack : MonoBehaviour
{

    [Header("射撃設定")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float fireRate = 1.0f;
    private float nextFireTime = 0f;

    void Update()
    {
        CheckAndShoot();
    }

    private void CheckAndShoot()
    {
        if (Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + (1.0f / fireRate);
        }
    }

    private void Shoot()
    {
        if (bulletPrefab != null && firePoint != null)
        {
            // ① 弾を生成し、生成した弾の情報を変数（bullet）に入れる
            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

            // ② 生成した弾から StraightBullet スクリプトを取得する
            StraightBullet straightBullet = bullet.GetComponent<StraightBullet>();

            if (straightBullet != null)
            {
                // ③ 同じエネミーについている「移動スクリプト(EnemyMovement)」を取得
                EnemyMovement movement = GetComponent<EnemyMovement>();

                if (movement != null)
                {
                    // 移動スクリプトがあれば、その移動方向（moveDirection）を弾にセットする
                    straightBullet.SetDirection(movement.moveDirection);
                }
                else
                {
                    // もし固定砲台などで移動スクリプトがない場合は、FirePointの右方向をセット
                    straightBullet.SetDirection(firePoint.right);
                }
            }
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: 弾のプレハブ、またはFirePointが設定されていません。");
        }
    }
}
