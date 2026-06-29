using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerState_None : IPlayerState
{
    private PlayerController p;

    public void Enter(PlayerController player)
    {
        p = player;
        Debug.Log("ステート変更：チャージ開始（空中スロー）");
    }

    public void UpdateState()
    {
    }

    public void FixedUpdateState()
    {
    }

    public void Exit()
    {
    }
}
