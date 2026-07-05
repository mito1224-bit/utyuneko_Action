using System.Collections;
using UnityEngine;

public class BarrierManager : MonoBehaviour
{
    // どこからでもアクセスできるようにするための合言葉（シングルトン）
    public static BarrierManager Instance { get; private set; }

    [Header("バリアのプレハブ")]
    [SerializeField] private GameObject barrierPrefab;

    [Header("レーザーのプレハブ")]
    [SerializeField] private GameObject laserPrefab;

    private void Awake()
    {
        // シーン内に1つだけ存在するように設定
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// バリアを生成する共通関数
    /// </summary>
    /// <param name="spawnPosition">生成する位置</param>
    /// <param name="spawnRotation">生成する回転</param>
    /// <param name="parent">追従させたい親オブジェクト（任意、指定しなければ独立して生成）</param>
    // ★ 変更1：戻り値を void から「BarrierDestruction」に変更
    public BarrierDestruction SpawnBarrier(Vector3 spawnPosition, Quaternion spawnRotation, Transform parent = null)
    {
        if (barrierPrefab == null)
        {
            Debug.LogWarning("BarrierManagerにバリアのプレハブが設定されていません！");
            return null; // ★ 変更2：voidではないので null を返す
        }

        GameObject barrier = Instantiate(barrierPrefab, spawnPosition, spawnRotation);

        // 親オブジェクトが指定されていたら、その子にする（追従モード）
        if (parent != null)
        {
            barrier.transform.SetParent(parent);
        }

        // ★ 変更3：生成したバリアにくっついているスクリプトを取得して、呼び出し元に返してあげる
        return barrier.GetComponent<BarrierDestruction>();
    }

    public void SpawnLaser(Transform muzzleTransform, Transform targetTransform, float duration)
    {
        if (laserPrefab == null)
        {
            Debug.LogWarning("BarrierManagerにレーザーのプレハブが設定されていません！");
            return;
        }

        if (muzzleTransform == null || targetTransform == null) return;

        // 1. 発射口からターゲットへの方向を計算する
        Vector3 direction = targetTransform.position - muzzleTransform.position;

        // 2. その方向を向くための回転（Rotation）を作成する
        Quaternion lookRotation = Quaternion.LookRotation(direction);

        // 3. 発射口の位置・計算した回転でレーザーを生成し、発射口（または敵）を親にして追従させる
        GameObject laser = Instantiate(laserPrefab, muzzleTransform.position, lookRotation, muzzleTransform);

        ParticleSystem ps = laser.GetComponentInChildren<ParticleSystem>();
        if (ps != null)
        {
            // 3. シーン内のPlayerを探し、Collider2D を取得する
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                Collider2D playerCollider2D = player.GetComponent<Collider2D>();

                if (playerCollider2D != null)
                {
                    // 4. Triggerモジュールに2Dコライダーをセット
                    var triggerModule = ps.trigger;
                    triggerModule.SetCollider(0, playerCollider2D);
                }
            }
            else
            {
                Debug.LogWarning("シーン内に 'Player' タグのついたオブジェクトが見つかりません！");
            }
        }      

        // 指定秒数後に自動で消えるタイマー
        StartCoroutine(DestroyLaserAfterTime(laser, duration));
    }

    // 自動消滅用のコルーチン
    private IEnumerator DestroyLaserAfterTime(GameObject laserObj, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (laserObj != null)
        {
            Destroy(laserObj);
        }
    }
}
