using UnityEngine;

public class BitCubeController : MonoBehaviour
{
    [Header("ビットキューブの設定")]
    [Tooltip("このビットキューブを獲得した時に獲得できるスコア（枚数）")]
    [SerializeField] private int scoreValue = 1;

    // 将来リザルトやマネージャーに枚数を伝えるための静的イベント（必要に応じて使用）
    public static System.Action<int> OnBitCubeCollected;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            OnBitCubeCollected?.Invoke(scoreValue);

            // ★ 自分自身（子）ではなく、親のオブジェクトごと消去する
            if (transform.parent != null)
            {
                Destroy(transform.parent.gameObject);
            }
            else
            {
                Destroy(gameObject); // 万が一、親がいない場合の保険
            }
        }
    }
}
