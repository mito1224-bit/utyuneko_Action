using UnityEngine;

public class AfterImageManager : MonoBehaviour
{
    // シングルトン（どこからでも HitStopManager.Instance で呼べるようにする）
    public static AfterImageManager Instance { get; private set; }

    [Header("設定")]
    [SerializeField] private GameObject afterImagePrefab; // ステップ1で作ったプレハブ
    [SerializeField] private float timeBetweenImages = 0.05f; // 残像を出す間隔（秒）
    [SerializeField] private float imageInitialAlpha = 0.5f; // 最初の透明度 (0?1)
    [SerializeField] private float imageDecaySpeed = 2f; // 消える速さ（大きいほどすぐ消える）
    [SerializeField] private Color imageColor = Color.white; // 残像の色

    private Transform playerTransform;
    private SpriteRenderer playerSR;
    private float timeSinceLastImage;
    private bool isEmitting = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void SetPlayer(Transform transform, SpriteRenderer sr)
    {
        playerTransform = transform;
        playerSR = sr;
    }

    // 残像を出し始める関数
    public void StartEmitting()
    {
        if (playerTransform == null) return;
        isEmitting = true;
        timeSinceLastImage = 0f; // すぐに1枚目を出す
    }

    // 残像を止める関数
    public void StopEmitting()
    {
        isEmitting = false;
    }

    void Update()
    {
        if (!isEmitting || playerTransform == null || playerSR == null) return;

        timeSinceLastImage += Time.deltaTime;

        if (timeSinceLastImage >= timeBetweenImages)
        {
            CreateAfterImage();
            timeSinceLastImage = 0f;
        }
    }

    private void CreateAfterImage()
    {
        // プレハブを生成
        GameObject obj = Instantiate(afterImagePrefab);
        AfterImage ai = obj.GetComponent<AfterImage>();

        if (ai != null)
        {
            // プレイヤーの今の状態を残像にコピーして初期化
            ai.Init(
                playerSR.sprite,
                playerTransform.position,
                playerTransform.rotation,
                playerTransform.localScale, // 縮尺もコピー
                imageColor,
                imageInitialAlpha,
                imageDecaySpeed
            );
        }
    }
}