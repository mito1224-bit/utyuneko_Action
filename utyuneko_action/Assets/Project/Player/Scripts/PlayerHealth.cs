using UnityEngine;
using UnityEngine.SceneManagement;

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

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;

    public System.Action OnHealthChanged;

    void Start()
    {
        currentHealth = maxHealth;

        // 司令塔（Controller2D版）を取得
        p = GetComponent<PlayerController>();

        // 子オブジェクトからRenderer（MeshRendererまたはSpriteRenderer）を自動取得
        visualRenderer = GetComponentInChildren<Renderer>();

        Debug.Log($"プレイヤーHP初期化: {currentHealth}/{maxHealth}");

        OnHealthChanged?.Invoke();
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
        SoundManager.Instance.PlaySE(SeType.PlayerEnemyAttackHit);
        SoundManager.Instance.FadeBGMVolume(0.2f, 0.0f);
        SoundManager.Instance.FadeBGMVolume(1.0f, 2.0f);

        TimeManager.Instance.StopSlowMotion();
        TimeManager.Instance.TriggerGlobalSlowMotion(0.3f,0.2f);

        currentHealth -= damageAmount;
        currentHealth = Mathf.Max(-1, currentHealth); // HPが-1以下にならないようにロック
        Debug.Log($"被弾！ ダメージ: {damageAmount} / 残りHP: {currentHealth}");

        OnHealthChanged?.Invoke();

        // 4. 無敵時間の開始
        isInvincible = true;
        iFrameTimer = iFrameDuration;

        // 5. 【Stateパターン連携】被弾したらバーストを強制解除してダメージ状態に戻す
        if (p != null)
        {
            // ※注意: 直近の敵の座標を取得するため、この関数の引数にGameObjectを渡すか、
            // 面倒なら「現在のdB君の見た目の向き（Y軸が50度なら右向き、310度なら左向きなど）の真後ろ」に飛ばす形にします。
            // ここでは一番簡単な「dB君が今向いている方向の真後ろ」に吹っ飛ばすロジックにします。
            float currentYAngle = p.visualManager.playerVisual.localRotation.eulerAngles.y;

            // 310度付近（右向き）なら左（-1）へ、50度付近（左向き）なら右（1）へ吹っ飛ばす
            float xDir = (currentYAngle > 180f) ? -1f : 1f;
            Vector2 knockbackVector = new Vector2(xDir, 0.5f);

            // ダメージステートに方向を伝えて、ステート遷移！
            p.StateDamage.SetKnockbackDirection(knockbackVector);
            p.TransitionToState(p.StateDamage);
        }

        // 6. 死亡判定
        if (currentHealth <= 0)
        {
            p.damageEffect.PlayDamageEffect(DamageType.Player);
            Die();
        }
        else
        {
            p.damageEffect.PlayDamageEffect(DamageType.Drone);
        }
    }

    // 回復するコアメソッド
    public void Heal(int healAmount)
    {
        // すでに死亡している（あるいは死亡処理中）なら回復しない
        if (currentHealth < 0) return;

        // 回復処理（最大HPを超えないように制限）
        currentHealth += healAmount;
        currentHealth = Mathf.Min(maxHealth, currentHealth);

        Debug.Log($"回復！ 回復量: {healAmount} / 残りHP: {currentHealth}");

        // UI（ビット）にHPが変わったことを通知して、センターに整列し直させる
        OnHealthChanged?.Invoke();

        // ここで「キュィィン！」というデータ復旧っぽいSEや緑のパーティクルを出すと最高です！
    }

    private void Die()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopLoopSE(p.gameObject);
        }

        SoundManager.Instance.PlaySE(SeType.PlayerDie);

        Debug.Log("プレイヤー死亡。ゲームオーバー処理を実行します");
        gameObject.SetActive(false);

        string currentSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentSceneName);
    }

    // ─── 2D用の衝突判定（Physics 2D） ───

    // 判定①：物理的にぶつかったとき（Solidな2Dコライダーを持つ敵やトゲ）
    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleDamageCollision(collision.gameObject);
        HandleHealCollision(collision.gameObject);
    }
    // 無敵時間が切れた瞬間にまだ触れていたらダメージを食らわせるための判定
    private void OnCollisionStay2D(Collision2D collision)
    {
        // 無敵が切れた瞬間にまだ触れていたらダメージを食らわせる
        HandleDamageCollision(collision.gameObject);
    }

    // 判定②：すり抜ける設定のとき（IsTriggerな2Dコライダーを持つセンサーやエフェクト）
    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleDamageCollision(other.gameObject);
        HandleHealCollision(other.gameObject);
    }
    // 無敵時間が切れた瞬間にまだ触れていたらダメージを食らわせるための判定
    private void OnTriggerStay2D(Collider2D other)
    {
        // 無敵が切れた瞬間にまだ触れていたらダメージを食らわせる
        HandleDamageCollision(other.gameObject);
    }

    // 衝突したオブジェクトからダメージ情報を抜き出す共通処理
    private void HandleDamageCollision(GameObject hitObject)
    {
        // 当たった相手が「DamageSource」スクリプトを持っているか調べる
        var source = hitObject.GetComponent<DamageSource>();
        var eventEnemy = hitObject.GetComponent<EventEnemy>();
        if(eventEnemy) if (eventEnemy.isDefeated) return;
        var timedBomb = hitObject.GetComponentInParent<StageSecondBossTimedBomb>();
        if (timedBomb) if (timedBomb.IsBlownAway) return;
        var mineBomb = hitObject.GetComponentInParent<StageSecondBossMineBomb>();
        if (mineBomb != null) if (mineBomb.IsBlownAway) return;


        if (source != null && source.enabled)
        {
            if (hitObject.CompareTag("Enemy") && 
                p.CurrentState == p.StateBurst) return;

            // 持っていたら設定されているダメージ量を喰らう
            TakeDamage(source.damageAmount);
        }
    }

    private void HandleHealCollision(GameObject hitObject)
    {
        HealSource source = hitObject.GetComponent<HealSource>();

        if (source != null)
        {
            SoundManager.Instance.PlaySE(SeType.PlayerRecovery);

            // もし「全回復」にチェックが入っていたら
            if (source.isFullHeal)
            {
                // 最大HP分を回復メソッドに渡す（Healメソッド側で最大HPを超えないようにガードしているのでこれで全回復になります）
                Heal(maxHealth);
                Debug.Log("【完全復旧】プレイヤーが全回復しました！");
            }
            else
            {
                // チェックがなければ、設定された通常の回復量
                Heal(source.healAmount);
            }

            // もし「消える」にチェックが入っていたら、回復アイテムを消す
            if (source.isDestroy)
                Destroy(hitObject);
        }
    }
}