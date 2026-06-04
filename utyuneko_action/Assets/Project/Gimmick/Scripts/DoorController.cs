using UnityEngine;
using UnityEngine.SceneManagement;

public class DoorController : MonoBehaviour
{
    [Header("移動先のシーン名")]
    [SerializeField] private string nextSceneName;

    private bool isPlayerInside = false;

    void Update()
    {
        // プレイヤーが範囲内にいて、かつPキーが押されたか
        if (isPlayerInside && Input.GetKeyDown(KeyCode.P))
        {
            TriggerSceneChange();
        }
    }

    // 2D当たり判定（トリガー）に入ったとき
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 衝突した相手のタグが「Player」かどうかをチェック
        if (collision.CompareTag("Player"))
        {
            isPlayerInside = true;
        }
    }

    // 2D当たり判定から出たとき
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerInside = false;
        }
    }

    // シーン遷移の実行
    private void TriggerSceneChange()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogWarning("移動先のシーン名が設定されていません！");
        }
    }
}
