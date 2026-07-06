using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 横一斉射：左右の壁沿いに互い違いの高さで「設置ユニット」（分身とは別モデル・sidebarUnitPrefab）を
/// 順次配置し、全員そろったら「X軸に平行・中央向き」のレーザーを一斉発射する。
/// その間、本体は上空からプレイヤーに狙いを定めていて、一斉発射に自分の狙い撃ちを重ねる。
///
/// 流れ:
///   本体がエリア上空中央へ瞬間移動
///   → 設置ユニットを sidebarPlaceInterval ごとに1体ずつ配置（左右交互・下から or 上からはランダム。
///      置かれたユニットはすぐ横向きの赤い射線を出すので、どの高さが塞がるかは順次わかる）
///   → 全員配置完了 → 一斉ロック（Difficulty.sidebarLockTime。本体もプレイヤーへ狙いを固定）
///   → 一斉発射（横バー全部＋本体の狙い撃ちが同時）→ 帰還（Return）
///
/// 設置ユニットは破壊不可（IsHazardUnit）：
///   - バーストで壊せない。バースト含め触れたプレイヤーが接触ダメージを受ける「触ると痛い設置物」。
///   - 対抗手段は「横バーの隙間の高さへ避ける」か「上空で無防備な本体を殴りに行く」の二択。
///
/// ユニット数とロック時間は難易度セット（Difficulty.sidebarCloneCount / sidebarLockTime）でスケール。
/// エリアは boss.GetSidebarArea()（マーカー2点 or 巡回ポイントの外接矩形）。
/// </summary>
public class BossSniperState_SideBarrage : BossSniperStateBase
{
    private enum Step { Teleporting, Placing, VolleyLock, Firing }
    private Step step;

    private readonly BossSniperTeleport teleport = new BossSniperTeleport();

    private readonly List<Vector2> slots = new List<Vector2>();   // 分身の配置ポイント（置く順）
    private readonly List<Vector2> slotDirs = new List<Vector2>(); // 各配置ポイントの射線方向（中央向き）
    private int placedCount;   // 置き終えた数
    private float placeTimer;  // 次の1体を置くまでのタイマー

    public override void Enter(BossSniper boss)
    {
        base.Enter(boss);

        // プレイヤー不在・プレハブ未設定なら成立しないので巡回へ戻る
        if (boss.Player == null || boss.clonePrefab == null)
        {
            boss.AttackTimer = boss.timeBetweenAttacks;
            boss.TransitionToState(boss.StatePatrol);
            return;
        }

        BuildSlots();

        // 本体はエリア上空中央へ（そこからプレイヤーに狙いを定める）
        boss.GetSidebarArea(out Vector2 min, out Vector2 max);
        Vector2 bossPos = new Vector2((min.x + max.x) * 0.5f, max.y);

        step = Step.Teleporting;
        teleport.Begin(boss.SelfUnit, bossPos, boss.teleportShrinkTime, boss.teleportExpandTime,
            onBeforeExpand: () => boss.SelfUnit.SnapVisualToPlayerImmediate());
    }

    // 左右交互・互い違いの高さの配置ポイントを作る（下から上へ or 上から下へはランダム）
    private void BuildSlots()
    {
        slots.Clear();
        slotDirs.Clear();
        placedCount = 0;
        placeTimer = 0f; // 1体目は到着後すぐ置く

        boss.GetSidebarArea(out Vector2 min, out Vector2 max);

        int count = Mathf.Max(1, boss.Difficulty.sidebarCloneCount);
        bool bottomUp = Random.value < 0.5f;      // 下から並べるか、上からか
        bool startLeft = Random.value < 0.5f;     // 1体目が左壁か右壁か

        for (int i = 0; i < count; i++)
        {
            // 高さ：エリアの上下を等間隔に割る（端に寄りすぎないよう 1/(count+1) 刻み）
            float t = (i + 1f) / (count + 1f);
            float y = bottomUp ? Mathf.Lerp(min.y, max.y, t) : Mathf.Lerp(max.y, min.y, t);

            // 左右：交互
            bool left = ((i % 2) == 0) == startLeft;
            float x = left ? min.x : max.x;

            slots.Add(new Vector2(x, y));
            slotDirs.Add(left ? Vector2.right : Vector2.left); // 中央向き（X軸に平行）
        }
    }

    public override void UpdateState()
    {
        switch (step)
        {
            case Step.Teleporting:
                teleport.Update();
                if (!teleport.Running)
                {
                    step = Step.Placing;
                }
                break;

            case Step.Placing:
                boss.SelfUnit.AimTick(); // 本体は上空からプレイヤーを照準（黄色い追従射線）
                TickPlacedClones();      // 置き終えた分身は赤い射線で自分の高さを示す

                placeTimer -= Time.deltaTime;
                if (placeTimer <= 0f && placedCount < slots.Count)
                {
                    PlaceNextClone();
                    placeTimer = Mathf.Max(0.05f, boss.sidebarPlaceInterval);
                }

                // 全員置き終えて出現し切ったら一斉ロックへ
                if (placedCount >= slots.Count && AllClonesExpanded())
                {
                    step = Step.VolleyLock;
                    timer = Mathf.Max(0.05f, boss.Difficulty.sidebarLockTime);
                }
                break;

            case Step.VolleyLock:
                boss.SelfUnit.LockTick(); // 本体：直前まで追っていた狙いを固定（赤）
                TickPlacedClones();
                if (Countdown())
                {
                    step = Step.Firing;
                    timer = Mathf.Max(0f, boss.sidebarFireDuration);
                    FireAll(); // fireDuration=0 でも最低1回は判定
                }
                break;

            case Step.Firing:
                FireAll();
                if (Countdown())
                {
                    boss.TransitionToState(boss.StateReturn); // 撃ち終えたら帰還→次の行動選択へ
                }
                break;
        }
    }

    // 次の1体を配置（収縮状態で生成→展開。射線は横向きに固定しておく）
    private void PlaceNextClone()
    {
        BossSniperBeamUnit u = boss.SpawnSidebarUnitAt(slots[placedCount]); // 分身とは別モデルの設置ユニット
        u.SetAimDirection(slotDirs[placedCount]);
        u.SnapVisualToPlayerImmediate(); // 出現前にモデルの向きを整える
        u.BeginExpand(boss.teleportExpandTime);
        placedCount++;
    }

    // 置き終えた分身の毎フレーム処理：出現し切ったら当たり判定を有効化し、赤い射線を出す
    private void TickPlacedClones()
    {
        foreach (BossSniperBeamUnit u in boss.Units)
        {
            if (u == null || u.IsReal) continue;
            if (u.IsScaleAnimating) continue; // まだ出現中

            u.SetHitboxEnabled(true); // 出現し切った設置ユニットは「触ると痛い」障害物になる（壊せない）
            u.LockTick();             // 横向きの赤い射線で「この高さが塞がる」と予告
        }
    }

    private bool AllClonesExpanded()
    {
        foreach (BossSniperBeamUnit u in boss.Units)
        {
            if (u != null && u.IsScaleAnimating) return false;
        }
        return true;
    }

    // 一斉発射：横バー全部＋本体の狙い撃ちを同時に
    private void FireAll()
    {
        boss.SelfUnit.FireTick();
        foreach (BossSniperBeamUnit u in boss.Units)
        {
            if (u == null || u.IsReal) continue;
            u.FireTick();
        }
    }

    public override void Exit()
    {
        boss.SelfUnit.HideBeam();
        // 分身の後片付けは Return の DespawnClones() が行う
    }

    // 本物（上空の本体）に当てれば通常ダメージ。
    // 設置ユニットは IsHazardUnit なので OnBurstHit 自体が飛んでこない（壊せない・接触ダメージ側で処理）
    public override void OnBurstHit(BossSniperBeamUnit unit, PlayerController pc)
    {
        if (unit.IsReal) boss.HandleNormalBurstHit(unit, pc);
    }
}