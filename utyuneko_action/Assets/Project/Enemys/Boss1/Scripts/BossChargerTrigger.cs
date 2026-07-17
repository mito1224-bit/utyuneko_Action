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

        // ① 先に親Canvas（BossUI）をONにする。
        //    HPバーの出現アニメはコルーチンなので、親が非アクティブのままでは起動できない。
        if (hpBarObject != null) hpBarObject.SetActive(true);

        // ② 親がONになった安全なタイミングで、トリガー側からHPバーの出現アニメを叩く
        //    （StageSecondBossTrigger と同じ流儀）。ここで最大値が入るので、これを飛ばすとゲージが正しく出ない。
        if (bossObject != null)
        {
            var bossHealth = bossObject.GetComponent<BossChargerHealth>();
            if (bossHealth != null && bossHealth.hpBar != null)
            {
                bossHealth.hpBar.StartAppearAnimation(bossHealth.maxHP, bossHealth.maxHP);
            }

            // ③ HPバーの準備が整ってからボスを実体化
            bossObject.SetActive(true);
        }

        if (bossWallObject != null) bossWallObject.SetActive(true);

        Destroy(gameObject);
    }
}
