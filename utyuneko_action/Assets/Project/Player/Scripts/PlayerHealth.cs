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

    [Header("バースト中の特殊設定")]
    [SerializeField] private bool isInvincibleDuringBurst = true; // バースト突進中は無敵にするか

    private PlayerController p;
    private SpriteRenderer spriteRenderer; // 被弾時にチカチカ点滅させる用（お好みで）

    void Start()
    {
        currentHealth = maxHealth;

        // プレイヤーの司令塔（Controller）と見た目（Renderer）を自動取得
        p = GetComponent<PlayerController>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        Debug.Log($"プレイヤーHP初期化: {currentHealth}/{maxHealth}");
    }

    void Update()
    {
        // 無敵時間のカウントダウン処理
        if (isInvincible)
        {
            iFrameTimer -= Time.deltaTime;

            // 【演出】無敵時間中はプレイヤーをチカチカ点滅させる（不要なら消してOK！）
            if (spriteRenderer != null)
            {
                float blink = Mathf.Sin(Time.time * 30f);
                spriteRenderer.enabled = blink > 0;
            }

            if (iFrameTimer <= 0f)
            {
                isInvincible = false;
                if (spriteRenderer != null) spriteRenderer.enabled = true; // 確実に表示に戻す
                Debug.Log("無敵状態が解除されました");
            }
        }
    }

    // ★ダメージを受けるコアメソッド（外部のギミックから直接呼ぶことも可能）
    public void TakeDamage(int damageAmount)
    {
        // 1. 被弾後の無敵時間中なら、すべてのダメージを無視
        if (isInvincible) return;

        // 2. 【Stateパターン連携】もし「バースト中は無敵」設定がONで、今バースト状態ならダメージを無視！
        // (p.CurrentState の記述方法は、実際のプロジェクトの変数名に合わせて調整してください)
        if (isInvincibleDuringBurst && p != null && p.CurrentState == p.StateBurst)
        {
            Debug.Log("バースト突進中のため、ダメージを弾き返しました！");

            // ここで「カキィン！」と火花エフェクトを出したり、ヒットストップをかけると最高に格好よくなります！
            return;
        }

        // 3. ダメージ適用
        currentHealth -= damageAmount;
        currentHealth = Mathf.Max(0, currentHealth); // HPが0以下にならないようにロック
        Debug.Log($"被弾！ ダメージ: {damageAmount} / 残りHP: {currentHealth}");

        // 4. 無敵時間の開始
        isInvincible = true;
        iFrameTimer = iFrameDuration;

        // 5. 【Stateパターン連携】被弾したらバーストを強制解除して通常状態に戻す場合
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
        // とりあえず最初は非アクティブに。ここに演出やリトライUI表示を書く
        gameObject.SetActive(false);
    }

    // ─── 衝突判定（コンポーネント吸い出し方式） ───

    // 判定①：物理的にぶつかったとき（Solidなコライダーを持つ敵やトゲ）
    private void OnCollisionEnter(Collision collision)
    {
        HandleDamageCollision(collision.gameObject);
    }

    // 判定②：すり抜ける設定のとき（IsTriggerなコライダーを持つセンサーや火の粉など）
    private void OnTriggerEnter(Collider other)
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
            // 持っていたら、相手のインスペクターで設定されているダメージ量をそのまま喰らう！
            TakeDamage(source.damageAmount);
        }
    }
}