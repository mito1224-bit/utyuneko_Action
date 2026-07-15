using System.Collections.Generic;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement; // シーン遷移に必要

public class PauseMenu : MonoBehaviour
{
    [Header("ポーズ画面のパネルUI")]
    [SerializeField] private GameObject pauseMenuPanel;

    [Header("ポーズ中も残したいキャンバス")]
    [SerializeField] private Canvas[] keepVisibleCanvases;

    [Header("遷移先の設定")]
    [Tooltip("タイトル画面のシーン名")]
    [SerializeField] private string titleSceneName = "TitleScene";

    [Header("設定画面（Panel）")]
    [SerializeField] private GameObject settingPanel;
    [SerializeField] private VolumeController volumeController;

    private bool isPaused = false;
    private Vector3 playerInitialPosition;
    private GameObject playerObj;
    private PlayerController playerController;

    // 消したキャンバスを覚えておく（再開時に戻すため）
    private readonly List<Canvas> hiddenCanvases = new List<Canvas>();

    
    GameInputActions ac;

    void Start()
    {
        // 念のため開始時はポーズ画面を閉じて時間を進める
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (settingPanel != null) settingPanel.SetActive(false);
        Time.timeScale = 1f;

        // 【最初の位置に戻る用】プレイヤーの初期位置を記憶しておく
        playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerInitialPosition = playerObj.transform.position;
            playerController = playerObj.GetComponent<PlayerController>();
        }
    }

    void Update()
    {
        // Escキー または Pキーでポーズの開閉切り替え
        if (InputManager.Instance.Player.Pose.triggered || InputManager.Instance.UI.Cancel.triggered)
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (isPaused) ResumeGame();
        else PauseGame();
    }

    // ─── パネルの開閉処理 ───


    public void PauseGame()
    {
        isPaused = true;
        pauseMenuPanel.SetActive(true);
        HideOtherCanvases();
        Time.timeScale = 0f; // ゲーム内の時間を完全に停止させる

        if (playerController != null && playerController.inputActions != null)
        {
            playerController.inputActions.Player.Disable();
            playerController.inputActions.UI.Enable();
            Debug.Log("【ポーズ】プレイヤーの入力を無効化しました。");
        }
    }

    public void ResumeGame()
    {
        pauseMenuPanel.SetActive(false);
        ShowOtherCanvases();
        Time.timeScale = 1f; //ゲーム内の時間を動かす
        isPaused = false;    //フラグを戻す

        //ゲーム再開時にプレイヤーの入力を再び有効化する
        if (playerController != null && playerController.inputActions != null)
        {
            playerController.inputActions.Player.Enable();
            playerController.inputActions.UI.Disable();
            Debug.Log("【再開】プレイヤーの入力を有効化しました。");
        }
    }

    // ─── ボタン用関数 ───

    /// <summary>
    /// 【パターンA】プレイヤーの位置だけを最初に戻す（取得数は維持）
    /// </summary>
    public void ResetPlayerPosition()
    {
        if (playerObj != null)
        {
            // 物理(Rigidbody)が勢いを持ったままワープするとバグるので、一旦速度をゼロにする
            if (playerObj.TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.linearVelocity = Vector2.zero; // 古いUnityなら velocity = Vector2.zero
            }
            playerObj.transform.position = playerInitialPosition;
        }
        ResumeGame(); // 時間を動かして画面を閉じる
    }

    /// <summary>
    /// 【パターンB】ステージを最初から完全にやり直す（リトライ）
    /// ※もし取得数もリセットして最初から数え直したい場合はこちらがおすすめ
    /// </summary>
    public void RestartStage()
    {
        Time.timeScale = 1f; // 時間を戻してからロードする

        // DataManagerはDontDestroy設定なので、リロードする前に一回消さないと
        // 次のシーンのStart()で「前の古いデータ」が残って重複してしまいます
        if (DataManager.Instance != null)
        {
            Destroy(DataManager.Instance.gameObject);
        }

        // 現在のシーンを再読み込み
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// タイトル画面に戻る
    /// </summary>
    public void ToTitle()
    {
        Time.timeScale = 1f; // 時間を戻す

        // タイトルに戻るので、現在のステージのDataManagerは完全に消去する
        if (DataManager.Instance != null)
        {
            Destroy(DataManager.Instance.gameObject);
        }

        SoundManager.Instance.FadeBGMVolume(0.0f, 0.5f);
        SoundManager.Instance.PlaySE(SeType.UiEnter);

        TransitionManager.Instance.ChangeScene(titleSceneName, TransitionType.Fade);
    }

    private void HideOtherCanvases()
    {
        hiddenCanvases.Clear();

        // シーン内の全キャンバスを取得
        Canvas[] all = FindObjectsByType<Canvas>(FindObjectsSortMode.None);

        foreach (var canvas in all)
        {
            // 残したいリストに入っておらず、今表示中のものだけ消す
            if (System.Array.IndexOf(keepVisibleCanvases, canvas) < 0 && canvas.enabled)
            {
                canvas.enabled = false;
                hiddenCanvases.Add(canvas); // 戻す用に記録
            }
        }
    }

    private void ShowOtherCanvases()
    {
        foreach (var canvas in hiddenCanvases)
        {
            if (canvas != null) canvas.enabled = true;
        }
        hiddenCanvases.Clear();
    }

    public void OnSetting()
    {
        if (settingPanel == null) return;

        settingPanel.SetActive(true);
        pauseMenuPanel.SetActive(false);
        
        SoundManager.Instance.PlaySE(SeType.UiEnter);
    }
}