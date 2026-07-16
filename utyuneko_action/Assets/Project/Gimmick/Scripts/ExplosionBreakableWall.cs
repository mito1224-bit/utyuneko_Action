using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ExplosionBreakableWall : MonoBehaviour
{
    [Tooltip("何回の爆発で壊れるか")]
    public int hitsToBreak = 1;

    [Tooltip("壊れたときのエフェクト（任意）")]
    public GameObject breakEffect;

    [Tooltip("爆発判定を識別するタグ")]
    public string explosionTag = "MineExplosion";

    private int currentHits = 0;

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryBreak(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryBreak(collision.gameObject);
    }

    private void TryBreak(GameObject hitObj)
    {
        // 地雷の爆発判定オブジェクトだけに反応する
        if (!hitObj.CompareTag(explosionTag)) return;

        currentHits++;
        if (currentHits < hitsToBreak) return;

        Break();
    }

    private void Break()
    {
        if (breakEffect != null)
        {
            Instantiate(breakEffect, transform.position, Quaternion.identity);
        }
        // SoundManager.Instance.PlaySE(SeType.WallBreak); // 必要なら
        Destroy(gameObject);
    }
}