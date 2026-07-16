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

    public void SpawnLaser(Vector3 muzzlePosition, Vector3 targetPosition, float duration, Transform followParent = null)
    {
        if (laserPrefab == null)
        {
            Debug.LogWarning("BarrierManagerにレーザーのプレハブが設定されていません！");
            return;
        }

        // 1. 発射口からターゲットへの方向を計算する
        Vector3 direction = targetPosition - muzzlePosition;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward; // ゼロベクトル対策

        // 2. その方向を向くための回転（Rotation）を作成する
        Quaternion lookRotation = Quaternion.LookRotation(direction);

        // 3. 発射口の位置・計算した回転でレーザーを生成する
        //    ※ 回転を巻き込まないよう、親には設定しない（位置だけ追従させたい場合は followParent を使う）
        GameObject laser = Instantiate(laserPrefab, muzzlePosition, lookRotation);

        // 敵の移動に位置だけ追従させたい場合（回転は追従させない）
        if (followParent != null)
        {
            laser.transform.SetParent(followParent, true); // ワールド座標維持
                                                           // 注意: SetParentすると回転も親に追従します。
                                                           // 位置だけ追従・回転は固定にしたい場合は下記のような追従専用スクリプトが必要です。
        }

        ParticleSystem ps = laser.GetComponentInChildren<ParticleSystem>();
        if (ps != null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                Collider2D playerCollider2D = player.GetComponent<Collider2D>();
                if (playerCollider2D != null)
                {
                    var triggerModule = ps.trigger;
                    triggerModule.SetCollider(0, playerCollider2D);
                }
            }
            else
            {
                Debug.LogWarning("シーン内に 'Player' タグのついたオブジェクトが見つかりません！");
            }
        }

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
