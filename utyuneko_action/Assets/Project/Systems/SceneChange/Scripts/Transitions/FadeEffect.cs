using UnityEngine;
using System.Collections;

public class FadeEffect : MonoBehaviour, ITransitionEffect
{
    [Header("マテリアル設定")]
    [SerializeField] private Material transitionMaterial;

    private int progressID;
    private float duration;

    public void Initialize(float duration)
    {
        this.duration = duration;
        progressID = Shader.PropertyToID("_Progress");
        if (transitionMaterial != null)
            transitionMaterial.SetFloat(progressID, 0f);
    }

    // 【フェードアウト】画面が黒くなっていく (Progress: 0 → 1)
    public IEnumerator FadeOut()
    {
        if (transitionMaterial == null)
        {
            Debug.LogError("[FadeEffect] Transition Material がセットされていません！");
            yield break;
        }
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transitionMaterial.SetFloat(progressID, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        transitionMaterial.SetFloat(progressID, 1f);
    }

    // 【フェードイン】黒から画面が現れる (Progress: 1 → 0)
    public IEnumerator FadeIn()
    {
        if (transitionMaterial == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transitionMaterial.SetFloat(progressID, Mathf.Clamp01(1f - elapsed / duration));
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