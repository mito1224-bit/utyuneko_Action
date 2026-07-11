using UnityEngine;
using UnityEngine.UI; // Sliderを使うために必要

/// <summary>
/// 全体音量・BGM・SE の3スライダーを管理するコントローラー。
/// マスター連動は「VolumeController側の掛け算」で実現しているため、
/// SoundManager が公開している SetGlobalBgmVolume / SetGlobalSeVolume に
/// 「各スライダー値 × マスター値」を渡すことで、マスターの効果を乗せています。
/// </summary>
public class VolumeController : MonoBehaviour
{
    [Header("スライダーの設定")]
    [SerializeField] private Slider globalSlider; // 全体音量（マスター）用
    [SerializeField] private Slider bgmSlider;    // BGM用
    [SerializeField] private Slider seSlider;     // SE用

    void Start()
    {
        // 1. 各スライダーの初期値を設定
        //    PlayerPrefs などで保存している場合は、ここでロードした値を入れてください。
        if (globalSlider != null) globalSlider.value = 1.0f;
        if (bgmSlider != null) bgmSlider.value = 1.0f;
        if (seSlider != null) seSlider.value = 1.0f;

        // 2. スライダー変更時のイベント（リスナー）を登録
        if (globalSlider != null) globalSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
        if (seSlider != null) seSlider.onValueChanged.AddListener(OnSeVolumeChanged);

        // 3. 起動時に現在の値を1度適用しておく
        ApplyAllVolumes();

        SoundManager.Instance.PlayBGM(BgmType.Opening, 0.5f);
    }

    /// <summary>
    /// 全体音量（マスター）スライダーが動いたとき。
    /// マスター単体を送る専用関数は SoundManager に無いため、
    /// BGM/SE をマスター込みで再計算して反映する。
    /// （BGM/SE スライダーの位置は動かない。値を読むだけ）
    /// </summary>
    private void OnMasterVolumeChanged(float value)
    {
        ApplyAllVolumes();
    }

    /// <summary>
    /// BGM音量スライダーが動いたとき。
    /// 最終音量 = BGM値 × マスター値 を SoundManager に渡す。
    /// </summary>
    private void OnBgmVolumeChanged(float value)
    {
        if (SoundManager.Instance == null) return;

        float masterValue = (globalSlider != null) ? globalSlider.value : 1f;
        SoundManager.Instance.SetGlobalBgmVolume(value * masterValue);
    }

    /// <summary>
    /// SE音量スライダーが動いたとき。
    /// 最終音量 = SE値 × マスター値 を SoundManager に渡す。
    /// </summary>
    private void OnSeVolumeChanged(float value)
    {
        if (SoundManager.Instance == null) return;

        float masterValue = (globalSlider != null) ? globalSlider.value : 1f;
        SoundManager.Instance.SetGlobalSeVolume(value * masterValue);
    }

    /// <summary>
    /// 現在のスライダー値を元に、すべての音量を再適用する。
    /// slider.value は「読む」だけなので、スライダーのつまみは動かない。
    /// </summary>
    private void ApplyAllVolumes()
    {
        if (bgmSlider != null) OnBgmVolumeChanged(bgmSlider.value);
        if (seSlider != null) OnSeVolumeChanged(seSlider.value);
    }

    void OnDestroy()
    {
        // メモリリーク防止のため、破棄時にリスナーを解除
        if (globalSlider != null) globalSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        if (bgmSlider != null) bgmSlider.onValueChanged.RemoveListener(OnBgmVolumeChanged);
        if (seSlider != null) seSlider.onValueChanged.RemoveListener(OnSeVolumeChanged);
    }
}