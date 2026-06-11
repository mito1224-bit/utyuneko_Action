using UnityEngine;
using System.Collections;

public class DigitalRainEffect : MonoBehaviour, ITransitionEffect
{
    [Header("マテリアル設定")]
    [SerializeField] private Material transitionMaterial; // Full Screen Pass にセットしたマテリアル

    private int progressID;
    private float duration;

    // TransitionManager から呼ばれて、必要な値をセットアップする
    public void Initialize(float duration)
    {
        this.duration = duration;
        progressID = Shader.PropertyToID("_Progress");

        // 起動時はエフェクトをオフ（0）にしておく
        if (transitionMaterial != null)
            transitionMaterial.SetFloat(progressID, 0f);
    }

    // 【フェードアウト】デジタル雨で画面を覆っていく (0 → 1)
    public IEnumerator FadeOut()
    {
        if (transitionMaterial == null)
        {
            Debug.LogError("[DigitalRainEffect] Transition Material がセットされていません！");
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

    // 【フェードイン】デジタル雨が晴れていく (1 → 0)
    public IEnumerator FadeIn()
    {
        if (transitionMaterial == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transitionMaterial.SetFloat(progressID, Mathf.Clamp01(1f - (elapsed / duration)));
            yield return null;
        }
        transitionMaterial.SetFloat(progressID, 0f);
    }

    private void OnDestroy()
    {
        // 破棄されるときにマテリアルを通常状態に戻す
        if (transitionMaterial != null)
            transitionMaterial.SetFloat(progressID, 0f);
    }
}