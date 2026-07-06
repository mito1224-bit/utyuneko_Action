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

        // プレイヤーの初期回転を確保し、強制的に待機状態へ（入力ブロックは StartEvent が行う）
        if (playerController != null && playerController.visualManager != null && playerController.visualManager.playerVisual != null)
        {
            originalPlayerVisualRotation = playerController.visualManager.playerVisual.localRotation;

            playerController.TransitionToState(playerController.StateNormal);
        }

        StartEvent();
        Debug.Log("<color=cyan>StartEvent 通過、タイムライン開始</color>");
        activeTimelineCoroutine = StartCoroutine(BossSniperAbsorbTimelineRoutine());
    }

    private IEnumerator BossSniperAbsorbTimelineRoutine()
    {
        // 【ステップ0】撃破の全体スロー（timeScale低下）が明けるのを実時間で待ってから始める
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, startDelayRealtime));

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
            float targetGroundY = playerTransform != null ? playerTransform.position.y : hosa.transform.position.y - 0.5f;
            Vector3 cubeTarget = new Vector3(stageCenterX, targetGroundY, 0f);

            GameObject spawnedCube = Instantiate(coreCubePrefab, hosa.transform.position, Quaternion.identity);

            yield return StartCoroutine(TossCubeLinearRoutine(spawnedCube, hosa.transform.position, cubeTarget, 0.65f, 3.5f));
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