using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class DigitalRainTransition : MonoBehaviour
{
    // どこからでも「DigitalRainTransition.Instance」でアクセスできるようにする仕組み（シングルトン）
    public static DigitalRainTransition Instance { get; private set; }

    [Header("マテリアル設定")]
    [SerializeField] private Material transitionMaterial; // Full Screen Passにセットしたマテリアル

    [Header("時間設定")]
    [SerializeField] private float duration = 0.6f; // 演出にかかる時間

    private int progressID;
    private bool isTransitioning = false; // 二重に実行されるのを防ぐフラグ

    void Awake()
    {
        // シーンが切り替わっても、このオブジェクトが消えないように保持する
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

        // Shader Graphで作成した「_Progress」プロパティのIDを取得
        progressID = Shader.PropertyToID("_Progress");

        // ゲーム開始時はエフェクトを完全にオフ（0）にして通常画面を見せる
        if (transitionMaterial != null)
        {
            transitionMaterial.SetFloat(progressID, 0f);
        }
    }

    // 他のスクリプトから呼び出されて、シーン遷移のコルーチンを開始する関数
    public void FadeToScene(string sceneName)
    {
        // すでに遷移中なら重複して実行しない
        if (isTransitioning) return;

        if (transitionMaterial == null)
        {
            Debug.LogError("Transition Material がインスペクターからセットされていません！");
            return;
        }

        StartCoroutine(TransitionRoutine(sceneName));
    }

    private IEnumerator TransitionRoutine(string sceneName)
    {
        isTransitioning = true;
        float elapsed = 0f;

        // 【1. フェードアウト】画面がデジタル雨で崩壊していく (0 ➔ 1)
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            transitionMaterial.SetFloat(progressID, progress);
            yield return null;
        }
        transitionMaterial.SetFloat(progressID, 1f); // 完全にデジタルノイズで画面を覆う

        // 【2. 裏でシーンを非同期読み込み】
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // 新しいシーンが始まった直後、少しだけタメを作る（0.1秒）
        yield return new WaitForSeconds(0.1f);

        // 【3. フェードイン】新しいシーンでデジタル雨が晴れていく (1 ➔ 0)
        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(1.0f - (elapsed / duration));
            transitionMaterial.SetFloat(progressID, progress);
            yield return null;
        }
        transitionMaterial.SetFloat(progressID, 0f); // 完全に通常画面に戻す

        isTransitioning = false;
    }

    private void OnDestroy()
    {
        // ゲーム終了時や破棄時に、マテリアルを元の通常状態（0）に戻しておく
        if (transitionMaterial != null)
        {
            transitionMaterial.SetFloat(progressID, 0f);
        }
    }
}