using System.Collections;
using UnityEngine;

public class RescueEventManager : MonoBehaviour
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

    [Header("⏱️ オート＆スキップスピード設定")]
    [Tooltip("スタンプが自動で消えて次に進むまでの基本の時間（秒）")]
    [SerializeField] private float defaultDisplayTime = 1.5f;

    // インスペクターからスキップ時の倍速を自由に変更できるようになりました！
    [Tooltip("長押しスキップ中に、演出や移動が何倍速になるか（デフォルトは100倍速）")]
    [SerializeField] private float skipSpeedMultiplier = 100f;

    [Header("補佐の移動スピード")]
    [SerializeField] private float hosaMoveSpeed = 5f;

    // 2.5Dロックオン用の調整パラメータ
    private float maxLookAngle = 30f;
    private float lookSmoothing = 12.0f;

    // 2.5D回転の目標角度（Y軸）
    private float hosaInDangerYAngle = 180f;
    private float hosaRightYAngle = 310f;
    private float hosaLeftYAngle = 50f;

    private EventEnemy targetEnemy;
    private Transform playerTransform;
    private PlayerController playerController;
    private Vector3 hosaFloorPosition;

    /// <summary>
    /// 今プレイヤーがスキップボタン（スペースキー or 左クリック）を長押ししているかを判定
    /// </summary>
    private bool IsSkipping => Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0);

    void Awake()
    {
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

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerController = playerObj.GetComponent<PlayerController>();
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
            float hosaAngleX = 0f;

            Quaternion targetHosaRot = Quaternion.Euler(hosaAngleX, targetHosaYAngle, 0f);
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

            if (playerController != null && playerController.inputActions != null)
            {
                playerController.inputActions.Player.Disable();
                playerController.TransitionToState(playerController.StateNormal);
            }

            StartCoroutine(RescueEventTimelineRoutine());
        }
    }

    private IEnumerator Speak(ImageBubble bubble, ImageBubble.StampType stampType, float customDuration = -1f)
    {
        if (bubble == null)
        {
            Debug.LogError($"[RescueEventManager] 吹き出しがセットされていません：{stampType}");
            yield break;
        }

        bubble.ShowStamp(stampType);
        yield return null;

        float displayDuration = (customDuration > 0f) ? customDuration : defaultDisplayTime;
        float elapsedTime = 0f;

        while (elapsedTime < displayDuration)
        {
            if (IsSkipping)
            {
                break;
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        bubble.StartFadeOut();
    }

    /// <summary>
    /// 長押しスキップに対応した、演出用のディレイ関数
    /// </summary>
    private IEnumerator Wait(float duration)
    {
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            // スキップ中なら skipSpeedMultiplier（100倍）の速さで時間を進める！
            float deltaTime = IsSkipping ? (Time.deltaTime * skipSpeedMultiplier) : Time.deltaTime;
            elapsedTime += deltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// 敵の吸引演出も100%倍速連動するシネマティック・タイムライン
    /// </summary>
    private IEnumerator RescueEventTimelineRoutine()
    {
        yield return StartCoroutine(Wait(2.0f));

        // ==========================================
        // 1. 補佐救出して補佐がお礼を言う
        // ==========================================
        yield return StartCoroutine(Speak(hosaBubble, ImageBubble.StampType.Joy));
        yield return StartCoroutine(Wait(0.5f));

        // ==========================================
        // 2. 補佐が敵に近づいていくのをdB君が「❓」と思う
        // ==========================================
        currentState = RescueState.Absorbing;

        Coroutine playerQuestion = StartCoroutine(Speak(playerBubble, ImageBubble.StampType.Question));

        if (targetEnemy != null && hosa != null)
        {
            Vector3 targetPosition = targetEnemy.transform.position + Vector3.up * 1.5f;
            while (Vector3.Distance(hosa.transform.position, targetPosition) > 0.05f)
            {
                float currentMoveSpeed = IsSkipping ? hosaMoveSpeed * skipSpeedMultiplier : hosaMoveSpeed;

                hosa.transform.position = Vector3.MoveTowards(hosa.transform.position, targetPosition, currentMoveSpeed * Time.deltaTime);
                yield return null;
            }
            hosa.transform.position = targetPosition;
        }

        yield return playerQuestion;

        // ==========================================
        // 3. 敵が補佐に吸い込まれて行って主人公はびっくりする
        // ==========================================
        float baseShrinkTime = 1.0f;
        float currentShrinkTime = IsSkipping ? (baseShrinkTime / skipSpeedMultiplier) : baseShrinkTime;

        if (targetEnemy != null && hosa != null)
        {
            targetEnemy.StartAbsorb(hosa.transform, currentShrinkTime);
        }

        // 驚きのスタンプをドン！
        yield return StartCoroutine(Speak(playerBubble, ImageBubble.StampType.Surprise));

        float baseWaitTime = 0.5f;
        float currentWaitTime = IsSkipping ? (baseWaitTime / skipSpeedMultiplier) : baseWaitTime;
        yield return new WaitForSeconds(currentWaitTime);

        // ==========================================
        // 🎬 4. 補佐が降りてきて自慢げにする
        // ==========================================
        if (hosa != null)
        {
            Vector3 targetDropPosition = new Vector3(hosa.transform.position.x, hosaFloorPosition.y, hosa.transform.position.z);
            while (Vector3.Distance(hosa.transform.position, targetDropPosition) > 0.05f)
            {
                // 💡【修正】ここもインスペクターの倍速設定を反映！
                float currentDropSpeed = IsSkipping ? hosaMoveSpeed * skipSpeedMultiplier : hosaMoveSpeed;

                hosa.transform.position = Vector3.MoveTowards(hosa.transform.position, targetDropPosition, currentDropSpeed * Time.deltaTime);
                yield return null;
            }
            hosa.transform.position = targetDropPosition;
        }

        currentState = RescueState.Talking;
        yield return StartCoroutine(Speak(hosaBubble, ImageBubble.StampType.Doya));

        // ==========================================
        // 🎬 5. オートスタンプ会話劇
        // ==========================================
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

    private void CompleteEvent()
    {
        currentState = RescueState.Finished;
        Debug.Log("イベント完了！");

        if (playerController != null && playerController.inputActions != null)
        {
            playerController.inputActions.Player.Enable();
        }

        if (hosa != null)
        {
            hosa.transform.localRotation = Quaternion.identity;
            hosa.TransitionToState(hosa.StateFollow);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AdvanceStoryPhase();
        }
    }
}