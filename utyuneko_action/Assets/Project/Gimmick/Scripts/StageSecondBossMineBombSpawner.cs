using UnityEngine;

/// <summary>
/// 💣 地雷（StageSecondBossMineBomb）のスポナー
/// このオブジェクトの位置に地雷を生成し、地雷が消滅（爆発・破壊）したら
/// 一定時間経過後に自動で再生成します。
/// </summary>
public class StageSecondBossMineBombSpawner : MonoBehaviour
{
    [Header("⚙️ 生成設定")]
    [Tooltip("生成する地雷のプレハブ（StageSecondBossMineBomb付き）")]
    public StageSecondBossMineBomb minePrefab;

    [Tooltip("生成位置（未設定ならこのスポナー自身の位置を使用）")]
    public Transform spawnPoint;

    [Tooltip("ゲーム開始時にすぐ1個生成するか")]
    public bool spawnOnStart = true;

    [Header("⏱️ 再生成設定")]
    [Tooltip("地雷が消滅してから再生成されるまでの秒数")]
    public float respawnDelay = 5.0f;

    [Tooltip("再生成を有効にするか（falseなら最初の1個だけ）")]
    public bool enableRespawn = true;

    [Header("✨ 演出（任意）")]
    [Tooltip("再生成時に出すエフェクト（不要ならNoneでOK）")]
    public GameObject respawnEffect;

    // 現在フィールドに存在している地雷のインスタンス
    private StageSecondBossMineBomb currentMine;
    // 再生成待ちタイマー
    private float respawnTimer = 0f;
    // 再生成待ち状態かどうか
    private bool isWaitingRespawn = false;

    void Start()
    {
        if (spawnOnStart)
        {
            SpawnMine();
        }
        else if (enableRespawn)
        {
            // 開始時に生成しない場合は、respawnDelay経過後に初回生成
            isWaitingRespawn = true;
            respawnTimer = 0f;
        }
    }

    void Update()
    {
        // 地雷がまだ生きているなら何もしない
        if (currentMine != null) return;

        if (!enableRespawn) return;

        // 地雷が消滅した瞬間 → 再生成待ちを開始
        if (!isWaitingRespawn)
        {
            isWaitingRespawn = true;
            respawnTimer = 0f;
        }

        respawnTimer += Time.deltaTime;
        if (respawnTimer >= respawnDelay)
        {
            SpawnMine();
        }
    }

    /// <summary>
    /// 地雷を1個生成する（外部から手動で呼んでもOK）
    /// </summary>
    public void SpawnMine()
    {
        if (minePrefab == null)
        {
            Debug.LogWarning($"[{name}] minePrefab が未設定です！インスペクターで設定してください。", this);
            return;
        }

        // すでに存在しているなら二重生成しない
        if (currentMine != null) return;

        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;

        currentMine = Instantiate(minePrefab, pos, Quaternion.identity);

        if (respawnEffect != null)
        {
            Instantiate(respawnEffect, pos, Quaternion.identity);
        }

        isWaitingRespawn = false;
        respawnTimer = 0f;
    }

    /// <summary>
    /// 現在の地雷を強制的に消して、再生成タイマーをリセットしたい時用
    /// </summary>
    public void DespawnCurrentMine()
    {
        if (currentMine != null)
        {
            Destroy(currentMine.gameObject);
            currentMine = null;
        }
        isWaitingRespawn = false;
        respawnTimer = 0f;
    }

    // シーンビューで生成位置が分かるようにギズモ表示
    void OnDrawGizmosSelected()
    {
        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
        Gizmos.DrawWireSphere(pos, 0.5f);

        if (minePrefab != null)
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.3f);
            Gizmos.DrawWireSphere(pos, minePrefab.activeTriggerRadius);
        }
    }
}