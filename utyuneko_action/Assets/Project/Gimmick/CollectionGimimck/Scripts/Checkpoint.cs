using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    private bool isTriggered = false; // ★1回踏んだら true にしてロックする

    private void OnTriggerEnter2D(Collider2D other) // (3DならCollider other)
    {
        if (isTriggered) return; // すでに踏まれていたら何もしない

        if (other.CompareTag("Player"))
        {
            if (DataManager.Instance != null)
            {
                isTriggered = true; // ★ここでロック！

                Vector3 spawnPosition = transform.position + Vector3.up * 0.5f;
                DataManager.Instance.UpdateCheckpoint(spawnPosition);
            }
        }
    }
}