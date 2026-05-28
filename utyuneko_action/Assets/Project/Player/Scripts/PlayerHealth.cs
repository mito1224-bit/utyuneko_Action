using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("基本ステータス")]
    [SerializeField] private int maxHealth = 3;
    private int currentHealth;

    [Header("無敵時間の設定")]
    [SerializeField] private float iFrameDuration = 1.0f; // 被弾後の無敵時間（秒）
    private float iFrameTimer = 0f;
    private bool isInvincible = false;

    [Header("バースト中の無敵設定")]
    [SerializeField] private bool isInvincibleDuringBurst = true; // バースト突進中は無敵にするか

    private PlayerController p;

    // 2.5Dゲームの見た目用コンポーネント（3DモデルならMeshRenderer、2DならSpriteRenderer）
    private Renderer visualRenderer;

    void Start()
    {
        currentHealth = maxHealth;

        // 司令塔（Controller2D版）を取得
        p = GetComponent<PlayerController>();

        // 子オブジェクトからRenderer（MeshRendererまたはSpriteRenderer）を自動取得
        visualRenderer = GetComponentInChildren<Renderer>();

        Debug.Log($"プレイヤーHP初期化: {currentHealth}/{maxHealth}");
    }

    void Update()
    {
        // 無敵時間のカウントダウン処理
        if (isInvincible)
        {
            iFrameTimer -= Time.deltaTime;

            // 【演出】無敵時間中はプレイヤーをチカチカ点滅させる
            if (visualRenderer != null)
            {
                float blink = Mathf.Sin(Time.time * 30f);
                visualRenderer.enabled = blink > 0;
            }

            if (iFrameTimer <= 0f)
            {
                isInvincible = false;
                if (visualRenderer != null) visualRenderer.enabled = true; // 確実に表示に戻す
                Debug.Log("無敵状態が解除されました");
            }
        }
    }

    // ダメージを受けるコアメソッド
    public void TakeDamage(int damageAmount)
    {
        // 1. 被弾後の無敵時間中なら、すべてのダメージを無視
        if (isInvincible) return;

        // 2. 【Stateパターン連携】バースト中はダメージを無視！
        if (isInvincibleDuringBurst && p != null && p.CurrentState == p.StateBurst)
        {
            Debug.Log("バースト突進中のため、ダメージを弾き返しました！");
            // ここで「カキィン！」と火花エフェクトを出したりすると最高です！
            return;
        }

        // 3. ダメージ適用
        currentHealth -= damageAmount;
        currentHealth = Mathf.Max(0, currentHealth); // HPが0以下にならないようにロック
        Debug.Log($"被弾！ ダメージ: {damageAmount} / 残りHP: {currentHealth}");

        // 4. 無敵時間の開始
        isInvincible = true;
        iFrameTimer = iFrameDuration;

        // 5. 【Stateパターン連携】被弾したらバーストを強制解除して通常状態に戻す
        if (p != null && p.CurrentState == p.StateBurst)
        {
            p.TransitionToState(p.StateNormal);
        }

        // 6. 死亡判定
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("プレイヤー死亡。ゲームオーバー処理を実行します");
        gameObject.SetActive(false);
    }

    // ─── 2D用の衝突判定（Physics 2D） ───

    // 判定①：物理的にぶつかったとき（Solidな2Dコライダーを持つ敵やトゲ）
    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleDamageCollision(collision.gameObject);
    }

    // 判定②：すり抜ける設定のとき（IsTriggerな2Dコライダーを持つセンサーやエフェクト）
    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleDamageCollision(other.gameObject);
    }

    // 衝突したオブジェクトからダメージ情報を抜き出す共通処理
    private void HandleDamageCollision(GameObject hitObject)
    {
        // 当たった相手が「DamageSource」スクリプトを持っているか調べる
        DamageSource source = hitObject.GetComponent<DamageSource>();

        if (source != null)
        {
            // 持っていたら設定されているダメージ量を喰らう
            TakeDamage(source.damageAmount);
        }
    }
}