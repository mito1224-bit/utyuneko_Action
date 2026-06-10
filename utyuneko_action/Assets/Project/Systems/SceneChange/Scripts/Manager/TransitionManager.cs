using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

// 使用できるトランジションの種類をここで定義する
// 新しいエフェクトを追加するときは、この enum に1行足すだけでOK
public enum TransitionType
{
    DigitalRain,
    Wipe,
}

public class TransitionManager : MonoBehaviour
{
    // どこからでも「TransitionManager.Instance」でアクセスできるようにする（シングルトン）
    public static TransitionManager Instance { get; private set; }

    [Header("各エフェクトのスクリプト")]
    [SerializeField] private DigitalRainEffect digitalRainEffect;
    [SerializeField] private WipeEffect wipeEffect;

    [Header("共通設定")]
    [SerializeField] private float duration = 0.6f;

    private bool isTransitioning = false; // 二重実行を防ぐフラグ

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 各エフェクトに共通の duration を渡して初期化
        digitalRainEffect.Initialize(duration);
        wipeEffect.Initialize(duration);
    }

    // 外部のスクリプト（トリガーなど）からここを呼ぶ
    // sceneName  : 遷移先のシーン名
    // type       : 使いたいエフェクトの種類
    public void ChangeScene(string sceneName, TransitionType type)
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionRoutine(sceneName, type));
    }

    private IEnumerator TransitionRoutine(string sceneName, TransitionType type)
    {
        isTransitioning = true;

        // 指定された種類に応じて、対応するエフェクトに処理を委譲する

        // 【1. フェードアウト】
        yield return StartCoroutine(GetEffect(type).FadeOut());

        // 【2. シーンを非同期読み込み】
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // 新シーン開始直後に少しタメを作る
        yield return new WaitForSeconds(0.1f);

        // 【3. フェードイン】
        yield return StartCoroutine(GetEffect(type).FadeIn());

        isTransitioning = false;
    }

    // TransitionType から対応するエフェクトを返すヘルパー関数
    private ITransitionEffect GetEffect(TransitionType type)
    {
        switch (type)
        {
            case TransitionType.DigitalRain: return digitalRainEffect;
            case TransitionType.Wipe: return wipeEffect;
            default:
                Debug.LogWarning($"未対応の TransitionType: {type}。DigitalRain を使います。");
                return digitalRainEffect;
        }
    }
}