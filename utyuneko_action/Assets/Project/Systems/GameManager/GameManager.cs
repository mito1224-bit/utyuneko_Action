using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // 外部からは読み取り専用にする
    public SaveData CurrentSaveData => currentSaveData;

    [SerializeField] private SaveData currentSaveData = new SaveData();
    private string saveFilePath;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // シーン切り替えでも消さない

            // セーブファイルの保存先パス（PCやスマホの安全な自動生成フォルダ）
            saveFilePath = Path.Combine(Application.persistentDataPath, "savedata.json");

            LoadGame(); // 起動時に自動ロード

            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void OnDestroy()
    {
        // このGameManager自身が破棄されるとき、登録したイベントを解除する
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }



    // ====================================================================
    // 全自動窓口関数（API）
    // ====================================================================

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // ハブエリアやタイトル画面など、データキューブがないシーンは無視する
        if (scene.name == "TitleScene" || scene.name == "LogoScene"
           || scene.name == "ResultScene") return;

        // 今のシーンのデータを取得または新規作成
        StageProgressData stage = GetOrCreateStageData(scene.name);

        // ビットキューブのシーン内の総数を数える
        GameObject[] allBitCubes = GameObject.FindGameObjectsWithTag("BitCube");
        stage.totalBitCubes = allBitCubes.Length;

        // データキューブのリスト拡張処理
        GameObject[] allDataCubes = GameObject.FindGameObjectsWithTag("DataCube");
        while (stage.dataCubeFlags.Count < allDataCubes.Length)
        {
            stage.dataCubeFlags.Add(false);
        }

        Debug.Log($"【自動登録】シーン「{scene.name}」: ビットキューブ総数 {stage.totalBitCubes}個 / データキューブ総数 {allDataCubes.Length}個");
    }

    // データキューブを拾った時：インデックス番号だけで自動保存
    public void CollectDataCube(int cubeIndex)
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        StageProgressData stage = GetOrCreateStageData(currentSceneName);

        if (stage != null && cubeIndex >= 0 && cubeIndex < stage.dataCubeFlags.Count)
        {
            stage.dataCubeFlags[cubeIndex] = true;
        }
    }

    // ステージクリア時：クリア時のビットキューブの数を渡す
    public void ClearStage(int currentBitCubes)
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        StageProgressData stage = GetOrCreateStageData(currentSceneName);

        if (stage != null)
        {
            stage.isCoreCubeCollected = true;
            if (currentBitCubes > stage.maxBitCubes)
            {
                stage.maxBitCubes = currentBitCubes;
            }

            AdvanceStoryPhase(); // ストーリーフェーズ進行
            SaveGame(); // オートセーブ
        }
    }

    // メインストーリーを1段階進める
    public void AdvanceStoryPhase()
    {
        if (currentSaveData.currentPhase != StoryPhase.GameClear)
        {
            currentSaveData.currentPhase++;
            Debug.Log($"【ストーリー進行】現在のフェーズ: {currentSaveData.currentPhase}");
            SaveGame(); // オートセーブ
        }
    }

    // ====================================================================
    // 内部処理・セーブ＆ロード
    // ====================================================================

    private StageProgressData GetOrCreateStageData(string stageId)
    {
        StageProgressData stage = currentSaveData.stageList.Find(x => x.stageId == stageId);
        if (stage == null)
        {
            stage = new StageProgressData { stageId = stageId };
            currentSaveData.stageList.Add(stage);
            Debug.Log($"【セーブデータ自動拡張】「{stageId}」の保存枠を作成しました。");
        }
        return stage;
    }

    public void SaveGame()
    {
        string json = JsonUtility.ToJson(currentSaveData, true);
        File.WriteAllText(saveFilePath, json);
        Debug.Log($"【自動セーブ完了】保存先: {saveFilePath}");
    }

    public void LoadGame()
    {
        if (File.Exists(saveFilePath))
        {
            string json = File.ReadAllText(saveFilePath);
            currentSaveData = JsonUtility.FromJson<SaveData>(json);
            Debug.Log("【自動ロード完了】データを復元しました。");
        }
        else
        {
            Debug.Log("セーブデータがありません。新規作成します。");
            SaveGame();
        }
    }
}