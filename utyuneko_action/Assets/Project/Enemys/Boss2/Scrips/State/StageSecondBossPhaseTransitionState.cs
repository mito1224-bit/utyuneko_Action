using UnityEngine;
using System.Collections;

public class StageSecondBossPhaseTransitionState : StageSecondBossBaseState
{
    private float phaseTimer = 0f;
    private int sequenceStep = 0;
    private Vector3 targetAirCenterPos;
    private Vector3 originalScale;

    // カメラコントローラーの記憶変数
    private CameraFollowWithZoom cameraController;

    // ===================================================================
    // 🛠️【新機能】カメラの先回りの目印になる、一時的な見えないターゲットオブジェクト
    // ===================================================================
    private GameObject tempCameraTarget;

    public StageSecondBossPhaseTransitionState(StageSecondBossController boss) : base(boss) { }

    public override void Enter()
    {
        phaseTimer = 0f;
        sequenceStep = 0;

        Debug.Log("<color=orange>🔥 ボス：第2フェーズ移行！ カメラを目的地に先回りさせて威嚇を開始します。</color>");

        if (boss.BossStatgeCamera)
        {
            boss.BossStatgeCamera.gameObject.SetActive(false);
        }

        boss.SetAllDamageSourcesEnabled(false);
        if (boss.bossAnimator != null) boss.bossAnimator.speed = 0f;

        // 威嚇中はバリアを強制OFFにする
        if (boss.TryGetComponent<StageSecondBossHealth>(out var health))
        {
            health.StopFlashAndReset();
            health.hasBarrier = false;
            health.UpdateBarrierVisual();
        }
        boss.ForceResetAllMaterials();

        // 画面上のすべての通常爆弾・地雷を根こそぎ完全消去
        StageSecondBossTimedBomb[] timedBombs = Object.FindObjectsByType<StageSecondBossTimedBomb>(FindObjectsSortMode.None);
        foreach (var bomb in timedBombs)
        {
            if (bomb != null) Object.Destroy(bomb.gameObject);
        }

        StageSecondBossMineBomb[] mineBombs = Object.FindObjectsByType<StageSecondBossMineBomb>(FindObjectsSortMode.None);
        foreach (var bomb in mineBombs)
        {
            if (bomb != null) Object.Destroy(bomb.gameObject);
        }

        // 階層のサイズと中央上空の座標（目的地）を自動計算
        Transform bossVisual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;
        originalScale = bossVisual.localScale;

        float centerX = (boss.stageMinX + boss.stageMaxX) / 2f;
        float topY = boss.stageMaxY - 4.0f;
        targetAirCenterPos = new Vector3(centerX, topY, boss.transform.position.z);

        // 物理モードを切って移動準備
        Rigidbody2D rb = boss.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }

        // ===================================================================
        // 🛠️【バグ修正：カメラ目的地先回りシステム】
        // ボス本体を凝視するのではなく、目的地の座標に見えない空のオブジェクトを生成し、
        // カメラには最初からそこをロックオン（先回り移動）させます！
        // ===================================================================
        cameraController = Object.FindFirstObjectByType<CameraFollowWithZoom>();
        if (cameraController != null)
        {
            // 見えない空のゲームオブジェクトを作成して目的地に配置
            tempCameraTarget = new GameObject("TempCameraEventTarget");
            tempCameraTarget.transform.position = targetAirCenterPos;

            // カメラにはボスではなく、この目的地のオブジェクトを追跡させる
            cameraController.StartTrackTarget(tempCameraTarget.transform, 3f, 2f);
        }

        // 0.6秒で画面中央の上空へ高速ホバー移動開始（ボスは遅れてここに滑り込む）
        boss.StartCoroutine(boss.HoverMoveRoutine(targetAirCenterPos, 0.6f));
    }

    public override void Update()
    {
        phaseTimer += Time.deltaTime;
        Transform bossVisual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;

        // ⏱️ ステップ0：中央上空へ移動中（0.6秒待機）
        if (sequenceStep == 0 && phaseTimer >= 0.6f)
        {
            sequenceStep = 1;
            phaseTimer = 0f;

            ShakeTarget.Instance.Shake(2.5f, 2.5f);

            SoundManager.Instance.PlaySE(SeType.EnemyRangeAttack);
            Debug.Log("<color=red>📢 ボス：目的地に到着、大咆哮！ 巨大化威嚇中！</color>");
        }

        // ⏱️ ステップ1：最大サイズ（1.6倍）で激しく威嚇（1.2秒間）
        if (sequenceStep == 1)
        {
            float pulse = 1.6f + Mathf.Sin(Time.time * 30f) * 0.1f;
            bossVisual.localScale = originalScale * pulse;

            if (phaseTimer >= 1.2f)
            {
                sequenceStep = 2;
                phaseTimer = 0f;
            }
        }

        // ⏱️ ステップ2：巨大化したサイズから元のサイズへ0.4秒かけて滑らかに縮小
        if (sequenceStep == 2)
        {
            float shrinkDuration = 0.4f;
            float t = Mathf.Clamp01(phaseTimer / shrinkDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            bossVisual.localScale = Vector3.Lerp(originalScale * 1.6f, originalScale, smoothT);

            if (t >= 1f)
            {
                // 攻撃スピード・アニメーションスピードを第2フェーズ用に引き上げ
                boss.attackSpeedMultiplier = boss.phase2SpeedMultiplier;
                if (boss.bossAnimator != null)
                {
                    boss.bossAnimator.speed = boss.attackSpeedMultiplier;
                }

                // バトルのIdleステートへ帰還
                boss.TransitionToState(boss.StateIdle);
            }
        }
    }

    public override void Exit()
    {
        // ① スケールを確実に元のサイズに戻す
        Transform bossVisual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;
        bossVisual.localScale = originalScale;

        // ② 威嚇が終わったらバリアを再展開
        boss.ResetBarrier();

        boss.SetAllDamageSourcesEnabled(true);

        // ③ カメラをプレイヤーに滑らかに戻す
        if (cameraController != null)
        {
            cameraController.ReturnToPlayerFromEvent(1.0f);
        }
        // ===================================================================
        // 🛠️【安全クリーンアップ】一時的に作ったカメラ用の目印オブジェクトを完全削除
        // ===================================================================
        if (tempCameraTarget != null)
        {
            Object.Destroy(tempCameraTarget);
        }

        if (boss.BossStatgeCamera)
        {
            boss.BossStatgeCamera.gameObject.SetActive(true);
        }

        Debug.Log("<color=green>✨ ボス：威嚇演出終了。バリアを再展開し、カメラターゲットを破棄しました。</color>");
    }
}