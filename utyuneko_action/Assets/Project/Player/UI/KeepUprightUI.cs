using UnityEngine;

// 親オブジェクト（プレイヤーの球体）がどれだけ回転しても、
// 自身の回転を常に正面（直立状態）にロックするスクリプト
public class KeepUprightUI : MonoBehaviour
{
    void LateUpdate()
    {
        // ワールド空間に対して、回転を完全にゼロ（角度なし）に固定します
        transform.rotation = Quaternion.identity;
    }
}