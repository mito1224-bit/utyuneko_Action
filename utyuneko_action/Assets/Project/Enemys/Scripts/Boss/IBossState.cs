using UnityEngine;

/// <summary>
/// ボスのステートが実装するインターフェース。
/// PlayerState と同じ責務分離パターン: Enter/Exit/Update/FixedUpdate を BossController が委譲する。
/// </summary>
public interface IBossState
{
    void Enter(BossController boss);
    void Exit();
    void UpdateState();
    void FixedUpdateState();
}
