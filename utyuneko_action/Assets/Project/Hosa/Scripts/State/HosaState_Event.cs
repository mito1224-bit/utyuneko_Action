using UnityEngine;

public class HosaState_Event : IHosaState
{
    private HosaController h;

    public void Enter(HosaController hosa)
    {
        h = hosa;
        Debug.Log("補佐ステート：イベント演出状態（Event）開始");
    }

    public void UpdateState()
    {
        // マネージャーに身を委ねる
    }

    public void FixedUpdateState()
    {
      
    }

    public void Exit()
    {
        Debug.Log("補佐ステート：イベント演出状態（Event）終了");
    }
}