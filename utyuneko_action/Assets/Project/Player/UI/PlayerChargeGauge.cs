using UnityEngine;
using UnityEngine.UI;

public class PlayerChargeGauge : MonoBehaviour
{
    [Header("参照設定")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Image gaugeImage;

    private float targetFillAmount = 1f;
    private bool isFullSEPlayed = true;
    private bool isGaugeZero = false;

    void Start()
    {
        // インスペクターで未設定なら、親オブジェクトから自動取得
        if (playerController == null) playerController = GetComponentInParent<PlayerController>();
        if (gaugeImage == null) gaugeImage = GetComponent<Image>();
    }

    void Update()
    {
        if (playerController == null || gaugeImage == null) return;

        float max = playerController.maxBurstCount;
        float current = playerController.currentBurstCount;

        // プレイヤーの着地状態・残弾数からゲージの目標値を計算
        if (playerController.IsGrounded())
        {
            targetFillAmount = 1f;

            if (isGaugeZero)
            {
                targetFillAmount = 0.1f;
                isGaugeZero = false;
            }
        }
        else
        {
            if (max <= 0) max = 1;

            targetFillAmount = 1f - (current / max);
        }

        // ゲージの増減と赤点滅の制御
        if (targetFillAmount <= 0f)
        {
            // 残り0回の時は、fillAmountをあえて1f(満タン)にして画像を描画させます
            gaugeImage.fillAmount = 1f;

            // 完全に透明(Color.clear)にするとチカチカした時に一瞬消えて安っぽくなるので、
            // 「鮮やかな赤」と「暗い赤(ノイズ風)」の間を高速往復させてサイバーな警告灯っぽくします
            Color darkRed = new Color(0.25f, 0f, 0f, 1f);
            gaugeImage.color = Color.Lerp(Color.red, darkRed, Mathf.PingPong(Time.time * 18f, 1f));

            isGaugeZero = true;
        }
        else
        {
            // 通常時の増減処理
            if (gaugeImage.fillAmount > targetFillAmount)
            {
                gaugeImage.fillAmount = targetFillAmount;
            }
            else
            {
                gaugeImage.fillAmount = Mathf.MoveTowards(gaugeImage.fillAmount, targetFillAmount, playerController.reloadSpeed * Time.deltaTime);
                playerController.currentBurstCount = (int)max - Mathf.FloorToInt(gaugeImage.fillAmount * max);
            }

            if (max == gaugeImage.fillAmount * max)
            {
                if (!isFullSEPlayed)
                {
                    if (!BaseEventManager.IsAnyEventPlaying)
                    {
                        SoundManager.Instance.PlaySE(SeType.ChargOK, 1.5f, 0f);
                    }
                    isFullSEPlayed = true; // 連打ブロック
                }
            }
            else
            {
                isFullSEPlayed = false;
            }

            // 通常時はサイバーネオンブルー
            gaugeImage.color = Color.cyan;
        }
    }
}