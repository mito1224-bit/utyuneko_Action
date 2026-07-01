using UnityEngine;

/// <summary>
/// プレイヤー方向へ ReflectableBullet を homingShotCount 回連射する。
/// 弾自体は直進だが、発射の瞬間にプレイヤー位置を狙う＝ゆるい追尾感を出す。
/// </summary>
public class BossState_HomingShot : IBossState
{
    private BossController b;
    private int remaining;
    private float fireTimer;

    public void Enter(BossController boss)
    {
        b = boss;
        if (b.movement != null) b.movement.IsLocked = true;

        remaining = b.homingShotCount;
        fireTimer = 0f;
    }

    public void UpdateState()
    {
        if (b.player == null || b.bulletPrefab == null || b.firePoint == null)
        {
            b.TransitionToState(b.StateIdle);
            return;
        }

        fireTimer -= Time.deltaTime;
        if (fireTimer <= 0f && remaining > 0)
        {
            FireOne();
            remaining--;
            fireTimer = b.homingShotInterval;
        }

        if (remaining <= 0 && fireTimer <= 0f)
        {
            b.TransitionToState(b.StateIdle);
        }
    }

    public void FixedUpdateState() { }
    public void Exit() { }

    private void FireOne()
    {
        Vector3 dir = (b.player.position - b.firePoint.position);
        dir.z = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        GameObject go = Object.Instantiate(b.bulletPrefab, b.firePoint.position, Quaternion.identity);
        ReflectableBullet bullet = go.GetComponent<ReflectableBullet>();
        if (bullet != null) bullet.SetDirection(dir.normalized);
    }
}
