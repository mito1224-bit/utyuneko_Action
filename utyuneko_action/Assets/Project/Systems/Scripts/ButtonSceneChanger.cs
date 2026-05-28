using UnityEngine;
using UnityEngine.SceneManagement; // シーン切り替えに必要

public class ButtonSceneChanger : MonoBehaviour
{
    // ボタンを押したときに実行する関数
    public void ChangeScene(string sceneName)
    {
        // 引数（sceneName）で受け取ったシーンへ切り替える
        SceneManager.LoadScene(sceneName);
    }
}