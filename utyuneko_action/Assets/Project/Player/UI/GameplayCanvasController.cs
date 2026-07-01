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
        // ===================================================================
        // 「今、何かイベント（またはエリア演出）やってる？」と親玉に1行聞くだけ！
        // ===================================================================
        bool isEventPlaying = BaseEventManager.IsAnyEventPlaying;

        // 💡 イベント中ならアルファ（透明度）の目標値を0（完全透明）に、通常時は1（くっきり表示）にする
        float targetAlpha = isEventPlaying ? 0f : 1f;

        // ⏱️ 滑らかにフェード変化させる（Time.deltaTimeのままでイベント中も滑らかに動きます）
        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);

        // 🚨【超重要バグ対策】完全に透明になっている間は、UIの当たり判定を完全に無効化！
        canvasGroup.blocksRaycasts = !isEventPlaying;
        canvasGroup.interactable = !isEventPlaying;
    }
}