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
    None = 0,

    //【100番台】Player関連
    [InspectorName("Player/Jump")] PlayerJump = 100,
    [InspectorName("Player/Jump2")] PlayerJump2 = 101,
    [InspectorName("Player/Landing")] PlayerLanding = 102,
    [InspectorName("Player/Landing2")] PlayerLanding2 = 103,
    [InspectorName("Player/Charging")] PlayerCharging = 104,
    [InspectorName("Player/ChargingUp")] PlayerChargingUp = 105,
    [InspectorName("Player/BurstBegin")] PlayerBurstBegin = 106,
    [InspectorName("Player/WallHit")] PlayerWallHit = 107,
    [InspectorName("Player/EnemyAttackHit")] PlayerEnemyAttackHit = 108,
    [InspectorName("Player/IconPop")] PlayerIconPop = 109,
    [InspectorName("Player/AttackHit")] PlayerAttackHit = 110,
    [InspectorName("Player/Die")] PlayerDie = 111,
    [InspectorName("Player/Recovery")] PlayerRecovery = 112,
    [InspectorName("Player/PlayerConfusion")] PlayerConfusion = 113,

    [InspectorName("PlayerUI/ChargOK")] ChargOK = 150,

    //【200番台】Hosa関連
    [InspectorName("Hosa/HosaConfusion")] HosaConfusion = 200,
    [InspectorName("Hosa/Action")] HosaAction = 201,
    [InspectorName("Hosa/Suction")] HosaSuction = 202,

    //【300番台】Enemy関連
    [InspectorName("Enemy/Die")] EnemyDie = 300,
    [InspectorName("Enemy/ShieldHit")] EnemyShieldHit = 301,
    [InspectorName("Enemy/RangeAttack")] EnemyRangeAttack = 302,
    [InspectorName("Enemy/Charge")] EnemyCharge = 303,
    [InspectorName("Enemy/ChargeWallHit")] EnemyChargeWallHit = 304,
    [InspectorName("Enemy/Confusion")] EnemyConfusion = 305,
    [InspectorName("Enemy/SniperAttack")] EnemySniperAttack = 306,
    [InspectorName("Enemy/ExplosionDelay")] EnemyExplosionDelay = 307,
    [InspectorName("Enemy/Explosion")] EnemyExplosion = 308,
    [InspectorName("Enemy/Suction")] EnemySuction = 309,

    //【400番台】UI関連
    [InspectorName("UI/Select")] UiSelect = 400,
    [InspectorName("UI/Enter")] UiEnter = 401,
    [InspectorName("UI/Cancel")] UiCancel = 402,
    [InspectorName("UI/GameEnd")] UiGameEnd = 403,
    [InspectorName("UI/Pose")] UiPose = 404,
    [InspectorName("UI/PoseCancel")] UiPoseCancel = 405,

    //【500番台】Gimmick関連
    [InspectorName("Gimmick/AccelerationFloor")] GimmickAccelerationFloor = 500,
    [InspectorName("Gimmick/AccelerationWall")] GimmickAccelerationWall = 501,
    [InspectorName("Gimmick/Cannon")] GimmickCannon = 502,
    [InspectorName("Gimmick/Warp")] GimmickWarp = 503,
    [InspectorName("Gimmick/Key_Move")] GimmickKey_Move = 504,
    [InspectorName("Gimmick/key_Hold")] Gimmickkey_Hold = 505,

    //【600番台】Item関連
    [InspectorName("Item/BitGet")] ItemBitGet = 600,
    [InspectorName("Item/DataGet")] ItemDataGet = 601,
    [InspectorName("Item/CoreGet")] ItemCoreGet = 602,

    //【700番台】Stage関連
    [InspectorName("Stage/GateIn")] StageGateIn = 700,
    [InspectorName("Stage/Goal")] StageGoal = 701,

    //【800番台】Result関連
    [InspectorName("Result/ScoreUp")] ResultScoreUp = 800,
    [InspectorName("Result/DataGet")] ResultDataGet = 801,

    //【900番台】Event関連
    [InspectorName("Event/Opening")] EventOpening = 900,

    //【1000番台】その他ロゴ等
    [InspectorName("Logo/Cat")] LogoCat = 1000,
}

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("オーディオミキサー設定")]
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

    // ===================================================================
    // 🛠️【大手術】SEのピッチ上書きバグを根絶する複数スピーカー（プール）インフラ
    // ===================================================================
    [Header("単発SEの同時再生制限数")]
    [Tooltip("同時に鳴らせる単発SEの最大数（12〜16個あれば激しい戦闘でも音が途切れません）")]
    [SerializeField] private int sePoolSize = 12;

    private List<AudioSource> sePoolSources = new List<AudioSource>();
    private int sePoolIndex = 0;

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
        AudioSource baseSeSource = (sources.Length > 1) ? sources[1] : gameObject.AddComponent<AudioSource>();

        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        baseSeSource.loop = false;
        baseSeSource.playOnAwake = false;

        // ===================================================================
        // 🏗️ 起動時に、設定された数だけSE用のAudioSourceを裏側で全自動量産する
        // ===================================================================
        sePoolSources.Clear();
        for (int i = 0; i < sePoolSize; i++)
        {
            AudioSource pSource = gameObject.AddComponent<AudioSource>();
            pSource.loop = false;
            pSource.playOnAwake = false;

            // インスペクターやミキサーの設定（SEボリュームグループなど）を全自動で完全同期！
            pSource.outputAudioMixerGroup = baseSeSource.outputAudioMixerGroup;

            sePoolSources.Add(pSource);
        }
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
    // BGM 再生ロジック（既存のまま）
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

    public void FadeBGMVolume(float targetMultiplier, float duration)
    {
        if (currentPlayingBgm == null || currentPlayingBgm.clip == null) return;

        if (bgmVolumeModifyCoroutine != null) StopCoroutine(bgmVolumeModifyCoroutine);
        bgmVolumeModifyCoroutine = StartCoroutine(FadeBGMVolumeRoutine(targetMultiplier, duration));
    }

    private IEnumerator FadeBGMVolumeRoutine(float targetMultiplier, float duration)
    {
        float startVolume = bgmSource.volume;
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
    /// 🛠️【神アップデート】PlayOneShotを廃止し、プールされた独立スピーカーで再生！
    /// これにより、後からどんなピッチのSEが鳴ろうが、再生中の音が上書きされることは100%なくなります！
    /// </summary>
    public void PlaySE(SeType type, float basePitch = 1.0f, float pitchRandomness = 0.08f)
    {
        SeData data = GetSeDataFromLibrary(type);
        if (data.clip == null) return;

        if (sePoolSources.Count == 0) return;

        // 🔄 プールから、現在順番が回ってきたAudioSourceを1個つまみ上げる
        AudioSource source = sePoolSources[sePoolIndex];
        sePoolIndex = (sePoolIndex + 1) % sePoolSources.Count; // 次のためにインデックスを進める

        // 独立したAudioSourceに対してピッチとボリュームを設定して、通常の「Play()」で再生！
        source.pitch = basePitch + Random.Range(-pitchRandomness, pitchRandomness);
        source.volume = data.volume;
        source.clip = data.clip;

        source.Play(); // これで他の音のピッチを一切汚さずに個別に鳴らせます！
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

    // ===================================================================
    // ループSE 再生ロジック（既存のまま）
    // ===================================================================
    public void PlayLoopSE(GameObject owner, SeType type, float basePitch = 1.0f, float pitchRandomness = 0.0f)
    {
        if (owner == null) return;

        CleanUpMissingLoopSources();
        StopLoopSE(owner);

        SeData data = GetSeDataFromLibrary(type);
        if (data.clip == null) return;

        AudioSource newSource = owner.AddComponent<AudioSource>();
        newSource.outputAudioMixerGroup = seSourcePoolCompatible(); // 下のヘルパーを活用

        newSource.clip = data.clip;
        newSource.volume = data.volume;
        newSource.loop = true;
        newSource.spatialBlend = 1f;

        newSource.pitch = basePitch + Random.Range(-pitchRandomness, pitchRandomness);

        newSource.Play();

        activeLoopSources[owner] = newSource;
    }

    // 互換性維持のための内部ヘルパー
    private AudioMixerGroup seSourcePoolCompatible()
    {
        if (sePoolSources.Count > 0 && sePoolSources[0] != null) return sePoolSources[0].outputAudioMixerGroup;
        return null;
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