using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// ダメージの種類を区別するenum
public enum DamageType
{
    Player, // プレイヤー本体がダメージを受けたとき（赤・強め）
    Drone   // ドローンが身代わりになったとき（青・軽め）
}

public class PlayerDamageEffect : MonoBehaviour
{
    // ----------------------------------------------------------------
    // Glitchシェーダー
    // ----------------------------------------------------------------
    [Header("Glitch Shader")]
    [Tooltip("DamageGlitch.matをアサインしてください")]
    public Material glitchMaterial;

    // ----------------------------------------------------------------
    // プレイヤーダメージの設定
    // ----------------------------------------------------------------
    [Header("Player Damage Settings")]

    [Tooltip("エフェクト全体の長さ（秒）")]
    public float playerEffectDuration = 0.3f;

    [Tooltip("横ズレの強さ")]
    public float playerGlitchIntensity = 0.3f;

    [Tooltip("RGBにじみの強さ")]
    public float playerChromaticIntensity = 0.04f;

    [Tooltip("ビネットの色")]
    public Color playerVignetteColor = new Color(0.8f, 0f, 0f);

    [Tooltip("周辺の暗さ（0〜1）")]
    public float playerVignetteIntensity = 0.6f;

    [Tooltip("ビネットのぼかし具合（0でくっきり、1でなめらか）")]
    public float playerVignetteSmoothness = 0.5f;

    [Tooltip("白黒になる量（0〜100）")]
    public float playerDesaturation = 100f;

    [Tooltip("ノイズの強さ")]
    public float playerGrainIntensity = 0.8f;

    [Tooltip("暗い部分へのノイズの出やすさ")]
    public float playerGrainResponse = 0.8f;

    [Tooltip("カーブの鋭さ。上げると瞬間的に強く出てすぐ消える")]
    public float playerSharpness = 3f;

    // ----------------------------------------------------------------
    // ドローンダメージの設定
    // ----------------------------------------------------------------
    [Header("Drone Damage Settings")]

    [Tooltip("エフェクト全体の長さ（秒）")]
    public float droneEffectDuration = 0.2f;

    [Tooltip("横ズレの強さ")]
    public float droneGlitchIntensity = 0.15f;

    [Tooltip("RGBにじみの強さ")]
    public float droneChromaticIntensity = 0.02f;

    [Tooltip("ビネットの色")]
    public Color droneVignetteColor = new Color(0.2f, 0.7f, 1f);

    [Tooltip("周辺の暗さ（0〜1）")]
    public float droneVignetteIntensity = 0.35f;

    [Tooltip("ビネットのぼかし具合（0でくっきり、1でなめらか）")]
    public float droneVignetteSmoothness = 0.5f;

    [Tooltip("白黒になる量（0〜100）")]
    public float droneDesaturation = 30f;

    [Tooltip("ノイズの強さ")]
    public float droneGrainIntensity = 0.3f;

    [Tooltip("暗い部分へのノイズの出やすさ")]
    public float droneGrainResponse = 0.8f;

    [Tooltip("カーブの鋭さ。上げると瞬間的に強く出てすぐ消える")]
    public float droneSharpness = 4f;

    // ----------------------------------------------------------------
    // 内部変数（Inspectorには表示しない）
    // ----------------------------------------------------------------
    private Volume _volume;                       // URPのポストエフェクト制御用Volume
    private Vignette _vignette;                   // 画面周辺を暗くするビネット
    private ColorAdjustments _colorAdjustments;   // 彩度などの色調整
    private FilmGrain _filmGrain;                 // フィルムグレイン（ノイズ）
    private Coroutine _effectCoroutine;           // 実行中のエフェクトコルーチン

    // ----------------------------------------------------------------
    // 初期化
    // ----------------------------------------------------------------
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

        // Vignette（画面周辺を暗くする）
        _vignette = profile.Add<Vignette>(overrides: true);
        _vignette.color.overrideState = true;
        _vignette.intensity.overrideState = true;
        _vignette.smoothness.overrideState = true;

        // ColorAdjustments（彩度）
        _colorAdjustments = profile.Add<ColorAdjustments>(overrides: true);
        _colorAdjustments.saturation.overrideState = true;

        // FilmGrain（ノイズ）
        _filmGrain = profile.Add<FilmGrain>(overrides: true);
        _filmGrain.type.overrideState = true;
        _filmGrain.intensity.overrideState = true;
        _filmGrain.response.overrideState = true;
        _filmGrain.type.value = FilmGrainLookup.Thin1;
    }

    // ----------------------------------------------------------------
    // 外部から呼ぶ
    // ----------------------------------------------------------------

    /// <summary>
    /// ダメージエフェクトを再生する。
    /// 例: GetComponent&lt;PlayerDamageEffect&gt;().PlayDamageEffect(DamageType.Player);
    /// </summary>
    public void PlayDamageEffect(DamageType type)
    {
        if (_effectCoroutine != null)
            StopCoroutine(_effectCoroutine);

        _effectCoroutine = StartCoroutine(DamageEffectCoroutine(type));
    }

    // ----------------------------------------------------------------
    // エフェクト本体
    // ----------------------------------------------------------------
    private IEnumerator DamageEffectCoroutine(DamageType type)
    {
        float duration = (type == DamageType.Player) ? playerEffectDuration : droneEffectDuration;
        float sharpness = (type == DamageType.Player) ? playerSharpness : droneSharpness;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;

            // Sin(t*PI) で 0→1→0 の山なりカーブ
            // Pow(sharpness) で鋭さを調整（大きいほど瞬間的に強く出てすぐ消える）
            float intensity = Mathf.Pow(Mathf.Sin(t * Mathf.PI), sharpness);

            ApplyEffects(intensity, type);

            elapsed += Time.deltaTime;
            yield return null;
        }

        ResetEffects();
        _effectCoroutine = null;
    }

    private void ApplyEffects(float intensity, DamageType type)
    {
        Color vigColor = (type == DamageType.Player) ? playerVignetteColor : droneVignetteColor;
        float vigIntensity = (type == DamageType.Player) ? playerVignetteIntensity : droneVignetteIntensity;
        float vigSmoothness = (type == DamageType.Player) ? playerVignetteSmoothness : droneVignetteSmoothness;
        float desaturation = (type == DamageType.Player) ? playerDesaturation : droneDesaturation;
        float grainInt = (type == DamageType.Player) ? playerGrainIntensity : droneGrainIntensity;
        float grainRes = (type == DamageType.Player) ? playerGrainResponse : droneGrainResponse;
        float glitchInt = (type == DamageType.Player) ? playerGlitchIntensity : droneGlitchIntensity;
        float chromInt = (type == DamageType.Player) ? playerChromaticIntensity : droneChromaticIntensity;

        // Vignette
        _vignette.color.value = vigColor;
        _vignette.intensity.value = vigIntensity * intensity;
        _vignette.smoothness.value = vigSmoothness;

        // 彩度（0で通常、-100で完全に白黒）
        _colorAdjustments.saturation.value = -desaturation * intensity;

        // FilmGrain
        _filmGrain.intensity.value = grainInt * intensity;
        _filmGrain.response.value = grainRes;

        // Glitchシェーダー
        if (glitchMaterial != null)
        {
            //glitchMaterial.SetFloat("_Intensity", glitchInt * intensity);
            glitchMaterial.SetFloat("_Intensity", glitchInt);
            glitchMaterial.SetFloat("_ChromaticIntensity", chromInt);
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

    // ----------------------------------------------------------------
    // 後始末
    // ----------------------------------------------------------------
    private void OnDestroy()
    {
        if (_volume != null && _volume.profile != null)
            Destroy(_volume.profile);
    }
}