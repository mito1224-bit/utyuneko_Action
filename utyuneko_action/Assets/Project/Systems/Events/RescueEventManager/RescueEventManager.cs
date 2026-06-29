using System.Collections;
using UnityEngine;

public class RescueEventManager : BaseEventManager
{
    public static RescueEventManager Instance { get; private set; }

    public enum RescueState
    {
        BeforeArea,     // 1. まだエリアに入っていない（初期状態）
        AreaSignShowed, // 2. エリアに入って、補佐のピンチ演出が終わった（敵撃破待ち）
        InEvent,        // 3. 敵を倒して、お礼を言っている最中
        Absorbing,      // 4. 補佐が敵に近づいて吸引している最中
        Talking,        // 5. 地面に降りてスタンプ会話劇をしている最中
        Finished        // 6. すべて終了
    }

    [Header("現在のイベント状態（確認用）")]
    [SerializeField] private RescueState currentState = RescueState.BeforeArea;
    public RescueState CurrentState => currentState;

    [Header("登場オブジェクトの設定")]
    [SerializeField] private HosaController hosa;

    [Header("頭上のスタンプ吹き出し（ImageBubble）の参照")]
    [SerializeField] private ImageBubble hosaBubble;
    [SerializeField] private ImageBubble playerBubble;

    [Header("補佐の移動スピード")]
    [SerializeField] private float hosaMoveSpeed = 5f;

    private float maxLookAngle = 30f;
    private float lookSmoothing = 12.0f;

    private float hosaInDangerYAngle = 180f;
    private float hosaRightYAngle = 310f;
    private float hosaLeftYAngle = 50f;

    private EventEnemy targetEnemy;
    private Vector3 hosaFloorPosition;

    // 💡【新設】エリア演出のコルーチンをピンポイントで止めるための専用の型
    private Coroutine areaNoticeCoroutine;

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
    }

    void Start()
    {
        currentState = RescueState.BeforeArea;

        if (hosa != null)
        {
            hosaFloorPosition = hosa.transform.position;
            hosa.transform.localRotation = Quaternion.Euler(0f, hosaInDangerYAngle, 0f);
            hosa.TransitionToState(hosa.StateEvent);
        }
    }

    void LateUpdate()
    {
        if (currentState != RescueState.BeforeArea &&currentState != RescueState.AreaSignShowed && currentState != RescueState.Finished)
        {
            KeepLookingAtEachOther();
        }
    }

    private void KeepLookingAtEachOther()
    {
        if (hosa == null || playerTransform == null || playerController == null) return;

        if (currentState == RescueState.Absorbing && targetEnemy != null)
        {
            Vector3 dirToEnemy = targetEnemy.transform.position - hosa.transform.position;
            float targetHosaYAngle = (dirToEnemy.x > 0f) ? hosaRightYAngle : hosaLeftYAngle;
            Quaternion targetHosaRot = Quaternion.Euler(0f, targetHosaYAngle, 0f);
            hosa.transform.localRotation = Quaternion.Lerp(hosa.transform.localRotation, targetHosaRot, Time.deltaTime * lookSmoothing);
        }
        else
        {
            Vector3 dirToPlayer = playerTransform.position - hosa.transform.position;
            float targetHosaYAngle = (dirToPlayer.x > 0f) ? hosaRightYAngle : hosaLeftYAngle;

            float hosaAbsX = Mathf.Abs(dirToPlayer.x);
            float hosaAngleX = 0f;
            if (hosaAbsX > 0.01f)
            {
                hosaAngleX = Mathf.Atan2(dirToPlayer.y, hosaAbsX) * Mathf.Rad2Deg;
                hosaAngleX = Mathf.Clamp(hosaAngleX, -maxLookAngle, maxLookAngle);
            }

            Quaternion targetHosaRot = Quaternion.Euler(hosaAngleX, targetHosaYAngle, 0f);
            hosa.transform.localRotation = Quaternion.Lerp(hosa.transform.localRotation, targetHosaRot, Time.deltaTime * lookSmoothing);
        }

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
    // 🏃‍♂️ ① 当たり判定に入った時に呼ばれる演出（操作禁止はカメラ側へ！）
    // ===================================================================
    public void OnAreaEntered()
    {
        if (currentState == RescueState.BeforeArea)
        {
            // 💡 後から安全に止められるように、変数に代入してキック！
            areaNoticeCoroutine = StartCoroutine(AreaNoticeRoutine());
        }
    }

    private IEnumerator AreaNoticeRoutine()
    {
        // 🛑【修正】BlockPlayerInput() を削除（カメラ側のプレハブで制御するため）

        // 🎥 カメラが向くわずかなタメ
        yield return new WaitForSecondsRealtime(0.2f);

        // 補佐の頭上に「混乱スタンプ」を表示！音がピキーンと鳴る
        if (hosaBubble != null)
        {
            hosaBubble.ShowStamp(ImageBubble.StampType.Confusion);
        }

        SoundManager.Instance.PlayLoopSE(hosa.gameObject, SeType.HosaConfusion);

        // 🛑【修正】ReleasePlayerInput() を削除（カメラ側のプレハブで制御するため）

        // 💡【安全弁】もしこの1.5秒の間にすでに敵が倒されて本番（InEvent）になっていたら、
        // 上書きしてしまわないようにステート変更をスルーする
        if (currentState == RescueState.BeforeArea)
        {
            currentState = RescueState.AreaSignShowed;
        }

        areaNoticeCoroutine = null;
    }

    // ===================================================================
    // ⚔️ ②【包容力アップ】周りの敵を全滅させた時に呼ばれる関数
    // ===================================================================
    public void OnEnemyDefeated(EventEnemy enemy)
    {
        // 💡【超重要バグ対策】エリア演出前（BeforeArea）だろうが、演出の途中だろうが、
        // 敵さえ死ねば「何が何でも確実に」本番イベント（InEvent）へ引きずり込む！
        if (currentState == RescueState.BeforeArea || currentState == RescueState.AreaSignShowed)
        {
            // 🧼 エリア演出のコルーチンがまだ動いている途中なら、安全に緊急停止する！
            if (areaNoticeCoroutine != null)
            {
                StopCoroutine(areaNoticeCoroutine);
                areaNoticeCoroutine = null;
            }

            targetEnemy = enemy;
            currentState = RescueState.InEvent;

            // 本格的な会話イベントが始まったので、親玉のシステム（操作ロック・UI隠し・長押し監視）をON！
            StartEvent();

            if (hosaBubble != null) hosaBubble.StartFadeOut();

            activeTimelineCoroutine = StartCoroutine(RescueEventTimelineRoutine());
        }
    }

    // ===================================================================
    // 🎬 ③ 本番の一本道会話劇タイムライン
    // ===================================================================
    private IEnumerator RescueEventTimelineRoutine()
    {
        SoundManager.Instance.StopLoopSE(hosa.gameObject);

        SoundManager.Instance.FadeBGMVolume(0.3f, 1.0f);

        yield return StartCoroutine(Wait(2.0f));

        // 1. お礼を言う
        yield return StartCoroutine(Speak(hosaBubble, ImageBubble.StampType.Joy));
        yield return StartCoroutine(Wait(0.5f));

        // 2. 補佐が敵に近づく
        currentState = RescueState.Absorbing;
        Coroutine playerQuestion = StartCoroutine(Speak(playerBubble, ImageBubble.StampType.Question));

        if (targetEnemy != null && hosa != null)
        {
            Vector3 targetPosition = targetEnemy.transform.position + Vector3.up * 1.5f;
            while (Vector3.Distance(hosa.transform.position, targetPosition) > 0.05f)
            {
                hosa.transform.position = Vector3.MoveTowards(hosa.transform.position, targetPosition, hosaMoveSpeed * Time.deltaTime);
                yield return null;
            }
            hosa.transform.position = targetPosition;
        }
        yield return playerQuestion;

        // 3. 敵の吸引
        if (targetEnemy != null && hosa != null)
        {
            targetEnemy.StartAbsorb(hosa.transform, 1.0f);
        }

        yield return StartCoroutine(Speak(playerBubble, ImageBubble.StampType.Surprise));
        yield return new WaitForSeconds(0.5f);

        // 4. 補佐が地面に降りてくる
        if (hosa != null)
        {
            Vector3 targetDropPosition = new Vector3(hosa.transform.position.x, hosaFloorPosition.y, hosa.transform.position.z);
            while (Vector3.Distance(hosa.transform.position, targetDropPosition) > 0.05f)
            {
                hosa.transform.position = Vector3.MoveTowards(hosa.transform.position, targetDropPosition, hosaMoveSpeed * Time.deltaTime);
                yield return null;
            }
            hosa.transform.position = targetDropPosition;
        }

        currentState = RescueState.Talking;
        yield return StartCoroutine(Speak(hosaBubble, ImageBubble.StampType.Doya));

        // 5. スタンプ会話劇
        yield return StartCoroutine(Speak(hosaBubble, ImageBubble.StampType.Right));
        yield return StartCoroutine(Wait(0.5f));
        yield return StartCoroutine(Speak(hosaBubble, ImageBubble.StampType.Enemy));
        yield return StartCoroutine(Wait(0.5f));
        yield return StartCoroutine(Speak(hosaBubble, ImageBubble.StampType.Denger));
        yield return StartCoroutine(Wait(0.5f));
        yield return StartCoroutine(Speak(hosaBubble, ImageBubble.StampType.Fellow));
        yield return StartCoroutine(Wait(0.5f));
        yield return StartCoroutine(Speak(playerBubble, ImageBubble.StampType.Surprise));
        yield return StartCoroutine(Wait(0.5f));
        yield return StartCoroutine(Speak(playerBubble, ImageBubble.StampType.Maru));
        yield return StartCoroutine(Wait(0.5f));
        yield return StartCoroutine(Speak(playerBubble, ImageBubble.StampType.OK));
        yield return StartCoroutine(Wait(0.5f));
        yield return StartCoroutine(Speak(hosaBubble, ImageBubble.StampType.Joy));
        yield return StartCoroutine(Wait(0.5f));
        yield return StartCoroutine(Speak(hosaBubble, ImageBubble.StampType.Go));
        yield return StartCoroutine(Wait(0.5f));
        yield return StartCoroutine(Speak(playerBubble, ImageBubble.StampType.OK));
        yield return StartCoroutine(Wait(0.5f));

        CompleteEvent();
    }

    protected override void OnSkipWarp()
    {
        Debug.Log("暗転の裏側でイベント終了状態へ強制ワープ処理を実行中...");

        SoundManager.Instance.FadeBGMVolume(1.0f, 1.0f);

        if (hosaBubble != null) hosaBubble.StartFadeOut();
        if (playerBubble != null) playerBubble.StartFadeOut();

        if (targetEnemy != null) Destroy(targetEnemy.gameObject);

        if (hosa != null)
        {
            Vector3 finalHosaPos = hosaFloorPosition;
            if (targetEnemy != null)
            {
                finalHosaPos = new Vector3(targetEnemy.transform.position.x, hosaFloorPosition.y, hosa.transform.position.z);
            }
            hosa.transform.position = finalHosaPos;
            hosa.transform.localRotation = Quaternion.identity;
            hosa.TransitionToState(hosa.StateFollow);
        }

        currentState = RescueState.Finished;
    }

    protected override void OnEventFullyCompleted()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AdvanceStoryPhase();
        }
    }

    private void CompleteEvent()
    {
        SoundManager.Instance.FadeBGMVolume(1.0f, 1.0f);

        if (hosa != null)
        {
            hosa.transform.localRotation = Quaternion.identity;
            hosa.TransitionToState(hosa.StateFollow);
        }
        currentState = RescueState.Finished;

        EndEvent();
    }
}