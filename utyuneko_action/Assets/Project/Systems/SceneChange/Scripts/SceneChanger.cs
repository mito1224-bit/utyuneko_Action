using UnityEngine;
using UnityEngine.SceneManagement; // シーン切り替えに必要

public class SceneChanger : MonoBehaviour
{
    [SerializeField] private string nextSceneName; // インスペクターからシーン名を指定

    // 2D用のトリガーイベントに変更
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 接触したオブジェクトが「Player」タグを持っているかチェック
        if (other.CompareTag("Player"))
        {
            // 指定したシーンをロード
            SceneManager.LoadScene(nextSceneName);
        }
    }
}