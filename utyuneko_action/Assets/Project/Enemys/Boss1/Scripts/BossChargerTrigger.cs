using UnityEngine;

/// <summary>
/// 突進×盾ボス（Boss3）の戦闘開始トリガー。StageSecondBossTrigger と同じ流儀:
/// プレイヤーが触れたら BGM停止 → HPバー表示 → ボス実体化 → 退路を塞ぐ壁ON → 自分は消える。
/// シーンにはトリガーコライダー（IsTrigger）を付けて置き、ボス本体は非アクティブで配置しておく。
/// </summary>
public class BossChargerTrigger : MonoBehaviour
{
    [Tooltip("非アクティブで置いたボス本体")]
    public GameObject bossObject;
    [Tooltip("HPバーを含むUIルート（BossUI）")]
    public GameObject hpBarObject;
    [Tooltip("退路を塞ぐ壁（任意）")]
    public GameObject bossWallObject;
    [Tooltip("接触時にBGMを止める")]
    public bool stopBgm = true;

    private bool isTriggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isTriggered || !other.CompareTag("Player")) return;
        isTriggered = true;

        // テストシーンには SoundManager がいないことがあるので null ガード（CLAUDE.md の流儀）
        if (stopBgm && SoundManager.Instance != null) SoundManager.Instance.StopBGM(1.0f);

        if (hpBarObject != null) hpBarObject.SetActive(true);
        if (bossObject != null) bossObject.SetActive(true);
        if (bossWallObject != null) bossWallObject.SetActive(true);

        Destroy(gameObject);
    }
}
