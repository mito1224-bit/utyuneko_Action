using UnityEngine;
using UnityEngine.SceneManagement; // ★ シーン管理のために追加

public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    [Header("--- 現在のステータス（確認用） ---")]
    [SerializeField] private int currentBitCubes = 0;
    [SerializeField] private int maxBitCube = 0;
    [Space(5)]
    [SerializeField] private int currentDataCubes = 0;
    [SerializeField] private int maxDataCube = 0;

    // スターコインの「何番目を取ったか」を記録する配列
    private bool[] collectedStarCoinFlags;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // ★ シーンロード時のイベントを登録（DontDestroyOnLoadのバグ対策）
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }


    void OnDestroy()
    {
        // ★ イベントの解除（メモリリーク防止）
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    void OnEnable()
    {
        // コイン側からの獲得通知イベントを登録
        BitCubeController.OnBitCubeCollected += AddNormalCoin;
        DataCubeController.OnDataCubeCollected += AddStarCoin;
    }

    void OnDisable()
    {
        // イベントの解除
        BitCubeController.OnBitCubeCollected -= AddNormalCoin;
        DataCubeController.OnDataCubeCollected -= AddStarCoin;
    }


    // ★ シーンがロードされたときに自動で実行される関数
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // ハブエリアやタイトル画面など、コインがないシーンは無視する（GameManagerと条件を合わせる）
        if (scene.name == "TitleScene" || scene.name == "LogoScene" ||
           scene.name == "ResultScene") return;

        // 1. 新しいステージに入ったので、現在の取得数をリセット
        currentBitCubes = 0;
        currentDataCubes = 0;

        // 2. シーン内に配置されているコインを自動でカウント（再計測）
        maxBitCube = Object.FindObjectsByType<BitCubeController>(FindObjectsSortMode.None).Length;
        maxDataCube = Object.FindObjectsByType<DataCubeController>(FindObjectsSortMode.None).Length;

        // 3. スターコインのフラグ配列を初期化
        collectedStarCoinFlags = new bool[maxDataCube + 1];

        Debug.Log($"【DataManager自動初期化】シーン「{scene.name}」: Bit最大 {maxBitCube} / Data最大 {maxDataCube}");
    }

    // 通常コインの加算
    private void AddNormalCoin(int amount)
    {
        currentBitCubes += amount;
        Debug.Log($"通常コイン獲得！ 現在: {currentBitCubes} / {maxBitCube}");
    }

    // スターコインの加算
    private void AddStarCoin(int id)
    {
        currentDataCubes++;

        // 獲得したIDのフラグをONにする
        if (id >= 0 && id < collectedStarCoinFlags.Length)
        {
            collectedStarCoinFlags[id] = true;
        }

        // ★【連携】GameManagerにリアルタイムでデータキューブ（スターコイン）の獲得を通知
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CollectDataCube(id);
        }

        Debug.Log($"スターコインID【{id}】獲得！ 現在: {currentDataCubes} / {maxDataCube}");
    }

    // ★【連携】ステージクリア時に外部（ゴールオブジェクトなど）から呼び出す関数
    public void ProcessStageClear()
    {
        if (GameManager.Instance != null)
        {
            // GameManagerに現在のBitCube（通常コイン）の数を渡して、セーブ＆クリア処理を行う
            GameManager.Instance.ClearStage(currentBitCubes);
            Debug.Log($"【クリア連携】GameManagerにBitCube数: {currentBitCubes} を送信しました。");
        }
    }

    // --- 情報受け渡し用関数群（変更なし） ---
    public (int current, int max) GetNormalCoinResult() => (currentBitCubes, maxBitCube);
    public (int current, int max) GetStarCoinResult() => (currentDataCubes, maxDataCube);

    public bool IsStarCoinCollected(int id)
    {
        if (id >= 0 && id < collectedStarCoinFlags.Length)
        {
            return collectedStarCoinFlags[id];
        }
        return false;
    }

    public float GetTotalCompletionRate()
    {
        int totalMax = maxBitCube + maxDataCube;
        if (totalMax == 0) return 100f;

        int totalCurrent = currentBitCubes + currentDataCubes;
        return ((float)totalCurrent / totalMax) * 100f;
    }
}




//using UnityEngine;

//public class DataManager : MonoBehaviour
//{
//    public static DataManager Instance { get; private set; }

//    [Header("--- 現在のステータス（確認用） ---")]
//    [SerializeField] private int currentBitCubes = 0;
//    [SerializeField] private int maxBitCube = 0;
//    [Space(5)]
//    [SerializeField] private int currentDataCubes = 0;
//    [SerializeField] private int maxDataCube = 0;

//    // スターコインの「何番目を取ったか」を記録する配列（リザルトで「2番目が未取得」等を表示するため）
//    // 配列の要素数は、配置されたスターコインの最大数に応じて自動で変わります
//    private bool[] collectedStarCoinFlags;

//    void Awake()
//    {
//        if (Instance == null)
//        {
//            Instance = this;
//            // ★これを追加！シーンが切り替わってもDataManagerが消えずに残るようになります
//            DontDestroyOnLoad(gameObject);
//        }
//        else
//        {
//            Destroy(gameObject);
//        }
//    }

//    void OnEnable()
//    {
//        // コイン側からの獲得通知イベントを登録
//        BitCubeController.OnBitCubeCollected += AddNormalCoin;
//        DataCubeController.OnDataCubeCollected += AddStarCoin;
//    }

//    void OnDisable()
//    {
//        // イベントの解除（メモリリーク防止）
//        BitCubeController.OnBitCubeCollected -= AddNormalCoin;
//        DataCubeController.OnDataCubeCollected -= AddStarCoin;
//    }

//    void Start()
//    {
//        // ★ シーン内に配置されているコインを自動でカウント
//        maxBitCube = Object.FindObjectsByType<BitCubeController>(FindObjectsSortMode.None).Length;
//        maxDataCube = Object.FindObjectsByType<DataCubeController>(FindObjectsSortMode.None).Length;

//        // スターコインのフラグ配列を初期化（最大数分用意する。インデックスのズレ防止に+1しておくと扱いやすい）
//        collectedStarCoinFlags = new bool[maxDataCube + 1];
//    }

//    // 通常コインの加算
//    private void AddNormalCoin(int amount)
//    {
//        currentBitCubes += amount;
//        Debug.Log($"通常コイン獲得！ 現在: {currentBitCubes} / {maxBitCube}");
//    }

//    // スターコインの加算
//    private void AddStarCoin(int id)
//    {
//        currentDataCubes++;

//        // 獲得したIDのフラグをONにする（例: ID 2のコインを取ったら index 2 を true に）
//        if (id >= 0 && id < collectedStarCoinFlags.Length)
//        {
//            collectedStarCoinFlags[id] = true;
//        }

//        Debug.Log($"スターコインID【{id}】獲得！ 現在: {currentDataCubes} / {maxDataCube}");
//    }

//    // --- ?? 後でリザルト画面から呼び出すための「情報受け渡し用」関数群 ---

//    /// <summary>
//    /// 通常コインの最終結果を取得 (現在の取得数, ステージ上の総数)
//    /// </summary>
//    public (int current, int max) GetNormalCoinResult() => (currentBitCubes, maxBitCube);

//    /// <summary>
//    /// スターコインの最終結果を取得 (現在の取得数, ステージ上の総数)
//    /// </summary>
//    public (int current, int max) GetStarCoinResult() => (currentDataCubes, maxDataCube);

//    /// <summary>
//    /// 特定のIDのスターコインを獲得しているか確認する
//    /// </summary>
//    public bool IsStarCoinCollected(int id)
//    {
//        if (id >= 0 && id < collectedStarCoinFlags.Length)
//        {
//            return collectedStarCoinFlags[id];
//        }
//        return false;
//    }

//    /// <summary>
//    /// コイン全体の総合達成度を 0.0 ～ 100.0 (%) の浮動小数点数で返す
//    /// </summary>
//    public float GetTotalCompletionRate()
//    {
//        int totalMax = maxBitCube + maxDataCube;
//        if (totalMax == 0) return 100f; // コインが1個も置かれていないステージなら100%

//        int totalCurrent = currentBitCubes + currentDataCubes;

//        // パーセンテージ計算（100倍してキャスト）
//        return ((float)totalCurrent / totalMax) * 100f;
//    }
//}
