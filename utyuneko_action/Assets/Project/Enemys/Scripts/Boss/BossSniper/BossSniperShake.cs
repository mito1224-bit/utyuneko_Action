using UnityEngine;

/// <summary>
/// ボススナイパーの被弾シェイク（独立コンポーネント・位置揺らしのみ）。
///
/// 見た目（visualTransform）の localPosition だけを毎フレームずらして、時間が来たら元へ戻す。
/// scale（瞬間移動の収縮）や rotation（振り向き）は BossSniperBeamUnit が制御しているが、
/// position は誰も触っていないので競合しない。
///
/// 2種類の揺れ:
///   - ShakeRandom(strength, duration): 全方向ランダムジッター。スタン一撃・撃破など向け。
///   - ShakeAxis(axis, strength, duration): 指定軸の ＋/− 方向にだけジッター（1次元）。
///     通常ダメージ用。プレイヤー→ボスの方向を軸に渡すと「殴られた方向に沿って震える」。
///
/// 揺れの強さ・時間は呼び出し側（BossSniperHitStop）が種別ごとに渡す。
/// 全体スロー中でもキレが出るよう unscaledDeltaTime で動かす。
/// 多重呼び出しは後勝ち（新しい揺れで上書き）。
/// </summary>
public class BossSniperShake : MonoBehaviour
{
    [Header("対象")]
    [Tooltip("揺らす見た目のルート。未指定なら BossSniperBeamUnit.visualTransform、無ければ自分の Transform")]
    public Transform visualRoot;

    private Vector3 homePosition;   // 揺れの基準となる元の localPosition
    private bool hasHome;

    private float timer;            // 残り時間
    private float duration;         // 全体時間（減衰に使う。今回は減衰なしの一定ジッターだが基準として保持）
    private float strength;
    private bool axisMode;          // true=軸±のみ, false=全方向
    private Vector2 axis;           // 軸モードのときの単位ベクトル

    void Awake()
    {
        if (visualRoot == null)
        {
            BossSniperBeamUnit unit = GetComponent<BossSniperBeamUnit>();
            if (unit == null) unit = GetComponentInParent<BossSniperBeamUnit>();
            if (unit != null && unit.visualTransform != null) visualRoot = unit.visualTransform;
        }
        if (visualRoot == null) visualRoot = transform;

        CaptureHome();
    }

    // 揺れの基準位置を記録（揺れていないときの localPosition）
    private void CaptureHome()
    {
        if (visualRoot != null)
        {
            homePosition = visualRoot.localPosition;
            hasHome = true;
        }
    }

    /// <summary>全方向ランダムジッター（スタン一撃・撃破など）。</summary>
    public void ShakeRandom(float strength, float duration)
    {
        BeginShake(strength, duration, axisMode: false, axis: Vector2.zero);
    }

    /// <summary>
    /// 指定軸の ＋/− 方向にだけジッター（通常ダメージ）。
    /// axis はプレイヤー→ボスの方向などを渡す（内部で正規化）。
    /// </summary>
    public void ShakeAxis(Vector2 axis, float strength, float duration)
    {
        Vector2 a = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector2.right;
        BeginShake(strength, duration, axisMode: true, axis: a);
    }

    private void BeginShake(float strength, float duration, bool axisMode, Vector2 axis)
    {
        if (strength <= 0f || duration <= 0f || visualRoot == null) return;

        // 揺れ中でなければ、今の位置を基準として記録（別演出が位置を動かしていないタイミングで捕捉）
        if (timer <= 0f) CaptureHome();

        this.strength = strength;
        this.duration = duration;
        this.timer = duration;
        this.axisMode = axisMode;
        this.axis = axis;
    }

    void LateUpdate()
    {
        if (!hasHome || visualRoot == null) return;

        if (timer > 0f)
        {
            timer -= Time.unscaledDeltaTime;

            if (timer <= 0f)
            {
                // 終了：元の位置へ戻す
                visualRoot.localPosition = homePosition;
            }
            else
            {
                Vector3 offset;
                if (axisMode)
                {
                    // 軸の ＋/− 方向にだけランダムにずらす（1次元ジッター）
                    float mag = Random.Range(-strength, strength);
                    offset = new Vector3(axis.x * mag, axis.y * mag, 0f);
                }
                else
                {
                    // 全方向ランダムジッター
                    offset = new Vector3(
                        Random.Range(-strength, strength),
                        Random.Range(-strength, strength),
                        0f);
                }
                visualRoot.localPosition = homePosition + offset;
            }
        }
    }

    void OnDisable()
    {
        // 無効化時に揺れっぱなしにならないよう元へ戻す
        if (hasHome && visualRoot != null) visualRoot.localPosition = homePosition;
        timer = 0f;
    }
}