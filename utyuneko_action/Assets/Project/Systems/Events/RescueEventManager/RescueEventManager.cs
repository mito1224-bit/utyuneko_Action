using System.Collections;
using UnityEngine;

public class RescueEventManager : BaseEventManager
{
    public static RescueEventManager Instance { get; private set; }

    public enum RescueState { InDanger, Thanking, Absorbing, Talking, Finished }

    [Header("現在のイベント状態（確認用）")]
    [SerializeField] private RescueState currentState = RescueState.InDanger;
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

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
    }

    void Start()
    {
        currentState = RescueState.InDanger;

        if (hosa != null)
        {
            hosaFloorPosition = hosa.transform.position;
            hosa.transform.localRotation = Quaternion.Euler(0f, hosaInDangerYAngle, 0f);
            hosa.TransitionToState(hosa.StateEvent);

            if (hosaBubble != null)
            {
                hosaBubble.ShowStamp(ImageBubble.StampType.Confusion);
            }
        }
    }

    void LateUpdate()
    {
        if (currentState != RescueState.InDanger && currentState != RescueState.Finished)
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

    public void OnEnemyDefeated(EventEnemy enemy)
    {
        if (currentState == RescueState.InDanger)
        {
            targetEnemy = enemy;
            currentState = RescueState.Thanking;

            // 親の長押し監視・入力ロックシステムを起動！
            StartEvent();

            if (hosaBubble != null) hosaBubble.StartFadeOut();

            // 親玉の activeTimelineCoroutine に代入してコルーチンをキックする
            activeTimelineCoroutine = StartCoroutine(RescueEventTimelineRoutine());
        }
    }

    private IEnumerator RescueEventTimelineRoutine()
    {
        SoundManager.Instance.FadeBGMVolume(0.5f,1.0f);

        // スキップ用の余計なコードが全消滅し、めちゃくちゃ綺麗な一本道のタイムラインになりました！
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

        // 4. 補佐が降りてくる
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
        // 最後まで正常に再生し終わったら、通常ルートの終了関数を呼ぶ
        CompleteEvent();
    }

    /// <summary>
    /// 長押しスキップが成立（画面が真っ黒に暗転）した瞬間に、親玉から呼ばれるワープ関数！
    /// 画面が真っ黒な状態の裏で、一瞬でイベントが終わった時の状態（最終形態）へ強制ワープさせます。
    /// </summary>
    protected override void OnSkipWarp()
    {
        Debug.Log("暗転の裏側でイベント終了状態へ強制ワープ処理を実行中...");

        // 1. 吹き出しを即座に非表示
        if (hosaBubble != null) hosaBubble.StartFadeOut();
        if (playerBubble != null) playerBubble.StartFadeOut();

        // 2. 敵がまだ吸い込まれていなければ、即座に完全消滅させる
        if (targetEnemy != null) Destroy(targetEnemy.gameObject);

        // 3. 補佐の座標と向きを、イベント終了時の最終状態（地面の上）へワープ
        if (hosa != null)
        {
            Vector3 finalHosaPos = hosaFloorPosition;
            if (targetEnemy != null)
            {
                // 本来敵がいた位置の、床の高さへワープ
                finalHosaPos = new Vector3(targetEnemy.transform.position.x, hosaFloorPosition.y, hosa.transform.position.z);
            }
            hosa.transform.position = finalHosaPos;
            hosa.transform.localRotation = Quaternion.identity;
            hosa.TransitionToState(hosa.StateFollow); // フォロー状態に戻す
        }

        currentState = RescueState.Finished;
    }

    /// <summary>
    /// 🎬 スキップでも通常終了でも、画面が完全にゲームに戻った瞬間に呼ばれる共通の出口
    /// </summary>
    protected override void OnEventFullyCompleted()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AdvanceStoryPhase();
        }
    }

    // 通常終了ルート（中身は最小限に）
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