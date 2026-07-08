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
///   4. ForceStopEventCameraWork でイベント状態を畳み、部屋の固定点へロックし直す
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



    /// <summary>

    /// 部屋の固定点（CameraPoint / targetZOffset）へカメラのロックを直接掛け直す静的窓口。

    /// トリガーの再発火（Enter/Stay）に依存せず確実に部屋の構図へ戻せる。

    /// 撃破イベント（BossSniperAbsorbEventManager）の終了処理からも呼ぶ。

    /// </summary>

    public static void RelockRoomCamera()

    {

        if (instance != null) instance.RelockToBoundsPoint();

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



    [Header("強化：テレポート")]

    [Tooltip("中央へ／巡回地点へテレポートするときの収縮時間（秒）。未指定ならボス本体の値を使う")]

    [SerializeField] private float enrageTeleportShrinkTime = 0.15f;

    [Tooltip("テレポートの展開時間（秒）")]

    [SerializeField] private float enrageTeleportExpandTime = 0.15f;

    [Tooltip("演出が終わってカメラが戻ってから、行動を再開するまでの待ち時間（秒）")]

    [SerializeField] private float enrageResumeDelay = 0.6f;



    private BossSniperShake bossShake;

    private Coroutine playingRoutine;

    private GameObject tempCameraTarget;

    private readonly BossSniperTeleport enrageTeleport = new BossSniperTeleport();



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

    // Rigidbody2D がスリープしていると発火せず、寄せた Z が残ってしまう。

    // そこで CameraBoundsTrigger と同じ固定点・targetZOffset を読んで、ここで一度だけ

    // LockCamera を直接呼び、Z を部屋の値へ戻す。

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



        // ロック位置に紛れ込む z（CameraBoundsTrigger オブジェクト自身の z）は使わない。

        // 奥行き（引き量）は必ず z に統一する

        lockPos.z = z;

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

        if (playingRoutine != null) return;

        if (bossSniper == null || cameraFollow == null) return;

        playingRoutine = StartCoroutine(IntroRoutine());

    }



    private IEnumerator IntroRoutine()

    {

        // 部屋のカメラ固定をオフにして、イベントカメラに道を譲る

        SetBoundsObjectActive(false);

        // 攻撃・テレポートは止める（出現アニメのスケールは別で動き続ける）

        bossSniper.SetEventPaused(true);



        // 寄り先ターゲットを作成 → StartTrackTarget で寄る

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



        // ── 終了（アンロックを一切挟まない最小構成）──

        // ① まず部屋トリガーを復活（内部の Enter/Stay ロックは無視されても構わない。決定打は③）

        SetBoundsObjectActive(true);



        // ② イベント状態だけを畳む（プレイヤー解放・速度復元・isEventWorking=false）。

        //    ForceStop はアンロック＆プレイヤー位置へのワープを伴うが、

        //    "同じフレーム内で" 直後に③のロックへ差し替えるので追従計算は 1 フレームも走らない

        cameraFollow.ForceStopEventCameraWork();



        // ③ 部屋の固定点（XY）＋引き量（z）へロックし直す。これで z が確実に戻る

        RelockToBoundsPoint();



        DestroyTempTarget();

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

        SetBoundsObjectActive(false);

        // スタン一撃などの全体スロー（timeScale低下）が明けてから始める

        yield return new WaitUntil(() => Time.timeScale >= 0.99f);



        // 攻撃・テレポート（通常）を止める（凍った射線も消える）

        bossSniper.SetEventPaused(true);



        // 分身は「止める」のではなく消す（お供分身・分身攻撃の複製をまとめて即消去）。

        // EventPaused 中は再出現しないので、演出が終わって巡回へ戻ってから自然に湧き直す

        bossSniper.DespawnAllMinions();



        BossSniperBeamUnit unit = bossSniper.SelfUnit;

        Transform visual = unit != null ? unit.visualTransform : null;



        // ① 中央（StageCenter）へテレポート。展開直前に正面を向かせるので、出現時にはもう正面向き

        yield return TeleportWithFacing(bossSniper.StageCenter(), faceFront: true, visual);



        // ② 出現後、正面向きのままカメラが寄る

        CreateTempTarget(bossSniper.transform.position, enrageCameraZ);

        cameraFollow.StartTrackTarget(tempCameraTarget.transform, enragePositionSpeed, enrageZoomSpeed);



        yield return new WaitForSeconds(enrageApproachWait);



        // ③ 覚醒の瞬間：ボスの揺れ＋カメラシェイク（演出中はずっと正面向き）

        if (bossShake != null)

            bossShake.ShakeRandom(enrageBossShakeStrength, enrageBossShakeDuration);

        if (ShakeTarget.Instance != null)

            ShakeTarget.Instance.Shake(enrageShakeDuration, enrageShakeMagnitude, 12f);



        yield return new WaitForSeconds(enrageHoldTime);



        // ④ カメラを部屋へ戻す（アンロックを挟まない最小構成）

        SetBoundsObjectActive(true);

        cameraFollow.ForceStopEventCameraWork();

        RelockToBoundsPoint();

        DestroyTempTarget();



        // ⑤ 一定時間待つ（インスペクタ設定）

        yield return new WaitForSeconds(enrageResumeDelay);



        // ⑥ 巡回エリア内のランダム地点へ再テレポート。展開直前に通常向き（プレイヤー方向）へ戻すので、

        //    出現したときにはもう通常向き

        yield return TeleportWithFacing(bossSniper.RandomPatrolPoint(), faceFront: false, visual);



        // ⑦ 行動再開
        yield return new WaitForSeconds(1.0f);
        bossSniper.SetEventPaused(false);

        playingRoutine = null;

    }



    // ボスを destination へテレポートする。展開直前に、faceFront=true なら正面向き（enrageFrontYAngle）、

    // false なら通常向き（プレイヤー方向）へ確定させるので、出現時には狙った向きになっている。

    // 完了までコルーチンでブロックする（テレポートは所有者が毎フレーム Update() を回す必要がある）。

    private IEnumerator TeleportWithFacing(Vector2 destination, bool faceFront, Transform visual)

    {

        BossSniperBeamUnit unit = bossSniper.SelfUnit;

        if (unit == null) yield break;



        bool done = false;



        enrageTeleport.Begin(

            unit,

            destination,

            enrageTeleportShrinkTime,

            enrageTeleportExpandTime,

            onBeforeExpand: () =>

            {

                // 消えている間に向きを確定（切り替わりが見えない）

                if (faceFront)

                {

                    if (visual != null) visual.localRotation = Quaternion.Euler(0f, enrageFrontYAngle, 0f);

                }

                else

                {

                    unit.SnapVisualToPlayerImmediate(); // 通常向き（プレイヤー方向）

                }

            },

            onFinished: () => done = true);



        // 収縮→展開の完了まで毎フレーム Update を回す

        while (!done)

        {

            enrageTeleport.Update();

            yield return null;

        }

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

    /// カメラ本体の後始末（ForceStop）は撃破イベント側が行うので、

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