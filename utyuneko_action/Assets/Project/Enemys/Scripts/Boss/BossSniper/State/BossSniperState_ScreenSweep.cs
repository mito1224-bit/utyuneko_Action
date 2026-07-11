using UnityEngine;

/// <summary>
/// スクリーンスイープ：巡回エリアの左端または右端に本体が陣取り、
/// 「下段 → 中段 → 上段」の順に、水平（X軸平行）の極太ビームを1本ずつ撃つ攻撃。
/// プレイヤーは狙わない（各ビームは水平固定で画面を横断する）。
///
/// 端の選び方: プレイヤーから遠い方の端。左端なら右向き、右端なら左向きに撃つ。
///
/// 1段ぶんの流れ（これを下→中→上で3回繰り返す）:
///   その段の高さへテレポート（既存の収縮→展開を流用）
///   → 予告射線だけ少し見せる（telegraphLeadTime）
///   → 予告射線＋本体シェイクで溜め（chargeTime。カメラシェイクはしない）
///   → 極太ビーム発射（beamFireDuration）
///   → 次の段へ（強化中は段間の間隔が縮む）
///
/// 撃っている間、本体は端に露出しているのでバーストで殴れる（OnBurstHit で通常ダメージ）。
///
/// 幅・時間はすべて BossSniper 側のインスペクタ項目で調整する（screenSweep... フィールド）。
/// 発射中の極太ビーム幅／予告射線の幅は、本体 SelfUnit の beamWidth / sightWidth を
/// この攻撃の間だけ専用値へ差し替え、Exit で必ず元に戻す。
/// </summary>
public class BossSniperState_ScreenSweep : BossSniperStateBase
{
    private enum Step { Teleporting, Telegraph, Charge, Firing, Gap }
    private Step step;

    private readonly BossSniperTeleport teleport = new BossSniperTeleport();

    private int tier;            // 現在の段（0=下, 1=中, 2=上）
    private float edgeX;         // 陣取る端のX（左端 or 右端）
    private Vector2 fireDir;     // 撃つ向き（左端→右、右端→左）
    private float[] tierY = new float[3]; // 下・中・上のY

    private bool chargeShakeStarted; // この段の溜めシェイクを開始済みか

    // 差し替えた幅を元に戻すための退避
    private float savedSightWidth;
    private float savedBeamWidth;
    private bool widthsSaved;

    public override void Enter(BossSniper boss)
    {
        base.Enter(boss);
        boss.RestoreFlightBody();
        boss.SelfUnit.HideBeam();

        // 3段の高さを、エリア縦範囲から上下端マージンを除いて等間隔（下・中・上）に取る
        boss.GetSidebarArea(out Vector2 min, out Vector2 max);
        float m = Mathf.Max(0f, boss.screenSweepEdgeMargin);
        float loY = min.y + m;
        float hiY = max.y - m;
        if (hiY < loY) { float mid = (min.y + max.y) * 0.5f; loY = hiY = mid; } // マージン過大でも破綻させない
        tierY[0] = loY;                         // 下
        tierY[1] = (loY + hiY) * 0.5f;          // 中
        tierY[2] = hiY;                         // 上

        // プレイヤーから遠い端を選ぶ（左端 or 右端）。撃つ向きは中央（＝反対側）へ
        float centerX = (min.x + max.x) * 0.5f;
        float playerX = boss.Player != null ? boss.Player.position.x : centerX;
        bool useLeftEdge = playerX >= centerX; // プレイヤーが右寄り → 左端が遠い
        edgeX = useLeftEdge ? min.x : max.x;
        fireDir = useLeftEdge ? Vector2.right : Vector2.left;

        // この攻撃専用のビーム幅へ差し替え（Exit で戻す）
        SaveAndApplyWidths();

        // 1段目（下）からテレポート開始
        tier = 0;
        BeginTeleportToTier();
    }

    public override void UpdateState()
    {
        switch (step)
        {
            case Step.Teleporting:
                teleport.Update();
                if (!teleport.Running)
                {
                    // 出現し切ったら予告フェーズへ。射線方向は水平固定
                    boss.SelfUnit.SetAimDirection(fireDir);
                    step = Step.Telegraph;
                    timer = Mathf.Max(0f, boss.screenSweepTelegraphLeadTime);
                }
                break;

            case Step.Telegraph:
                // 予告射線だけ見せる（細い赤ライン。幅は sightWidth＝screenSweepSightWidth）
                boss.SelfUnit.SetAimDirection(fireDir);
                boss.SelfUnit.LockTick();
                if (Countdown())
                {
                    step = Step.Charge;
                    timer = Mathf.Max(0f, boss.screenSweepChargeTime);
                    chargeShakeStarted = false;
                }
                break;

            case Step.Charge:
                // 予告射線＋本体シェイク（溜め）。カメラシェイクはしない
                boss.SelfUnit.SetAimDirection(fireDir);
                boss.SelfUnit.LockTick();
                if (!chargeShakeStarted)
                {
                    chargeShakeStarted = true;
                    if (boss.BodyShake != null)
                        boss.BodyShake.ShakeRandom(boss.screenSweepChargeShakeStrength,
                                                   Mathf.Max(0.01f, boss.screenSweepChargeTime));
                }
                if (Countdown())
                {
                    step = Step.Firing;
                    timer = Mathf.Max(0f, boss.screenSweepBeamFireDuration);
                    boss.SelfUnit.SetAimDirection(fireDir);
                    boss.SelfUnit.FireTick(); // fireDuration=0 でも最低1回は判定
                }
                break;

            case Step.Firing:
                // 極太ビーム（幅は beamWidth＝screenSweepBeamWidth）
                boss.SelfUnit.SetAimDirection(fireDir);
                boss.SelfUnit.FireTick();
                if (Countdown())
                {
                    boss.SelfUnit.HideBeam();

                    if (tier >= 2)
                    {
                        // 3段撃ち終えたら帰還 → 次の行動抽選へ
                        boss.TransitionToState(boss.StateReturn);
                        return;
                    }

                    // 次の段まで少し空ける（強化中は間隔を縮める）
                    step = Step.Gap;
                    timer = Mathf.Max(0f, CurrentTierGap());
                }
                break;

            case Step.Gap:
                if (Countdown())
                {
                    tier++;
                    BeginTeleportToTier();
                }
                break;
        }
    }

    // その段の高さ・選んだ端へテレポート（既存の収縮→展開を流用）
    private void BeginTeleportToTier()
    {
        boss.SelfUnit.HideBeam();
        Vector2 dest = new Vector2(edgeX, tierY[Mathf.Clamp(tier, 0, 2)]);
        step = Step.Teleporting;
        teleport.Begin(boss.SelfUnit, dest, boss.teleportShrinkTime, boss.teleportExpandTime,
            onBeforeExpand: () =>
            {
                // 消えている間に射線方向を水平へ確定（プレイヤー追従にしない）
                boss.SelfUnit.SetAimDirection(fireDir);
                boss.SelfUnit.SnapVisualToCurrentAimImmediate();
            });
    }

    // 次の段までの間隔。強化中は screenSweepTierGapEnraged を使う（通常より短い想定）
    private float CurrentTierGap()
    {
        if (boss.IsEnraged && boss.screenSweepTierGapEnraged >= 0f)
            return boss.screenSweepTierGapEnraged;
        return boss.screenSweepTierGap;
    }

    private void SaveAndApplyWidths()
    {
        if (widthsSaved) return;
        BossSniperBeamUnit u = boss.SelfUnit;
        savedSightWidth = u.sightWidth;
        savedBeamWidth = u.beamWidth;
        widthsSaved = true;

        u.sightWidth = Mathf.Max(0.001f, boss.screenSweepSightWidth);
        u.beamWidth = Mathf.Max(0.01f, boss.screenSweepBeamWidth);
    }

    private void RestoreWidths()
    {
        if (!widthsSaved) return;
        BossSniperBeamUnit u = boss.SelfUnit;
        u.sightWidth = savedSightWidth;
        u.beamWidth = savedBeamWidth;
        widthsSaved = false;
    }

    public override void Exit()
    {
        // 幅を必ず元に戻し、ビームを消す
        RestoreWidths();
        boss.SelfUnit.HideBeam();
    }

    // 撃っている間、本体は端に露出：バーストで通常ダメージ（無敵時間つき）
    public override void OnBurstHit(BossSniperBeamUnit unit, PlayerController pc)
    {
        boss.HandleNormalBurstHit(unit, pc);
    }
}