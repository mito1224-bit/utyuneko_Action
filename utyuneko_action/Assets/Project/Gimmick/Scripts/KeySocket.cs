using UnityEngine;
using System.Collections;
using UnityEngine.Events; // ★これを追加！

public class KeySocket : MonoBehaviour
{
    [Header("吸い込み設定")]
    [SerializeField] private float snapDuration = 0.2f;

    [Header("連動する仕掛け")]
    [SerializeField] private UnityEvent onKeySnapped; // ★インスペクターからイベントを設定できるようにする

    private bool isSnapped = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isSnapped && other.CompareTag("Key"))
        {
            Rigidbody2D keyRb = other.GetComponent<Rigidbody2D>();
            KeyGimmick keyGimmick = other.GetComponent<KeyGimmick>();

            if (keyRb != null)
            {
                isSnapped = true;

                keyRb.linearVelocity = Vector2.zero;
                keyRb.bodyType = RigidbodyType2D.Kinematic; // 前回の修正部分

                if (keyGimmick != null)
                {
                    keyGimmick.enabled = false;
                }

                StartCoroutine(SnapRoutine(other.transform));
            }
        }
    }

    private IEnumerator SnapRoutine(Transform keyTransform)
    {
        float elapsedTime = 0f;
        Vector3 startPos = keyTransform.position;
        Vector3 targetPos = transform.position;

        while (elapsedTime < snapDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsedTime / snapDuration);

            keyTransform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        keyTransform.position = targetPos;

        Debug.Log("カギがカチッとはまりました！仕掛け起動！");

        // ★ここで壁（登録したイベント）を動かす！
        if (onKeySnapped != null)
        {
            onKeySnapped.Invoke();
        }
    }
}