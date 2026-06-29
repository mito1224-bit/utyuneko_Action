
using System.IO;
using System.Collections.Generic; // ★ Listを使うために追加
using UnityEngine;
using UnityEngine.SceneManagement;

// ★追加：チェックポイントのデータを一時的に預かるためのクラス
[System.Serializable]
public class CheckpointCache
{
    public bool hasSaved = false;       // 一度でもチェックポイントに触れたか
    public string stageId;              // どのステージのチェックポイントか
    public Vector3 playerPosition;      // その時点のプレイヤー座標
    public int bitCubes;                // その時点の通常コイン数
    public List<bool> dataCubeFlags = new List<bool>(); // その時点のスターコインフラグ
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // 外部からは読み取り専用にする
    public SaveData CurrentSaveData => currentSaveData;

    [SerializeField] private SaveData currentSaveData = new SaveData();
    private string saveFilePath;

    // ★追加：最後に通過したチェックポイントのデータ
    public CheckpointCache LastCheckpoint { get; private set; } = new CheckpointCache();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            // ※ DontDestroyOnLoad は GlobalSystems 側で行われているため、ここでは不要です！

            // セーブファイルの保存先パス
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
        if (scene.name == "TitleScene" || scene.name == "LogoScene"
           || scene.name == "ResultScene") return;

        StageProgressData stage = GetOrCreateStageData(scene.name);

        GameObject[] allBitCubes = GameObject.FindGameObjectsWithTag("BitCube");
        stage.totalBitCubes = allBitCubes.Length;

        GameObject[] allDataCubes = GameObject.FindGameObjectsWithTag("DataCube");
        while (stage.dataCubeFlags.Count < allDataCubes.Length)
        {
            stage.dataCubeFlags.Add(false);
        }

        // -------------------------------------------------------------
        // ★追加：死亡リロード時、または新規ステージ突入時のデータ制御
        // -------------------------------------------------------------
        if (LastCheckpoint.hasSaved && LastCheckpoint.stageId == scene.name)
        {
            // 【死亡リロード時】
            // 本番セーブデータのスターコインフラグを、チェックポイント通過時の状態に「巻き戻す」
            for (int i = 0; i < stage.dataCubeFlags.Count; i++)
            {
                if (i < LastCheckpoint.dataCubeFlags.Count)
                {
                    stage.dataCubeFlags[i] = LastCheckpoint.dataCubeFlags[i];
                }
            }
            Debug.Log($"【GameManager】死亡リロード検知：本番データをチェックポイント時点に巻き戻しました。");
        }
        else
        {
            // 【新規ステージ開始時、または一度もチェックポイントを踏んでいない場合】
            // このステージのフラグを一度すべて未獲得(false)にリセットする
            for (int i = 0; i < stage.dataCubeFlags.Count; i++)
            {
                stage.dataCubeFlags[i] = false;
            }
            LastCheckpoint = new CheckpointCache(); // キャッシュを完全に初期化
            Debug.Log($"【GameManager】新規ステージ開始：フラグを初期化しました。");
        }

        Debug.Log($"【自動登録】シーン「{scene.name}」: ビットキューブ総数 {stage.totalBitCubes}個 / データキューブ総数 {allDataCubes.Length}個");
    }

    // ★追加：DataManagerがチェックポイントに触れた時に呼び出すデータ確定関数
    public void SaveCheckpointData(Vector3 pos, int coins, bool[] flags)
    {
        string currentSceneName = SceneManager.GetActiveScene().name;

        LastCheckpoint.hasSaved = true;
        LastCheckpoint.stageId = currentSceneName;
        LastCheckpoint.playerPosition = pos;
        LastCheckpoint.bitCubes = coins;

        LastCheckpoint.dataCubeFlags = new List<bool>(flags);

        Debug.Log("【GameManager】チェックポイントのデータをキャッシュ（確定）しました。");
    }

    public void CollectDataCube(int cubeIndex)
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        StageProgressData stage = GetOrCreateStageData(currentSceneName);

        if (stage != null && cubeIndex >= 0 && cubeIndex < stage.dataCubeFlags.Count)
        {
            stage.dataCubeFlags[cubeIndex] = true;
        }
    }

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

            // ★追加：クリアしたので次のステージのためにチェックポイントをリセット
            LastCheckpoint = new CheckpointCache();

            AdvanceStoryPhase();
            SaveGame();
        }
    }

    public void AdvanceStoryPhase()
    {
        if (currentSaveData.currentPhase != StoryPhase.GameClear)
        {
            currentSaveData.currentPhase++;
            Debug.Log($"【ストーリー進行】現在のフェーズ: {currentSaveData.currentPhase}");
            SaveGame();
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

    public void ResetCheckpointCache()
    {
        LastCheckpoint = new CheckpointCache();
        Debug.Log("【GameManager】チェックポイントのキャッシュをリセットしました。");
    } // ←スクリプトの一番最後の波括弧
}

//using System.IO;
//using UnityEngine;
//using UnityEngine.SceneManagement;

//public class GameManager : MonoBehaviour
//{
//    public static GameManager Instance { get; private set; }

//    // 外部からは読み取り専用にする
//    public SaveData CurrentSaveData => currentSaveData;

//    [SerializeField] private SaveData currentSaveData = new SaveData();
//    private string saveFilePath;

//    private void Awake()
//    {
//        if (Instance == null)
//        {
//            Instance = this;
//            // セーブファイルの保存先パス（PCやスマホの安全な自動生成フォルダ）
//            saveFilePath = Path.Combine(Application.persistentDataPath, "savedata.json");

//            LoadGame(); // 起動時に自動ロード

//            SceneManager.sceneLoaded += OnSceneLoaded;
//        }
//        else
//        {
//            Destroy(gameObject);
//        }
//    }
//    private void OnDestroy()
//    {
//        // このGameManager自身が破棄されるとき、登録したイベントを解除する
//        if (Instance == this)
//        {
//            SceneManager.sceneLoaded -= OnSceneLoaded;
//        }
//    }



//    // ====================================================================
//    // 全自動窓口関数（API）
//    // ====================================================================

//    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
//    {
//        // ハブエリアやタイトル画面など、データキューブがないシーンは無視する
//        if (scene.name == "TitleScene" || scene.name == "LogoScene"
//           || scene.name == "ResultScene") return;

//        // 今のシーンのデータを取得または新規作成
//        StageProgressData stage = GetOrCreateStageData(scene.name);

//        // ビットキューブのシーン内の総数を数える
//        GameObject[] allBitCubes = GameObject.FindGameObjectsWithTag("BitCube");
//        stage.totalBitCubes = allBitCubes.Length;

//        // データキューブのリスト拡張処理
//        GameObject[] allDataCubes = GameObject.FindGameObjectsWithTag("DataCube");
//        while (stage.dataCubeFlags.Count < allDataCubes.Length)
//        {
//            stage.dataCubeFlags.Add(false);
//        }

//        Debug.Log($"【自動登録】シーン「{scene.name}」: ビットキューブ総数 {stage.totalBitCubes}個 / データキューブ総数 {allDataCubes.Length}個");
//    }

//    // データキューブを拾った時：インデックス番号だけで自動保存
//    public void CollectDataCube(int cubeIndex)
//    {
//        string currentSceneName = SceneManager.GetActiveScene().name;
//        StageProgressData stage = GetOrCreateStageData(currentSceneName);

//        if (stage != null && cubeIndex >= 0 && cubeIndex < stage.dataCubeFlags.Count)
//        {
//            stage.dataCubeFlags[cubeIndex] = true;
//        }
//    }

//    // ステージクリア時：クリア時のビットキューブの数を渡す
//    public void ClearStage(int currentBitCubes)
//    {
//        string currentSceneName = SceneManager.GetActiveScene().name;
//        StageProgressData stage = GetOrCreateStageData(currentSceneName);

//        if (stage != null)
//        {
//            stage.isCoreCubeCollected = true;
//            if (currentBitCubes > stage.maxBitCubes)
//            {
//                stage.maxBitCubes = currentBitCubes;
//            }

//            AdvanceStoryPhase(); // ストーリーフェーズ進行
//            SaveGame(); // オートセーブ
//        }
//    }

//    // メインストーリーを1段階進める
//    public void AdvanceStoryPhase()
//    {
//        if (currentSaveData.currentPhase != StoryPhase.GameClear)
//        {
//            currentSaveData.currentPhase++;
//            Debug.Log($"【ストーリー進行】現在のフェーズ: {currentSaveData.currentPhase}");
//            SaveGame(); // オートセーブ
//        }
//    }

//    // ====================================================================
//    // 内部処理・セーブ＆ロード
//    // ====================================================================

//    private StageProgressData GetOrCreateStageData(string stageId)
//    {
//        StageProgressData stage = currentSaveData.stageList.Find(x => x.stageId == stageId);
//        if (stage == null)
//        {
//            stage = new StageProgressData { stageId = stageId };
//            currentSaveData.stageList.Add(stage);
//            Debug.Log($"【セーブデータ自動拡張】「{stageId}」の保存枠を作成しました。");
//        }
//        return stage;
//    }

//    public void SaveGame()
//    {
//        string json = JsonUtility.ToJson(currentSaveData, true);
//        File.WriteAllText(saveFilePath, json);
//        Debug.Log($"【自動セーブ完了】保存先: {saveFilePath}");
//    }

//    public void LoadGame()
//    {
//        if (File.Exists(saveFilePath))
//        {
//            string json = File.ReadAllText(saveFilePath);
//            currentSaveData = JsonUtility.FromJson<SaveData>(json);
//            Debug.Log("【自動ロード完了】データを復元しました。");
//        }
//        else
//        {
//            Debug.Log("セーブデータがありません。新規作成します。");
//            SaveGame();
//        }
//    }
//}