using System.Collections;
using UnityEngine;

/// <summary>
/// ボススナイパー戦のカメラ演出係（出現・強化カットイン）。
///
/// 別ボスの StageSecondBossPhaseTransitionState（正常動作している実装）と同じ手順を踏む:
///   1. 部屋のカメラ固定オブジェクト（CameraBoundsTrigger を付けたオブジェクト）を SetActive(false) にする
///   2. 寄り先に見えない一時ターゲットを作り、cameraFollow.StartTrackTarget でそこをロックオン
///      （一時ターゲットの z がそのままカメラの寄り距離になる）
///   3. シェイクやスケール等の演出を挟む
///   4. cameraFollow.ReturnToPlayerFromEvent でプレイヤーへ戻す
///   5. 一時ターゲットを破棄し、固定オブジェクトを SetActive(true) に戻す
///
/// プレイヤーの操作ロックは StartTrackTarget の内部（SetPlayerActiveState(false)）が行う。
/// ボスとお供分身の行動停止は bossSniper.SetEventPaused(true/false) が行う。
///
/// 撃破イベントのカメラは BossSniperAbsorbEventManager 側が担当し、
/// 固定オブジェクトの ON/OFF はこのクラスの静的 SuspendBoundsLock/ResumeBoundsLock を使う。
///
/// セットアップ:
///   - 空オブジェクトにこのコンポーネントを付け、bossSniper と boundsTriggerObject を割り当てる。
///     （boundsTriggerObject = ボス部屋の CameraBoundsTrigger を付けたオブジェクト）
///   - cameraFollow は未設定なら MainCamera タグの親から自動取得。
///   - StageBossSniperTrigger の cameraDirector にこのオブジェクトを割り当てる。
/// </summary>
public class BossSniperCameraDirector : MonoBehaviour
{
    // Absorbイベント等から固定オブジェクトを操作するための静的窓口
    private static BossSniperCameraDirector instance;

    /// <summary>部屋のカメラ固定オブジェクトを一時停止（SetActive(false)）。イベント開始時に呼ぶ。</summary>
    public static void SuspendBoundsLock()
    {
        if (instance != null) instance.SetBoundsObjectActive(false);
    }

    /// <summary>部屋のカメラ固定オブジェクトを再開（SetActive(true)）。イベント終了時に呼ぶ。</summary>
    public static void ResumeBoundsLock()
    {
        if (instance != null) instance.SetBoundsObjectActive(true);
    }

    [Header("参照")]
    [Tooltip("ボススナイパー本体。onEnraged / onDefeated をコード購読する")]
    [SerializeField] private BossSniper bossSniper;

    [Tooltip("カメラ制御。未設定なら MainCamera タグの親から自動取得する")]
    [SerializeField] private CameraFollowWithZoom cameraFollow;

    [Tooltip("ボス部屋のカメラ固定オブジェクト（CameraBoundsTrigger を付けたオブジェクト）。" +
             "イベント中はこれを SetActive(false) にして、カメラの寄り／ズームを邪魔しないようにする")]
    [SerializeField] private GameObject boundsTriggerObject;

    [Header("出現演出")]
    [Tooltip("寄ったときのカメラ距離（z座標。通常 -10、近いほど 0 に近づける）")]
    [SerializeField] private float introCameraZ = -7f;
    [Tooltip("カメラが寄る速さ（位置）。大きいほど速い")]
    [SerializeField] private float introPositionSpeed = 3f;
    [Tooltip("カメラが寄る速さ（ズーム）。大きいほど速い")]
    [SerializeField] private float introZoomSpeed = 2f;
    [Tooltip("寄り始めてから衝撃シェイクを出すまでの待ち時間（秒）")]
    [SerializeField] private float introApproachWait = 0.7f;
    [Tooltip("寄った状態で見せる時間（秒）")]
    [SerializeField] private float introHold = 1.2f;
    [Tooltip("プレイヤーへ戻る時間（秒）")]
    [SerializeField] private float introReturnTime = 1.0f;
    [Tooltip("寄っていく間の地鳴り（縦のみ微振動）の秒数・強さ")]
    [SerializeField] private float introRumbleDuration = 0.9f;
    [SerializeField] private float introRumbleMagnitude = 0.15f;
    [Tooltip("出現の衝撃（カメラシェイク）の秒数・強さ")]
    [SerializeField] private float introImpactDuration = 0.35f;
    [SerializeField] private float introImpactMagnitude = 0.7f;
    [Tooltip("出現の衝撃（ボスモデルの揺れ）の強さ・秒数")]
    [SerializeField] private float introBossShakeStrength = 0.15f;
    [SerializeField] private float introBossShakeDuration = 0.25f;

    [Header("強化カットイン（出現より控えめ）")]
    [Tooltip("寄ったときのカメラ距離（z座標）。出現より控えめに（-8 など、-7 より引き気味）")]
    [SerializeField] private float enrageCameraZ = -8f;
    [SerializeField] private float enragePositionSpeed = 3.5f;
    [SerializeField] private float enrageZoomSpeed = 2.5f;
    [Tooltip("寄り始めてからボスが正面を向き始めるまでの待ち時間（秒）")]
    [SerializeField] private float enrageApproachWait = 0.5f;
    [Tooltip("ボスが正面を向くのにかける時間（秒）")]
    [SerializeField] private float enrageFaceFrontTime = 0.3f;
    [Tooltip("モデルが正面（カメラ側）を向くときの Y 回転角。モデルの作りにより 90 / -90 / 0 などに調整")]
    [SerializeField] private float enrageFrontYAngle = 90f;
    [Tooltip("正面を向いてからのタメ時間（秒）。この間にシェイクが走る")]
    [SerializeField] private float enrageHoldTime = 0.45f;
    [Tooltip("元の向きへ戻す時間（秒）")]
    [SerializeField] private float enrageFaceBackTime = 0.25f;
    [Tooltip("プレイヤーへ戻る時間（秒）")]
    [SerializeField] private float enrageReturnTime = 0.7f;
    [Tooltip("覚醒の瞬間のカメラシェイク（秒数・強さ）")]
    [SerializeField] private float enrageShakeDuration = 0.35f;
    [SerializeField] private float enrageShakeMagnitude = 0.6f;
    [Tooltip("覚醒の瞬間のボスモデルの揺れ（強さ・秒数）")]
    [SerializeField] private float enrageBossShakeStrength = 0.25f;
    [SerializeField] private float enrageBossShakeDuration = 0.4f;

    private BossSniperShake bossShake;
    private Coroutine playingRoutine;
    private GameObject tempCameraTarget;

    void Awake()
    {
        instance = this;

        if (cameraFollow == null)
        {
            GameObject mainCam = GameObject.FindWithTag("MainCamera");
            if (mainCam != null) cameraFollow = mainCam.GetComponentInParent<CameraFollowWithZoom>();
            if (cameraFollow == null) cameraFollow = Object.FindFirstObjectByType<CameraFollowWithZoom>();
        }

        if (bossSniper != null) bossShake = bossSniper.GetComponent<BossSniperShake>();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
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

    private void SetBoundsObjectActive(bool active)
    {
        if (boundsTriggerObject != null) boundsTriggerObject.SetActive(active);
    }

    // イベント後に部屋の固定点へ Z ごと確実に戻す。
    // CameraBoundsTrigger の再ロックは OnTriggerStay2D 頼みだが、プレイヤーが静止して
    // Rigidbody2D がスリープしていると発火せず、寄せた Z(-7/-8)が残ってしまう。
    // そこで CameraBoundsTrigger と同じ固定点・targetZOffset を読んで、ここで一度だけ
    // LockCamera を直接呼び、Z を部屋の値(-15等)へ戻す。
    private void RelockToBoundsPoint()
    {
        if (cameraFollow == null || boundsTriggerObject == null) return;

        // 固定点：子 "CameraPoint" があればその位置、無ければコライダー中心、それも無ければ本体位置
        Vector3 lockPos;
        Transform camPoint = boundsTriggerObject.transform.Find("CameraPoint");
        if (camPoint != null)
        {
            lockPos = camPoint.position;
        }
        else if (boundsTriggerObject.TryGetComponent<Collider2D>(out var col))
        {
            lockPos = col.bounds.center;
        }
        else
        {
            lockPos = boundsTriggerObject.transform.position;
        }

        // targetZOffset（引き量）は CameraBoundsTrigger から読む
        float z = -40f;
        if (boundsTriggerObject.TryGetComponent<CameraBoundsTrigger>(out var bounds))
        {
            z = bounds.targetZOffset;
        }

        cameraFollow.LockCamera(lockPos, z);
    }

    // 寄り先の一時ターゲットを作る（z が寄り距離になる）
    private void CreateTempTarget(Vector3 worldPos, float cameraZ)
    {
        DestroyTempTarget();
        tempCameraTarget = new GameObject("TempCameraEventTarget");
        tempCameraTarget.transform.position = new Vector3(worldPos.x, worldPos.y, cameraZ);
    }

    private void DestroyTempTarget()
    {
        if (tempCameraTarget != null)
        {
            Object.Destroy(tempCameraTarget);
            tempCameraTarget = null;
        }
    }

    // ─── 出現演出 ─────────────────────────────

    /// <summary>出現演出を再生する（StageBossSniperTrigger からボス有効化の直後に呼ぶ）。</summary>
    public void PlayIntro()
    {
        Debug.Log("[CameraDirector] PlayIntro 開始");
        if (playingRoutine != null) return;
        if (bossSniper == null || cameraFollow == null) return;
        playingRoutine = StartCoroutine(IntroRoutine());
    }

    private IEnumerator IntroRoutine()
    {
        Debug.Log("[CameraDirector] IntroRoutine 開始");

        SetBoundsObjectActive(false);
        // 攻撃・テレポートは止める（出現アニメのスケールは別で動き続ける）
        bossSniper.SetEventPaused(true);

        // ① 部屋の固定カメラをオフ → ② 寄り先ターゲット作成 → StartTrackTarget で寄る
        
        CreateTempTarget(bossSniper.transform.position, introCameraZ);
        cameraFollow.StartTrackTarget(tempCameraTarget.transform, introPositionSpeed, introZoomSpeed);

        // 寄っていく間の地鳴り（縦のみ微振動）
        if (ShakeTarget.Instance != null)
            ShakeTarget.Instance.Shake(introRumbleDuration, introRumbleMagnitude, 25f, false);

        yield return new WaitForSeconds(introApproachWait);

        // 出現の衝撃
        if (ShakeTarget.Instance != null)
            ShakeTarget.Instance.Shake(introImpactDuration, introImpactMagnitude, 15f);
        if (bossShake != null)
            bossShake.ShakeRandom(introBossShakeStrength, introBossShakeDuration);

        // 寄った状態で見せる
        yield return new WaitForSeconds(introHold);

        // プレイヤーへ戻す → 戻り切ってから固定カメラを復活
        cameraFollow.ReturnToPlayerFromEvent(introReturnTime);
        yield return new WaitForSeconds(introReturnTime + 0.05f);

        DestroyTempTarget();
        
        //RelockToBoundsPoint(); // スリープで OnTriggerStay が発火しなくても Z(-15等)へ確実に戻す
        bossSniper.SetEventPaused(false);
        playingRoutine = null;
        SetBoundsObjectActive(true);
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
        SetBoundsObjectActive(false);
        // スタン一撃などの全体スロー（timeScale低下）が明けてから始める
        yield return new WaitUntil(() => Time.timeScale >= 0.99f);

        // 攻撃・テレポートを止める（凍った射線も消える）
        bossSniper.SetEventPaused(true);

        // ① 部屋の固定カメラをオフ → ② 寄り先ターゲット → 寄る
        
        CreateTempTarget(bossSniper.transform.position, enrageCameraZ);
        cameraFollow.StartTrackTarget(tempCameraTarget.transform, enragePositionSpeed, enrageZoomSpeed);

        yield return new WaitForSeconds(enrageApproachWait);

        // ボスが正面を向く
        Transform visual = bossSniper.SelfUnit != null ? bossSniper.SelfUnit.visualTransform : null;
        Quaternion originalRot = visual != null ? visual.localRotation : Quaternion.identity;
        Quaternion frontRot = Quaternion.Euler(0f, enrageFrontYAngle, 0f);
        yield return RotateVisual(visual, originalRot, frontRot, enrageFaceFrontTime);

        // 覚醒の瞬間：ボスの揺れ＋カメラシェイク
        if (bossShake != null)
            bossShake.ShakeRandom(enrageBossShakeStrength, enrageBossShakeDuration);
        if (ShakeTarget.Instance != null)
            ShakeTarget.Instance.Shake(enrageShakeDuration, enrageShakeMagnitude, 12f);

        yield return new WaitForSeconds(enrageHoldTime);

        // 向き直してから帰還
        yield return RotateVisual(visual, frontRot, originalRot, enrageFaceBackTime);

        cameraFollow.ReturnToPlayerFromEvent(enrageReturnTime);
        yield return new WaitForSeconds(enrageReturnTime + 0.05f);

        DestroyTempTarget();
        
        RelockToBoundsPoint(); // スリープで OnTriggerStay が発火しなくても Z(-15等)へ確実に戻す
        bossSniper.SetEventPaused(false);
        playingRoutine = null;
        SetBoundsObjectActive(true);
    }

    // 見た目を from → to へ滑らかに回す
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
    /// 実行中のカットインを打ち切って後始末する（撃破時に onDefeated 経由で自動で呼ばれる）。
    /// カメラ本体の後始末（ReturnToPlayerFromEvent / ForceStop）は撃破イベント側が行うので、
    /// ここでは一時ターゲットの破棄・固定カメラの復活・ボスの再開だけを確実に済ませる。
    /// （撃破イベント側は直後に改めて SuspendBoundsLock するので二重でも問題ない）
    /// </summary>
    public void CancelCutscenes()
    {
        if (playingRoutine != null)
        {
            StopCoroutine(playingRoutine);
            playingRoutine = null;
        }

        DestroyTempTarget();
        
        if (bossSniper != null) bossSniper.SetEventPaused(false);
        SetBoundsObjectActive(true);
    }
}