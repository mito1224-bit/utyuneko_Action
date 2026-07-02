using UnityEngine;

/// <summary>
/// 「XZスケール収縮 → 座標切り替え → XZスケール復元」の瞬間移動シーケンスを進める小さなヘルパー。
/// 収縮を始めた瞬間から復元し終わるまで当たり判定を無効化する（消えている間は殴れない／触れない）。
/// MonoBehaviour ではないので、使うステートが所有して毎フレーム Update() を回す。
/// </summary>
public class BossSniperTeleport
{
    private enum Step { Idle, Shrinking, Expanding }
    private Step step = Step.Idle;

    private BossSniperBeamUnit unit;
    private Vector2 destination;
    private float expandTime;
    private System.Action onFinished;

    /// <summary>シーケンスが進行中か。</summary>
    public bool Running => step != Step.Idle;

    public void Begin(BossSniperBeamUnit unit, Vector2 destination, float shrinkTime, float expandTime, System.Action onFinished = null)
    {
        this.unit = unit;
        this.destination = destination;
        this.expandTime = expandTime;
        this.onFinished = onFinished;

        unit.SetHitboxEnabled(false); // 消えている間は当たらない
        unit.BeginShrink(shrinkTime);
        step = Step.Shrinking;
    }

    public void Update()
    {
        if (step == Step.Idle || unit == null) return;
        if (unit.IsScaleAnimating) return; // 収縮／復元のアニメ待ち

        if (step == Step.Shrinking)
        {
            // 縮み切った → 座標を切り替えて復元開始
            unit.transform.position = destination;
            unit.BeginExpand(expandTime);
            step = Step.Expanding;
        }
        else
        {
            // 復元し切った → 完了
            unit.SetHitboxEnabled(true);
            step = Step.Idle;
            onFinished?.Invoke();
        }
    }
}
