using UnityEngine;

public class ButtonPromptUI : MonoBehaviour
{
    [SerializeField, Tooltip("ゲームパッド用のTransform。未設定なら自分自身")]
    private Transform visualRoot;
    [SerializeField, Tooltip("キーボード用のTransform。未使用なら空でOK")]
    private Transform visualRoot2;

    [SerializeField, Tooltip("頭からさらに離す余白")]
    private Vector3 offset = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private float popDuration = 0.12f;
    [SerializeField] private float bobAmplitude = 0.08f;
    [SerializeField] private float bobSpeed = 4f;
    [SerializeField] private bool autoHeightFromCollider = true;

    [Header("拡大縮小")]
    [SerializeField, Tooltip("どれくらい膨らむか（0.1 = ±10%）")]
    private float pulseAmplitude = 0.1f;
    [SerializeField, Tooltip("1往復にかかる秒数（小さいほど速い）")]
    private float pulsePeriod = 0.8f;

    private PlayerController target;
    private Vector3 baseScale;
    private Vector3 baseScale2;
    private float shownTime;
    private float headHeight;
    private bool isVisible;

    private void Awake()
    {
        if (visualRoot == null) visualRoot = transform;
        baseScale = visualRoot.localScale;
        visualRoot.localScale = Vector3.zero;

        if (visualRoot2 != null)
        {
            baseScale2 = visualRoot2.localScale;
            visualRoot2.localScale = Vector3.zero;
        }

        isVisible = false;
    }

    public void Show(PlayerController player)
    {
        target = player;

        headHeight = 0f;
        if (autoHeightFromCollider && player.circleCollider2D != null)
        {
            var col = player.circleCollider2D;
            float s = Mathf.Max(Mathf.Abs(player.transform.lossyScale.x),
                                Mathf.Abs(player.transform.lossyScale.y));
            headHeight = (col.offset.y + col.radius) * s;
        }
    }

    public void Hide()
    {
        target = null;
    }

    private void LateUpdate()
    {
        bool shouldShow = target != null && target.CurrentState == target.StateNormal;

        if (shouldShow && !isVisible) shownTime = 0f;
        isVisible = shouldShow;

        if (!shouldShow)
        {
            visualRoot.localScale = Vector3.zero;
            if (visualRoot2 != null) visualRoot2.localScale = Vector3.zero;
            return;
        }

        UpdatePosition();

        shownTime += Time.unscaledDeltaTime;

        // ① ポップイン（0 → 1、軽くオーバーシュート）
        float t = Mathf.Clamp01(shownTime / popDuration);
        float pop = Mathf.Sin(t * Mathf.PI * 0.5f) * (1f + 0.15f * (1f - t));

        // ② 拡大縮小（ポップ完了後から始動。sin(0)=0なので繋ぎ目が出ない）
        float pulseTime = Mathf.Max(0f, shownTime - popDuration);
        float pulse = 1f + Mathf.Sin(pulseTime / pulsePeriod * Mathf.PI * 2f) * pulseAmplitude;

        float scale = pop * pulse;
        visualRoot.localScale = baseScale * scale;
        if (visualRoot2 != null) visualRoot2.localScale = baseScale2 * scale;
    }

    private void UpdatePosition()
    {
        float bob = Mathf.Sin(Time.unscaledTime * bobSpeed) * bobAmplitude;
        transform.position = target.transform.position
                           + offset
                           + new Vector3(0f, headHeight + bob, 0f);
    }
}