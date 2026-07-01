using System.Collections;
using UnityEngine;

public class CoreCubeEventManager : BaseEventManager
{
    public static CoreCubeEventManager Instance { get; private set; }

    public enum EventState
    {
        BeforeArea,       // 1. まだエリアに入っていない（初期状態）
        WaitingForCore,   // 2. キューブの位置を見せて、プレイヤーの回収を待っている状態
        CoreCollected,    // 3. キューブを回収して、後半演出が始まった状態
        Finished          // 4. すべて終了
    }

    [Header("現在のイベント状態（確認用）")]
    [SerializeField] private EventState currentState = EventState.BeforeArea;

    [Header("登場オブジェクトの設定")]
    [SerializeField] private HosaController hosa;
    [SerializeField] private Transform coreCubeTransform; // コアキューブの位置
    [SerializeField] private GameObject gateObject;        // 出現させたいゲートのオブジェクト

    [Header("🎬 ゲート出現時の演出設定")]
    [Tooltip("ゲートが『0』から元の大きさに拡大しきるまでにかける時間（秒）")]
    [SerializeField] private float gateAppearDuration = 0.8f;

    // ===================================================================
    // 🧱【新設】戻り防止の壁 ＆ 補佐の定位置（ベースポジション）設定
    // ===================================================================
    [Header("🚧 戻り防止の壁設定")]
    [Tooltip("コアキューブを取った瞬間に、元来た道を塞ぐために Active(true) にしたい壁オブジェクト")]
    [SerializeField] private GameObject blockingWall;

    [Header("🏃‍♂️ 補佐のイベント後待機位置設定")]
    [Tooltip("イベント終了後、補佐をいつもいさせたい『ステージの端』などに配置した空のGameObject")]
    [SerializeField] private Transform hosaBasePosition;

    [Header("頭上のスタンプ吹き出し（ImageBubble）の参照")]
    [SerializeField] private ImageBubble hosaBubble;

    [Header("補佐の移動スピード")]
    [SerializeField] private float hosaMoveSpeed = 6f;

    private CameraFollowWithZoom cameraFollow;
    private Vector3 originalGateScale = Vector3.one;

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
    }

    void Start()
    {
        currentState = EventState.BeforeArea;
        cameraFollow = FindFirstObjectByType<CameraFollowWithZoom>();

        if (gateObject != null)
        {
            originalGateScale = gateObject.transform.localScale;
            gateObject.SetActive(false);
        }

        // 🧱【新設】戻り防止用の壁は、ゲーム開始時は最初は消しておく（通れる状態）
        if (blockingWall != null)
        {
            blockingWall.SetActive(false);
        }
    }

    // 🚩 ①【前半戦】エリアに入った瞬間に全自動で呼ばれる演出（前回同様）
    protected override IEnumerator BaseAreaNoticeRoutine()
    {
        if (cameraFollow == null || hosa == null || coreCubeTransform == null)
        {
            baseAreaNoticeCoroutine = null;
            yield break;
        }

        InputManager.Instance.Disable();

        yield return StartCoroutine(Speak(hosaBubble, ImageBubble.StampType.Surprise, 1.0f));

        Vector3 hosaTargetPos = coreCubeTransform.position + Vector3.up * 1.5f;
        hosa.TransitionToState(hosa.StateEvent);

        while (Vector3.Distance(hosa.transform.position, hosaTargetPos) > 0.05f)
        {
            hosa.transform.position = Vector3.MoveTowards(hosa.transform.position, hosaTargetPos, hosaMoveSpeed * Time.deltaTime);
            yield return null;
        }
        hosa.transform.position = hosaTargetPos;

        float cameraTimeToTarget = 1.0f;
        //float cameraFreezeDuration = 2.0f;
        float cameraTimeToReturn = 1.0f;

        cameraFollow.StartTrackTarget(coreCubeTransform, 3f, 2f);

        yield return new WaitForSecondsRealtime(cameraTimeToTarget);

        if (hosaBubble != null)
        {
            yield return StartCoroutine(Speak(hosaBubble, ImageBubble.StampType.Star));
            yield return StartCoroutine(Wait(0.5f));
            hosaBubble.ShowStamp(ImageBubble.StampType.Bottom);
        }

        yield return new WaitForSecondsRealtime(1.5f);

        cameraFollow.ReturnToPlayerFromEvent(cameraTimeToReturn);

        yield return new WaitForSecondsRealtime(cameraTimeToReturn);

        currentState = EventState.WaitingForCore;
        baseAreaNoticeCoroutine = null;
    }

    // 💎 ②【後半戦】プレイヤーがコアキューブを拾った瞬間に呼ばれる演出
    public void OnCoreCollected()
    {
        if (currentState == EventState.BeforeArea || currentState == EventState.WaitingForCore)
        {
            if (baseAreaNoticeCoroutine != null)
            {
                StopCoroutine(baseAreaNoticeCoroutine);
                baseAreaNoticeCoroutine = null;
            }

            currentState = EventState.CoreCollected;
            StartEvent();

            if (hosaBubble != null) hosaBubble.StartFadeOut();

            activeTimelineCoroutine = StartCoroutine(CoreCollectionTimelineRoutine());
        }
    }

    private IEnumerator CoreCollectionTimelineRoutine()
    {
        yield return new WaitForSecondsRealtime(0.3f);

        // ===================================================================
        // 🧱【最速反映】コアキューブを取った瞬間に、後ろの壁をドン！と出現させる
        // ===================================================================
        if (blockingWall != null)
        {
            blockingWall.SetActive(true);

            // 🎵 ドン！という岩が閉まるような音（壁衝突音など）を鳴らすとさらに効果的！
            SoundManager.Instance.PlaySE(SeType.PlayerWallHit);
        }

        // 1. 補佐が大喜びする
        yield return StartCoroutine(Speak(hosaBubble, ImageBubble.StampType.Joy, 1.5f));

        // 🎥 2. カメラをゲートへ向かわせる（移動に1秒）
        float cameraTimeToTarget = 1.0f;
        float freezeTime = gateAppearDuration + 1.0f;
        float cameraTimeToReturn = 1.0f;

        if (gateObject != null && cameraFollow != null)
        {
            cameraFollow.PlayEventCameraWork(gateObject.transform, cameraTimeToTarget, freezeTime, cameraTimeToReturn);
        }

        yield return new WaitForSecondsRealtime(cameraTimeToTarget);

        // 🚪 3. カメラの目の前でゲートが拡大出現
        if (gateObject != null)
        {
            gateObject.transform.localScale = Vector3.zero;
            gateObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < gateAppearDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / gateAppearDuration);
                float lerpValue = Mathf.SmoothStep(0f, 1f, t);
                gateObject.transform.localScale = Vector3.Lerp(Vector3.zero, originalGateScale, lerpValue);
                yield return null;
            }
            gateObject.transform.localScale = originalGateScale;
        }

        // ===================================================================
        // 🏃‍♂️【補佐の移動演出】カメラがドアを映してプレイヤーに戻るまでの「タメ時間」を
        // 利用して、補佐をステージの端のベースポジションへスムーズに飛行移動させる！
        // ===================================================================
        if (hosa != null && hosaBasePosition != null)
        {
            StartCoroutine(MoveHosaToBasePositionRoutine());
        }

        // ドアを見つめる残り時間 ＋ カメラがプレイヤーに戻る時間を待つ
        yield return new WaitForSecondsRealtime(1.0f + cameraTimeToReturn);

        CompleteEvent();
    }

    // 🏃‍♂️ 内部コルーチン：補佐を指定されたベースポジションへ滑らかに飛ばす
    private IEnumerator MoveHosaToBasePositionRoutine()
    {
        if (hosa == null || hosaBasePosition == null) yield break;

        while (Vector3.Distance(hosa.transform.position, hosaBasePosition.position) > 0.05f)
        {
            hosa.transform.position = Vector3.MoveTowards(hosa.transform.position, hosaBasePosition.position, hosaMoveSpeed * Time.deltaTime);
            yield return null;
        }
        hosa.transform.position = hosaBasePosition.position;
        hosa.transform.rotation = hosaBasePosition.rotation; // 向きも合わせる
    }

    // ⏩ 長押しスキップされた時の裏側ワープ処理（安全弁も完全対応！）
    protected override void OnSkipWarp()
    {
        Debug.Log("コアキューブイベントをスキップしました。");

        if (cameraFollow != null) cameraFollow.ForceStopEventCameraWork();
        if (hosaBubble != null) hosaBubble.StartFadeOut();

        if (gateObject != null)
        {
            gateObject.SetActive(true);
            gateObject.transform.localScale = originalGateScale;
        }

        // 🧱 スキップされても、壁は確実にアクティブ（出現状態）にする
        if (blockingWall != null) blockingWall.SetActive(true);

        // 🏃‍♂️ スキップされたら、補佐を一瞬でステージの端（ベースポジション）に強制ワープ配置する
        if (hosa != null && hosaBasePosition != null)
        {
            hosa.transform.position = hosaBasePosition.position;
            hosa.transform.rotation = hosaBasePosition.rotation;

            // 💡 プレイヤーを一生追従（Follow）させず、ずっとステージの端にいてほしいので、
            // StateFollow には戻さずに Event 状態のままその場に居座らせます！
        }

        currentState = EventState.Finished;
    }

    private void CompleteEvent()
    {
        // 🏃‍♂️ 通常終了時、念のため補佐の座標をベースポジションに完全固定
        if (hosa != null && hosaBasePosition != null)
        {
            hosa.transform.position = hosaBasePosition.position;
            hosa.transform.rotation = hosaBasePosition.rotation;
        }

        currentState = EventState.Finished;
        EndEvent();
    }
}