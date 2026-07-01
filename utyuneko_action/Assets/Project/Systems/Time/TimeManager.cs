using System.Collections;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    private bool isGlobalHitStopping = false;

    private float normalTimeScale = 1.0f;
    private float normalFixedDeltaTime = 0.02f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    ///  ２つの特定のオブジェクトのヒットストップ管理
    /// </summary>
    public void TriggerIndividualHitStop(GameObject objA, GameObject objB, float duration)
    {
        if (objA != null)
        {
            HitStoppable stoppableA = objA.GetComponent<HitStoppable>();
            if (stoppableA != null) stoppableA.TriggerHitStop(duration);
        }
        if (objB != null)
        {
            HitStoppable stoppableB = objB.GetComponent<HitStoppable>();
            if (stoppableB != null) stoppableB.TriggerHitStop(duration);
        }
    }

    /// <summary>
    ///  特定のオブジェクト以外スローモーション管理
    /// </summary>
    public void TriggerAsynchronousHitStop(GameObject playerObj, float duration, float worldSlowScale = 0.05f)
    {
        if (isGlobalHitStopping) return;
        StartCoroutine(AsynchronousHitStopRoutine(playerObj, duration, worldSlowScale));
    }

    private IEnumerator AsynchronousHitStopRoutine(GameObject playerObj, float duration, float worldSlowScale)
    {
        isGlobalHitStopping = true;

        float previousTimeScale = Time.timeScale;
        float previousFixedDeltaTime = Time.fixedDeltaTime;

        // 1. 自機の HitStoppable を使って個別に完全フリーズさせる
        HitStoppable playerStop = playerObj != null ? playerObj.GetComponent<HitStoppable>() : null;
        if (playerStop != null) playerStop.TriggerHitStop(duration);

        // 2. 世界の時間は「超スローモーション」にする（エフェクトを美しく魅せる）
        Time.timeScale = worldSlowScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        yield return new WaitForSecondsRealtime(duration);

        // 3. ヒットストップが来たら、元の時間（チャージ中ならチャージのスロー）に安全に戻す
        Time.timeScale = previousTimeScale;
        Time.fixedDeltaTime = previousFixedDeltaTime;

        isGlobalHitStopping = false;
    }

    /// <summary>
    ///  ゲーム全体のストップ管理
    /// </summary>
    public void TriggerGlobalHitStop(float duration)
    {
        if (isGlobalHitStopping) return;
        StartCoroutine(GlobalHitStopRoutine(duration));
    }

    private IEnumerator GlobalHitStopRoutine(float duration)
    {
        isGlobalHitStopping = true;

        float previousTimeScale = Time.timeScale;
        float previousFixedDeltaTime = Time.fixedDeltaTime;

        Time.timeScale = 0f; // 完全ストップ

        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale = previousTimeScale;
        Time.fixedDeltaTime = previousFixedDeltaTime;

        isGlobalHitStopping = false;
    }

    /// <summary>
    /// ゲーム全体を指定時間だけスローモーションにする
    /// </summary>
    /// <param name="duration">スローにする時間（秒）</param>
    /// <param name="slowScale">スローの強さ（0.2f なら 5倍スロー）</param>
    public void TriggerGlobalSlowMotion(float duration, float slowScale)
    {
        // 他の全体ヒットストップ中や時限スロー中なら多重発動しないガード
        if (isGlobalHitStopping) return;
        StartCoroutine(GlobalSlowMotionRoutine(duration, slowScale));
    }

    private IEnumerator GlobalSlowMotionRoutine(float duration, float slowScale)
    {
        isGlobalHitStopping = true;

        // 発動前のTimeScale（通常時なら 1.0、空中チャージ中ならそのスロー値）を記憶
        float previousTimeScale = Time.timeScale;
        float previousFixedDeltaTime = Time.fixedDeltaTime;

        // ゲーム全体の時間を指定されたスロー速度に変更
        Time.timeScale = slowScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        // 現実世界の時間で指定秒数だけきっちり待つ
        yield return new WaitForSecondsRealtime(duration);

        // スローが明けたら、割り込まれる前の「正しい時間」に復元する
        Time.timeScale = previousTimeScale;
        Time.fixedDeltaTime = previousFixedDeltaTime;

        isGlobalHitStopping = false;
    }

    /// <summary>
    ///  ゲーム全体のスローモーション管理
    /// </summary>
    public void StartSlowMotion(float slowScale)
    {
        if (!isGlobalHitStopping)
        {
            Time.timeScale = slowScale;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
        }

        normalTimeScale = slowScale;
        normalFixedDeltaTime = 0.02f * slowScale;
    }

    /// <summary>
    /// ゲーム全体の時間を通常速度（1.0）に戻す
    /// </summary>
    public void StopSlowMotion()
    {
        normalTimeScale = 1.0f;
        normalFixedDeltaTime = 0.02f;

        if (!isGlobalHitStopping)
        {
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = 0.02f;
        }
    }
}