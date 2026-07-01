using UnityEngine;
using System.Collections;

public class MovingWall : MonoBehaviour
{
    [Header("移動設定")]
    [SerializeField] private Vector3 moveOffset = new Vector3(0f, 3f, 0f); // どれくらい動かすか（例：上に3マス）
    [SerializeField] private float moveDuration = 1.0f; // 動く時間（秒）

    private bool isOpened = false;

    // 外（KeySocket）からこの関数を呼び出す
    public void OpenWall()
    {
        if (!isOpened)
        {
            isOpened = true;
            StartCoroutine(MoveRoutine());
        }
    }

    private IEnumerator MoveRoutine()
    {
        float elapsedTime = 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + moveOffset; // 現在の場所から指定した分だけズレた位置

        while (elapsedTime < moveDuration)
        {
            elapsedTime += Time.deltaTime;
            // カギの時と同じく、滑らかに（SmoothStep）移動
            float t = Mathf.SmoothStep(0f, 1f, elapsedTime / moveDuration);

            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        transform.position = targetPos; // 完全に固定
    }
}