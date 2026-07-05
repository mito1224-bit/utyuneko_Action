using UnityEngine;

/// <summary>
/// お供分身：強化モード（HP半分以下）の巡回中に1体だけ常駐する自律ユニット。
/// 本体の巡回と同じように、巡回エリア内を瞬間移動しながらプレイヤーへスナイパー攻撃する。
///
/// ライフサイクル（BossSniper 側が管理）:
///   - 出現条件: 強化モード ＆ 巡回系ステート（Patrol / PatrolShot） ＆ この巡回中に未撃破。
///   - 本体が巡回以外の状態（全体攻撃・分身攻撃・スタンなど）へ移ると退場（Destroy）。
///   - プレイヤーがバーストで当てると撃破：バースト回数を回復させ、その巡回中は再出現しない。
///
/// 行動ループ（自律・BossSniper のヒットストップとは独立して動く）:
///   出現（収縮→展開） → 待機 → 瞬間移動 → ロック（狙い固定・赤い射線） → レーザー → 待機 → …
///   攻撃パラメータは BossSniper.companionSettings（本体の出現撃ちより弱めに調整できる）。
///
/// 生成方法: BossSniper.SpawnCompanion() が clonePrefab を Instantiate し、
/// このコンポーネントを AddComponent して Init() を呼ぶ（プレハブに事前に付ける必要はない）。
/// </summary>
public class BossSniperCompanion : MonoBehaviour
{
    private enum Step { Appearing, Idle, Teleporting, PreparingAim, Locking, Firing }
    private Step step;

    private BossSniper boss;
    private BossSniperBeamUnit unit;
    private readonly BossSniperTeleport teleport = new BossSniperTeleport();
    private float timer;

    /// <summary>BossSniper.SpawnCompanion() から呼ばれる初期化。</summary>
    public void Init(BossSniper boss, BossSniperBeamUnit unit)
    {
        this.boss = boss;
        this.unit = unit;

        // 被弾はボスのステートではなく、この分身自身が処理する（撃破＝消滅）
        unit.OnBurstHit = HandleBurstHit;

        // 収縮状態から「にゅっ」と出現する（瞬間移動の出現と同じ演出）
        unit.SetShrunkenImmediate();
        unit.BeginExpand(boss.teleportExpandTime);
        step = Step.Appearing;
    }

    void Update()
    {
        if (boss == null || unit == null) return;
        BossSniper.CompanionSettings cs = boss.companionSettings;

        switch (step)
        {
            case Step.Appearing:
                unit.FaceTick();
                if (!unit.IsScaleAnimating)
                {
                    unit.SetHitboxEnabled(true);
                    timer = cs.teleportInterval;
                    step = Step.Idle;
                }
                break;

            case Step.Idle:
                unit.FaceTick(); // 待機中はプレイヤーの方を向く
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    teleport.Begin(unit, boss.RandomPatrolPoint(), boss.teleportShrinkTime, boss.teleportExpandTime, onBeforeExpand: () => boss.SelfUnit.SnapVisualToPlayerImmediate());

                    step = Step.Teleporting;
                }
                break;

            case Step.Teleporting:
                unit.FacePlayerVisualOnlyTick();
                teleport.Update();
                if (!teleport.Running)
                {
                    BeginPrepareAim(); // 出現した瞬間に狙いを固定して撃つ
                }
                break;

            case Step.PreparingAim:
                unit.AimVisualOnlyTick();
                if(unit.IsVisualAlignedToAim())
                {
                    timer = Mathf.Max(0.05f, cs.lockTime);
                    step = Step.Locking;
                }
                break;

            case Step.Locking:
                unit.LockTick(); // 固定した狙いを赤い射線で見せる（最終警告）
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    step = Step.Firing;
                    timer = Mathf.Max(0f, cs.fireDuration);
                    unit.FireTick(); // fireDuration=0 でも最低1回は判定
                }
                break;

            case Step.Firing:
                unit.FireTick();
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    unit.HideBeam();
                    timer = cs.teleportInterval;
                    step = Step.Idle; // 撃ち終えたら次のテレポートまで待機
                }
                break;
        }
    }

    // 出現した瞬間に狙いを1回だけ決めて固定する（本体の出現撃ちと同じ流儀・パラメータは弱め設定）
    private void BeginPrepareAim()
    {
        BossSniper.CompanionSettings cs = boss.companionSettings;

        if (boss.Player == null)
        {
            timer = cs.teleportInterval;
            step = Step.Idle;
            return;
        }

        Vector2 aimPoint = boss.Player.position;

        // 偏差撃ち：プレイヤーの現在速度から少し先の位置を狙う
        if (boss.PlayerRb != null && Random.value < cs.leadChance)
        {
            aimPoint += boss.PlayerRb.linearVelocity * cs.leadTime;
        }

        unit.SetAimPoint(aimPoint);

        step = Step.PreparingAim;
    }

    // プレイヤーのバースト体当たり → 撃破（バースト回数の回復と再出現禁止はボス側が処理）
    private void HandleBurstHit(BossSniperBeamUnit hitUnit, PlayerController pc)
    {
        if (boss != null) boss.OnCompanionKilled(pc);
        Destroy(gameObject);
    }
}