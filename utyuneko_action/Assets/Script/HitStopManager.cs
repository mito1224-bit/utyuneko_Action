using UnityEngine;
using System.Collections;

public class HitStopManager : MonoBehaviour
{
    // シングルトン（どこからでも HitStopManager.Instance で呼べるようにする）
    public static HitStopManager Instance { get; private set; }

    private bool isWaiting = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ヒットストップを実行する関数
    public void Stop(float duration)
    {
        if (isWaiting) return;
        StartCoroutine(DoStop(duration));
    }

    private IEnumerator DoStop(float duration)
    {
        isWaiting = true;
        float originalTimeScale = Time.timeScale;

        // 時間を止める（完全な0だと不都合がある場合は0.01などにする）
        Time.timeScale = 0f;

        // 現実世界の時間で指定秒数待つ
        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale = originalTimeScale;
        isWaiting = false;
    }
}