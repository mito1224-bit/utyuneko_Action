using UnityEngine;

public class RescueTriggerArea : MonoBehaviour
{
    [SerializeField] private RescueEventManager manager;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (manager != null)
            {
                manager.OnAreaEntered();
            }
            else
            {
                Debug.LogError("マネージャーがインスペクターでセットされていません！");
            }
        }
    }
}