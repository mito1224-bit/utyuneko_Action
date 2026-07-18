using System.Collections;
using UnityEngine;

/// <summary>
/// ボススナイパー撃破後の「吸収イベント」（BossAbsorbEventManager のボススナイパー専用版）。
///
/// 流れ:
///   撃破（BossSniper.onDefeated）→ 少し間を置く（撃破の全体スローが明けるのを待つ）
///   → 補佐が上空から倒れたボスの真上へ降りてくる（プレイヤーと視線追従）
///   → プレイヤーがびっくり（!）→ タメ → 補佐が星スタンプ
///   → 補佐がボスを吸い込む（縮小しながら引き寄せて消す）
///   → 補佐が地面へ降りる → どくろスタンプ
///   → コアキューブをステージ中央（BossSniper.StageCenter）へ放物線トス → 締め
///
/// 別ボス版との違い:
///   - 起動はエリア侵入ではなく BossSniper.onDefeated。インスペクターで
///     BossSniper の onDefeated に、このコンポーネントの OnBossDefeated() を配線する。
///   - ステージ中央は stageMinX/MaxX ではなく BossSniper.StageCenter()（巡回ポイントの重心）。
///   - 吸い込み前にボスを Kinematic に戻し（RestoreFlightBody）、当たり判定を切る
///     （倒れているボスは Dynamic なので、物理と演出の位置操作が喧嘩しないように）。
///
/// セットアップの注意:
///   - BossSniper の destroyDelayOnDefeat は 0（自動Destroyしない）にしておく。
///     ボスの消去はこのイベント（吸い込み → SetActive(false)）が担当する。
///   - startDelayRealtime は BossSniperHitStop.defeatSlowDuration より少し長めにすると、
///     全体スローの余韻が終わってからイベントが始まる。
/// </summary>
public class BossSniperAbsorbEventManager : BaseEventManager
{
    public static BossSniperAbsorbEventManager Instance { get; private set; }

    public enum EventPhase
    {
        BeforeEvent,
        InTimeline,
        Finished
    }

    [Header("現在のイベント進行状態")]
    [SerializeField] private EventPhase currentPhase = EventPhase.BeforeEvent;

    [Header("登場オブジェクトの設定")]
    [SerializeField] private HosaController hosa;

    [Tooltip("ボススナイパー本体。吸い込み対象＆ステージ中央の取得に使う")]
    [SerializeField] private BossSniper bossSniper;

    [SerializeField] private GameObject coreCubePrefab;

    [Header("頭上のスタンプ吹き出し（ImageBubble）の参照")]
    [SerializeField] private ImageBubble hosaBubble;
    [SerializeField] private ImageBubble playerBubble;

    [Header("補佐の設定")]
    [SerializeField] private float hosaMoveSpeed = 6f;
    [Tooltip("補佐がボスのどれくらい【上】で静止するか")]
    [SerializeField] private float hosaHeightOffsetFromBoss = 4.2f;

    [Header("開始の間")]
    [Tooltip("撃破からイベント開始までの間（実時間）。撃破の全体スロー（BossSniperHitStop.defeatSlowDuration）より少し長めに")]
    [SerializeField] private float startDelayRealtime = 0.6f;

    [Header("カメラ・シェイク演出")]
    [Tooltip("カメラ演出係。強化カットイン等が万一実行中だった場合の打ち切りに使う（未設定でも可）")]
    [SerializeField] private BossSniperCameraDirector cameraDirector;

    [Tooltip("イベント中、カメラがボス位置へ寄る際のなめらかさ（位置）")]
    [SerializeField] private float trackPositionSpeed = 3f;

    [Tooltip("イベント中、カメラがボス位置へ寄る際のなめらかさ（ズーム）")]
    [SerializeField] private float trackZoomSpeed = 2f;

    [Tooltip("撃破イベント中のカメラの寄り距離（z座標。通常 -10、近いほど 0）。倒れたボスへの寄り具合")]
    [SerializeField] private float trackCameraZ = -8f;

    [Tooltip("イベント終了時にカメラがプレイヤーへ戻る時間（秒）")]
    [SerializeField] private float cameraReturnTime = 1.0f;

    [Tooltip("イベント開始時（スロー明け直後）の撃破シェイクの秒数・強さ・細かさ")]
    [SerializeField] private float defeatShakeDuration = 0.5f;
    [SerializeField] private float defeatShakeMagnitude = 1.1f;
    [SerializeField] private float defeatShakeFrequency = 12f;

    [Tooltip("コアキューブの設置位置Y")]
    [SerializeField] private float positionCoreCubeY = 0.5f;

    private CameraFollowWithZoom cameraController;
    private GameObject tempCameraTarget;

    private float maxLookAngle = 30f;
    private float lookSmoothing = 12.0f;
    private bool isLookingActive = false;

    private Quaternion originalPlayerVisualRotation;



    protected override void Awake()
    {
        base.Awake();
        Instance = this;
    }

    void Start()
    {
        currentPhase = EventPhase.BeforeEvent;
        isLookingActive = false;
    }

    // ─── 撃破イベントの購読（インスペクター配線は不要） ───
    // bossSniper 参照さえ割り当ててあれば、コード側から onDefeated を自動購読する。
    // ボスのオブジェクトがシーン開始時に非アクティブでも、参照経由の AddListener は問題なく効く。
    // （インスペクターで onDefeated に手動配線してあっても、OnBossDefeated 側の
    //   currentPhase ガードで二重起動はしないので安全）
    void OnEnable()
    {
        if (bossSniper != null)
        {
            bossSniper.onDefeated.AddListener(OnBossDefeated);
            Debug.Log("<color=cyan>[AbsorbEvent] onDefeated をコード購読しました / bossID=" + bossSniper.GetInstanceID() + "</color>");
        }
        else
        {
            Debug.LogWarning("[AbsorbEvent] bossSniper が未割り当てです。インスペクターでボス本体を割り当ててください。");
        }
    }

    void OnDisable()
    {
        if (bossSniper != null)
        {
            bossSniper.onDefeated.RemoveListener(OnBossDefeated);
        }
    }

    void LateUpdate()
    {
        // イベント中のリアルタイム視線追従
        if (isLookingActive)
        {
            KeepLookingAtEachOther();
        }
    }

    // 撃破イベントなので、エリア侵入では起動しない（BaseEventManager の口だけ塞いでおく）
    public override void OnAreaEntered() { }

    /// <summary>
    /// イベント開始の入口。BossSniper の onDefeated（UnityEvent）にインスペクターから配線する。
    /// </summary>
    public void OnBossDefeated()
    {
        Debug.Log("<color=cyan>OnBossDefeated 呼ばれた / phase=" + currentPhase + "</color>");
        if (currentPhase != EventPhase.BeforeEvent) return;

        currentPhase = EventPhase.InTimeline;

        // 撃破の瞬間に即座にプレイヤーを止める。
        // カメラの寄り（＝操作無効化を行う StartTrackTarget）はスロー明け後まで待つため、
        // ここで止めておかないと、それまでの間プレイヤーが動けてしまう
        // （倒した瞬間の入力の速度が残って滑り続ける・移動しっぱなしになる）
        if (playerController != null)
        {
            if (playerController.visualManager != null && playerController.visualManager.playerVisual != null)
            {
                originalPlayerVisualRotation = playerController.visualManager.playerVisual.localRotation;
            }

            playerController.TransitionToState(playerController.StateNormal);
            if (playerController.inputActions != null) playerController.inputActions.Player.Disable();
            playerController.enabled = false;

            // 物理速度をゼロにして、残った慣性での滑りを止める
            Rigidbody2D prb = playerController.GetComponent<Rigidbody2D>();
            if (prb == null && playerTransform != null) prb = playerTransform.GetComponent<Rigidbody2D>();
            if (prb != null)
            {
                prb.linearVelocity = Vector2.zero;
                prb.angularVelocity = 0f;
            }
        }

        // 撃破と同じ同期タイミングで、先行イベントを確実に止める。
        //   - 強化カットイン等のコルーチンを打ち切る（CancelCutscenes）
        //   - カメラ側の isEventWorking を確実に false へ落とす（ForceStopEventCameraWork）
        // これをスロー明けまで遅らせると、その間に isEventWorking が残ったままになり、
        // 後段の StartTrackTarget が「二重発動防止」で丸ごとスキップされてカメラが居座る。
        // CancelCutscenes は内部で ResumeBoundsLock するので、Suspend より先に呼ぶ
        if (cameraDirector != null) cameraDirector.CancelCutscenes();

        cameraController = Object.FindFirstObjectByType<CameraFollowWithZoom>();
        if (cameraController != null) cameraController.ForceStopEventCameraWork();

        // 部屋のカメラ固定トリガー（CameraBoundsTrigger）を一時停止して、撃破カメラに道を譲る。
        // スロー明けを待つ間もトリガーが無効なので、部屋の固定点へ引き戻されない
        BossSniperCameraDirector.SuspendBoundsLock();

        StartEvent();
        Debug.Log("<color=cyan>StartEvent 通過、タイムライン開始</color>");
        activeTimelineCoroutine = StartCoroutine(BossSniperAbsorbTimelineRoutine());
    }

    private IEnumerator BossSniperAbsorbTimelineRoutine()
    {
        // 【ステップ0】撃破の全体スロー（timeScale低下）が明けるのを待ってから始める。
        // 実時間の間を置いたうえで、timeScale が確実に 1 へ戻るまで待つ
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, startDelayRealtime));
        yield return new WaitUntil(() => Time.timeScale >= 0.99f);

        // 【ステップ0.5】カメラを倒れたボスへ寄せる。
        // 先行イベントの打ち切り（CancelCutscenes / ForceStopEventCameraWork）は
        // OnBossDefeated の同期タイミングで済ませてあるので、ここでは isEventWorking==false が保証され、
        // StartTrackTarget が「二重発動防止」で弾かれない。
        // StartTrackTarget 内の SetPlayerActiveState(false) がプレイヤー操作を無効化する
        if (cameraController == null) cameraController = Object.FindFirstObjectByType<CameraFollowWithZoom>();
        if (cameraController != null)
        {
            // 寄り先の見えない一時ターゲット。z が寄り距離になる（StartTrackTarget の仕様）
            Vector3 basePos = bossSniper != null
                ? bossSniper.transform.position
                : (playerTransform != null ? playerTransform.position : Vector3.zero);

            tempCameraTarget = new GameObject("TempCameraEventTarget");
            tempCameraTarget.transform.position = new Vector3(basePos.x, basePos.y, trackCameraZ);

            cameraController.StartTrackTarget(tempCameraTarget.transform, trackPositionSpeed, trackZoomSpeed);
        }

        // 撃破の衝撃（カメラシェイク・大）
        if (ShakeTarget.Instance != null)
        {
            ShakeTarget.Instance.Shake(defeatShakeDuration, defeatShakeMagnitude, defeatShakeFrequency);
        }

        SoundManager.Instance.FadeBGMVolume(0.3f, 1.0f);

        // 【ステップ1】補佐が上から降りてくる（倒れているボスの真上へ）
        Vector3 originalBossPos = bossSniper != null ? bossSniper.transform.position : Vector3.zero;
        Vector3 bossAirTopPos = originalBossPos + Vector3.up * hosaHeightOffsetFromBoss;
        Vector3 hosaSpawnAirPos = bossAirTopPos + Vector3.up * 8f;

        if (hosa != null)
        {
            hosa.transform.position = hosaSpawnAirPos;
            hosa.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            hosa.TransitionToState(hosa.StateEvent);

            // 【ステップ2】プレイヤーが補佐を向く（見つめ合い開始）
            isLookingActive = true;

            while (Vector3.Distance(hosa.transform.position, bossAirTopPos) > 0.05f)
            {
                hosa.transform.position = Vector3.MoveTowards(hosa.transform.position, bossAirTopPos, hosaMoveSpeed * Time.deltaTime);
                yield return null;
            }
            hosa.transform.position = bossAirTopPos;
        }

        // 【ステップ3】プレイヤーがびっくりする
        yield return StartCoroutine(Speak(playerBubble, ImageBubble.StampType.Surprise));
        yield return StartCoroutine(Wait(0.4f));

        // 【ステップ4】補佐がボスの上で止まるタメ
        yield return StartCoroutine(Wait(0.5f));

        // 【ステップ5】補佐が星マークを出す
        yield return StartCoroutine(Speak(hosaBubble, ImageBubble.StampType.Star));
        yield return StartCoroutine(Wait(0.3f));

        // 【ステップ6】補佐がボスを100%吸い込む
        if (bossSniper != null && hosa != null)
        {
            SoundManager.Instance.PlaySE(SeType.EnemySuction);

            // 吸い込みの瞬間（カメラシェイク・小）
            if (ShakeTarget.Instance != null)
            {
                ShakeTarget.Instance.Shake(ShakeTarget.ShakeStyle.Small);
            }

            // 物理と演出が喧嘩しないよう、Kinematic に戻して当たり判定も切る
            // （倒れているボスは Dynamic ＝ 重力とコライダーが生きているため）
            bossSniper.RestoreFlightBody();
            bossSniper.SelfUnit.SetHitboxEnabled(false);

            GameObject targetBoss = bossSniper.gameObject;
            Vector3 originalBossScale = targetBoss.transform.localScale;
            float absorbTimer = 0f;
            float absorbDuration = 0.6f;

            while (absorbTimer < absorbDuration)
            {
                absorbTimer += Time.deltaTime;
                float t = Mathf.Clamp01(absorbTimer / absorbDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                if (targetBoss != null)
                {
                    targetBoss.transform.localScale = Vector3.Lerp(originalBossScale, Vector3.zero, smoothT);
                    targetBoss.transform.position = Vector3.Lerp(originalBossPos, hosa.transform.position, smoothT);
                }
                yield return null;
            }
            targetBoss.SetActive(false);
        }
        yield return StartCoroutine(Wait(0.3f));

        // 【ステップ6.5】補佐がボスを吸い込んだ後、地面に降りてくる
        if (hosa != null)
        {
            float groundY = playerTransform != null ? playerTransform.position.y : originalBossPos.y - 3f;
            Vector3 hosaLandingPos = new Vector3(hosa.transform.position.x, groundY + 0.5f, hosa.transform.position.z); // 少し浮かせると神々しい

            while (Vector3.Distance(hosa.transform.position, hosaLandingPos) > 0.05f)
            {
                hosa.transform.position = Vector3.MoveTowards(hosa.transform.position, hosaLandingPos, (hosaMoveSpeed * 0.7f) * Time.deltaTime);
                yield return null;
            }
            hosa.transform.position = hosaLandingPos;
        }
        yield return StartCoroutine(Wait(0.4f));

        // 【ステップ7】補佐がどくろマークを出す
        yield return StartCoroutine(Speak(hosaBubble, ImageBubble.StampType.Dokuro));
        yield return StartCoroutine(Wait(0.4f));

        // 【ステップ8】ステージ中央へ放物線トス（コアキューブ）
        if (coreCubePrefab != null && hosa != null)
        {
            SoundManager.Instance.PlaySE(SeType.EnemyRangeAttack); // 投射音

            // ステージ中央は巡回ポイントの重心（BossSniper.StageCenter）から。X だけ使い、着地は地面の高さへ
            float stageCenterX = bossSniper != null ? bossSniper.StageCenter().x : hosa.transform.position.x;
            float targetGroundY = playerTransform != null ? playerTransform.position.y : hosa.transform.position.y - 2.5f;
            Vector3 cubeTarget = new Vector3(stageCenterX, targetGroundY + positionCoreCubeY, 0f);

            GameObject spawnedCube = Instantiate(coreCubePrefab, hosa.transform.position, Quaternion.identity);

            yield return StartCoroutine(TossCubeLinearRoutine(spawnedCube, hosa.transform.position, cubeTarget, 0.65f, 3.5f));

            // キューブ着地の衝撃（カメラシェイク・微）
            if (ShakeTarget.Instance != null)
            {
                ShakeTarget.Instance.Shake(ShakeTarget.ShakeStyle.Tiny);
            }
        }
        yield return StartCoroutine(Wait(0.5f));

        // すべての演出が完了したので、締めくくりへ移行
        CompleteEvent();
    }

    /// <summary>コアキューブを綺麗な放物線（Toss）で移動させる汎用ルーチン（別ボス版と同じ）。</summary>
    private IEnumerator TossCubeLinearRoutine(GameObject cube, Vector3 start, Vector3 end, float duration, float height)
    {
        float t = 0f;
        while (t < duration)
        {
            if (cube == null) yield break;

            t += Time.deltaTime;
            float linearT = Mathf.Clamp01(t / duration);

            // XとZは直線補間
            float currentX = Mathf.Lerp(start.x, end.x, linearT);
            float currentZ = Mathf.Lerp(start.z, end.z, linearT);

            // Yは放物線アークを合成（Mathf.Sinで山を作る）
            float arcY = Mathf.Sin(linearT * Mathf.PI) * height;
            float currentY = Mathf.Lerp(start.y, end.y, linearT) + arcY;

            cube.transform.position = new Vector3(currentX, currentY, currentZ);
            yield return null;
        }
        if (cube != null) cube.transform.position = end; // 最終座標にピッタリ固定
    }

    private void KeepLookingAtEachOther()
    {
        if (hosa == null || playerTransform == null || playerController == null) return;

        Vector3 dirToPlayer = playerTransform.position - hosa.transform.position;
        float targetHosaYAngle = (dirToPlayer.x > 0f) ? 310f : 50f;
        float hosaAbsX = Mathf.Abs(dirToPlayer.x);
        float hosaAngleX = 0f;
        if (hosaAbsX > 0.01f)
        {
            hosaAngleX = Mathf.Atan2(dirToPlayer.y, hosaAbsX) * Mathf.Rad2Deg;
            hosaAngleX = Mathf.Clamp(hosaAngleX, -maxLookAngle, maxLookAngle);
        }
        Quaternion targetHosaRot = Quaternion.Euler(hosaAngleX, targetHosaYAngle, 0f);
        hosa.transform.localRotation = Quaternion.Lerp(hosa.transform.localRotation, targetHosaRot, Time.deltaTime * lookSmoothing);

        if (playerController.visualManager != null && playerController.visualManager.playerVisual != null)
        {
            Vector3 dirToHosa = hosa.transform.position - playerTransform.position;
            float playerDirX = dirToHosa.x > 0 ? 1f : -1f;
            float targetPlayerYAngle = (playerDirX > 0f) ? 310f : 50f;
            float playerAbsX = Mathf.Abs(dirToHosa.x);
            float playerAngleX = 0f;
            if (playerAbsX > 0.01f)
            {
                playerAngleX = Mathf.Atan2(dirToHosa.y, playerAbsX) * Mathf.Rad2Deg;
                playerAngleX = Mathf.Clamp(playerAngleX, -maxLookAngle, maxLookAngle);
            }
            Quaternion targetPlayerRot = Quaternion.Euler(playerAngleX, targetPlayerYAngle, 0f);
            playerController.visualManager.playerVisual.localRotation = Quaternion.Lerp(
                playerController.visualManager.playerVisual.localRotation,
                targetPlayerRot,
                Time.deltaTime * lookSmoothing
            );
        }
    }

    // スキップ時の処置：演出を飛ばして「ボス消去＋キューブ設置」という最終状態へ帳尻を合わせる
    protected override void OnSkipWarp()
    {
        SoundManager.Instance.FadeBGMVolume(1.0f, 1.0f);
        if (hosaBubble != null) hosaBubble.StartFadeOut();
        if (playerBubble != null) playerBubble.StartFadeOut();
        if (bossSniper != null) bossSniper.gameObject.SetActive(false);

        isLookingActive = false;
        ResetPlayerVisualRotation();

        // カメラを即時にプレイヤーへ戻す（スキップ時の後始末）。
        // 先に戻しを呼んで isEventWorking の後始末を任せ、その後フラグを下ろす
        if (cameraController == null) cameraController = Object.FindFirstObjectByType<CameraFollowWithZoom>();
        if (cameraController != null)
        {
            cameraController.ReturnToPlayerFromEvent(0.1f);
        }
        BossSniperCameraDirector.ResumeBoundsLock();
        if (tempCameraTarget != null)
        {
            Object.Destroy(tempCameraTarget);
            tempCameraTarget = null;
        }

        if (coreCubePrefab != null && hosa != null)
        {
            float stageCenterX = bossSniper != null ? bossSniper.StageCenter().x : hosa.transform.position.x;
            float targetGroundY = playerTransform != null ? playerTransform.position.y : hosa.transform.position.y;
            Instantiate(coreCubePrefab, new Vector3(stageCenterX, targetGroundY, 0f), Quaternion.identity);
        }

        if (hosa != null)
        {
            hosa.transform.localRotation = Quaternion.identity;
        }
        currentPhase = EventPhase.Finished;
    }

    // 通常終了時の滑らかな向き直り演出
    private void CompleteEvent()
    {
        StartCoroutine(SmoothEndSequenceRoutine());
    }

    private IEnumerator SmoothEndSequenceRoutine()
    {
        // 1. 見つめ合いのリアルタイムロックを解除
        isLookingActive = false;
        ResetPlayerVisualRotation();

        // カメラをプレイヤーへ戻す（ReturnToPlayerFromEvent の内部で操作ロックも解放される）。
        // 部屋の固定トリガーの再開は戻りが終わってから。早く戻すと、戻り途中で
        // トリガーが LockCamera を呼び、部屋の固定点へ引っ張られて戻りが乱れる
        if (cameraController != null)
        {
            cameraController.ReturnToPlayerFromEvent(cameraReturnTime);
        }
        yield return new WaitForSeconds(cameraReturnTime + 0.05f);
        BossSniperCameraDirector.ResumeBoundsLock();

        if (tempCameraTarget != null)
        {
            Object.Destroy(tempCameraTarget);
            tempCameraTarget = null;
        }

        // 2. 0.4秒ほどかけて、補佐の顔を滑らかに正面（Quaternion.identity）へ戻す
        if (hosa != null)
        {
            Quaternion startRot = hosa.transform.localRotation;
            float lerpT = 0f;
            while (lerpT < 1f)
            {
                lerpT += Time.deltaTime * 2.5f;
                hosa.transform.localRotation = Quaternion.Slerp(startRot, Quaternion.identity, Mathf.Clamp01(lerpT));
                yield return null;
            }
            hosa.transform.localRotation = Quaternion.identity;
        }

        currentPhase = EventPhase.Finished;

        // 入力ブロックを解除してイベント終了
        EndEvent();
    }

    private void ResetPlayerVisualRotation()
    {
        if (playerController != null && playerController.visualManager != null && playerController.visualManager.playerVisual != null)
        {
            playerController.visualManager.playerVisual.localRotation = originalPlayerVisualRotation;
        }
    }

    protected override void OnEventFullyCompleted() { }
}