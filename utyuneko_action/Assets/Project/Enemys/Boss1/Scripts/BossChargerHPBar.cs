using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 突進×盾ボス（Boss3）のHPバー。Fill方式の Image を割り当てるだけの最小構成。
/// BossChargerHealth が被弾のたびに SetRatio を呼ぶ。
/// </summary>
public class BossChargerHPBar : MonoBehaviour
{
    [Tooltip("Image Type=Filled にした前景イメージ")]
    public Image fillImage;

    [Tooltip("HPの減りを滑らかに追従させる速さ（1秒あたりの割合）。0以下で即時反映")]
    public float smoothSpeed = 1.5f;

    private float targetRatio = 1f;

    public void SetRatio(float ratio)
    {
        targetRatio = Mathf.Clamp01(ratio);
        if (smoothSpeed <= 0f && fillImage != null) fillImage.fillAmount = targetRatio;
    }

    void Update()
    {
        if (smoothSpeed <= 0f || fillImage == null) return;
        fillImage.fillAmount = Mathf.MoveTowards(fillImage.fillAmount, targetRatio, smoothSpeed * Time.deltaTime);
    }
}
