using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

// ITransitionEffect.cs を削除し、ここに同居させる
// DigitalRainEffect・WipeEffect から普通に実装できる（ネストではなく同ファイル配置）
public interface ITransitionEffect
{
    void Initialize(float duration);
    IEnumerator FadeOut();
    IEnumerator FadeIn();
}

public enum TransitionType
{
    DigitalRain,
    Wipe,
    Fade,
    // 新しいエフェクトを追加するときはここに1行足すだけ
}

public class TransitionManager : MonoBehaviour
{
    public static TransitionManager Instance { get; private set; }

    // エフェクトの登録エントリ
    // 新しいエフェクトはInspectorのリストに追加するだけ（コード修正不要）
    [System.Serializable]
    private class TransitionEntry
    {
        public TransitionType type;
        public MonoBehaviour effect; // ITransitionEffect を実装したコンポーネントをアサイン
    }

    [Header("エフェクト登録（Inspectorでアサイン）")]
    [SerializeField] private List<TransitionEntry> effectEntries;

    [Header("共通設定")]
    [SerializeField] private float duration = 0.6f;

    private Dictionary<TransitionType, ITransitionEffect> effectMap;
    private bool isTransitioning = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        effectMap = new Dictionary<TransitionType, ITransitionEffect>();
        foreach (var entry in effectEntries)
        {
            if (entry.effect is ITransitionEffect effect)
            {
                effect.Initialize(duration);
                effectMap[entry.type] = effect;
            }
            else
            {
                Debug.LogWarning($"[TransitionManager] {entry.effect?.name} は ITransitionEffect を実装していません。");
            }
        }
    }

    // 外部スクリプトから呼ぶ口はここだけ
    public void ChangeScene(string sceneName, TransitionType type)
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionRoutine(sceneName, type));
    }

    private IEnumerator TransitionRoutine(string sceneName, TransitionType type)
    {
        isTransitioning = true;

        ITransitionEffect effect = GetEffect(type);
        if (effect == null) { isTransitioning = false; yield break; }

        // 【1. フェードアウト】
        yield return StartCoroutine(effect.FadeOut());

        // 【2. シーンを非同期読み込み】
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone) yield return null;

        yield return new WaitForSeconds(0.1f);

        // 【3. フェードイン】
        yield return StartCoroutine(effect.FadeIn());

        isTransitioning = false;
    }

    private ITransitionEffect GetEffect(TransitionType type)
    {
        if (effectMap.TryGetValue(type, out var effect)) return effect;

        Debug.LogWarning($"[TransitionManager] 未対応の TransitionType: {type}。最初のエフェクトで代替します。");
        foreach (var e in effectMap.Values) return e; // 先頭をフォールバック

        Debug.LogError("[TransitionManager] エフェクトが1つも登録されていません！");
        return null;
    }
}