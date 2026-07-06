using System.Collections;
using UnityEngine;

public class CoreCubeController : MonoBehaviour
{
    // ここにProjectビューの「Goalプレハブ」をアタッチ
    [SerializeField] private GameObject goalPrefab;

    // ここにヒエラルキー上に作った「空のオブジェクト」をアタッチ
    [Header("ゴールの出現位置（空のオブジェクト）")]
    [SerializeField] private Transform spawnPoint;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        
        if (collision.CompareTag("Player"))
        {
            if (goalPrefab != null)
            {
                // 初期位置としてキューブ（親オブジェクト）の位置を設定しておく
                Vector3 targetPosition = transform.parent != null ? transform.parent.position : transform.position;

                // もしインスペクターに「空のオブジェクト」が登録されていれば、その位置を優先する
                if (spawnPoint != null)
                {
                    targetPosition = spawnPoint.position;
                    Debug.Log($"【デバッグ】指定されたオブジェクトの位置（{targetPosition}）にGoalを生成しました！");
                }
                else
                {
                    // 万が一、空のオブジェクトを登録し忘れたときの保険のログ
                    Debug.LogWarning("【デバッグ】警告: Spawn Point（空のオブジェクト）が未設定のため、キューブの位置に生成します。");
                }

                // 指定した位置にゴールを生成
                Instantiate(goalPrefab, targetPosition, Quaternion.identity);
            }
            else
            {
                Debug.LogError("【デバッグ】エラー: Goalプレハブが登録されていません。");
            }

            // キューブ（親ごと）を消去
            if (transform.parent != null)
            {
                Destroy(transform.parent.gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
