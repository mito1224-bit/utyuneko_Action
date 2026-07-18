using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

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

    [Header("死亡演出の設定")]
    [SerializeField] private TransitionType deathTransitionType = TransitionType.Fade;
    [SerializeField] private float deathHangTimeScale = 0.05f;     // タメ中のtimeScale
    [SerializeField] private float deathHangRealDuration = 0.4f;   // タメの実時間（timeScaleの影響を受けない）
    [SerializeField] private float deathShakeDuration = 0.6f;
    [SerializeField] private float deathShakeMagnitude = 4.0f;
    [SerializeField] private Color deathFlashColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private float deathFlashDuration = 0.15f;
    [SerializeField] private float deathFallRotationSpeed = 260f;  // 頽れる回転速度(度/秒)
    [SerializeField] private LayerMask groundLayerMask; // 地面のレイヤーを指定
    [SerializeField] private float deathFallSpeed = 15f; // 疑似落下速度
    [SerializeField] private GameObject glassShatterPrefab;
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
            return;
        }

        // 3. ダメージ適用
        SoundManager.Instance.PlaySE(SeType.PlayerEnemyAttackHit);
        SoundManager.Instance.FadeBGMVolume(0.2f, 0.0f);
        SoundManager.Instance.FadeBGMVolume(1.0f, 2.0f);

        TimeManager.Instance.StopSlowMotion();
        TimeManager.Instance.TriggerGlobalSlowMotion(0.3f, 0.2f);

        ShakeTarget.Instance.Shake(0.5f, 2.0f);

        p.OnEnemyKilledInBurst();

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
        if (currentHealth < 0) return;

        currentHealth += healAmount;
        currentHealth = Mathf.Min(maxHealth, currentHealth);

        Debug.Log($"回復！ 回復量: {healAmount} / 残りHP: {currentHealth}");

        OnHealthChanged?.Invoke();
    }

    private void Die()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopBGM(0.5f);
            SoundManager.Instance.StopLoopSE(p.gameObject);
            SoundManager.Instance.PlaySE(SeType.PlayerDie);
        }

        Debug.Log("プレイヤー死亡。ゲームオーバー処理を実行します");

        if (p != null) p.enabled = false;
        var col2D = GetComponent<Collider2D>();
        if (col2D != null) col2D.enabled = false;

        if (visualRenderer != null) visualRenderer.enabled = true;

        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;

        // 1. 既存の被弾エフェクト
        if (p != null && p.damageEffect != null)
            p.damageEffect.PlayDamageEffect(DamageType.Player);

        // ===================================================================
        // 👑【バグ修正箇所】一瞬の赤フラッシュの安全ガード処理
        // ===================================================================
        Color originalColor = Color.white;
        bool hasColorProperty = false;
        string activeColorPropertyName = "_Color"; // デフォルトの名前

        if (visualRenderer != null && visualRenderer.material != null)
        {
            Material mat = visualRenderer.material;

            // シェーダーがどのプロパティでメインカラーを保持しているかをチェック
            if (mat.HasProperty("_BaseColor"))
            {
                activeColorPropertyName = "_BaseColor";
                originalColor = mat.GetColor("_BaseColor");
                hasColorProperty = true;
                mat.SetColor("_BaseColor", deathFlashColor);
            }
            else if (mat.HasProperty("_Color"))
            {
                activeColorPropertyName = "_Color";
                originalColor = mat.color;
                hasColorProperty = true;
                mat.color = deathFlashColor;
            }
        }

        // 3. 強めのシェイク＋タメのスロー
        if (ShakeTarget.Instance != null)
            ShakeTarget.Instance.Shake(deathShakeDuration, deathShakeMagnitude);

        TimeManager.Instance.StopSlowMotion();
        TimeManager.Instance.TriggerGlobalSlowMotion(deathHangTimeScale, deathHangRealDuration);

        yield return new WaitForSecondsRealtime(deathFlashDuration);

        // ===================================================================
        // 👑【バグ修正箇所】元のマテリアルの色へ安全に戻す処理
        // ===================================================================
        if (hasColorProperty && visualRenderer != null && visualRenderer.material != null)
        {
            if (activeColorPropertyName == "_BaseColor")
            {
                visualRenderer.material.SetColor("_BaseColor", originalColor);
            }
            else
            {
                visualRenderer.material.color = originalColor;
            }
        }

        yield return new WaitForSecondsRealtime(deathHangRealDuration - deathFlashDuration);
        if (glassShatterPrefab != null)
        {
            Instantiate(glassShatterPrefab, transform.position, transform.rotation);
        }

        // 5. 時間を戻してからフェードへバトンタッチ
        TimeManager.Instance.StopSlowMotion();

        if (TransitionManager.Instance != null)
            TransitionManager.Instance.ChangeScene(currentSceneName, deathTransitionType);
        else
            SceneManager.LoadScene(currentSceneName);

        gameObject.SetActive(false);
    }

    // ─── 2D用の衝突判定 ───
    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleDamageCollision(collision.gameObject);
        HandleHealCollision(collision.gameObject);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleDamageCollision(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleDamageCollision(other.gameObject);
        HandleHealCollision(other.gameObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleDamageCollision(other.gameObject);
    }

    private void HandleDamageCollision(GameObject hitObject)
    {
        var source = hitObject.GetComponent<DamageSource>();
        var eventEnemy = hitObject.GetComponent<EventEnemy>();
        if (eventEnemy) if (eventEnemy.isDefeated) return;
        var timedBomb = hitObject.GetComponentInParent<StageSecondBossTimedBomb>();
        if (timedBomb) if (timedBomb.IsBlownAway) return;
        var mineBomb = hitObject.GetComponentInParent<StageSecondBossMineBomb>();
        if (mineBomb != null) if (mineBomb.IsBlownAway) return;

        if (source != null && source.enabled)
        {
            if (hitObject.CompareTag("Enemy") && p.CurrentState == p.StateBurst ||
                hitObject.CompareTag("Enemy") && p.CurrentState == p.StateCharge) return;
            TakeDamage(source.damageAmount);
        }
    }

    private void HandleHealCollision(GameObject hitObject)
    {
        HealSource source = hitObject.GetComponent<HealSource>();

        if (source != null)
        {
            SoundManager.Instance.PlaySE(SeType.PlayerRecovery);

            if (source.isFullHeal)
            {
                Heal(maxHealth);
                Debug.Log("【完全復旧】プレイヤーが全回復しました！");
            }
            else
            {
                Heal(source.healAmount);
            }

            if (source.isDestroy)
                Destroy(hitObject);
        }
    }
}