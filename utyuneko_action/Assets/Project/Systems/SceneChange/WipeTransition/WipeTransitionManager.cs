using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class WipeTransitionManager : MonoBehaviour
{
    // どこからでも呼び出せるようにする仕組み（シングルトン）
    public static WipeTransitionManager Instance { get; private set; }

    [SerializeField] private Image maskImage;
    [SerializeField] private float duration = 0.6f;   // シュッと動くように少し速めに

    private Material transitionMaterial;
    private int circleSizeID;
    private bool isTransitioning = false; // 二重クリック防止用

    void Awake()
    {
        // シーンが変わってもこのオブジェクト（Canvasごと）を消さない設定
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // これが最重要！
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        transitionMaterial = maskImage.material;
        circleSizeID = Shader.PropertyToID("_TransitionScale");

        // 最初は画面が見える状態（0）にしておく
        transitionMaterial.SetFloat(circleSizeID, 0f);
    }

    // 外部のスクリプト（ゴールやメニューボタン）からこれを呼ぶ
    public void ChangeScene(string sceneName)
    {
        if (isTransitioning) return;
        StartCoroutine(PlaySceneTransition(sceneName));
    }

    IEnumerator PlaySceneTransition(string sceneName)
    {
        isTransitioning = true;
        float elapsed = 0f;

        // 【1. フェードアウト】穴を小さくして画面を真っ黒にする
        float startSize = 0f;
        float endSize = 50f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            transitionMaterial.SetFloat(circleSizeID, Mathf.Lerp(startSize, endSize, easedT));
            //maskImage.transform.Rotate(0, 0, rotateSpeed * Time.deltaTime);
            yield return null;
        }
        transitionMaterial.SetFloat(circleSizeID, endSize); // 完全に真っ黒に固定

        // 【2. 裏でシーンを読み込む】
        // 画面が真っ黒な隙に、非同期で次のシーンをロードします
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);

        // ロードが完全に終わるまで、ここで待機
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // 新しいシーンが始まったので、ちょっとだけタメを作る（0.1秒）
        yield return new WaitForSeconds(0.1f);

        // 【3. フェードイン】新しいシーンで穴を大きくして画面を見せる
        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            transitionMaterial.SetFloat(circleSizeID, Mathf.Lerp(endSize, startSize, easedT));
            //maskImage.transform.Rotate(0, 0, rotateSpeed * Time.deltaTime);
            yield return null;
        }

        // 完全に開ききった状態に戻す
        transitionMaterial.SetFloat(circleSizeID, startSize);
        isTransitioning = false;
    }
}