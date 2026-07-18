using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 👑 崩壊した補佐：演出・ビジュアル専門コンポーネントクラス（出現演出Lerp大修正版）
/// </summary>
public class GlitchHosaVisual : MonoBehaviour
{
    private GlitchHosaController controller;

    [Header("✨ 演出用パラメータ")]
    public float afterimageDuration = 0.5f;
    public float afterimageInterval = 0.02f;
    public Color afterimageColor = new Color(1f, 0.15f, 0.15f, 0.65f);

    private List<RendererData> originalRendererData = new List<RendererData>();

    private struct RendererData
    {
        public SpriteRenderer spriteRenderer;
        public SkinnedMeshRenderer skinnedRenderer;
        public MeshRenderer meshRenderer;
        public Material originalMaterial;
    }

    public void Initialize(GlitchHosaController bossController)
    {
        controller = bossController;

        Transform visualRoot = controller.ultVisualOffsetObject != null ? controller.ultVisualOffsetObject : controller.transform;
        originalRendererData.Clear();
        foreach (var sr in visualRoot.GetComponentsInChildren<SpriteRenderer>(true)) if (sr != null) originalRendererData.Add(new RendererData { spriteRenderer = sr, originalMaterial = sr.sharedMaterial });
        foreach (var smr in visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (smr != null) originalRendererData.Add(new RendererData { skinnedRenderer = smr, originalMaterial = smr.sharedMaterial });
        foreach (var mr in visualRoot.GetComponentsInChildren<MeshRenderer>(true)) if (mr != null) originalRendererData.Add(new RendererData { meshRenderer = mr, originalMaterial = mr.sharedMaterial });
    }

    public void ApplyGlobalFlashMaterial(Material mat)
    {
        if (mat == null) return;
        foreach (var data in originalRendererData)
        {
            if (data.spriteRenderer != null)
            {
                data.spriteRenderer.sharedMaterial = mat;
                data.spriteRenderer.color = Color.white;
            }
            if (data.skinnedRenderer != null) data.skinnedRenderer.sharedMaterial = mat;
            if (data.meshRenderer != null) data.meshRenderer.sharedMaterial = mat;
        }
    }

    public void ForceResetAllMaterials(bool isHealthFlashing)
    {
        if (isHealthFlashing) return;

        foreach (var data in originalRendererData)
        {
            if (data.spriteRenderer != null) { data.spriteRenderer.sharedMaterial = data.originalMaterial; data.spriteRenderer.color = Color.white; }
            if (data.skinnedRenderer != null) data.skinnedRenderer.sharedMaterial = data.originalMaterial;
            if (data.meshRenderer != null) data.meshRenderer.sharedMaterial = data.originalMaterial;
        }
    }

    /// <summary>
    /// ワープアウト（消える）：元のサイズから 0.01f へ縮小
    /// </summary>
    public IEnumerator TeleportOutRoutine(float duration)
    {
        Vector3 origScale = controller.originalVisualLocalScale;
        SoundManager.Instance.PlaySE(SeType.EnemyCharge);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float ratio = Mathf.Clamp01(t / duration);
            float smoothRatio = Mathf.SmoothStep(0f, 1f, ratio);

            float x = Mathf.Lerp(origScale.x, 0.01f, smoothRatio);
            float y = Mathf.Lerp(origScale.y, origScale.y * 1.5f, smoothRatio);
            controller.targetScale = new Vector3(x, y, origScale.z);
            yield return null;
        }
        controller.targetScale = new Vector3(0.01f, origScale.y * 1.5f, origScale.z);
    }

    /// <summary>
    /// ワープイン（出現する）：極小 0.01f から 元のサイズへ拡大復旧
    /// </summary>
    public IEnumerator TeleportInRoutine(Vector3 targetPos, float duration)
    {
        Vector3 origScale = controller.originalVisualLocalScale;

        controller.targetScale = new Vector3(0.01f, origScale.y * 1.5f, origScale.z);
        controller.transform.position = targetPos;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float ratio = Mathf.Clamp01(t / duration);
            float smoothRatio = Mathf.SmoothStep(0f, 1f, ratio);

            // 👑【大修正】消去用 Lerp から、極小 0.01f ➔ 元のサイズ origScale へと正しく拡大する数式に修正！
            float x = Mathf.Lerp(0.01f, origScale.x, smoothRatio);
            float y = Mathf.Lerp(origScale.y * 1.5f, origScale.y, smoothRatio);
            controller.targetScale = new Vector3(x, y, origScale.z);
            yield return null;
        }
        controller.targetScale = origScale;
        controller.targetXRotation = controller.defaultXRotation;
        controller.targetYRotation = controller.defaultYRotation;
        controller.targetZRotation = 0f;
    }

    public IEnumerator TeleportWithSquashRoutine(Vector3 targetPos, float duration)
    {
        yield return StartCoroutine(TeleportOutRoutine(duration));
        yield return StartCoroutine(TeleportInRoutine(targetPos, duration));
    }

    public void CreateAfterimage()
    {
        Transform visualTarget = controller.ultVisualOffsetObject != null ? controller.ultVisualOffsetObject : controller.transform;
        if (visualTarget == null) return;

        GameObject clone = Instantiate(visualTarget.gameObject, visualTarget.position, visualTarget.rotation);
        clone.name = "HosaHoverAfterimage_Clone";
        clone.transform.SetParent(null);
        clone.transform.localScale = visualTarget.lossyScale;

        if (clone.TryGetComponent<GlitchHosaController>(out var c)) Destroy(c);
        if (clone.TryGetComponent<Rigidbody2D>(out var rb)) Destroy(rb);
        if (clone.TryGetComponent<Collider2D>(out var col)) Destroy(col);
        if (clone.TryGetComponent<Animator>(out var anim)) Destroy(anim);
        foreach (var childAnim in clone.GetComponentsInChildren<Animator>()) Destroy(childAnim);
        foreach (var childCol in clone.GetComponentsInChildren<Collider2D>()) Destroy(childCol);

        clone.AddComponent<StageSecondBossAfterimageFade>().Initialize(afterimageDuration, afterimageColor);
    }
}