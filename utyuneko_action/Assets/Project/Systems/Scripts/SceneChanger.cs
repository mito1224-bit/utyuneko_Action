using UnityEngine;
using UnityEngine.SceneManagement; // シーン切り替えに必要

public class SceneChanger : MonoBehaviour
{
    [SerializeField] private string nextSceneName; // インスペクターからシーン名を指定

    // プレイヤーがトリガーに接触したときに呼ばれる
    private void OnTriggerEnter(Collider other)
    {
        // 接触したオブジェクトが「Player」タグを持っているかチェック
        if (other.CompareTag("Player"))
        {
            // 指定したシーンをロード
            SceneManager.LoadScene(nextSceneName);
        }
    }
}