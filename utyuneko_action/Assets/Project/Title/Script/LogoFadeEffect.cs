using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LogoFadeEffect : MonoBehaviour
{
    [SerializeField] private Image blackScreen; // 黒い画面のImage
    [SerializeField] private float fadeDuration = 2.0f; // フェードにかける時間（秒）
    [SerializeField] private float waitTime = 1.0f;     // ロゴ表示後の待ち時間（秒）
    [SerializeField] private string nextSceneName = "TitleScene"; // 次のシーン名

    void Start()
    {
        // 自動で「BlackScreen」という名前の画像を探してセットする魔法のコード
        if (blackScreen == null)
        {
            blackScreen = GameObject.Find("BlackScreen").GetComponent<UnityEngine.UI.Image>();
        }

        StartCoroutine(FadeAndChangeScene());
    }

    IEnumerator FadeAndChangeScene()
    {
        float timer = 0f;
        Color initialColor = blackScreen.color;

        // 1. 最初は画面を真っ黒にする
        initialColor.a = 1f;
        blackScreen.color = initialColor;

        // 2. フェードイン（だんだん明るくなってロゴが見える）
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            initialColor.a = alpha;
            blackScreen.color = initialColor;
            yield return null;
        }

        // 完全に明るくする
        initialColor.a = 0f;
        blackScreen.color = initialColor;

        // 3. ロゴを見せるための待ち時間
        yield return new WaitForSeconds(waitTime);

        // 4. フェードアウト（だんだん暗くなって真っ黒になる）★ここを追加！
        timer = 0f; // タイマーをリセット
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, timer / fadeDuration); // 今度は 0 から 1 へ（暗くする）
            initialColor.a = alpha;
            blackScreen.color = initialColor;
            yield return null;
        }

        // 完全に真っ黒にする
        initialColor.a = 1f;
        blackScreen.color = initialColor;

        // ちょっとだけ真っ黒な余韻を残してからシーン切り替え
        yield return new WaitForSeconds(0.5f);

        // 5. 次のシーンへ
        SceneManager.LoadScene(nextSceneName);
    }
}