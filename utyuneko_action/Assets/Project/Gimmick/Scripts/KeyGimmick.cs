using System.Runtime.CompilerServices;
using UnityEngine;
using System.Collections;

public class KeyGimmick : MonoBehaviour
{
    [Header("プレイヤーの跳ね返り設定")]
    [SerializeField] private float reflexRate = 1.2f;        // プレイヤーの跳ね返り速度への反映率
    [SerializeField] private float minImpact = 3.0f;         // 最低限の弾かれ度合い

    [Header("自動レール移動の設定")]
    [SerializeField] private float travelDuration = 0.5f;    // カギ穴に到達するまでの時間（秒）
    [SerializeField] private float curveHeight = 0.0f;       // ★ここを 0 にすると完全な直線ルートになります。少しフワッとさせたいなら 1 などを入れてください。

    [Header("目指すカギ穴（ソケット）")]
    [SerializeField] private KeySocket targetSocket;

    private Rigidbody2D myRb;
    private Collider2D myCollider;
    private bool isFlying = false;

    private void Start()
    {
        myRb = GetComponent<Rigidbody2D>();
        myCollider = GetComponent<Collider2D>();

        if (myRb == null) Debug.LogError("カギに Rigidbody2D がついていません！");
        if (targetSocket == null) Debug.LogWarning("targetSocket（カギ穴）をインスペクターで設定してください！");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // すでに飛行中（レール移動中）なら、連続で叩かれても無視する
        if (isFlying) return;

        // プレイヤーがカギに触れた時
        if (other.CompareTag("Player") && targetSocket != null)
        {
            Rigidbody2D playerRb = other.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                isFlying = true;

                // 1. プレイヤー側の挙動：カギに当たったら、プレイヤーだけを反対方向に弾く
                float playerSpeed = playerRb.linearVelocity.magnitude;
                if (playerSpeed < minImpact) playerSpeed = minImpact;

                // プレイヤーを弾き返す方向（カギ穴とは逆の方向へ飛ばす）
                Vector2 reflexDirection = ((Vector2)transform.position - (Vector2)targetSocket.transform.position).normalized;
                playerRb.linearVelocity = Vector2.zero;
                playerRb.AddForce(reflexDirection * (playerSpeed * reflexRate), ForceMode2D.Impulse);

                // 2. カギ側の挙動：【最重要】物理演算を完全に止めて、青い方向への移動を封じる
                myRb.linearVelocity = Vector2.zero;
                myRb.bodyType = RigidbodyType2D.Kinematic; // これで重力やプレイヤーの衝突によるズレが完全にゼロになります
                if (myCollider != null) myCollider.enabled = false; // 移動中にプレイヤーとゴツゴツ当たらないようにコライダーをオフにする

                // 赤い軌道（ルート）に強制的に乗せて移動させる
                StartCoroutine(FlyOnRedRouteRoutine());
            }
        }
    }

    // プレイヤーがどこから当たっても、100%確実に赤いルートしか通らなくなるコルーチン
    private IEnumerator FlyOnRedRouteRoutine()
    {
        float elapsedTime = 0f;
        Vector3 startPos = transform.position; // 叩かれた瞬間のカギの現在地
        Vector3 targetPos = targetSocket.transform.position; // カギ穴の正確な位置

        // 軌道の計算（中間地点を割り出す）
        Vector3 midPoint = (startPos + targetPos) / 2f;
        // curveHeightが0なら直線、数値が入っていれば上空を通る放物線のルートになります
        Vector3 controlPoint = midPoint + Vector3.up * curveHeight;

        while (elapsedTime < travelDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / travelDuration;

            // スムーズな加減速を適用（ベジェ曲線でルートを固定）
            float easeT = Mathf.SmoothStep(0f, 1f, t);

            Vector3 m1 = Vector3.Lerp(startPos, controlPoint, easeT);
            Vector3 m2 = Vector3.Lerp(controlPoint, targetPos, easeT);

            // カギの座標を、計算された赤いルートの上に強制的に書き換える
            transform.position = Vector3.Lerp(m1, m2, easeT);

            yield return null;
        }

        // 最後にカギ穴の真ん中にぴったり合わせる
        transform.position = targetPos;

        // カギ穴（KeySocket）のスクリプトを呼び出して、ガチャンとはめる仕掛けを起動
        targetSocket.SendMessage("OnTriggerEnter2D", myCollider, SendMessageOptions.DontRequireReceiver);
    }
}