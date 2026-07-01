using UnityEngine;

/// <summary>
/// 範囲攻撃。プレイヤーの現在位置を狙って AreaImpact プレハブを上空から落下させる。
/// AreaImpact プレハブは ReflectableBullet を持つ前提だが、initial direction は下向き（Vector3.down）にする。
/// 着弾検知は ReflectableBullet 内で実施される（壁/地面に接触で消滅は要追加だが、本実装では lifeTime で自然消滅）。
/// </summary>
public class BossState_AreaImpact : IBossState
{
    private BossController b;
    private bool fired = false;
    private float exitTimer = 0.8f;

    public void Enter(BossController boss)
    {
        b = boss;
        if (b.movement != null) b.movement.IsLocked = true;

        fired = false;
        exitTimer = 0.8f;
    }

    public void UpdateState()
    {
        if (!fired)
        {
            FireImpact();
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

    private void FireImpact()
    {
        if (b.player == null) return;
        GameObject prefab = b.areaImpactPrefab != null ? b.areaImpactPrefab : b.bulletPrefab;
        if (prefab == null) return;

        Vector3 spawnPos = b.player.position + Vector3.up * b.areaImpactSpawnHeight;
        GameObject go = Object.Instantiate(prefab, spawnPos, Quaternion.identity);
        ReflectableBullet bullet = go.GetComponent<ReflectableBullet>();
        if (bullet != null) bullet.SetDirection(Vector3.down);
    }
}
