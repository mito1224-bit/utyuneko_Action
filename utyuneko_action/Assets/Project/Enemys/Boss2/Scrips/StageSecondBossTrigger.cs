using UnityEngine;

public class StageSecondBossTrigger : MonoBehaviour
{
    public GameObject bossObject;
    public GameObject hpBarObject; // ここに「BossUI」をセット
    public GameObject bossWallObject;

    private bool isTriggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isTriggered || !other.CompareTag("Player")) return;
        isTriggered = true;

        SoundManager.Instance.StopBGM(0.5f);

        // ① まず親Canvas（BossUI）をアクティブにする
        if (hpBarObject != null)
        {
            hpBarObject.SetActive(true);
        }

        // ===================================================================
        // 🛠️【確実な解決策】親が確実にONになった直後のこの安全なタイミングで、
        // トリガー側から直接HPバーの出現アニメーションを叩いて起動させます！
        // ===================================================================
        if (bossObject != null)
        {
            var bossHealth = bossObject.GetComponent<StageSecondBossHealth>();
            if (bossHealth != null && bossHealth.bossHpBar != null)
            {
                // 親がONになったコンテキストなので、100%安全にコルーチンが開始できます
                bossHealth.bossHpBar.StartAppearAnimation(bossHealth.maxHP, bossHealth.maxHP);
            }

            // HPバーの準備が完全に整ったあとで、ボスをアクティブ化！
            bossObject.SetActive(true);
        }

        if (bossWallObject != null)
        {
            bossWallObject.SetActive(true);
        }

        Destroy(gameObject);
    }
}