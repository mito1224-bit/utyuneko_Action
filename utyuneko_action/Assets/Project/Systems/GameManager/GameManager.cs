
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

    public int FinalBitCurrent { get; private set; }
    public int FinalBitMax { get; private set; }
    public int FinalDataCurrent { get; private set; }
    public int FinalDataMax { get; private set; }
    public float FinalCompletionRate { get; private set; }
    //スターコイン用のフラグ
    public List<bool> FinalDataFlags { get; private set; } = new List<bool>();

    private string lastClearedSceneName = null;

    // 指定したシーン名が「直前にクリアされたシーン」と一致すれば true を返し、記憶を消費する
    public bool TryConsumeLastClearedScene(string sceneNameToCheck)
    {
        if (lastClearedSceneName == sceneNameToCheck)
        {
            lastClearedSceneName = null; // 一度使ったら消す（誤発火防止）
            return true;
        }
        return false;
    }

    // --- 【追加】DataManagerがクリアした瞬間に、リザルト用データを確定させる関数 ---
    public void SaveFinalResult(int bitCur, int bitMax, int dataCur, int dataMax, float rate,bool[] flags)
    {
        FinalBitCurrent = bitCur;
        FinalBitMax = bitMax;
        FinalDataCurrent = dataCur;
        FinalDataMax = dataMax;
        FinalCompletionRate = rate;

        FinalDataFlags = new List<bool>(flags);
        Debug.Log($"【GameManager】リザルト画面用のデータを保存しました: {rate:F1}%");
    }

    // ====================================================================
    // ゲート演出（新ゲート披露）の既読管理用API
    // ====================================================================

    // 指定フェーズのゲート披露演出を再生済みにし、セーブする
    public void MarkGateRevealPlayed(StoryPhase phase)
    {
        if (!currentSaveData.playedGateReveals.Contains(phase))
        {
            currentSaveData.playedGateReveals.Add(phase);
            SaveGame();
            Debug.Log($"【GameManager】フェーズ「{phase}」のゲート披露演出を再生済みに記録しました。");
        }
    }

    // 指定フェーズのゲート披露演出が再生済みかどうかを返す
    public bool IsGateRevealPlayed(StoryPhase phase)
    {
        return currentSaveData.playedGateReveals.Contains(phase);
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            // --- ここから書き換え ---
            string saveDirectoryPath = "";

            DontDestroyOnLoad(gameObject);

#if UNITY_EDITOR
            // 【Unityエディタで実行中】
            // プロジェクトのルートフォルダ（Assetsフォルダの1つ上）に「SaveData」フォルダを作る
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            saveDirectoryPath = Path.Combine(projectRoot, "SaveData");
#else
            // 【ビルドしたゲームで実行中】
            // 実行ファイル（.exeなど）と同じフォルダに「SaveData」フォルダを作る
            // ※Windowsビルドの場合、Application.dataPathは「ゲーム名_Data」フォルダを指すため、その親が実行ファイルのある位置になります。
            string exeDirectory = Directory.GetParent(Application.dataPath).FullName;
            saveDirectoryPath = Path.Combine(exeDirectory, "SaveData");
#endif

            // フォルダが存在しない場合は自動で作成する
            if (!Directory.Exists(saveDirectoryPath))
            {
                Directory.CreateDirectory(saveDirectoryPath);
            }

            // 最終的なファイルのフルパス
            saveFilePath = Path.Combine(saveDirectoryPath, "savedata.json");

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
    // 演出フラグ管理用の関数（API）
    // ====================================================================
    public void MarkEventAsPlayed(StoryPhase phase)
    {
        if (!currentSaveData.playedEventPhases.Contains(phase))
        {
            currentSaveData.playedEventPhases.Add(phase);
            SaveGame();
            Debug.Log($"【GameManager】フェーズ「{phase}」の演出を再生済みに記録しました。");
        }
    }

    // 指定フェーズの演出がすでに再生済みかどうかを返す
    public bool IsEventPlayed(StoryPhase phase)
    {
        return currentSaveData.playedEventPhases.Contains(phase);
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
        else if (stage.isCoreCubeCollected)
        {
            // 【クリア済みステージの再挑戦時】★新規追加
            // チェックポイントのキャッシュだけ初期化し、dataCubeFlags には触らない
            LastCheckpoint = new CheckpointCache();
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

        if (flags != null)
        {
            LastCheckpoint.dataCubeFlags = new List<bool>(flags);
        }
        else
        {
            LastCheckpoint.dataCubeFlags = new List<bool>();
        }

        Debug.Log("【GameManager】チェックポイントのデータをキャッシュ（確定）しました。");
    }

    public bool IsStageCoreCubeCollected(string stageId)
    {
        StageProgressData stage = currentSaveData.stageList.Find(x => x.stageId == stageId);
        return stage != null && stage.isCoreCubeCollected;
    }

    // ★追加：指定ステージの、本番セーブデータ上のDataCube取得済みフラグを外部から取得するための関数
    public List<bool> GetStageDataCubeFlags(string stageId)
    {
        StageProgressData stage = currentSaveData.stageList.Find(x => x.stageId == stageId);
        return stage != null ? new List<bool>(stage.dataCubeFlags) : new List<bool>();
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

            lastClearedSceneName = currentSceneName; // どのステージをクリアしたか記憶
            // ★追加：クリアしたので次のステージのためにチェックポイントをリセット
            LastCheckpoint = new CheckpointCache();
            //無条件に AdvanceStoryPhase() を呼ぶのをやめ、判定関数を挟む
            CheckAndAdvanceStory(currentSceneName);

            SaveGame();//フェーズが進まなくても、ステージのクリア状況は必ず保存する
        }
    }

    //クリアしたステージと現在のフェーズを比較し、正しい進行であればストーリーを進める
    private void CheckAndAdvanceStory(string clearedSceneName)
    {
        // ※実際のステージの「シーン名」に合わせて文字列を調整してください
        // 例: ステージ1のシーン名が "Stage1"、ステージ2が "Stage2"... の場合

        if (clearedSceneName == "Stage1" && currentSaveData.currentPhase == StoryPhase.Opening)
        {
            currentSaveData.currentPhase = StoryPhase.Stage1_Cleared;
            Debug.Log("【ストーリー進行】ステージ1を初クリア！フェーズが Stage1_Cleared に進みました。");
            SaveGame();
        }
        else if (clearedSceneName == "Stage2" && currentSaveData.currentPhase == StoryPhase.Stage1_Cleared)
        {
            currentSaveData.currentPhase = StoryPhase.Stage2_Cleared;
            Debug.Log("【ストーリー進行】ステージ2を初クリア！フェーズが Stage2_Cleared に進みました。");
            SaveGame();
        }
        else if (clearedSceneName == "Stage3" && currentSaveData.currentPhase == StoryPhase.Stage2_Cleared)
        {
            currentSaveData.currentPhase = StoryPhase.Stage3_Cleared;
            Debug.Log("【ストーリー進行】ステージ3を初クリア！フェーズが Stage3_Cleared に進みました。");
            SaveGame();
        }
        else if (clearedSceneName == "BossStage" && currentSaveData.currentPhase == StoryPhase.Stage3_Cleared)
        {
            currentSaveData.currentPhase = StoryPhase.GameClear;
            Debug.Log("【ストーリー進行】ボス撃破！フェーズが GameClear に進みました。");
            SaveGame();
        }
        else
        {
            // すでに先のステージに進んでいる場合や、過去ステージの再クリア時はここに来る
            Debug.Log($"【ストーリー維持】現在のフェーズ({currentSaveData.currentPhase})に対して、クリアしたステージ({clearedSceneName})が古いため進行をスキップしました。");
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