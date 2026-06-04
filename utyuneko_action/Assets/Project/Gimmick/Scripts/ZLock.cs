using UnityEngine;

[ExecuteAlways] // エディタ上でも常に実行する
public class ZLock : MonoBehaviour
{
    [SerializeField] private float fixedZPosition = 0f;

    void Update()
    {
        // エディタ上でもゲーム中でも、Z座標を常に監視して固定する
        if (transform.position.z != fixedZPosition)
        {
            Vector3 pos = transform.position;
            pos.z = fixedZPosition;
            transform.position = pos;
        }
    }
}