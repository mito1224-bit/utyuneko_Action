using UnityEngine;
using UnityEngine.UI;

public class PlayerChargeGauge : MonoBehaviour
{
    [Header("参照設定")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Image gaugeImage;

    [Header("リロード演出速度")]
    [SerializeField] private float reloadSpeed = 8f; // 地面に着いた時にゲージが溜まる速度

    private float targetFillAmount = 1f;
    private float oldAmount = 1f;

    void Start()
    {
        // インスペクターで未設定なら、親オブジェクトから自動取得
        if (playerController == null) playerController = GetComponentInParent<PlayerController>();
        if (gaugeImage == null) gaugeImage = GetComponent<Image>();
    }

    void Update()
    {
        oldAmount = gaugeImage.fillAmount;

        if (playerController == null || gaugeImage == null) return;

        // プレイヤーの着地状態・残弾数からゲージの目標値を計算
        if (playerController.IsGrounded())
        {
            targetFillAmount = 1f;
        }
        else
        {
            float max = playerController.maxBurstCount;
            float current = playerController.currentBurstCount;

            if (max <= 0) max = 1;

            targetFillAmount = 1f - (current / max);
        }

        // ゲージの増減と赤点滅の制御
        if (targetFillAmount <= 0f)
        {
            // 💡【重要】残り0回の時は、fillAmountをあえて1f(満タン)にして画像を描画させます
            gaugeImage.fillAmount = 1f;

            // 💡完全に透明(Color.clear)にするとチカチカした時に一瞬消えて安っぽくなるので、
            // 「鮮やかな赤」と「暗い赤(ノイズ風)」の間を高速往復させてサイバーな警告灯っぽくします
            Color darkRed = new Color(0.25f, 0f, 0f, 1f);
            gaugeImage.color = Color.Lerp(Color.red, darkRed, Mathf.PingPong(Time.time * 18f, 1f));
        }
        else
        {
            // 通常時の増減処理
            if (gaugeImage.fillAmount > targetFillAmount)
            {
                if (oldAmount != targetFillAmount)
                {
                    SoundManager.Instance.PlaySE(SeType.PlayerChargingUp);
                }

                gaugeImage.fillAmount = targetFillAmount;
            }
            else
            {
                gaugeImage.fillAmount = Mathf.MoveTowards(gaugeImage.fillAmount, targetFillAmount, reloadSpeed * Time.deltaTime);
            }

            // 通常時はサイバーネオンブルー
            gaugeImage.color = Color.cyan;
        }
    }
}