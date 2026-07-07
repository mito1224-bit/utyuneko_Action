using System.Collections.Generic;
using UnityEngine;

public class StoryPhaseSceneController : MonoBehaviour
{
    [Header("チュートリアル用のオブジェクト群")]
    [SerializeField] private GameObject OpeningEvent;
    [SerializeField] private GameObject RescueEvent;
    [SerializeField] private List<GameObject> tutorialObject = new List<GameObject>();

    [Header("ステージセレクト用のオブジェクト群")]
    [SerializeField] private GameObject stageSelectWall;
    [SerializeField] private GameObject stage1Portal;
    [SerializeField] private GameObject stage2Portal;
    [SerializeField] private GameObject stage3Portal;
    [SerializeField] private GameObject stage4Portal;

    [Header("プレイヤーの初期スポーン位置")]
    [SerializeField] private PlayerController Player;
    [SerializeField] private Transform tutorialSpawnPoint;
    [SerializeField] private Transform stageSelectSpawnPoint;

    void Start()
    {
        if (!GameManager.Instance) return;

        StoryPhase currentPhase = GameManager.Instance.CurrentSaveData.currentPhase;

        switch (currentPhase)
        {
            // ゲーム開始直後
            case StoryPhase.StartEvent:
                Debug.Log("【シーン制御】フェーズ:チュートリアルスタート");

                // プレイヤーをチュートリアル用の初期位置
                if (DataManager.Instance)
                    DataManager.Instance.UpdateCheckpoint(tutorialSpawnPoint.position);

                // チュートリアル要素をON、ステージセレクト要素をOFF
                if (OpeningEvent) OpeningEvent.SetActive(true);
                if (RescueEvent) RescueEvent.SetActive(true);
                foreach (var obj in tutorialObject)
                {
                    if (obj) obj.SetActive(true);
                }

                if (stageSelectWall) stageSelectWall.SetActive(false);
                if (stage1Portal) stage1Portal.SetActive(false);
                if (stage2Portal) stage2Portal.SetActive(false);
                if (stage3Portal) stage3Portal.SetActive(false);
                if (stage4Portal) stage4Portal.SetActive(false);

                break;

            // ゲーム開始直後から補佐を助ける前
            case StoryPhase.Tutorial:
                Debug.Log("【シーン制御】フェーズ:チュートリアル中");

                // プレイヤーをチュートリアル用の初期位置
                if (DataManager.Instance)
                    DataManager.Instance.UpdateCheckpoint(tutorialSpawnPoint.position);

                // チュートリアル要素をON、ステージセレクト要素をOFF
                if (OpeningEvent) OpeningEvent.SetActive(false);
                if (RescueEvent) RescueEvent.SetActive(true);
                foreach (var obj in tutorialObject)
                {
                    if (obj) obj.SetActive(true);
                }

                if (stageSelectWall) stageSelectWall.SetActive(false);
                if (stage1Portal) stage1Portal.SetActive(false);
                if (stage2Portal) stage2Portal.SetActive(false);
                if (stage3Portal) stage3Portal.SetActive(false);
                if (stage4Portal) stage4Portal.SetActive(false);

                break;

            // ゲームセレクトバグ消去後
            case StoryPhase.Opening:
                Debug.Log("【シーン制御】フェーズ:チュートリアルクリア");
                StageSelectActive();

                //ステージ１だけ解放
                if (stage1Portal) stage1Portal.SetActive(true);
                if (stage2Portal) stage2Portal.SetActive(false);
                if (stage3Portal) stage3Portal.SetActive(false);
                if (stage4Portal) stage4Portal.SetActive(false);

                break;

            // ステージ1クリア
            case StoryPhase.Stage1_Cleared:
                Debug.Log("【シーン制御】フェーズ:ステージ１クリア");
                StageSelectActive();

                if (stage1Portal) stage1Portal.SetActive(true);
                if (stage2Portal) stage2Portal.SetActive(true);
                if (stage3Portal) stage3Portal.SetActive(false);
                if (stage4Portal) stage4Portal.SetActive(false);
                break;

            // ステージ2クリア
            case StoryPhase.Stage2_Cleared:
                Debug.Log("【シーン制御】フェーズ:ステージ２クリア");
                StageSelectActive();

                if (stage1Portal) stage1Portal.SetActive(true);
                if (stage2Portal) stage2Portal.SetActive(true);
                if (stage3Portal) stage3Portal.SetActive(true);
                if (stage4Portal) stage4Portal.SetActive(false);
                break;

            // ステージ3クリア
            case StoryPhase.Stage3_Cleared:
                Debug.Log("【シーン制御】フェーズ:ステージ３クリア");
                StageSelectActive();

                if (stage1Portal) stage1Portal.SetActive(true);
                if (stage2Portal) stage2Portal.SetActive(true);
                if (stage3Portal) stage3Portal.SetActive(true);
                if (stage4Portal) stage4Portal.SetActive(true);
                break;

            // 全クリア
            case StoryPhase.GameClear:
                Debug.Log("【シーン制御】フェーズ:ゲームクリア");
                StageSelectActive();

                if (stage1Portal) stage1Portal.SetActive(true);
                if (stage2Portal) stage2Portal.SetActive(true);
                if (stage3Portal) stage3Portal.SetActive(true);
                if (stage4Portal) stage4Portal.SetActive(true);
                break;

            default:
                Debug.Log("【シーン制御】フェーズ:None");
                break;
        }
    }

    void StageSelectActive()
    {
        if (DataManager.Instance)
            DataManager.Instance.UpdateCheckpoint(stageSelectSpawnPoint.position);

        Player.gameObject.transform.position = stageSelectSpawnPoint.position;

        SoundManager.Instance.PlayBGM(BgmType.StageSelect);

        if (OpeningEvent) OpeningEvent.SetActive(false);
        if (RescueEvent) RescueEvent.SetActive(false);
        foreach (var obj in tutorialObject)
        {
            if (obj) obj.SetActive(false);
        }

        if (stageSelectWall) stageSelectWall.SetActive(true);
    }
}