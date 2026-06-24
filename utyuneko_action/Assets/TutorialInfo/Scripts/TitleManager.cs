//using UnityEngine;
//using UnityEngine.SceneManagement;
//using UnityEngine.UI;
//using UnityEngine.InputSystem; // 1. 新しいInput Systemを使うために追加

//public class TitleManager : MonoBehaviour
//{
//    [Header("画面全体を覆うフェード用画像（黒）")]
//    [SerializeField] private Image fadeImage;
//    [Header("フェードさせるボタングループ")]
//    [SerializeField] private CanvasGroup buttonGroup;

//    [Header("フェード速度の設定")]
//    [SerializeField] private float sceneFadeInSpeed = 1.5f; // シーン全体のフェードイン（じわーっと遅め）
//    [SerializeField] private float buttonFadeSpeed = 4.0f;  // ボタンのフェード（サッと速め）

//    [Header("ボタンの登録（上から順番に）")]
//    [SerializeField] private Button startButton;
//    [SerializeField] private Button settingButton;
//    [SerializeField] private Button endButton;

//    [Header("ゲーム終了の確認画面（Panel）")]
//    [SerializeField] private GameObject confirmPanel;

//    [Header("確認画面のボタン")]
//    [SerializeField] private Button yesButton;
//    [SerializeField] private Button noButton;

//    private enum TitleState
//    {
//        SceneFadingIn,    // 1. 黒画面がじわーっと透明になり、シーン全体が出現中
//        WaitingForEnter,  // 2. シーンが出きって、Enter入力を待っている状態
//        ButtonsFadingIn,  // 3. Enterが押されて、ボタンがフェードイン中
//        ActiveMenu        // 4. ボタンも出きって、メニュー操作ができる状態
//    }
//    private TitleState currentState = TitleState.SceneFadingIn;

//    private int selectedIndex = 0;
//    private bool isInConfirmMenu = false;

//    void Start()
//    {
//        if (confirmPanel != null) confirmPanel.SetActive(false);

//        // ボタンは最初は完全に透明にしておく
//        if (buttonGroup != null)
//        {
//            buttonGroup.alpha = 0;
//            buttonGroup.interactable = false;
//            buttonGroup.blocksRaycasts = false;
//        }

//        // フェード画像（黒）を確実に表示・有効化しておく
//        if (fadeImage != null)
//        {
//            fadeImage.gameObject.SetActive(true);
//            Color c = fadeImage.color;
//            c.a = 1.0f; // 完全に真っ黒
//            fadeImage.color = c;
//        }
//    }

//    void Update()
//    {
//        // キーボードが接続されていない場合は処理をスキップ（念のための安全策）
//        if (Keyboard.current == null) return;

//        switch (currentState)
//        {
//            // ==========================================
//            // ステップ1: 黒画面を透明にしていき、シーン全体を出す
//            // ==========================================
//            case TitleState.SceneFadingIn:
//                if (fadeImage != null)
//                {
//                    Color c = fadeImage.color;
//                    c.a -= Time.deltaTime * sceneFadeInSpeed; // アルファ値を減らす（透明にする）

//                    if (c.a <= 0.0f)
//                    {
//                        c.a = 0.0f;
//                        fadeImage.gameObject.SetActive(false); // 完全に透明になったら邪魔なので非アクティブに
//                        currentState = TitleState.WaitingForEnter; // Enter待ちへ
//                    }
//                    fadeImage.color = c;
//                }
//                else
//                {
//                    currentState = TitleState.WaitingForEnter;
//                }
//                break;

//            // ==========================================
//            // ステップ2: ボタンなし（背景とタイトルのみ）の状態でEnterを待つ
//            // ==========================================
//            case TitleState.WaitingForEnter:
//                // 2. EnterキーまたはSpaceキーが押されたか判定
//                if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
//                {
//                    currentState = TitleState.ButtonsFadingIn;
//                }
//                break;

//            // ==========================================
//            // ステップ3: ボタンをサッとフェードイン
//            // ==========================================
//            case TitleState.ButtonsFadingIn:
//                if (buttonGroup != null)
//                {
//                    buttonGroup.alpha += Time.deltaTime * buttonFadeSpeed;
//                    if (buttonGroup.alpha >= 1.0f)
//                    {
//                        buttonGroup.alpha = 1.0f;
//                        buttonGroup.interactable = true;
//                        buttonGroup.blocksRaycasts = true;
//                        currentState = TitleState.ActiveMenu;

//                        SelectButton(); // 最初のボタンを選択状態に
//                    }
//                }
//                break;

//            // ==========================================
//            // ステップ4: メニュー操作
//            // ==========================================
//            case TitleState.ActiveMenu:
//                HandleMenuInput();
//                break;
//        }
//    }

//    void HandleMenuInput()
//    {
//        var keyboard = Keyboard.current;

//        if (isInConfirmMenu)
//        {
//            // 左矢印 または Aキー
//            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
//            {
//                selectedIndex = 0;
//                SelectConfirmButton();
//            }
//            // 右矢印 または Dキー
//            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
//            {
//                selectedIndex = 1;
//                SelectConfirmButton();
//            }
//            // Enter または Spaceキー
//            if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
//            {
//                if (selectedIndex == 0) OnConfirmYes();
//                else OnConfirmNo();
//            }
//            return;
//        }

//        // 下矢印 または Sキー
//        if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
//        {
//            selectedIndex = (selectedIndex + 1) % 3;
//            SelectButton();
//        }
//        // 上矢印 または Wキー
//        if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
//        {
//            selectedIndex = (selectedIndex + 2) % 3;
//            SelectButton();
//        }
//        // Enter または Spaceキー
//        if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
//        {
//            if (selectedIndex == 0) OnStart();
//            else if (selectedIndex == 1) OnSetting();
//            else if (selectedIndex == 2) OnEnd();
//        }
//    }

//    void SelectButton()
//    {
//        if (startButton != null && selectedIndex == 0) startButton.Select();
//        if (settingButton != null && selectedIndex == 1) settingButton.Select();
//        if (endButton != null && selectedIndex == 2) endButton.Select();
//    }

//    void SelectConfirmButton()
//    {
//        if (yesButton != null && selectedIndex == 0) yesButton.Select();
//        if (noButton != null && selectedIndex == 1) noButton.Select();
//    }

//    void OnStart() { SceneManager.LoadScene("Stage1"); }
//    void OnSetting() { Debug.Log("設定未実装"); }

//    void OnEnd()
//    {
//        if (confirmPanel != null)
//        {
//            confirmPanel.SetActive(true);
//            isInConfirmMenu = true;
//            selectedIndex = 1;
//            SelectConfirmButton();
//        }
//    }

//    void OnConfirmYes()
//    {
//#if UNITY_EDITOR
//        UnityEditor.EditorApplication.isPlaying = false;
//#else
//        Application.Quit();
//#endif
//    }

//    void OnConfirmNo()
//    {
//        if (confirmPanel != null)
//        {
//            confirmPanel.SetActive(false);
//            isInConfirmMenu = false;
//            selectedIndex = 2;
//            SelectButton();
//        }
//    }
//}
//↑動く

//↓改造していいよん
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class TitleManager : MonoBehaviour
{
    [Header("画面全体を覆うフェード用画像（黒）")]
    [SerializeField] private Image fadeImage;
    [Header("フェードさせるボタングループ")]
    [SerializeField] private CanvasGroup buttonGroup;

    [Header("フェード速度の設定")]
    [SerializeField] private float sceneFadeInSpeed = 1.5f;
    [SerializeField] private float buttonFadeSpeed = 4.0f;

    [Header("ボタンの登録（上から順番に）")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingButton;
    [SerializeField] private Button endButton;

    [Header("ゲーム終了の確認画面（Panel）")]
    [SerializeField] private GameObject confirmPanel;

    [Header("確認画面のボタン")]
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    private enum TitleState
    {
        SceneFadingIn,
        WaitingForEnter,
        ButtonsFadingIn,
        ActiveMenu
    }
    private TitleState currentState = TitleState.SceneFadingIn;

    private int selectedIndex = 0;
    private bool isInConfirmMenu = false;

    // 新しいInput Systemのアクションクラス
    private GameInputActions inputActions;
    private bool isDirectionPressed = false; // スティックの連打防止用フラグ

    void Awake()
    {
        // インスタンスの生成
        inputActions = new GameInputActions();
    }

    void OnEnable()
    {
        // アクションマップ「Title」を有効化
        inputActions.Title.Enable();
    }

    void OnDisable()
    {
        // 無効化
        inputActions.Title.Disable();
    }

    void Start()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);

        if (buttonGroup != null)
        {
            buttonGroup.alpha = 0;
            buttonGroup.interactable = false;
            buttonGroup.blocksRaycasts = false;
        }

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            Color c = fadeImage.color;
            c.a = 1.0f;
            fadeImage.color = c;
        }
    }

    void Update()
    {
        switch (currentState)
        {
            case TitleState.SceneFadingIn:
                if (fadeImage != null)
                {
                    Color c = fadeImage.color;
                    c.a -= Time.deltaTime * sceneFadeInSpeed;

                    if (c.a <= 0.0f)
                    {
                        c.a = 0.0f;
                        fadeImage.gameObject.SetActive(false);
                        currentState = TitleState.WaitingForEnter;
                    }
                    fadeImage.color = c;
                }
                else
                {
                    currentState = TitleState.WaitingForEnter;
                }
                break;

            case TitleState.WaitingForEnter:
                // Submitアクション（Enterキーやゲームパッドの南ボタン）が押されたか判定
                if (inputActions.Title.Submit.triggered)
                {
                    currentState = TitleState.ButtonsFadingIn;
                }
                break;

            case TitleState.ButtonsFadingIn:
                if (buttonGroup != null)
                {
                    buttonGroup.alpha += Time.deltaTime * buttonFadeSpeed;
                    if (buttonGroup.alpha >= 1.0f)
                    {
                        buttonGroup.alpha = 1.0f;
                        buttonGroup.interactable = true;
                        buttonGroup.blocksRaycasts = true;
                        currentState = TitleState.ActiveMenu;

                        SelectButton();
                    }
                }
                break;

            case TitleState.ActiveMenu:
                HandleMenuInput();
                break;
        }
    }

    void HandleMenuInput()
    {
        Vector2 moveInput = inputActions.Title.Move.ReadValue<Vector2>();

        int verticalInput = 0;
        int horizontalInput = 0;

        if (moveInput.magnitude > 0.5f)
        {
            if (!isDirectionPressed)
            {
                if (moveInput.y > 0.5f) verticalInput = 1;   // 上
                if (moveInput.y < -0.5f) verticalInput = -1; // 下
                if (moveInput.x > 0.5f) horizontalInput = 1; // 右
                if (moveInput.x < -0.5f) horizontalInput = -1;// 左
                isDirectionPressed = true;
            }
        }
        else
        {
            isDirectionPressed = false;
        }

        // キャンセル（戻るボタン）の判定だけは残す
        bool isCancelTriggered = inputActions.Title.Cancel.triggered;

        if (isInConfirmMenu)
        {
            if (isCancelTriggered)
            {
                OnConfirmNo();
                return;
            }

            if (horizontalInput < 0) { selectedIndex = 0; SelectConfirmButton(); }
            if (horizontalInput > 0) { selectedIndex = 1; SelectConfirmButton(); }

            return;
        }

        if (verticalInput < 0) { selectedIndex = (selectedIndex + 1) % 3; SelectButton(); }
        if (verticalInput > 0) { selectedIndex = (selectedIndex + 2) % 3; SelectButton(); }
    }

    void SelectButton()
    {
        if (startButton != null && selectedIndex == 0) startButton.Select();
        if (settingButton != null && selectedIndex == 1) settingButton.Select();
        if (endButton != null && selectedIndex == 2) endButton.Select();
    }

    void SelectConfirmButton()
    {
        if (yesButton != null && selectedIndex == 0) yesButton.Select();
        if (noButton != null && selectedIndex == 1) noButton.Select();
    }

    public void OnStart() { SceneManager.LoadScene("Stage1"); }
    public void OnSetting() { Debug.Log("設定未実装"); }

    public void OnEnd()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(true);
            isInConfirmMenu = true;
            selectedIndex = 1;
            SelectConfirmButton();
        }
    }

    public void OnConfirmYes()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnConfirmNo()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
            isInConfirmMenu = false;
            selectedIndex = 2;
            SelectButton();
        }
    }
}