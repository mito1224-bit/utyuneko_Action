using UnityEngine;

public class PlayerState_Damage : IPlayerState
{
    private PlayerController p;
    private Vector2 knockbackDir;
    private float damageTimer;

    /// <summary>
    /// 外部（Health）から吹っ飛ぶ方向をセットするための関数
    /// </summary>
    public void SetKnockbackDirection(Vector2 direction)
    {
        knockbackDir = direction.normalized;
    }

    public void Enter(PlayerController player)
    {
        p = player;
        damageTimer = p.damageDuration;

        // ★超重要：Enterした瞬間に、回っていたドリルやしなりを完全に正面に戻す！
        if (p.visualManager != null)
        {
            p.visualManager.ResetVisuals();
        }

        if (p.anim != null)
        {
            // アニメーションに被弾トリガー（isDamageなど）があれば発動
            p.anim.SetTrigger("isDamage");
        }

        // バースト中の残像やトレイルを消す
        if (p.trailRenderer != null) p.trailRenderer.enabled = false;
        if (p.afterImageEffect != null) p.afterImageEffect.enabled = false;

        // ★物理：斜め上（2Dアクションの王道ノックバック）に吹っ飛ばす！
        // 敵が右にいれば左上、左にいれば右上へ
        Vector2 finalForce = new Vector2(knockbackDir.x * p.knockbackForceX, p.knockbackForceY);
        p.rb2D.linearVelocity = finalForce;
    }

    public void UpdateState()
    {
        damageTimer -= Time.deltaTime;

        // 一定時間（0.3秒）経ったら自動的に通常状態（Normal）に戻す
        if (damageTimer <= 0f)
        {
            p.TransitionToState(p.StateNormal);
        }
    }

    public void FixedUpdateState()
    {
        // のけぞり中は moveInput による速度上書きを「一切行わない」
        // これにより物理演算による綺麗な吹っ飛びが維持されます
    }

    public void Exit()
    {
        //のけぞりが終わったら、速度を完全にリセットして通常状態に戻す
        p.rb2D.linearVelocity = Vector2.zero;

        Debug.Log("のけぞり状態終了");
    }
}