using UnityEngine;

/// <summary>
/// ボススナイパーの「生きているモーション」― 脈動スケール（独立コンポーネント）。
///
/// visualTransform の localScale を Mathf.Sin でループさせ、心臓の拍動のような
/// 微小な伸縮を常時かける。Flash・Shake とは独立したコンポーネント。
///
/// BossSniperBeamUnit が収縮演出（テレポート）中は自動的に脈動を止め、
/// 収縮演出が終わったら再開する（スケール競合防止）。
///
/// チャージ中のスロー（Time.timeScale）の影響を受けないよう unscaledDeltaTime で動かす。
/// </summary>
public class BossSniperPulse : MonoBehaviour
{
    [Header("対象")]
    [Tooltip("脈動させる見た目のルート。未指定なら BossSniperBeamUnit.visualTransform、無ければ自分の Transform")]
    public Transform visualRoot;

    [Header("脈動")]
    [Tooltip("スケールの振れ幅（例: 0.05 で ±5% の脈動）")]
    [Range(0f, 0.3f)]
    public float amplitude = 0.05f;

    [Tooltip("脈動の速さ（回/秒）。1.0 で 1 秒に 1 拍")]
    [Range(0.1f, 5f)]
    public float frequency = 1.2f;

    private BossSniperBeamUnit beamUnit;
    private Vector3 baseScale;
    private bool hasBaseScale;
    private float phase;

    void Awake()
    {
        beamUnit = GetComponent<BossSniperBeamUnit>();
        if (beamUnit == null) beamUnit = GetComponentInParent<BossSniperBeamUnit>();

        if (visualRoot == null && beamUnit != null)
            visualRoot = beamUnit.visualTransform;
        if (visualRoot == null)
            visualRoot = transform;
    }

    void Start()
    {
        if (visualRoot != null)
        {
            baseScale = visualRoot.localScale;
            hasBaseScale = true;
        }
    }

    void LateUpdate()
    {
        if (!hasBaseScale || visualRoot == null) return;

        // 収縮演出中はスケールを BeamUnit に任せる（競合防止）
        if (beamUnit != null && beamUnit.IsScaleAnimating) return;

        phase += Time.unscaledDeltaTime * frequency * Mathf.PI * 2f;

        float pulse = 1f + amplitude * Mathf.Sin(phase);
        visualRoot.localScale = new Vector3(
            baseScale.x * pulse,
            baseScale.y * pulse,
            baseScale.z * pulse
        );
    }

    void OnDisable()
    {
        // 無効化時に元のスケールへ戻す
        if (hasBaseScale && visualRoot != null)
            visualRoot.localScale = baseScale;
        phase = 0f;
    }
}
