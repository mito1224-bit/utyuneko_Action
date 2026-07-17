using System.Collections;
using UnityEngine;

/// <summary>
/// 👑 崩壊した補佐：分身専用・完全独立型スケールアニメーションヘルパー
/// ボス本体のコルーチン破棄やステート遷移（気絶や撃破など）から完全に独立し、
/// 分身が現れる時（Unsquash）と消える時（Squash ➔ Destroy）の
/// テレポート風スケールアニメーションを100%美しく完走させます。
/// </summary>
public class GlitchHosaCloneHelper : MonoBehaviour
{
    private Transform squashTarget;
    private Vector3 originalScale;
    private bool isSquashing = false;

    public void Initialize(Transform target, Vector3 origScale)
    {
        this.squashTarget = target;
        this.originalScale = origScale;
    }

    /// <summary>
    /// 縦長に伸びた極小状態から、元のスケールへ滑らかに復元（Unsquash）します。
    /// </summary>
    public void StartUnsquash(float duration)
    {
        StartCoroutine(UnsquashRoutine(duration));
    }

    /// <summary>
    /// 元のスケールから縦長に極小まで潰し（Squash）、縮みきった瞬間に自己消滅します。
    /// </summary>
    public void StartSquashAndDestroy(float duration)
    {
        if (isSquashing) return;
        isSquashing = true;

        StopAllCoroutines(); // 出現中などの他のコルーチンを安全に止めて消滅を優先
        StartCoroutine(SquashRoutine(duration));
    }

    private IEnumerator UnsquashRoutine(float duration)
    {
        if (squashTarget == null) yield break;

        float t = 0f;
        // 初期状態：ペチャンコ（横0.01倍、縦1.5倍）
        squashTarget.localScale = new Vector3(0.01f, originalScale.y * 1.5f, originalScale.z);

        while (t < duration)
        {
            t += Time.deltaTime;
            float ratio = Mathf.Clamp01(t / duration);
            float smoothRatio = Mathf.SmoothStep(0f, 1f, ratio);

            float x = Mathf.Lerp(0.01f, originalScale.x, smoothRatio);
            float y = Mathf.Lerp(originalScale.y * 1.5f, originalScale.y, smoothRatio);
            squashTarget.localScale = new Vector3(x, y, originalScale.z);
            yield return null;
        }
        squashTarget.localScale = originalScale;
    }

    private IEnumerator SquashRoutine(float duration)
    {
        if (squashTarget == null)
        {
            Destroy(gameObject);
            yield break;
        }

        float t = 0f;
        Vector3 startScale = squashTarget.localScale;

        while (t < duration)
        {
            t += Time.deltaTime;
            float ratio = Mathf.Clamp01(t / duration);
            float smoothRatio = Mathf.SmoothStep(0f, 1f, ratio);

            float x = Mathf.Lerp(startScale.x, 0.01f, smoothRatio);
            float y = Mathf.Lerp(startScale.y, originalScale.y * 1.5f, smoothRatio);
            squashTarget.localScale = new Vector3(x, y, originalScale.z);
            yield return null;
        }

        squashTarget.localScale = new Vector3(0.01f, originalScale.y * 1.5f, originalScale.z);

        // 縮みきった瞬間に、この分身オブジェクトをメモリから完全クリーンアップ！
        Destroy(gameObject);
    }
}