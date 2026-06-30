using UnityEngine;

public class EventTriggerArea2D : MonoBehaviour
{
    [Header("連動させるイベントマネージャー")]
    [SerializeField] private BaseEventManager eventManager;

    private bool hasTriggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // プレイヤーが触れて、かつまだ未発動の場合のみ実行
        if (other.CompareTag("Player") && !hasTriggered)
        {
            if (eventManager != null)
            {
                hasTriggered = true; // 重複発動防止

                // 共通関数を呼び出す
                eventManager.OnAreaEntered();
            }
            else
            {
                Debug.LogError($"[{gameObject.name}] 連動するイベントマネージャーがインスペクターでセットされていません！");
            }
        }
    }
}