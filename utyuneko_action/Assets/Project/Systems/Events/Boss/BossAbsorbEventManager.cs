using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SocialPlatforms;

public class BossAbsorbEventManager : BaseEventManager
{
    public static BossAbsorbEventManager Instance { get; private set; }

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
    [SerializeField] private GameObject targetBoss;
    [SerializeField] private GameObject coreCubePrefab;
    [SerializeField] private Transform coreCubeTransfome;

    [Header("頭上のスタンプ吹き出し（ImageBubble）の参照")]
    [SerializeField] private ImageBubble hosaBubble;
    [SerializeField] private ImageBubble playerBubble;

    [Header("補佐の設定")]
    [SerializeField] private float hosaMoveSpeed = 6f;
    [Tooltip("補佐がボスのどれくらい【上】で静止するか")]
    [SerializeField] private float hosaHeightOffsetFromBoss = 4.2f;

    private CameraFollowWithZoom cameraController;
    private GameObject tempCameraTarget;

    private float maxLookAngle = 30f;
    private float lookSmoothing = 12.0f;
    private bool isLookingActive = false;
    private bool isCore = false;

    private Quaternion originalPlayerVisualRotation;
    private StageSecondBossController bossController;

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

    void LateUpdate()
    {
        // イベント中のリアルタイム視線追従
        if (isLookingActive)
        {
            KeepLookingAtEachOther();
        }
    }

    public override void OnAreaEntered()
    {
        if (currentPhase != EventPhase.BeforeEvent) return;

        currentPhase = EventPhase.InTimeline;

        // 事前にボスのコントローラーとプレイヤーの初期回転を確保しておく
        if (targetBoss != null)
        {
            bossController = targetBoss.GetComponent<StageSecondBossController>();

            // Boss1(BossChargerController)には StageSecondBossController が無いので bossController は null。
            // その場合は Boss2 専用のステージカメラ切替をスキップする（Boss1 は BossStatgeCamera を持たない）。
            if (bossController != null && bossController.BossStatgeCamera)
            {
                bossController.BossStatgeCamera.gameObject.SetActive(false);
            }

            cameraController = Object.FindFirstObjectByType<CameraFollowWithZoom>();
            if (cameraController != null)
            {
                // 見えない空のゲームオブジェクトを作成して目的地に配置
                tempCameraTarget = new GameObject("TempCameraEventTarget");
                tempCameraTarget.transform.position = targetBoss.transform.position;

                // カメラにはボスではなく、この目的地のオブジェクトを追跡させる
                cameraController.StartTrackTarget(tempCameraTarget.transform, 3f, 2f);
            }
        }

        if (playerController != null && playerController.visualManager != null && playerController.visualManager.playerVisual != null)
        {
            originalPlayerVisualRotation = playerController.visualManager.playerVisual.localRotation;

            playerController.TransitionToState(playerController.StateNormal); // プレイヤーを強制的に待機状態にする
        }

        StartEvent();
        activeTimelineCoroutine = StartCoroutine(BossAbsorbTimelineRoutine());
    }

    private IEnumerator BossAbsorbTimelineRoutine()
    {
        SoundManager.Instance.FadeBGMVolume(0.3f, 1.0f);

        // 【ステップ1】補佐が上から降りてくる
        Vector3 bossAirTopPos = targetBoss != null ? targetBoss.transform.position + Vector3.up * hosaHeightOffsetFromBoss : Vector3.up * 4.5f;
        Vector3 hosaSpawnAirPos = bossAirTopPos + Vector3.up * 10f;
        Vector3 originalBossPos = targetBoss != null ? targetBoss.transform.position : Vector3.zero;

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
        if (targetBoss != null && hosa != null)
        {
            SoundManager.Instance.PlaySE(SeType.EnemySuction);

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

        // ===================================================================
        // 🛠️【要望②：補佐がボスを吸い込んだ後、地面に降りてくる】
        // プレイヤーの足元（地面の高さ）を検知して、フワッと綺麗に着地させます！
        // ===================================================================
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

        // ===================================================================
        // 🛠️【要望③＆④：ステージ中心へ美しい放物線トス（爆弾投擲演出風）】
        // 補佐の手元からステージのド中心の地面へ向かって、コアキューブを放り投げます！
        // ===================================================================
        if (coreCubePrefab != null && hosa != null)
        {
            SoundManager.Instance.PlaySE(SeType.PlayerIconPop); // 投射音

            // ステージの中心X座標の計算（安全ガード付き）
            float stageCenterX = (bossController != null) ? (bossController.stageMinX + bossController.stageMaxX) / 2f : hosa.transform.position.x;
            float targetGroundY = playerTransform != null ? playerTransform.position.y : hosa.transform.position.y - 0.5f;
            Vector3 cubeTargetWorkspace = new Vector3(stageCenterX, targetGroundY, 0f);

            if (coreCubeTransfome) cubeTargetWorkspace = coreCubeTransfome.position;

            // 補佐の現在地からプレハブを生成
            GameObject spawnedCube = Instantiate(coreCubePrefab, hosa.transform.position, Quaternion.identity);

            isCore = true;
            // 🚀 放物線移動コルーチンを実行して、着地までしっかり演出！
            yield return StartCoroutine(TossCubeLinearRoutine(spawnedCube, hosa.transform.position, cubeTargetWorkspace, 0.65f, 3.5f));
        }
        yield return StartCoroutine(Wait(0.5f));

        // 🎬 すべての演出が完了したので、締めくくりへ移行
        CompleteEvent();
    }

    /// <summary>
    /// 🚀【新機能】コアキューブを綺麗な放物線（Toss）で移動させる汎用物理シミュレーター
    /// </summary>
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

            // Yは綺麗な二次関数の放物線アークを合成（Mathf.Sinで山を作る）
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

    // ===================================================================
    // 🛠️【要望①＆⑤：スキップ時の処置】
    // スキップされても滑らかリセットを適応し、追従は完全に遮断！
    // ===================================================================
    protected override void OnSkipWarp()
    {
        SoundManager.Instance.FadeBGMVolume(1.0f, 1.0f);
        if (hosaBubble != null) hosaBubble.StartFadeOut();
        if (playerBubble != null) playerBubble.StartFadeOut();
        if (targetBoss != null) targetBoss.SetActive(false);

        isLookingActive = false;
        ResetPlayerVisualRotation();

        if (cameraController != null)
        {
            cameraController.ReturnToPlayerFromEvent(0.1f);
        }
        if (bossController != null && bossController.BossStatgeCamera)
        {
            bossController.BossStatgeCamera.gameObject.SetActive(false);
        }

        if (coreCubePrefab != null && !isCore)
        {
            Instantiate(coreCubePrefab, coreCubeTransfome.position, Quaternion.identity);
        }

        if (hosa != null)
        {
            // スキップ時も一瞬ではなく、現在の回転を維持したまま通常モードへ解放
            hosa.transform.localRotation = Quaternion.identity;

            Vector3 originalBossPos = targetBoss != null ? targetBoss.transform.position : Vector3.zero;
            float groundY = playerTransform != null ? playerTransform.position.y : originalBossPos.y - 3f;
            hosa.transform.position = new Vector3(hosa.transform.position.x, groundY + 0.5f, hosa.transform.position.z);
        }
        currentPhase = EventPhase.Finished;
    }

    // ===================================================================
    // 🛠️【要望①＆⑤：通常終了時の滑らかな向き直り演出】
    // 視線を切り、0.5秒かけてヌルッと美しく正面を向かせてからイベントを閉じます！
    // ===================================================================
    private void CompleteEvent()
    {
        StartCoroutine(SmoothEndSequenceRoutine());
    }

    private IEnumerator SmoothEndSequenceRoutine()
    {
        // 1. まず見つめ合いのリアルタイムロックを解除！
        isLookingActive = false;
        ResetPlayerVisualRotation();

        SoundManager.Instance.FadeBGMVolume(1.0f, 1.0f);


        if (cameraController != null)
        {
            cameraController.ReturnToPlayerFromEvent(1.0f);
        }
        if (tempCameraTarget != null)
        {
            Object.Destroy(tempCameraTarget);
        }

        // 2. ⏳【要望①】0.4秒かけて、補佐の顔を滑らかに「正面（Quaternion.identity）」へ戻す！
        if (hosa != null)
        {
            Quaternion startRot = hosa.transform.localRotation;
            float lerpT = 0f;
            while (lerpT < 1f)
            {
                lerpT += Time.deltaTime * 2.5f; // 0.4秒ほどでスッと戻る速度
                hosa.transform.localRotation = Quaternion.Slerp(startRot, Quaternion.identity, Mathf.Clamp01(lerpT));
                yield return null;
            }
            hosa.transform.localRotation = Quaternion.identity;
        }

        // 3. 補佐はイベント終了後も「ついてこなくて大丈夫」なので、
        // StateFollow（追従ステート）への遷移コードは完全に消去された状態をキープ！
        currentPhase = EventPhase.Finished;

        if (bossController != null && bossController.BossStatgeCamera)
        {
            bossController.BossStatgeCamera.gameObject.SetActive(false);
        }

        // 入力ブロックを解除してガチでイベント終了！
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