
using UnityEngine;

public class ShakeTarget : MonoBehaviour
{
    public static ShakeTarget Instance { get; private set; }

    // ★完全カスタム用の「Custom」を型に追加
    public enum ShakeStyle { Tiny, Small, Medium, Large, Custom }

    [Header("自動連携設定（Player側の変更・インスペクター登録は不要）")]
    [Tooltip("ONにすると、プレイヤーが衝突した瞬間に自動でカメラが揺れます。")]
    public bool shakeOnPlayerCollision = true;
    [Tooltip("プレイヤーが衝突した時の揺れの種類を選べます。")]
    public ShakeStyle collisionShakeStyle = ShakeStyle.Medium;

    [Header("ズーム（距離）による自動補正設定")]
    [Tooltip("ONにすると、カメラが遠くに引いている(Z軸が深い)時、揺れが小さく見えてしまうのを防ぐため、自動で揺れを激しく補正します。")]
    public bool useZoomCompensation = true;
    [Tooltip("カメラがこのZ座標にいる時を「基準の揺れの強さ(1倍)」とします。")]
    public float referenceZOffset = -10f;

    private PlayerController playerController;

    private Vector3 initialLocalPosition;
    private float shakeMagnitude = 0f;
    private float shakeDuration = 0f;
    private float shakeTimer = 0f;
    private float noiseSampleTimer = 0f;
    private ShakeStyle currentStyle = ShakeStyle.Medium;

    // ★【新設】型以外を完全自由に指定された際、数値を保持するための内部変数
    private float customFrequency = 15f;
    private bool allowHorizontalShake = true;

    void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }
    }

    void Start()
    {
        initialLocalPosition = transform.localPosition;

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerController = playerObj.GetComponent<PlayerController>();
        }
        else
        {
            playerController = Object.FindFirstObjectByType<PlayerController>();
        }

        if (playerController != null && shakeOnPlayerCollision)
        {
            playerController.OnCollisionEnterEvent += HandlePlayerCollision;
        }
        else if (playerController == null)
        {
            Debug.LogWarning("[ShakeTarget] 'Player' タグの付いたオブジェクト、または PlayerController が見つかりません。");
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

            // 1. 周波数（ガタガタ度）の決定
            float frequency = customFrequency; // デフォルトはカスタム値を使用
            if (currentStyle != ShakeStyle.Custom)
            {
                switch (currentStyle)
                {
                    case ShakeStyle.Tiny: frequency = 25f; break; // 微振動は細かく
                    case ShakeStyle.Small: frequency = 20f; break;
                    case ShakeStyle.Medium: frequency = 15f; break;
                    case ShakeStyle.Large: frequency = 12f; break; // 大揺れはダイナミックに
                }
            }

            noiseSampleTimer += Time.deltaTime * frequency;

            // 2. パーリンノイズによるベースの揺れ計算
            float xNoise = (Mathf.PerlinNoise(noiseSampleTimer, 0f) - 0.5f) * 2f;
            float yNoise = (Mathf.PerlinNoise(0f, noiseSampleTimer) - 0.5f) * 2f;

            // 3. 【工夫：縦横の制限】
            // 型がTiny/Smallの場合、またはカスタム指定で横揺れが禁止されている場合は横揺れをカット
            if (currentStyle == ShakeStyle.Tiny || currentStyle == ShakeStyle.Small ||
               (currentStyle == ShakeStyle.Custom && !allowHorizontalShake))
            {
                xNoise = 0f; // 横揺れを完全にカットして2Dでの酔いを防止
            }

            // 4. 【工夫：渦巻くような収束】
            float t = shakeTimer / shakeDuration;
            float damptarget = t * t;

            float swirlAngle = (1f - t) * 90f;
            Vector3 rawNoise = new Vector3(xNoise, yNoise, 0f);
            Vector3 swirledNoise = Quaternion.Euler(0, 0, swirlAngle) * rawNoise;

            // 5. 【工夫：距離（ズーム）による強さの自動補正】
            float zoomMultiplier = 1f;
            if (useZoomCompensation && transform.parent != null)
            {
                float currentZ = Mathf.Abs(transform.parent.position.z);
                float refZ = Mathf.Abs(referenceZOffset);
                if (refZ > 0)
                {
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
        // 新しい共通関数を呼び出す
        Shake(collisionShakeStyle);
    }

    private (float duration, float magnitude) GetPreset(ShakeStyle style)
    {
        return style switch
        {
            ShakeStyle.Tiny => (0.20f, 0.30f),
            ShakeStyle.Small => (0.20f, 0.40f),
            ShakeStyle.Medium => (0.20f, 0.55f),
            ShakeStyle.Large => (0.28f, 0.90f),
            _ => (0.15f, 0.40f)
        };
    }

    // ===================================================================
    // ??【拡張】外部呼び出し用インターフェース（オーバーロードで整理）
    // ===================================================================

    /// <summary>
    /// パターンA：既存の型（大中小など）のプリセットで手軽に揺らします。
    /// </summary>
    public void Shake(ShakeStyle style)
    {
        var preset = GetPreset(style);
        if (shakeTimer > 0 && preset.magnitude < shakeMagnitude) return;

        currentStyle = style;
        shakeDuration = preset.duration;
        shakeTimer = preset.duration;
        shakeMagnitude = preset.magnitude;
        noiseSampleTimer = Random.Range(0f, 100f);
    }

    /// <summary>
    /// パターンB：秒数と揺れの強さを自由な数値で指定して揺らします（細かさは標準）。
    /// </summary>
    public void Shake(float duration, float magnitude)
    {
        // パターンCのフルカスタム関数に標準値を渡して実行
        Shake(duration, magnitude, 15f, true);
    }

    /// <summary>
    /// パターンC：型以外のすべてのパラメータ（秒数、強さ、細かさ、横揺れ許可）を完全に自由指定して揺らします。
    /// </summary>
    /// <param name="duration">揺れる秒数</param>
    /// <param name="magnitude">揺れの強さ・振幅</param>
    /// <param name="frequency">ガタガタの細かさ（数値が大きいほど細かく高速に震える）</param>
    /// <param name="allowHorizontal">横揺れを含めるか（falseにすると縦揺れのみになり2Dで酔いにくくなります）</param>
    public void Shake(float duration, float magnitude, float frequency, bool allowHorizontal = true)
    {
        // 既にこれより強い揺れが実行中なら、上書きせずに無視する安全弁
        if (shakeTimer > 0 && magnitude < shakeMagnitude) return;

        currentStyle = ShakeStyle.Custom; // カスタムモードに設定
        shakeDuration = duration;
        shakeTimer = duration;
        shakeMagnitude = magnitude;
        customFrequency = frequency;
        allowHorizontalShake = allowHorizontal;
        noiseSampleTimer = Random.Range(0f, 100f);
    }
}

//using UnityEngine;

//public class ShakeTarget : MonoBehaviour
//{
//    public static ShakeTarget Instance { get; private set; }

//    // 揺れの種類を大中小・微振動で定義
//    public enum ShakeStyle { Tiny, Small, Medium, Large }

//    [Header("自動連携設定（Player側の変更・インスペクター登録は不要）")]
//    [Tooltip("ONにすると、プレイヤーが衝突した瞬間に自動でカメラが揺れます。")]
//    public bool shakeOnPlayerCollision = true;
//    [Tooltip("プレイヤーが衝突した時の揺れの種類を選べます。")]
//    public ShakeStyle collisionShakeStyle = ShakeStyle.Medium;

//    [Header("ズーム（距離）による自動補正設定")]
//    [Tooltip("ONにすると、カメラが遠くに引いている(Z軸が深い)時、揺れが小さく見えてしまうのを防ぐため、自動で揺れを激しく補正します。")]
//    public bool useZoomCompensation = true;
//    [Tooltip("カメラがこのZ座標にいる時を「基準の揺れの強さ(1倍)」とします。")]
//    public float referenceZOffset = -10f;

//    // 内部で保持するため、シリアライズ（インスペクター表示）は不要に
//    private PlayerController playerController;

//    private Vector3 initialLocalPosition;
//    private float shakeMagnitude = 0f;
//    private float shakeDuration = 0f;
//    private float shakeTimer = 0f;
//    private float noiseSampleTimer = 0f;
//    private ShakeStyle currentStyle = ShakeStyle.Medium;

//    void Awake()
//    {
//        if (Instance == null) { Instance = this; }
//        else { Destroy(gameObject); }
//    }

//    void Start()
//    {
//        initialLocalPosition = transform.localPosition;

//        // ★ タグが "Player" のオブジェクトを探して、コンポーネントを自動取得
//        GameObject playerObj = GameObject.FindWithTag("Player");
//        if (playerObj != null)
//        {
//            playerController = playerObj.GetComponent<PlayerController>();
//        }
//        else
//        {
//            // タグで見つからない場合の保険として、型検索も残しておく
//            playerController = Object.FindFirstObjectByType<PlayerController>();
//        }

//        // イベントの登録
//        if (playerController != null && shakeOnPlayerCollision)
//        {
//            playerController.OnCollisionEnterEvent += HandlePlayerCollision;
//        }
//        else if (playerController == null)
//        {
//            Debug.LogWarning("[ShakeTarget] 'Player' タグの付いたオブジェクト、または PlayerController が見つかりません。");
//        }
//    }

//    void OnDestroy()
//    {
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

//            // 1. 揺れの種類（スタイル）に応じた、周波数（ガタガタ度）の決定
//            float frequency = 15f;
//            switch (currentStyle)
//            {
//                case ShakeStyle.Tiny: frequency = 25f; break; // 微振動は細かく
//                case ShakeStyle.Small: frequency = 20f; break;
//                case ShakeStyle.Medium: frequency = 15f; break;
//                case ShakeStyle.Large: frequency = 12f; break; // 大揺れはダイナミックに
//            }

//            noiseSampleTimer += Time.deltaTime * frequency;

//            // 2. パーリンノイズによるベースの揺れ計算
//            float xNoise = (Mathf.PerlinNoise(noiseSampleTimer, 0f) - 0.5f) * 2f;
//            float yNoise = (Mathf.PerlinNoise(0f, noiseSampleTimer) - 0.5f) * 2f;

//            // 3. 【工夫：縦横の制限】微振動と小は「縦揺れのみ」にする
//            if (currentStyle == ShakeStyle.Tiny || currentStyle == ShakeStyle.Small)
//            {
//                xNoise = 0f; // 横揺れを完全にカットして2Dでの酔いを防止
//            }

//            // 4. 【工夫：渦巻くような収束】
//            float t = shakeTimer / shakeDuration;
//            float damptarget = t * t;

//            float swirlAngle = (1f - t) * 90f;
//            Vector3 rawNoise = new Vector3(xNoise, yNoise, 0f);
//            Vector3 swirledNoise = Quaternion.Euler(0, 0, swirlAngle) * rawNoise;

//            // 5. 【工夫：距離（ズーム）による強さの自動補正】
//            float zoomMultiplier = 1f;
//            if (useZoomCompensation && transform.parent != null)
//            {
//                float currentZ = Mathf.Abs(transform.parent.position.z);
//                float refZ = Mathf.Abs(referenceZOffset);
//                if (refZ > 0)
//                {
//                    zoomMultiplier = currentZ / refZ;
//                }
//            }

//            // 最終的な揺れの強さを適用
//            float finalMagnitude = shakeMagnitude * damptarget * zoomMultiplier;
//            transform.localPosition = initialLocalPosition + swirledNoise * finalMagnitude;

//            if (shakeTimer <= 0)
//            {
//                transform.localPosition = initialLocalPosition;
//            }
//        }
//    }

//    private void HandlePlayerCollision(Collision2D collision)
//    {
//        // 登録されたスタイルで直接揺らす
//        RequestShakeStyle(collisionShakeStyle);
//    }

//    private (float duration, float magnitude) GetPreset(ShakeStyle style)
//    {
//        return style switch
//        {
//            ShakeStyle.Tiny => (0.20f, 0.30f),
//            ShakeStyle.Small => (0.20f, 0.40f),
//            ShakeStyle.Medium => (0.20f, 0.55f),
//            ShakeStyle.Large => (0.28f, 0.90f),
//            _ => (0.15f, 0.40f)
//        };
//    }

//    public void RequestShakeStyle(ShakeStyle style)
//    {
//        var preset = GetPreset(style);
//        if (shakeTimer > 0 && preset.magnitude < shakeMagnitude) return;

//        currentStyle = style;
//        shakeDuration = preset.duration;
//        shakeTimer = preset.duration;
//        shakeMagnitude = preset.magnitude;
//        noiseSampleTimer = Random.Range(0f, 100f);
//    }

//    public void RequestShakeDirect(float duration, float magnitude, ShakeStyle style = ShakeStyle.Medium)
//    {
//        if (shakeTimer > 0 && magnitude < shakeMagnitude) return;

//        currentStyle = style;
//        shakeDuration = duration;
//        shakeTimer = duration;
//        shakeMagnitude = magnitude;
//        noiseSampleTimer = Random.Range(0f, 100f);
//    }
//}
