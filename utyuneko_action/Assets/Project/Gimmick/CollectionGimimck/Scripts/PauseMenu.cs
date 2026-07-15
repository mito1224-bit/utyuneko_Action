using UnityEngine;
using UnityEngine.SceneManagement; // シーン遷移に必要
using TMPro;
using UnityEngine.InputSystem;

public class PauseMenu : MonoBehaviour
{
    [Header("ポーズ画面のパネルUI")]
    [SerializeField] private GameObject pauseMenuPanel;

    [Header("遷移先の設定")]
    [Tooltip("タイトル画面のシーン名")]
    [SerializeField] private string titleSceneName = "TitleScene";

    private bool isPaused = false;
    private Vector3 playerInitialPosition;
    private GameObject playerObj;
    private PlayerController playerController;
    

    GameInputActions ac;

    void Start()
    {
        // 念のため開始時はポーズ画面を閉じて時間を進める
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
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
        if (InputManager.Instance.Player.Pose.triggered)
        {
            if (isPaused) ResumeGame();
            else PauseGame();
        }
    }

    // ─── パネルの開閉処理 ───


    public void PauseGame()
    {
        isPaused = true;
        pauseMenuPanel.SetActive(true);
        Time.timeScale = 0f; // ★ゲーム内の時間を完全に停止させる

        if (playerController != null && playerController.inputActions != null)
        {
            playerController.inputActions.Player.Disable();
            Debug.Log("【ポーズ】プレイヤーの入力を無効化しました。");
        }

        // 開いた瞬間の最新の取得数をDataManagerから吸い上げて表示
        if (DataManager.Instance != null)
        {
            var bitResult = DataManager.Instance.GetNormalCoinResult();
            var dataResult = DataManager.Instance.GetStarCoinResult();
        }
    }

    public void ResumeGame()
    {
        pauseMenuPanel.SetActive(false);
        Time.timeScale = 1f; // ★ゲーム内の時間を動かす
        isPaused = false;    // フラグを戻す（trueにしていたバグもついでに修正）

        // ★【追加】ゲーム再開時にプレイヤーの入力を再び有効化する
        if (playerController != null && playerController.inputActions != null)
        {
            playerController.inputActions.Player.Enable();
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

        SceneManager.LoadScene(titleSceneName);
    }
}