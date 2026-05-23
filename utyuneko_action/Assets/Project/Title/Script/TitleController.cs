using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TitleController : MonoBehaviour
{
    [Header("フェードさせるグループ（CanvasGroup）")]
    [SerializeField] private CanvasGroup titleCanvasGroup;
    [SerializeField] private CanvasGroup uiCanvasGroup;

    [Header("フェードにかかる時間（秒）")]
    [SerializeField] private float titleFadeDuration = 1.0f;
    [SerializeField] private float uiFadeDuration = 0.5f;

    [Header("最初にコントローラーで選択させるボタン")]
    [SerializeField] private Button firstSelectedButton;
    [Header("ゲーム終了確認用パネル")]
    [SerializeField] private GameObject exitPanel;

    void Start()
    {
        // 最初はすべて透明＆終了画面は非表示にする
        if (titleCanvasGroup != null) titleCanvasGroup.alpha = 0;
        if (uiCanvasGroup != null) uiCanvasGroup.alpha = 0;
        if (exitPanel != null) exitPanel.SetActive(false);

        StartCoroutine(TitleFadeSequence());
    }

    IEnumerator TitleFadeSequence()
    {
        // 1. タイトルがフェードイン
        float timer = 0;
        if (titleCanvasGroup != null)
        {
            while (timer < titleFadeDuration)
            {
                timer += Time.deltaTime;
                titleCanvasGroup.alpha = Mathf.Lerp(0, 1, timer / titleFadeDuration);
                yield return null;
            }
            titleCanvasGroup.alpha = 1;
        }

        // 2. ボタンなどのUIがフェードイン
        timer = 0;
        if (uiCanvasGroup != null)
        {
            while (timer < uiFadeDuration)
            {
                timer += Time.deltaTime;
                uiCanvasGroup.alpha = Mathf.Lerp(0, 1, timer / uiFadeDuration);
                yield return null;
            }
            uiCanvasGroup.alpha = 1;
        }

        // 3. コントローラー操作のために最初のボタンを選択状態にする
        if (firstSelectedButton != null)
        {
            EventSystem.current.SetSelectedGameObject(firstSelectedButton.gameObject);
        }
    }

    // 各ボタンが押された時の処理
    public void OnStartPressed()
    {
        Debug.Log("ゲームスタートが押されました（現状機能なし）");
    }

    public void OnOptionPressed()
    {
        Debug.Log("オプションが押されました（現状機能なし）");
    }

    public void OnExitButtonPressed()
    {
        // 終了確認画面を出す
        if (exitPanel != null) exitPanel.SetActive(true);
    }
}