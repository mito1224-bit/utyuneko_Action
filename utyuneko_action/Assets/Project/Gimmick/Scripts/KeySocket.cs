using UnityEngine;
using System.Collections;

public class KeySocket : MonoBehaviour
{
    [Header("吸い込み設定")]
    [SerializeField] private float snapDuration = 0.2f; // カチッとはまるまでの時間（秒）

    private bool isSnapped = false; // すでにカギがハマっているか

    private void OnTriggerEnter2D(Collider2D other)
    {
        // まだハマっていなくて、衝突したのが「カギ」だった場合
        if (!isSnapped && other.CompareTag("Key"))
        {
            // カギのRigidbody2Dとスクリプトを取得
            Rigidbody2D keyRb = other.GetComponent<Rigidbody2D>();
            KeyGimmick keyGimmick = other.GetComponent<KeyGimmick>();

            if (keyRb != null)
            {
                isSnapped = true;

                // 1. カギの物理挙動を完全に停止させる
                keyRb.linearVelocity = Vector2.zero;
               // keyRb.isKinematic = true; // 物理演算の影響を受けなくする（壁への衝突なども無効化）

                // 2. カギ側のスクリプトをオフにして、プレイヤーが叩いても動かなくする
                if (keyGimmick != null)
                {
                    keyGimmick.enabled = false;
                }

                // 3. 台座の中心へカチッと吸い込むコルーチンを開始
                StartCoroutine(SnapRoutine(other.transform));
            }
        }
    }

    private IEnumerator SnapRoutine(Transform keyTransform)
    {
        float elapsedTime = 0f;
        Vector3 startPos = keyTransform.position;
        // 目的地は、この台座（専用オブジェクト）の真ん中
        Vector3 targetPos = transform.position;

        // SE（カチッという音）を鳴らすならここ！
        // AudioSource.PlayClipAtPoint(snapSound, transform.position);

        while (elapsedTime < snapDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / snapDuration;

            // Lerpで台座の中心へスムーズに移動
            keyTransform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        // 完全に中心に固定
        keyTransform.position = targetPos;

        // ハマった後のイベント（扉が開くなど）をここに書く
        Debug.Log("カギがカチッとはまりました！仕掛け起動！");
    }
}