using System.Collections.Generic;
using UnityEngine;

// 物語のメインストーリーの段階
public enum StoryPhase
{
    StartEvent,         // ゲーム開始直後
    Tutorial,           // ゲーム開始直後から補佐を助ける前
    Opening,            // ゲームセレクトバグ消去後
    Stage1_Cleared,     // ステージ1クリア
    Stage2_Cleared,     // ステージ2クリア
    Stage3_Cleared,     // ステージ3クリア
    GameClear           // ゲームクリア
}

// 1ステージごとのクリア状況
[System.Serializable]
public class StageProgressData
{
    public string stageId;                     // シーン名
    public bool isCoreCubeCollected = false;   // ステージクリアしたか
    public int maxBitCubes = 0;                // ビットキューブのハイスコア
    public int totalBitCubes = 0;              // ステージ内のビットキューブ総数
    public List<bool> dataCubeFlags = new List<bool>(); // データキューブの個別取得フラグ
}

[System.Serializable]
public class SaveData
{
    [Header("ストーリーフラグ")]
    public StoryPhase currentPhase = StoryPhase.StartEvent;


    [Header("演出再生フラグ")]
    // 各ストーリーフェーズの演出（カットシーン）を再生済みかどうか
    public List<StoryPhase> playedEventPhases = new List<StoryPhase>();
    [Header("ゲート演出の既読フラグ")]
    // 「新しいゲートを見せる」演出を再生済みのフェーズ一覧
    public List<StoryPhase> playedGateReveals = new List<StoryPhase>();

    [Header("イベントフラグ")]
    // 特定のイベントが発生したかどうかを管理するフラグ

    //一度見たイベントのIDを保存するリスト
    public List<string> watchedEventIds = new List<string>();

    [Header("各ステージのデータリスト")]
    public List<StageProgressData> stageList = new List<StageProgressData>();
}