using UnityEngine;
using UnityEngine.UI; // Imageを操作するために必要

public class UIEffectController : MonoBehaviour
{
    public enum EffectType { Scale, Jump, Fade }

    [Header("演出設定")]
    [SerializeField] private EffectType effectType = EffectType.Fade;
    [SerializeField] private float speed = 2.0f;
    [SerializeField] private float intensity = 0.5f; // Fadeなら最小アルファ値

    private Vector3 initialPos;
    private Vector3 initialScale;
    private Image targetImage;
    private Color initialColor;

    private void OnEnable()
    {
        initialPos = transform.localPosition;
        initialScale = transform.localScale;

        targetImage = GetComponent<Image>();
        if (targetImage != null) initialColor = targetImage.color;
    }

    private void Update()
    {
        float t = Mathf.PingPong(Time.unscaledTime * speed, 1f);
        float smoothT = Mathf.SmoothStep(0f, 1f, t);

        switch (effectType)
        {
            case EffectType.Scale:
                float scaleMod = 1.0f + (smoothT * (intensity / 100f));
                transform.localScale = initialScale * scaleMod;
                break;

            case EffectType.Jump:
                transform.localPosition = initialPos + new Vector3(0, smoothT * intensity, 0);
                break;

            case EffectType.Fade:
                if (targetImage != null)
                {
                    // intensity(最小アルファ) ～ 1.0 の間で点滅
                    float alpha = Mathf.Lerp(intensity, 1.0f, smoothT);
                    targetImage.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);
                }
                break;
        }
    }
}