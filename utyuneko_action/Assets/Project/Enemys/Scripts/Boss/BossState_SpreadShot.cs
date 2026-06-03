using UnityEngine;

/// <summary>
/// プレイヤー方向を中心に spreadAngleDeg の範囲で spreadCount 発を同時に扇状発射。
/// </summary>
public class BossState_SpreadShot : IBossState
{
    private BossController b;
    private bool fired = false;
    private float exitTimer = 0.5f;

    public void Enter(BossController boss)
    {
        b = boss;
        if (b.movement != null) b.movement.IsLocked = true;

        fired = false;
        exitTimer = 0.5f;
    }

    public void UpdateState()
    {
        if (!fired)
        {
            FireSpread();
            fired = true;
        }

        exitTimer -= Time.deltaTime;
        if (exitTimer <= 0f)
        {
            b.TransitionToState(b.StateIdle);
        }
    }

    public void FixedUpdateState() { }
    public void Exit() { }

    private void FireSpread()
    {
        if (b.player == null || b.bulletPrefab == null || b.firePoint == null) return;

        Vector3 toPlayer = b.player.position - b.firePoint.position;
        toPlayer.z = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f) return;
        Vector3 baseDir = toPlayer.normalized;

        int n = Mathf.Max(1, b.spreadCount);
        float halfAngle = b.spreadAngleDeg * 0.5f;

        for (int i = 0; i < n; i++)
        {
            float t = (n == 1) ? 0.5f : (float)i / (n - 1);
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector3 dir = Quaternion.Euler(0f, 0f, angle) * baseDir;

            GameObject go = Object.Instantiate(b.bulletPrefab, b.firePoint.position, Quaternion.identity);
            ReflectableBullet bullet = go.GetComponent<ReflectableBullet>();
            if (bullet != null) bullet.SetDirection(dir);
        }
    }
}
