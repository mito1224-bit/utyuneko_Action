/// <summary>
/// ボス用ステートの共通インターフェース（プレイヤーの IPlayerState と同じ流儀）。
/// 加えて、ボス特有のイベント（被弾・着地）もステート側で処理できるようにしてある。
/// </summary>
public interface IBossSniperState
{
    // その状態になった瞬間に1回だけ呼ばれる
    void Enter(BossSniper boss);

    // その状態のフレーム毎に呼ばれる（Updateの代わり）
    void UpdateState();

    // 物理計算のフレーム毎に呼ばれる（FixedUpdateの代わり）
    void FixedUpdateState();

    // その状態が終わって、次の状態に行く瞬間に1回だけ呼ばれる
    void Exit();

    // プレイヤーのバースト体当たりを受けた（unit = 当たったユニット。本物かは unit.IsReal）
    void OnBurstHit(BossSniperBeamUnit unit, PlayerController pc);

    // 地形（obstacleLayer）に接触した（スタン落下の着地検知に使う）
    void OnGroundHit();
}
