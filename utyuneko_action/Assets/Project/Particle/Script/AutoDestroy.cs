using UnityEngine;

public class AutoDestroy : MonoBehaviour
{
    [SerializeField] private float delay = 5f; // è¡Ç¶ÇÈÇ‹Ç≈ÇÃïbêî
    void Start()
    {
        Destroy(gameObject, delay);
    }
}
