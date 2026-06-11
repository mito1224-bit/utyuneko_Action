using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class WipeEffect : MonoBehaviour, ITransitionEffect
{
    [Header("マスク設定")]
    [SerializeField] private Image maskImage; // ワイプ用のUI Image

    private Material transitionMaterial;
    private int circleSizeID;
    private float duration;

    private const float StartSize = 0f;  // 画面が見えている状態
    private const float EndSize = 50f; // 完全に真っ黒な状態

    // TransitionManager から呼ばれて、必要な値をセットアップする
    public void Initialize(float duration)
    {
        this.duration = duration;

        if (maskImage == null)
        {
            Debug.LogError("[WipeEffect] Mask Image がセットされていません！");
            return;
        }

        transitionMaterial = maskImage.material;
        circleSizeID = Shader.PropertyToID("_TransitionScale");

        // 起動時は画面が見えている状態（0）にしておく
        transitionMaterial.SetFloat(circleSizeID, StartSize);
    }

    // 【フェードアウト】穴を小さくして画面を真っ黒にする (0 → 50)
    public IEnumerator FadeOut()
    {
        if (transitionMaterial == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float easedT = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            transitionMaterial.SetFloat(circleSizeID, Mathf.Lerp(StartSize, EndSize, easedT));
            yield return null;
        }
        transitionMaterial.SetFloat(circleSizeID, EndSize);
    }

    // 【フェードイン】穴を大きくして画面を見せる (50 → 0)
    public IEnumerator FadeIn()
    {
        if (transitionMaterial == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float easedT = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            transitionMaterial.SetFloat(circleSizeID, Mathf.Lerp(EndSize, StartSize, easedT));
            yield return null;
        }
        transitionMaterial.SetFloat(circleSizeID, StartSize);
    }
}