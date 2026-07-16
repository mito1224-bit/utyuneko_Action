using UnityEngine;

/// <summary>
/// 地面叩き→隆起衝撃柱（技⑦）。盾で地面を叩き、プレイヤー方向へ衝撃柱を一列に波及させて隆起させる。
///
/// フェーズ: 予兆(Windup:プレイヤーを向いてタメ) → 発動(Slam:柱を一列生成) → 硬直(Recover) → 待機へ。
///
/// 柱そのもの（予兆・隆起・当たり判定・見た目）は BossChargerPillar が自己完結で処理する。
/// このステートは「どこへ・何本・どの時間差で」並べるかだけを担当する。
/// 地上で棒立ちしているプレイヤーを咎め、ジャンプ／バーストを強制する技。
/// </summary>
//public class BossChargerGroundSlamState : BossChargerBaseState
//{
//    private enum Phase { Windup, Recover }
//    private Phase phase;
//    private float timer;
//    private int dir = -1;

//    public BossChargerGroundSlamState(BossChargerController boss) : base(boss) { }

//    public override void Enter()
//    {
//        phase = Phase.Windup;
//        timer = boss.groundSlamWindupTime / boss.SpeedMultiplier;
//        boss.GroundSlamTimer = boss.groundSlamCooldown; // クールダウン開始（連発防止）

//        Vector2 toPlayer = boss.DirectionToPlayer(true);
//        dir = toPlayer.x == 0 ? boss.Facing : (int)Mathf.Sign(toPlayer.x);
//        boss.SetFacing(dir);
//    }

//    public override void Update()
//    {
//        boss.UpdateModelFacing();
//        timer -= Time.deltaTime;

//        switch (phase)
//        {
//            case Phase.Windup:
//                if (timer <= 0f)
//                {
//                    SpawnPillars();
//                    phase = Phase.Recover;
//                    // 波が走り終わるまで待ってから戻る（自分で撒いた柱に突っ込まないため）
//                    timer = WaveDuration() + boss.groundSlamRecoverTime;
//                }
//                break;

//            case Phase.Recover:
//                if (timer <= 0f) boss.TransitionToState(boss.StateIdle);
//                break;
//        }
//    }

//    // プレイヤー方向へ、足元から一列に柱を生成。i 本目ほど遅れて起きる（波及感）
//    private void SpawnPillars()
//    {
//        Vector3 feet = boss.transform.position + (Vector3)boss.shockwaveSpawnOffset;
//        int count = Mathf.Max(1, boss.groundSlamPillarCount);

//        for (int i = 0; i < count; i++)
//        {
//            float distance = boss.groundSlamPillarStartOffset + i * boss.groundSlamPillarSpacing;
//            Vector3 pos = new Vector3(feet.x + dir * distance, feet.y, feet.z);

//            GameObject go = new GameObject("BossChargerPillar");
//            go.transform.position = pos;
//            BossChargerPillar pillar = go.AddComponent<BossChargerPillar>();
//            pillar.Init(
//                waitTime: i * boss.groundSlamPillarStagger,
//                telegraphTime: boss.pillarTelegraphTime,
//                riseTime: boss.pillarRiseTime,
//                activeTime: boss.pillarActiveTime,
//                width: boss.pillarWidth,
//                height: boss.pillarHeight,
//                damage: boss.pillarDamage,
//                targetLayers: boss.attackTargetLayers,
//                visual: new BossChargerPillar.VisualConfig
//                {
//                    material = boss.pillarMaterial,
//                    tint = boss.tintPillarMaterial,
//                    telegraphStart = boss.pillarTelegraphColorStart,
//                    telegraphEnd = boss.pillarTelegraphColorEnd,
//                    active = boss.pillarActiveColor,
//                });
//        }
//    }

//    // 最後の柱が消えるまでの概算時間
//    private float WaveDuration()
//    {
//        int count = Mathf.Max(1, boss.groundSlamPillarCount);
//        float lastStart = (count - 1) * boss.groundSlamPillarStagger;
//        return lastStart + boss.pillarTelegraphTime + boss.pillarRiseTime + boss.pillarActiveTime;
//    }
//}
