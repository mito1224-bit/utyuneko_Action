using UnityEngine;

public class KeyGimmick : MonoBehaviour
{
    [Header("設定")]
    [SerializeField] private float moveSpeed = 8.0f;
    [SerializeField] private Transform[] pathPoints;
    [SerializeField] private KeySocket targetSocket;

    private Rigidbody2D myRb;
    private bool isFlying = false;
    private int currentPointIndex = 0;

    private void Start()
    {
        myRb = GetComponent<Rigidbody2D>();
        // 最新形式：bodyTypeを使用
        myRb.bodyType = RigidbodyType2D.Dynamic;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isFlying || !other.CompareTag("Player")) return;

        // 1. 物理エンジンとの接続を完全に断つ
        myRb.simulated = false;

        // 2. プレイヤーを弾く処理
        Rigidbody2D playerRb = other.GetComponent<Rigidbody2D>();
        if (playerRb != null)
        {
            Vector2 reflexDirection = ((Vector2)transform.position - (Vector2)targetSocket.transform.position).normalized;
            playerRb.linearVelocity = Vector2.zero;
            playerRb.AddForce(reflexDirection * 10f, ForceMode2D.Impulse);
        }

        // 3. レール移動開始
        isFlying = true;
        currentPointIndex = 0;
    }

    private void FixedUpdate()
    {
        if (!isFlying) return;

        // 移動中は物理を無視して位置を更新し続ける
        if (currentPointIndex < pathPoints.Length)
        {
            Vector2 targetPos = pathPoints[currentPointIndex].position;
            transform.position = Vector2.MoveTowards(transform.position, targetPos, moveSpeed * Time.fixedDeltaTime);
            if (Vector2.Distance(transform.position, targetPos) < 0.2f) currentPointIndex++;
        }
        else
        {
            // ゴール到着時
            MoveToFinalSocket();
        }
    }

    private void MoveToFinalSocket()
    {
        Vector2 targetPos = targetSocket.transform.position;
        transform.position = Vector2.MoveTowards(transform.position, targetPos, moveSpeed * Time.fixedDeltaTime);

        if (Vector2.Distance(transform.position, targetPos) < 0.1f)
        {
            isFlying = false;
            // 最後に物理を復活させる
            myRb.simulated = true;
            myRb.bodyType = RigidbodyType2D.Dynamic;
            targetSocket.SendMessage("OnTriggerEnter2D", GetComponent<Collider2D>(), SendMessageOptions.DontRequireReceiver);
            enabled = false;
        }
    }
}