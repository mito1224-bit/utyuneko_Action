using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ExitPopupController : MonoBehaviour
{
    [SerializeField] private Button noButton; // 「いいえ」ボタン
    [SerializeField] private Button mainExitButton; // タイトル画面の「ゲーム終了」ボタン

    private void OnEnable()
    {
        // 確認画面が開いたら、自動的に「いいえ」を選択状態にする
        if (noButton != null)
        {
            EventSystem.current.SetSelectedGameObject(noButton.gameObject);
        }
    }

    // 「はい」が押されたとき
    public void OnYesPressed()
    {
        Debug.Log("ゲームを終了します");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }

    // 「いいえ」が押されたとき
    public void OnNoPressed()
    {
        this.gameObject.SetActive(false); // この画面を閉じる

        // タイトル画面の終了ボタンにフォーカスを戻す
        if (mainExitButton != null)
        {
            EventSystem.current.SetSelectedGameObject(mainExitButton.gameObject);
        }
    }
}