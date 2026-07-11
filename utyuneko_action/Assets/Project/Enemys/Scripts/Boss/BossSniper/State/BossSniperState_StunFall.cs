using UnityEngine;

/// <summary>
/// スタン落下：本物を見破られた。分身を解除し、無敵のまま重力で地面へ落ちる。
/// このボスで唯一の「瞬間移動ではない移動」。
/// 着地（地形との接触）で着地猶予（StunGrace）へ。
/// 保険として「ほぼ静止が続いた」場合も着地とみなし、着地を検知できないまま
/// 時間切れになったら諦めて帰還（Return）する。
/// </summary>
public class BossSniperState_StunFall : BossSniperStateBase
{
    private float stillTimer;

    public override void Enter(BossSniper boss)
    {
        base.Enter(boss);

        Debug.Log($"<color=red>[StunFall] Enter  IsEnraged={boss.IsEnraged} EventPaused={boss.EventPaused} HP={(boss.Health != null ? boss.Health.CurrentHP : -1f)}</color>\n{System.Environment.StackTrace}");

        boss.DespawnClones();
        boss.SelfUnit.HideBeam();
        boss.BeginFallBody(); // Dynamic に切り替えて落下開始（コライダーも有効化される）

        if (boss.Health != null) boss.Health.ResetStunHit(); // このスタンの「倍率一撃」枠をリセット

        timer = Mathf.Max(0.5f, boss.stunFallTimeout);
        stillTimer = 0f;

        boss.onStunned?.Invoke();
    }

    public override void UpdateState()
    {
        // 保険1：ほぼ静止が続いたら着地とみなす（接触イベントを取り逃した場合用）
        if (boss.Rb.linearVelocity.sqrMagnitude < 0.01f)
        {
            stillTimer += Time.deltaTime;
            if (stillTimer >= 0.2f)
            {
                boss.TransitionToState(boss.StateStunGrace);
                return;
            }
        }
        else
        {
            stillTimer = 0f;
        }

        // 保険2：着地を検知できないまま時間切れ（穴に落ち続けている等）なら諦めて復帰
        if (Countdown())
        {
            boss.TransitionToState(boss.StateReturn);
        }
    }

    // 地形（obstacleLayer）に接触 → 着地
    public override void OnGroundHit()
    {
        boss.TransitionToState(boss.StateStunGrace);
    }

    // OnBurstHit は実装しない ＝ 落下中は無敵
    // （下から突き上げたバーストに落下中のボスが再接触して、
    //   スタンと同じ1回のバーストでダメージまで入ってしまう連続ヒットを防ぐ）
}