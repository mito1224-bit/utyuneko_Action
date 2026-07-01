using UnityEngine;
using System.Collections;

public class WipeEffect : MonoBehaviour, ITransitionEffect
{
    [Header("マテリアル設定")]
    [SerializeField] private Material transitionMaterial; // Full Screen Pass にセットしたマテリアル

    private int progressID;
    private float duration;

    // TransitionManager から呼ばれて初期化
    public void Initialize(float duration)
    {
        this.duration = duration;
        progressID = Shader.PropertyToID("_Progress");
        if (transitionMaterial != null)
            transitionMaterial.SetFloat(progressID, 0f);
    }

    // 【フェードアウト】円が縮んで画面を覆う (Progress: 0 → 1)
    public IEnumerator FadeOut()
    {
        if (transitionMaterial == null)
        {
            Debug.LogError("[WipeEffect] Transition Material がセットされていません！");
            yield break;
        }
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // SmoothStepで円の動きをイーズイン/アウト
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            transitionMaterial.SetFloat(progressID, t);
            yield return null;
        }
        transitionMaterial.SetFloat(progressID, 1f);
    }

    // 【フェードイン】円が広がって画面が見える (Progress: 1 → 0)
    public IEnumerator FadeIn()
    {
        if (transitionMaterial == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            transitionMaterial.SetFloat(progressID, 1f - t);
            yield return null;
        }
        transitionMaterial.SetFloat(progressID, 0f);
    }

    private void OnDestroy()
    {
        if (transitionMaterial != null)
            transitionMaterial.SetFloat(progressID, 0f);
    }
}