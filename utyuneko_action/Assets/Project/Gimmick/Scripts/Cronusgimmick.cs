using System.Collections;
using UnityEngine;

public class CronusGimmick : MonoBehaviour
{
    [Header("── 時間制御設定 ──────────────────")]
    [Tooltip("プレイヤーが触れた瞬間に、時間の進みを何倍にするか（0fで完全停止、0.1fで超スロー）")]
    [Range(0f, 1f)]
    [SerializeField] private float timeScaleAmount = 0.1f;

    [Tooltip("スロー（停止）を維持する時間（現実世界の実時間での秒数）")]
    [SerializeField] private float durationSeconds = 0.8f;

    [Tooltip("── 演出設定 ──────────────────")]
    [SerializeField] private float scaleEffectDuration = 0.15f;

    private bool _isUsed = false;
    private Collider2D _collider;
    private SpriteRenderer _spriteRenderer;
    private bool _isCollected = false;
    private Vector3 _originalScale;


    private void Start()
    {
        _collider = GetComponent<Collider2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _originalScale = transform.localScale;

        if (_collider != null) _collider.isTrigger = true;

    }


    private void OnTriggerEnter2D(Collider2D other)
    {
        // 既に発動中なら無視
        if (_isUsed) return;

        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                StartCoroutine(TimeControlRoutine());
            }
        }
    }

    private IEnumerator TimeControlRoutine()
    {
        _isUsed = true;

        // 【触れた瞬間の魔法】ゲーム全体の時間をスロー（または停止）にする
        Time.timeScale = timeScaleAmount;

        // 物理演算の更新頻度も時間の進みに合わせないと、画面がガタつくのを防ぐ
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        float elapsedTime = 0f;
        while (elapsedTime < scaleEffectDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / scaleEffectDuration;
            transform.localScale = Vector3.Lerp(_originalScale, Vector3.zero, t);
            yield return null;
        }

        // 見た目を一時的に消す（触れて砕け散った演出用）
        GetComponent<SpriteRenderer>().enabled = false;
        GetComponent<Collider2D>().enabled = false;

        Debug.Log($"クロノス発動！ 時間の速さを {timeScaleAmount} 倍にしました。");

        // ★重要★ Time.timeScale の影響を受けない「現実の時間（実時間）」で指定秒数待つ
        yield return new WaitForSecondsRealtime(durationSeconds);

        // 時間の進み方を元通り（通常）に戻す
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;

        Debug.Log("クロノスの効果が終了。時間が元に戻りました。");

        // 3秒後にギミックを復活させる（ステージ再利用のため）
        yield return new WaitForSeconds(3.0f);
        GetComponent<SpriteRenderer>().enabled = true;
        GetComponent<Collider2D>().enabled = true;
        _isUsed = false;
    }

    // 万が一、ステージクリア時などにスローのまま残るのを防ぐ安全装置
    private void OnDisable()
    {
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;
    }
}