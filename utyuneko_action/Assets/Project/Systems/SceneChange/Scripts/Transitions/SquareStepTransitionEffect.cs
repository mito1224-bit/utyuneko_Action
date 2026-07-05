using UnityEngine;
using System.Collections;

// TransitionManager の ITransitionEffect パターンに合わせた SquareStepTransition 用エフェクト。
// DigitalRainEffect / FadeEffect / WipeEffect と同じ形にしてあるので、
// Inspector の effectEntries リストに「TransitionType.SquareStep（要追加）」として登録するだけで使えます。
//
// ★ Full Screen Pass Renderer Feature 方式(A方式)の場合:
//   transitionMaterial には、URPの Renderer Data アセットに追加した
//   "Full Screen Pass Renderer Feature" の Pass Material に割り当てたのと
//   【同じ Material アセット】を参照させてください(実体は1つ、参照が2箇所にあるだけ)。
//   このスクリプトは Material.SetFloat で直接値を書き換えるので、
//   Renderer Feature 側が参照しているのと同一アセットであれば問題なく反映されます。
//
// シェーダー側 (SquareStepTransition_FullScreen.shader) が _Progress を StepCount 段階に
// 自動で量子化してくれるため、ここでは他の Effect と同じく単純に
// 0→1 / 1→0 の連続値を渡すだけで「段階的にパッパッと迫る」見た目になります。
public class SquareStepTransitionEffect : MonoBehaviour, ITransitionEffect
{
    [Header("マテリアル設定")]
    [SerializeField] private Material transitionMaterial; // SquareStepTransition マテリアルをアサイン

    [Header("イージング（任意）")]
    [SerializeField] private bool useSmoothStep = false; // trueにするとWipeEffect同様のイーズがかかる

    private int progressID;
    private float duration;

    public void Initialize(float duration)
    {
        this.duration = duration;
        progressID = Shader.PropertyToID("_Progress");
        Debug.Log($"[SquareStepTransitionEffect] Material: {transitionMaterial?.name} (InstanceID: {transitionMaterial?.GetInstanceID()})");

        if (transitionMaterial != null)
            transitionMaterial.SetFloat(progressID, 0f);
    }

    // 【フェードアウト】四角い枠が段階的に迫り、画面全体を覆う (Progress: 0 → 1)
    public IEnumerator FadeOut()
    {
        if (transitionMaterial == null)
        {
            Debug.LogError("[SquareStepTransitionEffect] Transition Material がセットされていません！");
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float raw = Mathf.Clamp01(elapsed / duration);
            float t = useSmoothStep ? Mathf.SmoothStep(0f, 1f, raw) : raw;
            transitionMaterial.SetFloat(progressID, t);
            yield return null;
        }
        // 必ず1を明示的に入れて、画面全体が完全な単色になる状態を保証する
        transitionMaterial.SetFloat(progressID, 1f);
    }

    // 【フェードイン】枠が段階的に中央から外側へ開いていく (Progress: 1 → 0)
    public IEnumerator FadeIn()
    {
        if (transitionMaterial == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float raw = Mathf.Clamp01(elapsed / duration);
            float t = useSmoothStep ? Mathf.SmoothStep(0f, 1f, raw) : raw;
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
