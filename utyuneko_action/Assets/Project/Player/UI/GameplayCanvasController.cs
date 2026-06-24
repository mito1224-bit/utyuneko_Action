using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class GameplayCanvasController : MonoBehaviour
{
    [Header("フェードスピード")]
    [SerializeField] private float fadeSpeed = 6.0f;

    private CanvasGroup canvasGroup;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    void Update()
    {
        bool isEventPlaying = false;

        // 🎬 シーン内にイベントマネージャーがいたら、今イベント中かどうかをチェック
        if (RescueEventManager.Instance != null)
        {
            var s = RescueEventManager.Instance.CurrentState;
            // 状態が「戦闘中」でも「終了」でもない ＝ まさにイベント真っ最中！
            isEventPlaying = (s != RescueEventManager.RescueState.InDanger && s != RescueEventManager.RescueState.Finished);
        }

        // 💡 イベント中ならアルファ（透明度）の目標値を0（完全透明）に、通常時は1（くっきり表示）にする
        float targetAlpha = isEventPlaying ? 0f : 1f;

        // ⏱️ Mathf.Lerpを使って、Canvas全体の透明度を滑らかに変化させる！
        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);

        // 🚨【超重要バグ対策】完全に透明になっている間は、UIの当たり判定（ボタンなど）を
        // 完全に無効化して、裏側のゲームプレイやクリックを絶対に邪魔しないようにする！
        canvasGroup.blocksRaycasts = !isEventPlaying;
        canvasGroup.interactable = !isEventPlaying;
    }
}