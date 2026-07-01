using System.Collections;
using UnityEngine;

public class RescueEventManager : BaseEventManager
{
    public static RescueEventManager Instance { get; private set; }

    public enum RescueState
    {
        BeforeArea,
        AreaSignShowed,
        InEvent,
        Absorbing,
        Talking,
        Finished
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

    [Header("イベント終了後破壊されるオブジェクト")]
    [SerializeField] GameObject breakObject;

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
        if (currentState != RescueState.BeforeArea && currentState != RescueState.AreaSignShowed && currentState != RescueState.Finished)
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
    // 🛠️【上書き】親玉の仮想関数を override して、Rescue特有の演出を流す！
    // ===================================================================
    protected override IEnumerator BaseAreaNoticeRoutine()
    {
        // 🎥 カメラが向くわずかなタメ
        yield return new WaitForSecondsRealtime(0.2f);

        // 補佐の頭上に「混乱スタンプ」を表示！音がピキーンと鳴る
        if (hosaBubble != null)
        {
            hosaBubble.ShowStamp(ImageBubble.StampType.Confusion);
        }

        SoundManager.Instance.PlayLoopSE(hosa.gameObject, SeType.HosaConfusion);

        if (currentState == RescueState.BeforeArea)
        {
            currentState = RescueState.AreaSignShowed;
        }

        // 親玉の管理用変数を綺麗にリセット
        baseAreaNoticeCoroutine = null;
    }

    public void OnEnemyDefeated(EventEnemy enemy)
    {
        if (currentState == RescueState.BeforeArea || currentState == RescueState.AreaSignShowed)
        {
            // 🧼 エリア演出のコルーチン（親玉側）がまだ動いている途中なら、安全に緊急停止！
            if (baseAreaNoticeCoroutine != null)
            {
                StopCoroutine(baseAreaNoticeCoroutine);
                baseAreaNoticeCoroutine = null;
            }

            targetEnemy = enemy;
            currentState = RescueState.InEvent;

            StartEvent();

            if (hosaBubble != null) hosaBubble.StartFadeOut();

            activeTimelineCoroutine = StartCoroutine(RescueEventTimelineRoutine());
        }
    }

    private IEnumerator RescueEventTimelineRoutine()
    {
        SoundManager.Instance.StopLoopSE(hosa.gameObject);
        SoundManager.Instance.FadeBGMVolume(0.3f, 1.0f);

        yield return StartCoroutine(Wait(2.0f));

        yield return StartCoroutine(Speak(hosaBubble, ImageBubble.StampType.Joy));
        yield return StartCoroutine(Wait(0.5f));

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

        if (targetEnemy != null && hosa != null)
        {
            targetEnemy.StartAbsorb(hosa.transform, 1.0f);
        }

        yield return StartCoroutine(Speak(playerBubble, ImageBubble.StampType.Surprise));
        yield return new WaitForSeconds(0.5f);

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
        yield return StartCoroutine(Wait(0.5f));
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

        if (breakObject)
        {
            Destroy(breakObject);
        }
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

        if (breakObject)
        {
            Destroy(breakObject);
        }

        EndEvent();
    }
}