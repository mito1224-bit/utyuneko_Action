using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlayerDamageEffect : MonoBehaviour
{
    // ---------------------------------------------------------------
    // Glitchシェーダー
    // ---------------------------------------------------------------
    [Header("Glitch Shader")]

    [Tooltip("DamageGlitch.mat をアサインする")]
    public Material glitchMaterial;

    [Tooltip("画面の横ズレの最大強度（大きいほど激しくズレる）")]
    [Range(0f, 1f)] public float glitchIntensity = 0.3f;

    [Tooltip("色収差（RGBのにじみ）の最大強度")]
    [Range(0f, 0.2f)] public float chromaticIntensity = 0.04f;

    // ---------------------------------------------------------------
    // Vignette（画面周辺を赤く暗くするエフェクト）
    // ---------------------------------------------------------------
    [Header("Vignette")]

    [Tooltip("ビネットの色（デフォルトは赤）")]
    public Color vignetteColor = new Color(0.8f, 0f, 0f);

    [Tooltip("ビネットの最大強度（0〜1、大きいほど周辺が暗くなる）")]
    [Range(0f, 1f)] public float vignetteIntensity = 0.6f;

    [Tooltip("ビネットのぼかし具合（0でくっきり、1でなめらか）")]
    [Range(0f, 1f)] public float vignetteSmoothness = 0.5f;

    // ---------------------------------------------------------------
    // ColorAdjustments（彩度を下げて白黒に近づけるエフェクト）
    // ---------------------------------------------------------------
    [Header("Desaturation")]

    [Tooltip("彩度を下げる量（0で変化なし、100で完全に白黒）")]
    [Range(0f, 100f)] public float desaturationAmount = 100f;

    // ---------------------------------------------------------------
    // FilmGrain（ノイズ・ざらつきエフェクト）
    // ---------------------------------------------------------------
    [Header("Film Grain")]

    [Tooltip("ノイズの最大強度（大きいほどざらつく）")]
    [Range(0f, 1f)] public float grainIntensity = 0.8f;

    [Tooltip("ノイズの明暗への反応度（大きいほど暗い部分にノイズが出やすい）")]
    [Range(0f, 1f)] public float grainResponse = 0.8f;

    // ---------------------------------------------------------------
    // エフェクト全体のタイミング
    // ---------------------------------------------------------------
    [Header("Timing")]

    [Tooltip("エフェクト全体の長さ（秒）")]
    [Range(0.05f, 1f)] public float effectDuration = 0.2f;

    [Tooltip("強度カーブの鋭さ（大きいほど瞬間的に強く出てすぐ消える）")]
    [Range(1f, 10f)] public float sharpness = 1f;

    // ---------------------------------------------------------------
    // 内部変数
    // ---------------------------------------------------------------
    private Volume _volume;
    private Vignette _vignette;
    private ColorAdjustments _colorAdjustments;
    private FilmGrain _filmGrain;
    private Coroutine _effectCoroutine;

    // ---------------------------------------------------------------
    // 初期化
    // ---------------------------------------------------------------
    private void Awake()
    {
        SetupVolume();
        ResetEffects();
    }

    private void SetupVolume()
    {
        _volume = gameObject.AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.priority = 10;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _volume.profile = profile;

        _vignette = profile.Add<Vignette>(overrides: true);
        _vignette.color.overrideState = true;
        _vignette.intensity.overrideState = true;
        _vignette.smoothness.overrideState = true;

        _colorAdjustments = profile.Add<ColorAdjustments>(overrides: true);
        _colorAdjustments.saturation.overrideState = true;

        _filmGrain = profile.Add<FilmGrain>(overrides: true);
        _filmGrain.type.overrideState = true;
        _filmGrain.intensity.overrideState = true;
        _filmGrain.response.overrideState = true;
        _filmGrain.type.value = FilmGrainLookup.Thin1;
    }

    // ---------------------------------------------------------------
    // 外部から呼ぶ
    // ---------------------------------------------------------------
    public void PlayDamageEffect()
    {
        if (_effectCoroutine != null)
            StopCoroutine(_effectCoroutine);

        _effectCoroutine = StartCoroutine(DamageEffectCoroutine());
    }

    // ---------------------------------------------------------------
    // エフェクト本体
    // ---------------------------------------------------------------
    private IEnumerator DamageEffectCoroutine()
    {
        float elapsed = 0f;

        while (elapsed < effectDuration)
        {
            float t = elapsed / effectDuration;

            // sharpnessが1のとき山なり、大きくすると瞬間的に強く出てすぐ消える
            float intensity = Mathf.Pow(Mathf.Sin(t * Mathf.PI), sharpness);

            ApplyEffects(intensity);

            elapsed += Time.deltaTime;
            yield return null;
        }

        ResetEffects();
        _effectCoroutine = null;
    }

    private void ApplyEffects(float intensity)
    {
        // Vignette
        _vignette.color.value = vignetteColor;
        _vignette.intensity.value = vignetteIntensity * intensity;
        _vignette.smoothness.value = vignetteSmoothness;

        // 彩度（saturationは-100〜100なのでマイナス方向に下げる）
        _colorAdjustments.saturation.value = -desaturationAmount * intensity;

        // FilmGrain
        _filmGrain.intensity.value = grainIntensity * intensity;
        _filmGrain.response.value = grainResponse;

        // Glitchシェーダー
        if (glitchMaterial != null)
        {
            glitchMaterial.SetFloat("_Intensity", glitchIntensity * intensity);
            glitchMaterial.SetFloat("_ChromaticIntensity", chromaticIntensity * intensity);
        }
    }

    private void ResetEffects()
    {
        _vignette.intensity.value = 0f;
        _colorAdjustments.saturation.value = 0f;
        _filmGrain.intensity.value = 0f;

        if (glitchMaterial != null)
        {
            glitchMaterial.SetFloat("_Intensity", 0f);
            glitchMaterial.SetFloat("_ChromaticIntensity", 0f);
        }
    }

    // ---------------------------------------------------------------
    // 後始末
    // ---------------------------------------------------------------
    private void OnDestroy()
    {
        if (_volume != null && _volume.profile != null)
            Destroy(_volume.profile);
    }
}