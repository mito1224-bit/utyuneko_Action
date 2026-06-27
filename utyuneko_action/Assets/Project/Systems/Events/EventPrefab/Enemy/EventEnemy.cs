using System.Collections; // コルーチン用
using UnityEngine;

public class EventEnemy : MonoBehaviour
{
    [Header("イベント監督の参照")]
    [SerializeField] private RescueEventManager eventManager;

    [Header("吹っ飛び（ノックバック）の強さ")]
    [SerializeField] private float knockbackForceX = 8f;
    [SerializeField] private float knockbackForceY = 4f;

    [Header("見つめるターゲットの設定")]
    [Tooltip("プレイヤーや補佐など、こいつに見つめさせたいオブジェクトを登録する")]
    [SerializeField] private Transform targetToLookAt;

    [Header("回転パラメータ")]
    [Tooltip("ターゲットを振り向く時の滑らかさ")]
    [SerializeField] private float lookSmoothing = 12f;

    private Rigidbody2D rb;
    private Animator anim; // アニメーションを止めるためのコンポーネント用
    private bool isDefeated = false; // 二重撃破防止フラグ

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>(); // アニメーターを自動取得！

        if (rb != null)
        {
            // 当たる前の完全フリーズ
            rb.constraints = RigidbodyConstraints2D.FreezePositionX |
                             RigidbodyConstraints2D.FreezePositionY |
                             RigidbodyConstraints2D.FreezeRotation;
        }
    }

    void Update()
    {
        // 撃破されておらず、かつターゲットがセットされている場合のみ見つめる！
        if (!isDefeated && targetToLookAt != null)
        {
            KeepLookingAtTarget();
        }
    }

    // ===================================================================
    // ターゲットを3D空間で完全にロックオンする処理
    // ===================================================================
    private void KeepLookingAtTarget()
    {
        // 1. 自分から見たターゲットへの「3Dの方向ベクトル」を計算する
        Vector3 direction = targetToLookAt.position - transform.position;

        // 完全に同じ位置にいる場合の計算エラー（ログ警告）を防ぐ安全ガード
        if (direction.sqrMagnitude > 0.001f)
        {
            // 2. その方向を完全に正面(Z軸)として捉える3次元の回転クォータニオンを生み出す
            Quaternion targetRotation = Quaternion.LookRotation(-direction);

            // 3. 現在の向きから、ターゲットの向きへ滑らかに回転させる
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * lookSmoothing);
        }
    }

    // Is TriggerがOFFなので「Collision2D」で受け取る
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDefeated) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerController p = collision.gameObject.GetComponent<PlayerController>();

            if (p != null && p.CurrentState is PlayerState_Burst)
            {
                // プレイヤーの位置を渡して撃破処理へ
                Defeated(collision.transform.position);

                TimeManager.Instance.TriggerGlobalSlowMotion(1.0f, 0.2f);
            }
        }
    }

    private void Defeated(Vector3 playerPosition)
    {
        isDefeated = true; // この瞬間、3DロックオンUpdateが100%完全に停止します！

        // アニメーションをその場のポーズで完全フリーズ！
        if (anim != null)
        {
            anim.speed = 0f;
        }

        if (rb != null)
        {
            // 当たった瞬間のフリーズ解除
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        // プレイヤーがいた方向と逆（奥）へ吹き飛ばすベクトルを計算
        float pushDirection = (transform.position.x - playerPosition.x) > 0 ? 1f : -1f;
        Vector2 knockbackVector = new Vector2(pushDirection * knockbackForceX, knockbackForceY);

        rb.linearVelocity = knockbackVector;
        rb.gravityScale = 1f; // 重力を有効にして自然に落ちるように

        // 地面に横たわる演出（Z軸を90度傾ける）
        // 見つめるUpdateが完全に止まっているので、この横倒し回転がバグらず100%綺麗に上書き適用されます！
        transform.rotation = Quaternion.Euler(0f, 0f, pushDirection * -90f);

        StartCoroutine(LayDownRoutine());

        // 監督にお知らせ
        if (eventManager != null)
        {
            eventManager.OnEnemyDefeated(this);
        }
    }

    private IEnumerator LayDownRoutine()
    {
        yield return new WaitForSeconds(0.6f);

        // 完全に動きを止めてその場に固定
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    public void StartAbsorb(Transform targetTransform, float duration)
    {
        StartCoroutine(AbsorbRoutine(targetTransform, duration));
    }

    private IEnumerator AbsorbRoutine(Transform target, float duration)
    {
        Vector3 startPosition = transform.position;
        Vector3 startScale = transform.localScale;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            if (target == null) break;

            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;

            transform.position = Vector3.Lerp(startPosition, target.position, t);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);

            yield return null;
        }

        Destroy(gameObject);
    }
}