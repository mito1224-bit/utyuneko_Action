using UnityEngine;

public class ShakeTarget : MonoBehaviour
{
    public static ShakeTarget Instance { get; private set; }

    // 揺れの種類を大中小・微振動で定義
    public enum ShakeStyle { Tiny, Small, Medium, Large }

    [Header("自動連携設定（Player側の変更は一切不要）")]
    [SerializeField] private PlayerController playerController;

    [Tooltip("ONにすると、プレイヤーが衝突した瞬間に自動でカメラが揺れます。")]
    public bool shakeOnPlayerCollision = true;
    [Tooltip("プレイヤーが衝突した時の揺れの種類を選べます。")]
    public ShakeStyle collisionShakeStyle = ShakeStyle.Medium;

    [Header("?? ズーム（距離）による自動補正設定")]
    [Tooltip("ONにすると、カメラが遠くに引いている(Z軸が深い)時、揺れが小さく見えてしまうのを防ぐため、自動で揺れを激しく補正します。")]
    public bool useZoomCompensation = true;
    [Tooltip("カメラがこのZ座標にいる時を「基準の揺れの強さ(1倍)」とします。")]
    public float referenceZOffset = -10f;

    private Vector3 initialLocalPosition;
    private float shakeMagnitude = 0f;
    private float shakeDuration = 0f;
    private float shakeTimer = 0f;
    private float noiseSampleTimer = 0f;
    private ShakeStyle currentStyle = ShakeStyle.Medium;

    void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }
    }

    void Start()
    {
        initialLocalPosition = transform.localPosition;

        if (playerController == null)
        {
            playerController = Object.FindFirstObjectByType<PlayerController>();
        }

        if (playerController != null && shakeOnPlayerCollision)
        {
            playerController.OnCollisionEnterEvent += HandlePlayerCollision;
        }
    }

    void OnDestroy()
    {
        if (playerController != null)
        {
            playerController.OnCollisionEnterEvent -= HandlePlayerCollision;
        }
    }

    void Update()
    {
        if (shakeTimer > 0)
        {
            shakeTimer -= Time.deltaTime;

            // 1. 揺れの種類（スタイル）に応じた、周波数（ガタガタ度）の決定
            float frequency = 15f;
            switch (currentStyle)
            {
                case ShakeStyle.Tiny: frequency = 25f; break; // 微振動は細かく
                case ShakeStyle.Small: frequency = 20f; break;
                case ShakeStyle.Medium: frequency = 15f; break;
                case ShakeStyle.Large: frequency = 12f; break; // 大揺れはダイナミックに
            }

            noiseSampleTimer += Time.deltaTime * frequency;

            // 2. パーリンノイズによるベースの揺れ計算
            float xNoise = (Mathf.PerlinNoise(noiseSampleTimer, 0f) - 0.5f) * 2f;
            float yNoise = (Mathf.PerlinNoise(0f, noiseSampleTimer) - 0.5f) * 2f;

            // 3. 【工夫：縦横の制限】微振動と小は「縦揺れのみ」にする
            if (currentStyle == ShakeStyle.Tiny || currentStyle == ShakeStyle.Small)
            {
                xNoise = 0f; // 横揺れを完全にカットして2Dでの酔いを防止
            }

            // 4. 【工夫：渦巻くような収束】
            // 残り時間(t)をなめらかなカーブ(t^2)にしてキュッと中心に吸い込まれるように減衰させる
            float t = shakeTimer / shakeDuration;
            float damptarget = t * t;

            // さらに、収束していくにつれてわずかに回転を加えることで「渦巻く収束」を表現
            float swirlAngle = (1f - t) * 90f; // 収束に向けて最大90度回転させる
            Vector3 rawNoise = new Vector3(xNoise, yNoise, 0f);
            Vector3 swirledNoise = Quaternion.Euler(0, 0, swirlAngle) * rawNoise;

            // 5. 【工夫：距離（ズーム）による強さの自動補正】
            float zoomMultiplier = 1f;
            if (useZoomCompensation && transform.parent != null)
            {
                // 親の現在のZ位置（遠さ）を取得
                float currentZ = Mathf.Abs(transform.parent.position.z);
                float refZ = Mathf.Abs(referenceZOffset);
                if (refZ > 0)
                {
                    // 基準より遠くに引いている場合、その比率に応じて揺れを激しくする
                    zoomMultiplier = currentZ / refZ;
                }
            }

            // 最終的な揺れの強さを適用
            float finalMagnitude = shakeMagnitude * damptarget * zoomMultiplier;
            transform.localPosition = initialLocalPosition + swirledNoise * finalMagnitude;

            if (shakeTimer <= 0)
            {
                transform.localPosition = initialLocalPosition;
            }
        }
    }

    private void HandlePlayerCollision(Collision2D collision)
    {
        // プレイヤー衝突時は、インスペクターで設定したスタイルで揺らす
        RequestShakeStyle(collisionGridStyle(collisionShakeStyle));
    }

    // スタイルに応じたプリセット数値を返す内部用関数
    private (float duration, float magnitude) GetPreset(ShakeStyle style)
    {
        return style switch
        {
            ShakeStyle.Tiny => (0.10f, 0.15f), // 微振動：一瞬、かなり弱い
            ShakeStyle.Small => (0.12f, 0.30f), // 小振動：短く、少し弱い
            ShakeStyle.Medium => (0.18f, 0.55f), // 中振動：標準
            ShakeStyle.Large => (0.28f, 0.90f), // 大振動：長く、激しい
            _ => (0.15f, 0.40f)
        };
    }

    /// <summary>
    /// 【新機能】大中小のスタイルを指定してカメラを揺らす（おすすめ）
    /// </summary>
    public void RequestShakeStyle(ShakeStyle style)
    {
        var preset = GetPreset(style);

        // 既に強いシェイクが走っている場合は上書きしない
        if (shakeTimer > 0 && preset.magnitude < shakeMagnitude) return;

        currentStyle = style;
        shakeDuration = preset.duration;
        shakeTimer = preset.duration;
        shakeMagnitude = preset.magnitude;
        noiseSampleTimer = Random.Range(0f, 100f);
    }

    /// <summary>
    /// 従来の、数値で直接細かく指定して揺らす関数（互換性用）
    /// </summary>
    public void RequestShakeDirect(float duration, float magnitude, ShakeStyle style = ShakeStyle.Medium)
    {
        if (shakeTimer > 0 && magnitude < shakeMagnitude) return;

        currentStyle = style;
        shakeDuration = duration;
        shakeTimer = duration;
        shakeMagnitude = magnitude;
        noiseSampleTimer = Random.Range(0f, 100f);
    }

    // タイポ吸収用
    private ShakeStyle collisionGridStyle(ShakeStyle style) => style;
}
//using UnityEngine;

//public class ShakeTarget : MonoBehaviour
//{
//    // どこからでも「ShakeTarget.Instance.RequestShake()」で呼べるようにするための仕組み
//    public static ShakeTarget Instance { get; private set; }

//    [Header("自動連携設定（Player側の変更は一切不要）")]
//    [SerializeField] private PlayerController playerController;

//    [Tooltip("ONにすると、プレイヤーが壁や敵に衝突した瞬間に自動でカメラが揺れます。")]
//    public bool shakeOnPlayerCollision = true;
//    public float collisionDuration = 0.15f; // 衝突時の揺れる長さ
//    public float collisionMagnitude = 0.4f;  // 衝突時の揺れの強さ

//    [Header("シェイクの基本設定")]
//    [Tooltip("揺れの細かさ（数値が大きいほどガタガタと細かく震えます）")]
//    public float noiseFrequency = 15f;

//    private Vector3 initialLocalPosition;
//    private float shakeMagnitude = 0f;
//    private float shakeDuration = 0f;
//    private float shakeTimer = 0f;
//    private float noiseSampleTimer = 0f;

//    void Awake()
//    {
//        // シングルトンの初期化
//        if (Instance == null)
//        {
//            Instance = this;
//        }
//        else
//        {
//            Destroy(gameObject);
//        }
//    }

//    void Start()
//    {
//        // Main Cameraの初期ローカル座標（通常は 0, 0, 0）を記憶
//        initialLocalPosition = transform.localPosition;

//        // インスペクターでPlayerが未設定の場合、シーン上から自動で探す
//        if (playerController == null)
//        {
//            playerController = Object.FindFirstObjectByType<PlayerController>();
//        }

//        // 【★重要】Player側のコードは書き換えたいので、カメラ側からイベントを監視しに行きます
//        if (playerController != null && shakeOnPlayerCollision)
//        {
//            playerController.OnCollisionEnterEvent += HandlePlayerCollision;
//        }
//    }

//    void OnDestroy()
//    {
//        // オブジェクトが破棄される際、メモリリーク防止のためイベントを解除
//        if (playerController != null)
//        {
//            playerController.OnCollisionEnterEvent -= HandlePlayerCollision;
//        }
//    }

//    void Update()
//    {
//        if (shakeTimer > 0)
//        {
//            shakeTimer -= Time.deltaTime;
//            noiseSampleTimer += Time.deltaTime * noiseFrequency;

//            // パーリンノイズによる滑らかでプロっぽいランダム座標の計算
//            float x = (Mathf.PerlinNoise(noiseSampleTimer, 0f) - 0.5f) * 2f;
//            float y = (Mathf.PerlinNoise(0f, noiseSampleTimer) - 0.5f) * 2f;

//            // 残り時間に合わせてだんだん揺れを弱くしていく（減衰処理）
//            float currentMagnitude = shakeMagnitude * (shakeTimer / shakeDuration);

//            // ローカル座標（親Pivotから見た位置）をずらす
//            transform.localPosition = initialLocalPosition + new Vector3(x, y, 0f) * currentMagnitude;

//            if (shakeTimer <= 0)
//            {
//                // シェイク終了時はピタッと元の位置（0,0,0）に戻す
//                transform.localPosition = initialLocalPosition;
//            }
//        }
//    }

//    // プレイヤーが何かにぶつかった瞬間に自動でトリガーされる関数
//    private void HandlePlayerCollision(Collision2D collision)
//    {
//        RequestShake(collisionDuration, collisionMagnitude);
//    }

//    /// <summary>
//    /// 外部のどんなスクリプトからでもカメラを揺らせる汎用関数
//    /// </summary>
//    /// <param name="duration">揺れる時間（秒）</param>
//    /// <param name="magnitude">揺れの強さ</param>
//    public void RequestShake(float duration, float magnitude)
//    {
//        // 既に強いシェイクが発生している場合は、新しく入ってきた弱いシェイクを無視する
//        if (shakeTimer > 0 && magnitude < shakeMagnitude) return;

//        shakeDuration = duration;
//        shakeTimer = duration;
//        shakeMagnitude = magnitude;

//        // 毎回違う揺れのパターンになるようにノイズの開始地点をランダムにする
//        noiseSampleTimer = Random.Range(0f, 100f);
//    }
//}