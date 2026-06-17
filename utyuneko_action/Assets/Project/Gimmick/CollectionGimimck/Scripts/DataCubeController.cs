using UnityEngine;

public class DataCubeController : MonoBehaviour
{
    [Header("データキューブの設定")]
    [Tooltip("このデータキューブの識別番号（例: 1枚目は 1、2枚目は 2）")]
    [SerializeField] private int dataCubeID = 1;

    // 将来リザルトやマネージャーに「何番目のデータキューブを取ったか」を伝えるイベント
    public static System.Action<int> OnDataCubeCollected;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            OnDataCubeCollected?.Invoke(dataCubeID);

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
