using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public enum DamageType
{
    Player,
    Drone
}

public class PlayerDamageEffect : MonoBehaviour
{
    //==================================================
    // Glitch Shader
    //==================================================
    [Header("Glitch Shader")]
    [Tooltip("DamageGlitch.matをアサインしてください")]
    public Material glitchMaterial;

    //==================================================
    // Player Damage Settings
    //==================================================
    [Header("Player Damage Settings")]
    [Tooltip("エフェクト全体の長さ（秒）")]
    [Range(0.05f, 1.5f)]
    public float playerEffectDuration = 0.2f;

    [Tooltip("カーブの鋭さ。上げると瞬間的に強く出てすぐ消える")]
    [Range(0.5f, 10f)]
    public float playerSharpness = 4f;

    [Header("Player Vignette")]
    public Color playerVignetteColor = new Color(0f, 0f, 0f);
    [Range(0f, 1f)] public float playerVignetteIntensity = 0.4f;
    [Range(0f, 1f)] public float playerVignetteSmoothness = 0.5f;
    public bool playerUseVignetteIntensity = false;

    [Header("Player Desaturation")]
    [Tooltip("白黒になる量（0〜100）")]
    [Range(0f, 100f)]
    public float playerDesaturation = 20f;
    public bool playerUseColorIntensity = false;

    [Header("Player Grain")]
    [Range(0f, 1f)] public float playerGrainIntensity = 0.6f;
    [Range(0f, 1f)] public float playerGrainResponse = 0.2f;
    public bool playerUseGrainIntensity = false;

    [Header("Player Glitch")]
    [Range(0f, 1f)] public float playerGlitchIntensity = 0.4f;
    public bool playerUseGlitchIntensity = false;

    [Header("Player Chromatic")]
    [Range(0f, 0.1f)] public float playerChromaticIntensity = 0.04f;
    public bool playerUseChromaticIntensity = false;

    [Header("Player Color Filter")]
    [Tooltip("画面全体にかける色味。白なら変化なし")]
    public Color playerColorFilter = new Color(1f, 0.3f, 0.3f, 1f);
    [Range(0f, 1f)] public float playerColorFilterStrength = 0.4f;
    public bool playerUseColorFilterIntensity = false;

    //==================================================
    // Drone Damage Settings
    //==================================================
    [Header("Drone Damage Settings")]
    [Tooltip("エフェクト全体の長さ（秒）")]
    [Range(0.05f, 1.5f)]
    public float droneEffectDuration = 0.2f;

    [Tooltip("カーブの鋭さ。上げると瞬間的に強く出てすぐ消える")]
    [Range(0.5f, 10f)]
    public float droneSharpness = 4f;

    [Header("Drone Vignette")]
    public Color droneVignetteColor = new Color(0.25f, 0.3f, 0.35f);
    [Range(0f, 1f)] public float droneVignetteIntensity = 0.4f;
    [Range(0f, 1f)] public float droneVignetteSmoothness = 0.7f;
    public bool droneUseVignetteIntensity = false;

    [Header("Drone Desaturation")]
    [Tooltip("白黒になる量（0〜100）")]
    [Range(0f, 100f)]
    public float droneDesaturation = 20f;
    public bool droneUseColorIntensity = false;

    [Header("Drone Grain")]
    [Range(0f, 1f)] public float droneGrainIntensity = 0.4f;
    [Range(0f, 1f)] public float droneGrainResponse = 0.2f;
    public bool droneUseGrainIntensity = false;

    [Header("Drone Glitch")]
    [Range(0f, 1f)] public float droneGlitchIntensity = 0.2f;
    public bool droneUseGlitchIntensity = false;

    [Header("Drone Chromatic")]
    [Range(0f, 0.1f)] public float droneChromaticIntensity = 0.02f;
    public bool droneUseChromaticIntensity = false;

    [Header("Drone Color Filter")]
    [Tooltip("画面全体にかける色味。白なら変化なし")]
    public Color droneColorFilter = new Color(1f, 0.3f, 0.3f, 1f);
    [Range(0f, 1f)] public float droneColorFilterStrength = 0.1f;
    public bool droneUseColorFilterIntensity = false;

    //==================================================
    // 内部参照
    //==================================================
    private Volume _volume;
    private Vignette _vignette;
    private ColorAdjustments _colorAdjustments;
    private FilmGrain _filmGrain;
    private Coroutine _effectCoroutine;

    // Material更新最適化用キャッシュ
    private float _prevGlitchIntensity = -1f;
    private float _prevChromaticIntensity = -1f;

    //==================================================
    // フラグ構造体
    //==================================================
    private struct EffectFlags
    {
        public bool vignette;
        public bool color;
        public bool grain;
        public bool glitch;
        public bool chromatic;
        public bool colorFilter;
    }

    //==================================================
    // 設定構造体
    //==================================================
    private struct DamageSettings
    {
        public float duration;
        public float sharpness;

        public Color vignetteColor;
        public float vignetteIntensity;
        public float vignetteSmoothness;

        public float desaturation;

        public float grainIntensity;
        public float grainResponse;

        public float glitchIntensity;
        public float chromaticIntensity;

        public Color colorFilter;
        public float colorFilterStrength;

        public EffectFlags flags;
    }

    //==================================================
    // 初期化
    //==================================================
    private void Awake()
    {
        SetupVolume();
        ResetEffects();
    }

    private void SetupVolume()
    {
        _volume = gameObject.AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.priority = 10f;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _volume.profile = profile;

        _vignette = profile.Add<Vignette>(true);
        _colorAdjustments = profile.Add<ColorAdjustments>(true);
        _filmGrain = profile.Add<FilmGrain>(true);

        // Vignette
        _vignette.color.overrideState = true;
        _vignette.intensity.overrideState = true;
        _vignette.smoothness.overrideState = true;

        // Color Adjustments
        _colorAdjustments.saturation.overrideState = true;
        _colorAdjustments.colorFilter.overrideState = true;

        // Film Grain
        _filmGrain.type.overrideState = true;
        _filmGrain.intensity.overrideState = true;
        _filmGrain.response.overrideState = true;
        _filmGrain.type.value = FilmGrainLookup.Thin1;
    }

    //==================================================
    // 外部呼び出し
    //==================================================
    public void PlayDamageEffect(DamageType type)
    {
        if (_effectCoroutine != null)
        {
            StopCoroutine(_effectCoroutine);
        }

        _effectCoroutine = StartCoroutine(DamageEffectCoroutine(type));
    }

    //==================================================
    // 設定取得
    //==================================================
    private DamageSettings GetSettings(DamageType type)
    {
        if (type == DamageType.Player)
        {
            return new DamageSettings
            {
                duration = playerEffectDuration,
                sharpness = playerSharpness,

                vignetteColor = playerVignetteColor,
                vignetteIntensity = playerVignetteIntensity,
                vignetteSmoothness = playerVignetteSmoothness,

                desaturation = playerDesaturation,

                grainIntensity = playerGrainIntensity,
                grainResponse = playerGrainResponse,

                glitchIntensity = playerGlitchIntensity,
                chromaticIntensity = playerChromaticIntensity,

                colorFilter = playerColorFilter,
                colorFilterStrength = playerColorFilterStrength,

                flags = new EffectFlags
                {
                    vignette = playerUseVignetteIntensity,
                    color = playerUseColorIntensity,
                    grain = playerUseGrainIntensity,
                    glitch = playerUseGlitchIntensity,
                    chromatic = playerUseChromaticIntensity,
                    colorFilter = playerUseColorFilterIntensity
                }
            };
        }

        return new DamageSettings
        {
            duration = droneEffectDuration,
            sharpness = droneSharpness,

            vignetteColor = droneVignetteColor,
            vignetteIntensity = droneVignetteIntensity,
            vignetteSmoothness = droneVignetteSmoothness,

            desaturation = droneDesaturation,

            grainIntensity = droneGrainIntensity,
            grainResponse = droneGrainResponse,

            glitchIntensity = droneGlitchIntensity,
            chromaticIntensity = droneChromaticIntensity,

            colorFilter = droneColorFilter,
            colorFilterStrength = droneColorFilterStrength,

            flags = new EffectFlags
            {
                vignette = droneUseVignetteIntensity,
                color = droneUseColorIntensity,
                grain = droneUseGrainIntensity,
                glitch = droneUseGlitchIntensity,
                chromatic = droneUseChromaticIntensity,
                colorFilter = droneUseColorFilterIntensity
            }
        };
    }

    //==================================================
    // コルーチン本体
    //==================================================
    private IEnumerator DamageEffectCoroutine(DamageType type)
    {
        DamageSettings settings = GetSettings(type);

        float elapsed = 0f;

        while (elapsed < settings.duration)
        {
            float t = elapsed / settings.duration;

            // 0 → 1 → 0 の山なり
            float intensity = Mathf.Pow(Mathf.Sin(t * Mathf.PI), settings.sharpness);

            ApplyEffects(intensity, settings);

            elapsed += Time.deltaTime;
            yield return null;
        }

        ResetEffects();
        _effectCoroutine = null;
    }

    //==================================================
    // 適用処理
    //==================================================
    private void ApplyEffects(float intensity, DamageSettings s)
    {
        // -----------------------------
        // Vignette
        // -----------------------------
        float vignetteValue = s.flags.vignette
            ? s.vignetteIntensity * intensity
            : s.vignetteIntensity;

        _vignette.color.value = s.vignetteColor;
        _vignette.intensity.value = vignetteValue;
        _vignette.smoothness.value = s.vignetteSmoothness;

        // -----------------------------
        // Desaturation
        // -----------------------------
        float saturationValue = s.flags.color
            ? -s.desaturation * intensity
            : -s.desaturation;

        _colorAdjustments.saturation.value = saturationValue;

        // -----------------------------
        // Color Filter
        // 白(Color.white)から指定色へ補間
        // -----------------------------
        float filterStrength = s.flags.colorFilter
            ? s.colorFilterStrength * intensity
            : s.colorFilterStrength;

        _colorAdjustments.colorFilter.value = Color.Lerp(Color.white, s.colorFilter, filterStrength);

        // -----------------------------
        // Grain
        // -----------------------------
        float grainValue = s.flags.grain
            ? s.grainIntensity * intensity
            : s.grainIntensity;

        _filmGrain.intensity.value = grainValue;
        _filmGrain.response.value = s.grainResponse;

        // -----------------------------
        // Glitch / Chromatic
        // -----------------------------
        float glitchValue = s.flags.glitch
            ? s.glitchIntensity * intensity
            : s.glitchIntensity;

        float chromaticValue = s.flags.chromatic
            ? s.chromaticIntensity * intensity
            : s.chromaticIntensity;

        if (glitchMaterial != null)
        {
            if (!Mathf.Approximately(_prevGlitchIntensity, glitchValue))
            {
                glitchMaterial.SetFloat("_Intensity", glitchValue);
                _prevGlitchIntensity = glitchValue;
            }

            if (!Mathf.Approximately(_prevChromaticIntensity, chromaticValue))
            {
                glitchMaterial.SetFloat("_ChromaticIntensity", chromaticValue);
                _prevChromaticIntensity = chromaticValue;
            }
        }
    }

    //==================================================
    // リセット
    //==================================================
    private void ResetEffects()
    {
        if (_vignette != null)
        {
            _vignette.intensity.value = 0f;
            _vignette.color.value = Color.black;
        }

        if (_colorAdjustments != null)
        {
            _colorAdjustments.saturation.value = 0f;
            _colorAdjustments.colorFilter.value = Color.white;
        }

        if (_filmGrain != null)
        {
            _filmGrain.intensity.value = 0f;
        }

        if (glitchMaterial != null)
        {
            glitchMaterial.SetFloat("_Intensity", 0f);
            glitchMaterial.SetFloat("_ChromaticIntensity", 0f);
        }

        _prevGlitchIntensity = -1f;
        _prevChromaticIntensity = -1f;
    }

    //==================================================
    // 後始末
    //==================================================
    private void OnDestroy()
    {
        if (_volume != null && _volume.profile != null)
        {
            Destroy(_volume.profile);
        }
    }
}