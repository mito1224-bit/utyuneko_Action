public interface IPlayerState
{
    // その状態になった瞬間に1回だけ呼ばれる（Startみたいなもの）
    void Enter(PlayerController player);

    // その状態のフレーム毎に呼ばれる（Updateの代わり）
    void UpdateState();

    // 物理計算のフレーム毎に呼ばれる（FixedUpdateの代わり）
    void FixedUpdateState();

    // その状態が終わって、次の状態に行く瞬間に1回だけ呼ばれる
    void Exit();
}