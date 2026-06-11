using System.Collections;
using UnityEngine;

public class FuelCrystal : MonoBehaviour
{
    // インスペクターで選べる回復モード
    public enum RecoveryType
    {
        FullRecovery,    // 全回復（一気に使える回数を満タンにする）
        CustomAmount     // 指定した回数分だけ回復（燃料をオトクに小回復）
    }

    [Header("── 燃料回復設定 ──────────────────")]
    [Tooltip("回復のタイプを選びます\n・FullRecovery: 全回復\n・CustomAmount: 指定回数分だけ回復")]
    [SerializeField] private RecoveryType recoveryType = RecoveryType.FullRecovery;

    [Tooltip("RecoveryType を CustomAmount にした時だけ有効です。\n何回分のバースト（燃料）を回復させるか")]
    [Min(1)]
    [SerializeField] private int recoveryAmount = 1;

    [Header("── 復活設定 ──────────────────")]
    [Tooltip("クリスタルが取られてから復活するまでの時間（秒）。-1 にすると使い捨てになります")]
    [SerializeField] private float respawnDelay = 3.0f;

    [Header("── 演出設定 ──────────────────")]
    [SerializeField] private float scaleEffectDuration = 0.15f;

    private Collider2D _collider;
    private SpriteRenderer _spriteRenderer;
    private bool _isCollected = false;
    private Vector3 _originalScale;

    void Start()
    {
        _collider = GetComponent<Collider2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _originalScale = transform.localScale;

        if (_collider != null) _collider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isCollected && other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                _isCollected = true;

                // ★ プレイヤーの燃料を回復する処理
                ApplyFuelRecovery(player);

                StartCoroutine(CollectAndRespawnRoutine());
            }
        }
    }

    // プレイヤーの数値を外部から書き換えて回復させる関数
    private void ApplyFuelRecovery(PlayerController player)
    {
        if (recoveryType == RecoveryType.FullRecovery)
        {
            // 【全回復】消費カウントを 0（未消費＝満タン）にする
            player.currentBurstCount = 0;
            Debug.Log($"[{gameObject.name}] プレイヤーの燃料を回復しました！");
        }
        else if (recoveryType == RecoveryType.CustomAmount)
        {
            // 【指定量回復】現在の消費カウントから、設定した回復量を引き算する
            // （例：2回消費している状態で1回分回復したら、消費数は1になる）
            player.currentBurstCount -= recoveryAmount;

            // 回復しすぎてマイナス（満タン以上）にならないように、0でストップさせる
            if (player.currentBurstCount < 0)
            {
                player.currentBurstCount = 0;
            }
            Debug.Log($"[{gameObject.name}] プレイヤーの燃料を {recoveryAmount} 回分回復しました！ (現在値: {player.currentBurstCount})");
        }
    }

    private IEnumerator CollectAndRespawnRoutine()
    {
        float elapsedTime = 0f;
        while (elapsedTime < scaleEffectDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / scaleEffectDuration;
            transform.localScale = Vector3.Lerp(_originalScale, Vector3.zero, t);
            yield return null;
        }

        SetCrystalActive(false);

        if (respawnDelay < 0)
        {
            Destroy(gameObject);
            yield break;
        }

        yield return new WaitForSeconds(respawnDelay);

        transform.localScale = _originalScale;
        SetCrystalActive(true);
        _isCollected = false;
    }

    private void SetCrystalActive(bool active)
    {
        if (_spriteRenderer != null) _spriteRenderer.enabled = active;
        if (_collider != null) _collider.enabled = active;
    }
}