using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement; // シーン遷移に必要
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    [Header("ポーズ画面のパネルUI")]
    [SerializeField] private GameObject pauseMenuPanel;
    [Header("管理するボタングループ")]
    [SerializeField] private CanvasGroup buttonGroup;
    [Header("ボタンの登録（上から順番に）")]
    [SerializeField] private Button checkpointButton;
    [SerializeField] private Button settingButton;
    [SerializeField] private Button logoutButton;

    [Header("ポーズ中も残したいキャンバス")]
    [SerializeField] private Canvas[] keepVisibleCanvases;

    [Header("遷移先の設定")]
    [Tooltip("タイトル画面のシーン名")]
    [SerializeField] private string titleSceneName = "TitleScene";

    [Header("設定画面（Panel）")]
    [SerializeField] private GameObject settingPanel;
    [SerializeField] private VolumeController volumeController;

    [Tooltip("A/D長押しでスライダーが1秒間に変化する量（1.0で1秒で端から端まで）")]
    [SerializeField] private float sliderAdjustSpeed = 1.0f;

    [Header("やめる画面（Panel）")]
    [SerializeField] private GameObject logoutPanel;
    [Header("やめる画面のボタン")]
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    private bool isPaused = false;
    private Vector3 playerInitialPosition;
    private GameObject playerObj;
    private PlayerController playerController;

    // 消したキャンバスを覚えておく（再開時に戻すため）
    private readonly List<Canvas> hiddenCanvases = new List<Canvas>();

    // ─── メニュー選択の状態 ───
    private int selectIndex = 0;   // メインの3ボタン（0:checkpoint / 1:setting / 2:logout）
    private int settingIndex = 0;  // 設定画面のスライダー
    private int logoutIndex = 1;   // やめる画面（0:yes / 1:no）※既定はNo

    private bool isInSettingMenu = false;
    private bool isInLogoutMenu = false;
    private bool isDirectionPressed = false;

    private GameInputActions inputActions;

    void Start()
    {
        inputActions = InputManager.Instance;

        // 念のため開始時はポーズ画面を閉じて時間を進める
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (settingPanel != null) settingPanel.SetActive(false);
        if (logoutPanel != null) logoutPanel.SetActive(false);
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
        // ポーズ中でなければ、ポーズを開くかどうかだけを見る
        if (!isPaused)
        {
            if (inputActions.Player.Pose.triggered)
            {
                PauseGame();
            }
            return;
        }

        // ここから下はポーズ中の操作
        HandleMenuInput();
    }

    // ─── ポーズ中のキーボード/パッド操作 ───
    void HandleMenuInput()
    {
        Vector2 moveInput = inputActions.UI.Move.ReadValue<Vector2>();

        int verticalInput = 0;
        int horizontalInput = 0;

        // スティック/十字キーは「1回倒すごとに1つ動く」ようにデバウンスする
        if (moveInput.magnitude > 0.5f)
        {
            if (!isDirectionPressed)
            {
                if (moveInput.y > 0.5f) verticalInput = 1;
                if (moveInput.y < -0.5f) verticalInput = -1;
                if (moveInput.x > 0.5f) horizontalInput = 1;
                if (moveInput.x < -0.5f) horizontalInput = -1;
                isDirectionPressed = true;

                SoundManager.Instance.PlaySE(SeType.UiSelect);
            }
        }
        else
        {
            isDirectionPressed = false;
        }

        bool isCancel = inputActions.UI.Cancel.triggered;

        // ── 設定画面を開いている間 ──
        if (isInSettingMenu)
        {
            if (isCancel)
            {
                CloseSetting();
                return;
            }

            int count = (volumeController != null) ? volumeController.SliderCount : 0;
            if (count > 0)
            {
                // 上下：1押しにつき1つ、スライダーの選択を移動
                if (verticalInput < 0) { settingIndex = (settingIndex + 1) % count; SelectSettingSlider(); }
                if (verticalInput > 0) { settingIndex = (settingIndex + count - 1) % count; SelectSettingSlider(); }

                // 左右：押しっぱなしで選択中のスライダーを連続増減
                // ★ポーズ中は Time.timeScale = 0 なので、Time.deltaTime ではなく unscaledDeltaTime を使う
                if (Mathf.Abs(moveInput.x) > 0.5f)
                {
                    volumeController.AdjustSlider(settingIndex, Mathf.Sign(moveInput.x) * sliderAdjustSpeed * Time.unscaledDeltaTime);
                }
            }

            return;
        }

        // ── やめる（ログアウト）確認画面を開いている間 ──
        if (isInLogoutMenu)
        {
            if (isCancel)
            {
                OnLogoutNo(); // キャンセルは「No」と同じ扱い
                return;
            }

            if (horizontalInput < 0) { logoutIndex = 0; SelectLogoutButton(); } // 左：Yes
            if (horizontalInput > 0) { logoutIndex = 1; SelectLogoutButton(); } // 右：No

            return;
        }

        // ── メインのポーズメニュー ──
        if (isCancel)
        {
            ResumeGame(); // 何も開いていなければ、キャンセルでポーズ解除
            return;
        }

        if (verticalInput < 0) { selectIndex = (selectIndex + 1) % 3; SelectButton(); }
        if (verticalInput > 0) { selectIndex = (selectIndex + 2) % 3; SelectButton(); }
    }

    public void TogglePause()
    {
        if (isPaused) ResumeGame();
        else PauseGame();
    }

    // ─── フォーカス（選択）の切り替え ───
    void SelectButton()
    {
        if (checkpointButton != null && selectIndex == 0) checkpointButton.Select();
        if (settingButton != null && selectIndex == 1) settingButton.Select();
        if (logoutButton != null && selectIndex == 2) logoutButton.Select();
    }

    void SelectLogoutButton()
    {
        if (yesButton != null && logoutIndex == 0) yesButton.Select();
        if (noButton != null && logoutIndex == 1) noButton.Select();
    }

    void SelectSettingSlider()
    {
        if (volumeController != null) volumeController.SelectSlider(settingIndex);
    }

    // ─── パネルの開閉処理 ───

    public void PauseGame()
    {
        isPaused = true;
        pauseMenuPanel.SetActive(true);
        HideOtherCanvases();
        Time.timeScale = 0f; // ゲーム内の時間を完全に停止させる

        // サブ画面は必ず閉じた状態から始める
        if (settingPanel != null) settingPanel.SetActive(false);
        if (logoutPanel != null) logoutPanel.SetActive(false);
        isInSettingMenu = false;
        isInLogoutMenu = false;
        if (buttonGroup != null) buttonGroup.interactable = true;

        if (playerController != null && playerController.inputActions != null)
        {
            playerController.inputActions.Player.Disable();
            playerController.inputActions.UI.Enable();
            Debug.Log("【ポーズ】プレイヤーの入力を無効化しました。");
        }

        // 先頭のボタンを選択状態にする
        selectIndex = 0;
        SelectButton();

        SoundManager.Instance.PlaySE(SeType.UiEnter);
    }

    public void ResumeGame()
    {
        pauseMenuPanel.SetActive(false);
        if (settingPanel != null) settingPanel.SetActive(false);
        if (logoutPanel != null) logoutPanel.SetActive(false);
        isInSettingMenu = false;
        isInLogoutMenu = false;

        ShowOtherCanvases();
        Time.timeScale = 1f; // ゲーム内の時間を動かす
        isPaused = false;    // フラグを戻す

        // ゲーム再開時にプレイヤーの入力を再び有効化する
        if (playerController != null && playerController.inputActions != null)
        {
            playerController.inputActions.Player.Enable();
            playerController.inputActions.UI.Disable();
            Debug.Log("【再開】プレイヤーの入力を有効化しました。");
        }

        SoundManager.Instance.PlaySE(SeType.UiCancel);
    }

    // ─── ボタン用関数 ───

    /// <summary>
    /// 【checkpointボタン】シーンを最初から読み込み直す（リセット）
    /// </summary>
    public void OnCheckPoint()
    {
        Time.timeScale = 1f; // 時間を戻してからロードする

        // DataManagerはDontDestroy設定なので、リロード前に一度消して重複を防ぐ
        if (DataManager.Instance != null)
        {
            Destroy(DataManager.Instance.gameObject);
        }

        SoundManager.Instance.PlaySE(SeType.UiEnter);

        // 現在のシーンを再読み込み
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// 【settingボタン】設定画面を開く
    /// </summary>
    public void OnSetting()
    {
        if (settingPanel == null) return;

        settingPanel.SetActive(true);
        if (logoutPanel != null) logoutPanel.SetActive(false);
        if (buttonGroup != null) buttonGroup.interactable = false;

        isInSettingMenu = true;
        settingIndex = 0;
        SelectSettingSlider();

        SoundManager.Instance.PlaySE(SeType.UiEnter);
    }

    /// <summary>
    /// 設定画面を閉じてポーズメニューへ戻る
    /// </summary>
    private void CloseSetting()
    {
        if (settingPanel != null) settingPanel.SetActive(false);
        isInSettingMenu = false;

        if (buttonGroup != null) buttonGroup.interactable = true;

        // settingボタン（index=1）にフォーカスを戻す
        selectIndex = 1;
        SelectButton();

        SoundManager.Instance.PlaySE(SeType.UiCancel);
    }

    /// <summary>
    /// 【logoutボタン】やめる確認画面を開く
    /// </summary>
    public void OnLogout()
    {
        if (logoutPanel == null) return;

        if (settingPanel != null) settingPanel.SetActive(false);
        if (buttonGroup != null) buttonGroup.interactable = false;

        logoutPanel.SetActive(true);
        isInLogoutMenu = true;
        logoutIndex = 1; // 既定はNo
        SelectLogoutButton();

        SoundManager.Instance.PlaySE(SeType.UiGameEnd);
    }

    /// <summary>
    /// 【やめる確認・Yes】タイトル画面へ遷移する
    /// </summary>
    public void OnLogoutYes()
    {
        ToTitle(); // 既存のタイトル遷移処理をそのまま使う
    }

    /// <summary>
    /// 【やめる確認・No】確認画面を閉じてポーズメニューへ戻る
    /// </summary>
    public void OnLogoutNo()
    {
        if (logoutPanel != null) logoutPanel.SetActive(false);
        isInLogoutMenu = false;

        if (buttonGroup != null) buttonGroup.interactable = true;

        // logoutボタン（index=2）にフォーカスを戻す
        selectIndex = 2;
        SelectButton();

        SoundManager.Instance.PlaySE(SeType.UiCancel);
    }

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
    /// </summary>
    public void RestartStage()
    {
        Time.timeScale = 1f; // 時間を戻してからロードする

        if (DataManager.Instance != null)
        {
            Destroy(DataManager.Instance.gameObject);
        }

        //SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        TransitionManager.Instance.ChangeScene(SceneManager.GetActiveScene().name, TransitionType.Fade);
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

    // ─── キャンバス表示の制御 ───

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
}