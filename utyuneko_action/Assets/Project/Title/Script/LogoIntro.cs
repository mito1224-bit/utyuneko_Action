using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LogoManager : MonoBehaviour
{
    [SerializeField] private Image fadeImage;       // フェード用の画像
    [SerializeField] private float displayTime = 2.0f; // ロゴを表示しておく時間
    [SerializeField] private float fadeTime = 1.0f;    // フェードアウトにかかる時間
    [SerializeField] private string nextSceneName = "TitleScene"; // 次のシーン名

    void Start()
    {
        // 起動時はフェード用画像を透明にしておく
        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = 0f;
            fadeImage.color = c;

            // 最初からフェードインさせたい場合は c.a = 1f; にして Coroutineで下げてもOKです
        }

        // 演出のカウントダウンを開始
        StartCoroutine(LogoSequence());
    }

    IEnumerator LogoSequence()
    {
        // 1. 設定した時間だけロゴを見せる
        yield return new WaitForSeconds(displayTime);

        // 2. フェードアウト（だんだん暗くなる）
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            if (fadeImage != null)
            {
                Color c = fadeImage.color;
                // アルファ値（透明度）を0から1へ近づける
                c.a = Mathf.Clamp01(elapsed / fadeTime);
                fadeImage.color = c;
            }
            yield return null; // 1フレーム待つ
        }

        // 確実に真っ暗にする
        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = 1f;
            fadeImage.color = c;
        }

        // 3. タイトルシーンへ切り替え
        SceneManager.LoadScene(nextSceneName);
    }
}