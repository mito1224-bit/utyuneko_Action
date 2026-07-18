using UnityEngine;
using UnityEngine.SceneManagement;

public class CreditScrollManager : MonoBehaviour
{
    [Header("⏱️ スクロール設定")]
    [SerializeField] private float scrollSpeed = 60f;       // 通常の昇り速度
    [SerializeField] private float fastForwardSpeed = 240f; // クリック/Space長押し時の早送り速度

    [Header("終着点（このY座標を超えたら終了）")]
    [SerializeField] private float endYPosition = 1200f;    // Contentの下端が画面外に消えるY座標

    [Header("🎬 終了後に遷移するシーン名")]
    [SerializeField] private string nextSceneName = "TitleScene";

    private RectTransform rectTransform;
    private bool isEnding = false;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();

        // 画面の下側（画面外）からスクロールが始まるように初期位置を調整
        // 画面サイズに応じて調整したい場合はインスペクターで初期値を決めてもOK
        Vector2 pos = rectTransform.anchoredPosition;
        pos.y = -Screen.height - 50f;
        rectTransform.anchoredPosition = pos;
    }

    void Update()
    {
        if (isEnding) return;

        // 1. スピードの決定（画面クリック中、またはSpaceキー長押し中で早送り）
        float currentSpeed = scrollSpeed;
        if (Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space))
        {
            currentSpeed = fastForwardSpeed;
        }

        // 2. 上方向へ等速移動
        rectTransform.anchoredPosition += Vector2.up * currentSpeed * Time.deltaTime;

        // 3. 終了判定（画面外へ完全に消えたらシーン遷移）
        if (rectTransform.anchoredPosition.y >= endYPosition)
        {
            isEnding = true;
            TransitionManager.Instance.ChangeScene(nextSceneName, TransitionType.Fade);
        }
    }
}