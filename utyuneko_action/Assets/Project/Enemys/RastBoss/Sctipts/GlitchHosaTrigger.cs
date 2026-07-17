using UnityEngine;

/// <summary>
/// 崩壊した補佐：戦の開幕を告げる専用ステージトリガー
/// プレイヤーの進入を検知し、BGMを切り替え、HPバー（BossUI）を最大値で完全同期起動します！
/// </summary>
public class GlitchHosaTrigger : MonoBehaviour
{
    public GameObject bossObject;
    [Tooltip("ここに親Canvasである『BossUI』プレハブ、またはオブジェクトをセットしてね")]
    public GameObject hpBarObject;
    [Tooltip("戦闘開始時に閉まるボスの部屋の壁をセットしてね")]
    public GameObject bossWallObject;

    private bool isTriggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isTriggered || !other.CompareTag("Player")) return;
        isTriggered = true;

        // 戦闘前の道中BGMを1秒かけてフェードアウトストップ
        SoundManager.Instance.StopBGM(1.0f);

        // ① まず親Canvas（BossUI）をアクティブにする
        if (hpBarObject != null)
        {
            hpBarObject.SetActive(true);
        }

        // ===================================================================
        // 🛠️【ラスボス完全同期】
        // トリガー側から直接ラスボスの『GlitchHosaHealth』を取得！
        // 親UIが確実にONになった直後のこの安全なタイミングで、HPバーの出現アニメーション
        // にラスボスの最大HP（maxHP）を叩き込んで100%完璧に同期駆動させます！
        // ===================================================================
        if (bossObject != null)
        {
            // 👑 古いボススクリプトではなく、新設した GlitchHosaHealth を確実にGetComponent！
            var bossHealth = bossObject.GetComponent<GlitchHosaHealth>();
            if (bossHealth != null && bossHealth.bossHpBar != null)
            {
                // 補佐の最大HP（maxHP）をUIの初期アニメーションに安全にデリバリー！
                bossHealth.bossHpBar.StartAppearAnimation(bossHealth.maxHP, bossHealth.maxHP);
            }

            // HPバーのセットアップと初期値の同期が完璧に整ったあとで、満を持してボス本体をアクティブ化！
            bossObject.SetActive(true);
        }

        // ボス部屋の退路を断つ壁をアクティブ化
        if (bossWallObject != null)
        {
            bossWallObject.SetActive(true);
        }

        // お役御免になったのでトリガー自体を消滅
        Destroy(gameObject);
    }
}