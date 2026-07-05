using UnityEngine;
using UnityEngine.SceneManagement; // シーン切り替えに必要

public class SceneChanger : MonoBehaviour
{
    [SerializeField] private string nextSceneName; // インスペクターからシーン名を指定

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 接触したオブジェクトが「Player」タグを持っているかチェック
        if (other.CompareTag("Player"))
        {
            if (DataManager.Instance != null)
            {
                DataManager.Instance.ProcessStageClear();
            }
            else
            {
                Debug.LogWarning("[SceneChanger] DataManagerが見つかりません。データが保存されずにシーンが切り替わります。");
            }

            // 指定したシーンをロード
            TransitionManager.Instance.ChangeScene(nextSceneName, TransitionType.Wipe);
        }
    }
}