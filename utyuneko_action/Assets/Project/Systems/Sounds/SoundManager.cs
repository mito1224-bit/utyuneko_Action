using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio; // Audio Mixerの操作に必要

// ===================================================================
// BGMの種類を管理する列挙型
// ===================================================================
public enum BgmType
{
    None,
    Opening,
    RescueEvent,
    StageSelect,
    Stage1,
    Stage2,
    Stage3,
    BossBattle
}

// ===================================================================
// SEの種類を管理する列挙型
// ===================================================================
public enum SeType
{
    None,
    [InspectorName("Player/Jump")] PlayerJump,
    [InspectorName("Player/Jump2")] PlayerJump2,
    [InspectorName("Player/Landing")] PlayerLanding,
    [InspectorName("Player/Landing2")] PlayerLanding2,
    [InspectorName("Player/Charging")] PlayerCharging,
    [InspectorName("Player/ChargingUp")] PlayerChargingUp,
    [InspectorName("Player/BurstBegin")] PlayerBurstBegin,
    [InspectorName("Player/WallHit")] PlayerWallHit,
    [InspectorName("Player/EnemyAttackHit")] PlayerEnemyAttackHit,
    [InspectorName("Player/IconPop")] PlayerIconPop,
    [InspectorName("Player/AttackHit")] PlayerAttackHit,
    [InspectorName("Player/Die")] PlayerDie,
    [InspectorName("Player/Recovery")] PlayerRecovery,
    [InspectorName("Player/PlayerConfusion")] PlayerConfusion,

    [InspectorName("PlayerUI/ChargOK")] ChargOK,

    [InspectorName("Hosa/HosaConfusion")] HosaConfusion,
    [InspectorName("Hosa/Action")] HosaAction,
    [InspectorName("Hosa/Suction")] HosaSuction,

    [InspectorName("Enemy/Die")] EnemyDie,
    [InspectorName("Enemy/ShieldHit")] EnemyShieldHit,
    [InspectorName("Enemy/RangeAttack")] EnemyRangeAttack,
    [InspectorName("Enemy/Charge")] EnemyCharge,
    [InspectorName("Enemy/ChargeWallHit")] EnemyChargeWallHit,
    [InspectorName("Enemy/Confusion")] EnemyConfusion,
    [InspectorName("Enemy/SniperAttack")] EnemySniperAttack,
    [InspectorName("Enemy/ExplosionDelay")] EnemyExplosionDelay,
    [InspectorName("Enemy/Explosion")] EnemyExplosion,
    [InspectorName("Enemy/Suction")] EnemySuction,

    [InspectorName("UI/Select")] UiSelect,
    [InspectorName("UI/Enter")] UiEnter,
    [InspectorName("UI/Cancel")] UiCancel,
    [InspectorName("UI/GameEnd")] UiGameEnd,
    [InspectorName("UI/Pose")] UiPose,
    [InspectorName("UI/PoseCancel")] UiPoseCancel,

    [InspectorName("Gimmick/AccelerationFloor")] GimmickAccelerationFloor,
    [InspectorName("Gimmick/AccelerationWall")] GimmickAccelerationWall,
    [InspectorName("Gimmick/Cannon")] GimmickCannon,
    [InspectorName("Gimmick/Warp")] GimmickWarp,
    [InspectorName("Gimmick/Key_Move")] GimmickKey_Move,
    [InspectorName("Gimmick/key_Hold")] Gimmickkey_Hold,

    [InspectorName("Item/BitGet")] ItemBitGet,
    [InspectorName("Item/DataGet")] ItemDataGet,
    [InspectorName("Item/CoreGet")] ItemCoreGet,

    [InspectorName("Stage/GateIn")] StageGateIn,
    [InspectorName("Stage/Goal")] StageGoal,

    [InspectorName("Result/ScoreUp")] ResultScoreUp,
    [InspectorName("Result/DataGet")] ResultDataGet,

    [InspectorName("Event/Opening")] EventOpening,

    [InspectorName("Logo/Cat")] LogoCat,
}

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("オーディオリキサー設定")]
    [Tooltip("MainMixer アセットをここにドラッグ＆ドロップ！")]
    [SerializeField] private AudioMixer mainMixer;

    [Header("BGM設定")]
    [SerializeField] private List<BgmData> bgmLibrary = new List<BgmData>();
    private AudioSource bgmSource;
    private Coroutine bgmFadeCoroutine;
    private Coroutine bgmVolumeModifyCoroutine;
    private BgmData currentPlayingBgm;

    [Header("SE設定（オーディオバンク方式）")]
    [Tooltip("作成した各オーディオバンク（Player用、Enemy用など）をここに登録する")]
    [SerializeField] private List<AudioBank> audioBanks = new List<AudioBank>();
    private AudioSource seSource;

    private Dictionary<GameObject, AudioSource> activeLoopSources = new Dictionary<GameObject, AudioSource>();

    [System.Serializable]
    public class BgmData
    {
        public BgmType bgmType;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [System.Serializable]
    public class SeData
    {
        public SeType seType;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        AudioSource[] sources = GetComponents<AudioSource>();

        bgmSource = (sources.Length > 0) ? sources[0] : gameObject.AddComponent<AudioSource>();
        seSource = (sources.Length > 1) ? sources[1] : gameObject.AddComponent<AudioSource>();

        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        seSource.loop = false;
        seSource.playOnAwake = false;
    }

    public void SetGlobalBgmVolume(float volume)
    {
        float dbValue = LinearToDecibel(volume);
        mainMixer.SetFloat("BGMVolume", dbValue);
    }

    public void SetGlobalSeVolume(float volume)
    {
        float dbValue = LinearToDecibel(volume);
        mainMixer.SetFloat("SEVolume", dbValue);
    }

    private float LinearToDecibel(float linear)
    {
        if (linear <= 0f) return -80f;
        return Mathf.Log10(Mathf.Clamp01(linear)) * 20f;
    }

    // ===================================================================
    // BGM 再生ロジック
    // ===================================================================
    public void PlayBGM(BgmType type, float fadeDuration = 0.5f)
    {
        if (type == BgmType.None) { StopBGM(fadeDuration); return; }

        BgmData targetData = GetBgmDataFromLibrary(type);
        if (targetData.clip == null) return;
        if (bgmSource.clip == targetData.clip && bgmSource.isPlaying) return;

        if (bgmFadeCoroutine != null) StopCoroutine(bgmFadeCoroutine);
        currentPlayingBgm = targetData;
        bgmFadeCoroutine = StartCoroutine(FadeBgmRoutine(targetData, fadeDuration));
    }

    public void StopBGM(float fadeDuration = 0.5f)
    {
        if (bgmFadeCoroutine != null) StopCoroutine(bgmFadeCoroutine);
        BgmData emptyData = new BgmData { clip = null, volume = 0f };
        bgmFadeCoroutine = StartCoroutine(FadeBgmRoutine(emptyData, fadeDuration));
    }

    private IEnumerator FadeBgmRoutine(BgmData nextBgm, float duration)
    {
        float startVolume = bgmSource.volume;

        if (bgmSource.isPlaying && duration > 0f)
        {
            while (bgmSource.volume > 0f)
            {
                bgmSource.volume -= (startVolume / duration) * Time.unscaledDeltaTime;
                yield return null;
            }
            bgmSource.Stop();
        }

        if (nextBgm.clip == null) yield break;

        bgmSource.clip = nextBgm.clip;
        bgmSource.volume = 0f;
        bgmSource.Play();

        float targetVolume = nextBgm.volume;

        if (duration > 0f)
        {
            while (bgmSource.volume < targetVolume)
            {
                bgmSource.volume += (targetVolume / duration) * Time.unscaledDeltaTime;
                yield return null;
            }
        }
        bgmSource.volume = targetVolume;
        bgmFadeCoroutine = null;
    }

    /// <summary>
    /// 現在流れているBGMの音量を、イベント演出用に指定時間で下げる/戻す関数
    /// </summary>
    /// <param name="targetMultiplier">元の音量に対する倍率（0.0で無音、0.3で元の30%の音量、1.0で通常に戻る）</param>
    /// <param name="duration">音量変化にかける時間（秒）</param>
    public void FadeBGMVolume(float targetMultiplier, float duration)
    {
        if (currentPlayingBgm == null || currentPlayingBgm.clip == null) return;

        if (bgmVolumeModifyCoroutine != null) StopCoroutine(bgmVolumeModifyCoroutine);
        bgmVolumeModifyCoroutine = StartCoroutine(FadeBGMVolumeRoutine(targetMultiplier, duration));
    }

    private IEnumerator FadeBGMVolumeRoutine(float targetMultiplier, float duration)
    {
        float startVolume = bgmSource.volume;
        // アセット自体が持つ本来の音量に対して掛け算する（グローバル設定を壊さないため）
        float targetVolume = currentPlayingBgm.volume * Mathf.Clamp01(targetMultiplier);
        float elapsed = 0f;

        if (duration > 0f)
        {
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / duration);
                yield return null;
            }
        }
        bgmSource.volume = targetVolume;
        bgmVolumeModifyCoroutine = null;
    }

    // ===================================================================
    // SE 再生ロジック
    // ===================================================================

    /// <summary>
    /// SEを一発再生する（ピッチの基準値と、そこからのランダムな揺れ幅を指定可能）
    /// </summary>
    /// <param name="type">SEの種類</param>
    /// <param name="basePitch">基準となる音程（1.0が標準、1.2で高く、0.8で低く鳴る）</param>
    /// <param name="pitchRandomness">鳴るたびにズレるランダム幅（デフォルトは0.08の微小な揺れ）</param>
    public void PlaySE(SeType type, float basePitch = 1.0f, float pitchRandomness = 0.08f)
    {
        SeData data = GetSeDataFromLibrary(type);
        if (data.clip == null) return;

        // 指定されたベースピッチを中心に、指定された幅でランダム化
        seSource.pitch = basePitch + Random.Range(-pitchRandomness, pitchRandomness);
        seSource.PlayOneShot(data.clip, data.volume);
    }

    private BgmData GetBgmDataFromLibrary(BgmType type)
    {
        foreach (var data in bgmLibrary) if (data.bgmType == type) return data;
        return new BgmData { clip = null, volume = 1f };
    }

    private SeData GetSeDataFromLibrary(SeType type)
    {
        foreach (var bank in audioBanks)
        {
            if (bank == null) continue;
            foreach (var data in bank.seList)
            {
                if (data.seType == type) return data;
            }
        }
        return new SeData { clip = null, volume = 1f };
    }

    /// <summary>
    /// その場にループ用スピーカーを自動生成して再生する（ピッチ変更・ランダム対応版）
    /// </summary>
    /// <param name="owner">鳴らしたいオブジェクト自身（this.gameObject を入れる）</param>
    /// <param name="type">SEの種類</param>
    /// <param name="basePitch">基準となる音程（1.0が標準）</param>
    /// <param name="pitchRandomness">再生時のランダム幅（ループ音は違和感が出やすいため、デフォルトは0.0の固定）</param>
    public void PlayLoopSE(GameObject owner, SeType type, float basePitch = 1.0f, float pitchRandomness = 0.0f)
    {
        if (owner == null) return;

        CleanUpMissingLoopSources();
        StopLoopSE(owner);

        SeData data = GetSeDataFromLibrary(type);
        if (data.clip == null) return;

        AudioSource newSource = owner.AddComponent<AudioSource>();
        newSource.outputAudioMixerGroup = seSource.outputAudioMixerGroup;

        newSource.clip = data.clip;
        newSource.volume = data.volume;
        newSource.loop = true;
        newSource.spatialBlend = 1f;

        newSource.pitch = basePitch + Random.Range(-pitchRandomness, pitchRandomness);

        newSource.Play();

        activeLoopSources[owner] = newSource;
    }

    public void StopLoopSE(GameObject owner)
    {
        if (owner == null) return;

        if (activeLoopSources.TryGetValue(owner, out AudioSource source))
        {
            if (source != null)
            {
                source.Stop();
                Destroy(source);
            }
            activeLoopSources.Remove(owner);
        }
    }

    private void CleanUpMissingLoopSources()
    {
        List<GameObject> deadKeys = new List<GameObject>();
        foreach (var kvp in activeLoopSources)
        {
            if (kvp.Key == null) deadKeys.Add(kvp.Value ? kvp.Value.gameObject : null);
        }

        foreach (var key in deadKeys)
        {
            if (key != null) activeLoopSources.Remove(key);
        }
    }
}