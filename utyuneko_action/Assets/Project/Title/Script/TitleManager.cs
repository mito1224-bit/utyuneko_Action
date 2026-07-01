using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleManager : MonoBehaviour
{
    [Header("遷移先シーン名")]
    [SerializeField] private string targetSceneName = "SampleScene";

    [Header("画面全体を覆うフェード用画像（黒）")]
    [SerializeField] private Image fadeImage;
    [Header("フェードさせるボタングループ")]
    [SerializeField] private CanvasGroup buttonGroup;

    [Header("フェード時間の設定（秒）")]
    [Tooltip("シーン開始時、真っ黒な画面から明るくなるまでにかける時間")]
    [SerializeField] private float sceneFadeInDuration = 1.5f;

    [Tooltip("『Press Enter』の後に、メニューボタンがフワッと出現するまでにかける時間")]
    [SerializeField] private float buttonFadeInDuration = 0.8f;

    //[Tooltip("ゲーム開始ボタンを押した後、画面が真っ黒に暗転するまでにかける時間")]
    //[SerializeField] private float sceneFadeOutDuration = 1.2f;

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
        ActiveMenu,
        SceneFadingOut
    }
    private TitleState currentState = TitleState.SceneFadingIn;

    private int selectedIndex = 0;
    private bool isInConfirmMenu = false;
    private float inputTimer = 0f;

    private GameInputActions inputActions;
    private bool isDirectionPressed = false;

    void OnEnable()
    {
        SoundManager.Instance.PlayBGM(BgmType.Title, sceneFadeInDuration * 2.0f);

        inputActions = InputManager.Instance;
        inputActions.UI.Enable();
        inputActions.Player.Disable();
    }

    void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.UI.Disable();
            inputActions.Player.Enable();
        }
    }

    void Start()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);

        if (buttonGroup != null)
        {
            buttonGroup.alpha = 0f;
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

        StartCoroutine(TitleFlowRoutine());
    }

    private IEnumerator TitleFlowRoutine()
    {
        currentState = TitleState.SceneFadingIn;
        if (fadeImage != null)
        {
            float elapsed = 0f;
            Color c = fadeImage.color;
            while (elapsed < sceneFadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / sceneFadeInDuration);

                c.a = Mathf.SmoothStep(1f, 0f, t);
                fadeImage.color = c;
                yield return null;
            }
            fadeImage.gameObject.SetActive(false);
        }

        currentState = TitleState.WaitingForEnter;
        inputTimer = 0f;

        while (!inputActions.UI.Submit.triggered && inputTimer < 1.0f)
        {
            inputTimer += Time.deltaTime;
            yield return null;
        }

        currentState = TitleState.ButtonsFadingIn;
        if (buttonGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < buttonFadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / buttonFadeInDuration);

                buttonGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
                yield return null;
            }
            buttonGroup.alpha = 1.0f;
            buttonGroup.interactable = true;
            buttonGroup.blocksRaycasts = true;
        }

        currentState = TitleState.ActiveMenu;
        SelectButton();
    }

    void Update()
    {
        if (currentState == TitleState.ActiveMenu)
        {
            HandleMenuInput();
        }
    }

    void HandleMenuInput()
    {
        Vector2 moveInput = inputActions.UI.Move.ReadValue<Vector2>();

        int verticalInput = 0;
        int horizontalInput = 0;

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

        bool isCancelTriggered = inputActions.UI.Cancel.triggered;

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

    public void OnStart()
    {
        if (currentState != TitleState.ActiveMenu) return;

        SoundManager.Instance.FadeBGMVolume(0.0f, 0.5f);
        SoundManager.Instance.PlaySE(SeType.UiEnter);

        currentState = TitleState.SceneFadingOut;

        TransitionManager.Instance.ChangeScene(targetSceneName, TransitionType.Fade);
    }

    public void OnSetting()
    {
        Debug.Log("設定未実装");
    }

    public void OnEnd()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(true);
            isInConfirmMenu = true;
            selectedIndex = 1;
            SelectConfirmButton();

            SoundManager.Instance.PlaySE(SeType.UiGameEnd);
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

            SoundManager.Instance.PlaySE(SeType.UiCancel);
        }
    }
}