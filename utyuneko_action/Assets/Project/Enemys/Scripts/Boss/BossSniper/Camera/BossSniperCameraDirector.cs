using System.Collections;
using UnityEngine;

/// <summary>
/// ボススナイパー戦のカメラ演出係（出現・強化カットイン）。
///
/// 担当:
///   - 出現演出: StageBossSniperTrigger から PlayIntro() を呼ぶ。
///     ボスは即時出現し、カメラが地鳴りとともにボスへ寄る → 出現の衝撃シェイク → プレイヤーへ帰還。
///   - 強化カットイン: BossSniper.onEnraged をコード購読して自動再生。
///     全体スロー明けを待つ → ボスへ寄る → ボスが正面を向く → ボスシェイク＋カメラシェイク → 向き直して帰還。
///
/// どちらの演出中も BossSniper.SetEventPaused(true) でボスとお供分身の行動を止める
/// （操作できないプレイヤーが撃たれないように）。プレイヤーの操作ロック自体は
/// CameraFollowWithZoom のイベントAPI（PlayZoomEvent / StartZoomTrack）が自動で行う。
///
/// 撃破イベントのカメラは BossSniperAbsorbEventManager 側が担当する。
/// 撃破時はこのコンポーネントの CancelCutscenes() が呼ばれ（onDefeated 購読）、
/// 実行中のカットインを打ち切ってポーズを解除する。
///
/// セットアップ:
///   - シーンに空のオブジェクトを作ってこのコンポーネントを付け、bossSniper を割り当てる
///     （ボスが開始時に非アクティブでも参照経由の購読は問題なく効く）。
///   - cameraFollow は未設定なら MainCamera タグの親から自動取得する。
///   - StageBossSniperTrigger の cameraDirector にこのオブジェクトを割り当てる。
/// </summary>
public class BossSniperCameraDirector : MonoBehaviour
{
    /// <summary>
    /// イベントカメラ（出現・強化・撃破の寄り／ズーム）が作動中かどうか。
    /// BossSniperCameraBoundsTrigger はこれが立っている間、部屋のカメラ固定を控えて
    /// イベントカメラに制御を譲る。撃破イベント（AbsorbEventManager）もこのフラグを使う。
    /// </summary>
    public static bool EventCameraActive { get; set; }

    [Header("参照")]
    [Tooltip("ボススナイパー本体。onEnraged / onDefeated をコード購読する")]
    [SerializeField] private BossSniper bossSniper;

    [Tooltip("カメラ制御。未設定なら MainCamera タグの親から自動取得する")]
    [SerializeField] private CameraFollowWithZoom cameraFollow;

    [Header("出現演出")]
    [Tooltip("寄ったときのカメラ距離（Z座標。通常は -10、近いほど 0 に近づける）")]
    [SerializeField] private float introZ = -7f;

    [Tooltip("ボスへ寄るのにかける時間（秒）")]
    [SerializeField] private float introTimeToTarget = 1.0f;

    [Tooltip("ボス位置で静止する時間（秒）")]
    [SerializeField] private float introFreeze = 1.2f;

    [Tooltip("プレイヤーへ戻るのにかける時間（秒）")]
    [SerializeField] private float introReturn = 1.0f;

    [Tooltip("寄っていく間の地鳴り（縦のみ微振動）の秒数")]
    [SerializeField] private float introRumbleDuration = 0.9f;

    [Tooltip("地鳴りの強さ")]
    [SerializeField] private float introRumbleMagnitude = 0.15f;

    [Tooltip("ボス出現の衝撃シェイク（カメラ）の秒数と強さ")]
    [SerializeField] private float introImpactDuration = 0.3f;
    [SerializeField] private float introImpactMagnitude = 0.55f;

    [Tooltip("ボス出現の衝撃（ボスモデルの揺れ）の強さと秒数")]
    [SerializeField] private float introBossShakeStrength = 0.15f;
    [SerializeField] private float introBossShakeDuration = 0.25f;

    [Header("強化カットイン（出現より控えめ）")]
    [Tooltip("寄ったときのカメラ距離（Z座標）")]
    [SerializeField] private float enrageZ = -7f;

    [Tooltip("ボスへ寄るのにかける時間（秒）")]
    [SerializeField] private float enrageTimeToTarget = 0.5f;

    [Tooltip("ボスが正面を向くのにかける時間（秒）")]
    [SerializeField] private float enrageFaceFrontTime = 0.3f;

    [Tooltip("モデルが正面（カメラ側）を向くときの Y 回転角。モデルの作りによって 90 / -90 / 0 などに調整する")]
    [SerializeField] private float enrageFrontYAngle = 90f;

    [Tooltip("正面を向いてからのタメ時間（秒）。この間にシェイクが走る")]
    [SerializeField] private float enrageHoldTime = 0.45f;

    [Tooltip("元の向きへ戻すのにかける時間（秒）")]
    [SerializeField] private float enrageFaceBackTime = 0.25f;

    [Tooltip("カメラがプレイヤーへ戻るのにかける時間（秒）")]
    [SerializeField] private float enrageReturnTime = 0.7f;

    [Tooltip("覚醒の瞬間のカメラシェイク（秒数・強さ・細かさ）")]
    [SerializeField] private float enrageShakeDuration = 0.35f;
    [SerializeField] private float enrageShakeMagnitude = 0.7f;
    [SerializeField] private float enrageShakeFrequency = 12f;

    [Tooltip("覚醒の瞬間のボスモデルの揺れ（強さ・秒数）")]
    [SerializeField] private float enrageBossShakeStrength = 0.25f;
    [SerializeField] private float enrageBossShakeDuration = 0.4f;

    private BossSniperShake bossShake;
    private Coroutine playingRoutine;

    void Awake()
    {
        if (cameraFollow == null)
        {
            GameObject mainCam = GameObject.FindWithTag("MainCamera");
            if (mainCam != null) cameraFollow = mainCam.GetComponentInParent<CameraFollowWithZoom>();
            if (cameraFollow == null) cameraFollow = Object.FindFirstObjectByType<CameraFollowWithZoom>();
        }

        if (bossSniper != null) bossShake = bossSniper.GetComponent<BossSniperShake>();
    }

    void OnEnable()
    {
        if (bossSniper != null)
        {
            bossSniper.onEnraged.AddListener(PlayEnrageCutin);
            bossSniper.onDefeated.AddListener(CancelCutscenes);
        }
        else
        {
            Debug.LogWarning("[CameraDirector] bossSniper が未割り当てです。インスペクターでボス本体を割り当ててください。");
        }
    }

    void OnDisable()
    {
        if (bossSniper != null)
        {
            bossSniper.onEnraged.RemoveListener(PlayEnrageCutin);
            bossSniper.onDefeated.RemoveListener(CancelCutscenes);
        }
    }

    // ─── 出現演出 ─────────────────────────────

    /// <summary>
    /// 出現演出を再生する（StageBossSniperTrigger からボス有効化の直後に呼ぶ）。
    /// ボスは即時出現し、展開アニメは動いたまま、攻撃行動だけを演出の間停止する。
    /// </summary>
    public void PlayIntro()
    {
        if (playingRoutine != null) return;
        if (bossSniper == null || cameraFollow == null) return;

        playingRoutine = StartCoroutine(IntroRoutine());
    }

    private IEnumerator IntroRoutine()
    {
        // 出現アニメ（収縮→展開）は動くが、攻撃・テレポートは止める
        bossSniper.SetEventPaused(true);
        EventCameraActive = true; // 部屋のカメラ固定（バウンドトリガー）に譲ってもらう

        // 地鳴り（縦のみの微振動）を敷きながらボスへ寄る。
        // この間のプレイヤー操作ロックは PlayZoomEvent が自動で行う
        if (ShakeTarget.Instance != null)
        {
            ShakeTarget.Instance.Shake(introRumbleDuration, introRumbleMagnitude, 25f, false);
        }

        cameraFollow.PlayZoomEvent(bossSniper.transform, introZ, introTimeToTarget, introFreeze, introReturn);

        // カメラ到着＝ボスの展開が見える頃に、出現の衝撃を1発
        yield return new WaitForSeconds(introTimeToTarget + 0.1f);

        if (ShakeTarget.Instance != null)
        {
            ShakeTarget.Instance.Shake(introImpactDuration, introImpactMagnitude, 15f);
        }
        if (bossShake != null)
        {
            bossShake.ShakeRandom(introBossShakeStrength, introBossShakeDuration);
        }

        // 静止が終わって帰還が始まるタイミングでフラグを下ろす。
        // バウンドトリガーが部屋の固定点へロックし直し、カメラは滑らかにそこへ戻っていく
        yield return new WaitForSeconds(Mathf.Max(0f, introFreeze - 0.1f));
        EventCameraActive = false;

        yield return new WaitForSeconds(introReturn);

        bossSniper.SetEventPaused(false);
        playingRoutine = null;
    }

    // ─── 強化カットイン ────────────────────────

    /// <summary>強化カットインを再生する（onEnraged から自動で呼ばれる）。</summary>
    public void PlayEnrageCutin()
    {
        if (playingRoutine != null) return;
        if (bossSniper == null || cameraFollow == null) return;

        playingRoutine = StartCoroutine(EnrageRoutine());
    }

    private IEnumerator EnrageRoutine()
    {
        // スタン一撃などの全体スロー（timeScale低下）が明けるまで待ってから始める
        yield return new WaitUntil(() => Time.timeScale >= 0.99f);
        yield return new WaitForSecondsRealtime(0.05f);

        // ボスとお供分身の行動を止める（凍った射線も消える）
        bossSniper.SetEventPaused(true);
        EventCameraActive = true; // 部屋のカメラ固定（バウンドトリガー）に譲ってもらう

        // ボスへ寄る（プレイヤー操作は StartZoomTrack が自動でロック）
        cameraFollow.StartZoomTrack(bossSniper.transform, enrageZ, enrageTimeToTarget);
        yield return new WaitForSeconds(enrageTimeToTarget);

        // ボスが正面を向く
        Transform visual = bossSniper.SelfUnit != null ? bossSniper.SelfUnit.visualTransform : null;
        Quaternion originalRot = visual != null ? visual.localRotation : Quaternion.identity;
        Quaternion frontRot = Quaternion.Euler(0f, enrageFrontYAngle, 0f);

        yield return RotateVisual(visual, originalRot, frontRot, enrageFaceFrontTime);

        // 覚醒の瞬間：ボスモデルの揺れ＋カメラシェイク
        if (bossShake != null)
        {
            bossShake.ShakeRandom(enrageBossShakeStrength, enrageBossShakeDuration);
        }
        if (ShakeTarget.Instance != null)
        {
            ShakeTarget.Instance.Shake(enrageShakeDuration, enrageShakeMagnitude, enrageShakeFrequency);
        }

        yield return new WaitForSeconds(enrageHoldTime);

        // 向き直してから帰還。フラグを下ろすと部屋の固定点へ滑らかに戻っていく
        yield return RotateVisual(visual, frontRot, originalRot, enrageFaceBackTime);

        cameraFollow.ReturnFromZoomEvent(enrageReturnTime);
        EventCameraActive = false;
        yield return new WaitForSeconds(enrageReturnTime);

        bossSniper.SetEventPaused(false);
        playingRoutine = null;
    }

    // 見た目を from → to へ滑らかに回す（イージング付き）
    private IEnumerator RotateVisual(Transform visual, Quaternion from, Quaternion to, float duration)
    {
        if (visual == null || duration <= 0f)
        {
            if (visual != null) visual.localRotation = to;
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            visual.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            yield return null;
        }
        visual.localRotation = to;
    }

    // ─── 打ち切り ─────────────────────────────

    /// <summary>
    /// 実行中のカットインを打ち切り、ボスのポーズを解除する
    /// （撃破時に onDefeated 経由で自動で呼ばれる。撃破イベント側からの明示呼び出しにも対応）。
    /// カメラ自体の後始末は、撃破イベント側の ForceStopEventCameraWork() が行う。
    /// </summary>
    public void CancelCutscenes()
    {
        if (playingRoutine != null)
        {
            StopCoroutine(playingRoutine);
            playingRoutine = null;
        }

        EventCameraActive = false; // 撃破イベント側が使う場合は、向こうで改めて立て直す
        if (bossSniper != null) bossSniper.SetEventPaused(false);
    }
}