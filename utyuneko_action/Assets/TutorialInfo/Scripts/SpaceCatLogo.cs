using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SpaceCatLogo : MonoBehaviour
{
    [Header("ここにヒエラルキーのLogoをドラッグする")]
    public Image logo;

    [Header("ここにヒエラルキーのFadeをドラッグする")]
    public Image fade幕;

    private bool スキップした = false;

    IEnumerator Start()
    {
        // 最初はロゴも幕も完全に透明にしておく
        SetAlpha(logo, 0);
        SetAlpha(fade幕, 0);

        // 1. ロゴをじわーっと出す（フェードイン：1秒）
        float time = 0;
        while (time < 1.0f && !スキップした)
        {
            time += Time.deltaTime;
            SetAlpha(logo, time / 1.0f);
            yield return null;
        }
        SetAlpha(logo, 1); // 確実に100%表示にする

        // 2. 仕様書：「1秒くらいしたらフェードアウトし始める」
        time = 0;
        while (time < 1.0f && !スキップした)
        {
            time += Time.deltaTime;
            yield return null;
        }

        // 3. 仕様書：「何かしらのボタンを押したらすぐフェードアウトし始める」
        // 黒い幕をじわーっと出して画面を消す（フェードアウト：1秒）
        time = 0;
        while (time < 1.0f)
        {
            time += Time.deltaTime;
            SetAlpha(fade幕, time / 1.0f);
            yield return null;
        }
        SetAlpha(fade幕, 1); // 確実に画面を真っ暗にする

        // 4. 仕様書：「消えたらタイトルへ」
        // TitleSceneという名前のシーンへ切り替えます
        SceneManager.LoadScene("Title");
    }

    void Update()
    {
        // ゲーム中に何かキーボードやマウスが押されたら、スキップフラグをONにする
        if (Input.anyKeyDown)
        {
            スキップした = true;
        }
    }

    // 透明度を書き換えるための便利機能
    void SetAlpha(Image img, float a)
    {
        if (img == null) return;
        Color c = img.color;
        c.a = a;
        img.color = c;
    }
}