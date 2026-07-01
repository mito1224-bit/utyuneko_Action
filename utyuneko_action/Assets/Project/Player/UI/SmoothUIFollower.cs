using UnityEngine;

public class SmoothUIFollower : MonoBehaviour
{
    [Header("追従するターゲット（Player）")]
    [SerializeField] private Transform target;

    [Header("追従の滑らかさ（大きいほどキビキビ）")]
    [SerializeField] private float smoothSpeed = 12.0f;

    [Header("頭上のオフセット位置（高さ調整）")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.5f, 0f);

    void Start()
    {
        // インスペクターで登録し忘れていても、Playerタグから自動で探してくる
        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                target = playerObj.transform;
            }
        }

        // ゲーム開始時に一瞬でプレイヤーの頭上にワープさせる（初期位置のズレ防止）
        if (target != null)
        {
            transform.position = target.position + offset;
        }
    }

    void LateUpdate()
    {
        // 回転を固定
        transform.rotation = Quaternion.identity;

        if (target == null) return;

        // 目標地点（プレイヤーの現在地 ＋ 頭上の高さ）を計算
        Vector3 desiredPosition = target.position + offset;

        // Vector3.Lerp（線形補間）を使い、現在地から目標地点まで滑らかに補間する
        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * smoothSpeed);
    }
}