using UnityEngine;
using System.Collections; // コルーチンを使うために必要

//using UnityEngine;
//using System.Collections;

public class BitCubeController : MonoBehaviour
{
    [Header("ビットキューブの設定")]
    [SerializeField] private int scoreValue = 1;

    [Header("演出の設定")]
    [SerializeField] private float animationDuration = 1.0f; // 演出時間
    [SerializeField] private float moveUpDistance = 1.5f;     // 浮き上がる距離
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0, 0, 360);

    public static System.Action<int> OnBitCubeCollected;

    private bool isCollected = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isCollected) return;

        if (collision.CompareTag("Player"))
        {
            isCollected = true;

            OnBitCubeCollected?.Invoke(scoreValue);

            // エフェクトはアイテムの現在位置で再生
            FXManager.Instance.Play(FXType.ItemGet, transform.position);
            StartCoroutine(CollectAnimationRoutine());
        }
    }

    private IEnumerator CollectAnimationRoutine()
    {
        // 1. プレイヤーと再び当たらないように、自分のコライダーを即座に無効化
        if (TryGetComponent<Collider2D>(out var col)) col.enabled = false;
       

        // ★演出対象を決定（親がいれば親、いなければ自分自身をターゲットにする）
        Transform targetTransform = transform.parent != null ? transform.parent : transform;

        float elapsed = 0f;
        Vector3 startPosition = targetTransform.position;
        Vector3 endPosition = startPosition + Vector3.up * moveUpDistance;
        Vector3 startScale = targetTransform.localScale;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;

            // --- 全て targetTransform（親）に対して処理を行う ---

            // A. 親ごと上に移動
            targetTransform.position = Vector3.Lerp(startPosition, endPosition, t);

            // B. 親ごと回転
            targetTransform.Rotate(rotationSpeed * Time.deltaTime);

            // C. 親ごと次第に小さくする
            targetTransform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);

            yield return null;
            
        }

        // 2. 演出が終わったら、ターゲット（親オブジェクト）ごと削除
        Destroy(targetTransform.gameObject);
        
    }
}



//using UnityEngine;
//// using UnityEngine.VFX; // Particle System を使うため不要になったので削除

//public class BitCubeController : MonoBehaviour
//{
//    [Header("ビットキューブの設定")]
//    [Tooltip("このビットキューブを獲得した時に獲得できるスコア（枚数）")]
//    [SerializeField] private int scoreValue = 1;
//    [SerializeField] private GameObject itemEffectPrefab; // ここに作ったParticleのPrefabを入れる

//    // 将来リザルトやマネージャーに枚数を伝えるための静的イベント（必要に応じて使用）
//    public static System.Action<int> OnBitCubeCollected;

//    private void OnTriggerEnter2D(Collider2D collision)
//    {
//        if (collision.CompareTag("Player"))
//        {
//            OnBitCubeCollected?.Invoke(scoreValue);

//            // ★重要: 消滅する「前」にエフェクトを生成する（位置情報を正しく使うため）
//            CollectItem();

//            // 自分自身（子）ではなく、親のオブジェクトごと消去する
//            if (transform.parent != null)
//            {
//                Destroy(transform.parent.gameObject);
//            }
//            else
//            {
//                Destroy(gameObject); // 万が一、親がいない場合の保険
//            }
//        }
//    }

//    private void CollectItem()
//    {
//        if (itemEffectPrefab != null)
//        {
//            // 1. アイテムの今いる位置にエフェクトを実体化させる
//            GameObject effectObj = Instantiate(itemEffectPrefab, transform.position, Quaternion.identity);

//            // 2. 生成したオブジェクトから ParticleSystem コンポーネントを取り出す
//            ParticleSystem ps = effectObj.GetComponent<ParticleSystem>();
//            if (ps != null)
//            {
//                // 3. パーティクルを再生
//                ps.Play();
//            }

//            // 4. 先ほどParticle System側で「Stop Action: Destroy」に設定しているので、
//            // スクリプト側で Destroy(effectObj, 3f); を書かなくても、再生終了時に自動で消滅してくれます！
//        }
//    }
//}