using UnityEngine;
using System.Collections;

/// <summary>
/// 👑 崩壊した補佐：形態変化（フェーズ移行）専用ステート
/// 👑【能力強奪ルート完全復旧版】演出終了後、フェーズ2なら能力強奪ステートへ正しくレールを分岐させます！
/// </summary>
public class GlitchHosaPhaseTransitionState : GlitchHosaBaseState
{
    private Coroutine transitionCoroutine;

    public GlitchHosaPhaseTransitionState(GlitchHosaController boss) : base(boss) { }

    public override void Enter()
    {
        // 1. 演出中に余計な判定が出ないよう完全遮断
        boss.SetAllCollidersEnabled(false);
        boss.SetAllDamageSourcesEnabled(false);

        // トランスフォームの見た目を通常の初期状態に一度安全リセット
        boss.targetScale = boss.originalVisualLocalScale;
        boss.targetXRotation = boss.defaultXRotation;
        boss.targetYRotation = boss.defaultYRotation;
        boss.targetZRotation = 0f;
        boss.targetVisualOffset = Vector3.zero;

        if (boss.bossAnimator != null) boss.bossAnimator.speed = 0f;

        boss.HideBossStamp();

        // 2. 演出タイムラインコルーチンをキック
        transitionCoroutine = boss.StartCoroutine(ExecutePhaseTransitionSequence());
    }

    private IEnumerator ExecutePhaseTransitionSequence()
    {
        int nextPhase = boss.pendingNextPhase;
        float speedMultiplier = boss.pendingSpeedMultiplier;

        // 🎥 3. カメラのズーム干渉を防ぐために、ステージのカメラ境界線制限を一時的にOFF
        if (boss.stageCamera != null)
        {
            boss.stageCamera.gameObject.SetActive(false);
        }

        // 🎥 4. カメラをボスへ向かってスッと滑らかにズームイン
        var cameraFollow = Object.FindFirstObjectByType<CameraFollowWithZoom>();
        if (cameraFollow != null)
        {
            cameraFollow.StartTrackTarget(boss.transform, 3.0f, 0.5f);
        }

        // ===================================================================
        // 🔮 5. フェーズに応じた「警告HDRアイコン」を頭上にピキーンと点灯！
        // ===================================================================
        if (nextPhase == 2)
        {
            boss.ShowBossStamp(ImageBubble.StampType.Denger); // 第2形態：危険（Denger）
        }
        else if (nextPhase == 3)
        {
            boss.ShowBossStamp(ImageBubble.StampType.Dokuro); // 最終形態：どくろ（Dokuro）
        }

        // 🎵 6. エラーチャージSEを再生
        SoundManager.Instance.PlaySE(SeType.EnemyCharge);

        // ===================================================================
        // 💥【タメ】その場で「ガガガガッ」とデジタル微振動
        // ===================================================================
        float elapsed = 0f;
        float chargeTime = 0.5f;
        while (elapsed < chargeTime)
        {
            elapsed += Time.deltaTime;

            float shakeX = Random.Range(-0.08f, 0.08f);
            float shakeY = Random.Range(-0.08f, 0.08f);
            boss.targetVisualOffset = new Vector3(shakeX, shakeY, 0f);
            yield return null;
        }
        boss.targetVisualOffset = Vector3.zero; // 振動終了

        // ===================================================================
        // 💥【大解放】頭上のアイコンを消し、SEと画面揺れをドカン
        // ===================================================================
        boss.HideBossStamp();
        SoundManager.Instance.PlaySE(SeType.EnemyRangeAttack);
        ShakeTarget.Instance.Shake(0.4f, 4.0f);

        yield return new WaitForSeconds(0.25f);

        if (boss.bossAnimator != null) boss.bossAnimator.speed = 1f;

        // 🎥 7. カメラワークをプレイヤー視点（等倍）へ通常復旧
        if (cameraFollow != null)
        {
            cameraFollow.ReturnToPlayerFromEvent(1.0f);
        }
        yield return new WaitForSeconds(0.35f);

        // 🎥 8. カメラが戻ったらステージの境界線制限を安全にONへ戻す
        if (boss.stageCamera != null)
        {
            boss.stageCamera.gameObject.SetActive(true);
        }

        // 9. 内部のフェードステータスを確定
        boss.ConfirmPhaseActivation(nextPhase, speedMultiplier);

        // ===================================================================
        // ⚔️ 10.【バグ解決・ルート分岐復活】
        // 反射能力を奪う「フェーズ2」への移行時のみ、通常Idleをバイパスし、
        // プレイヤーの能力を強奪して奥へかっ飛ぶ専用ステート（StateP2_HackingSteal）へ正しく誘導！
        // ===================================================================
        if (nextPhase == 2)
        {
            Debug.Log("<color=cyan>⚡ StateTransition：フェーズ2を検知。能力強奪ハッキングステートへ移行します！</color>");
            boss.TransitionToState(boss.StateP2_HackingSteal);
        }
        else
        {
            // フェーズ3などの場合はそのまま通常戦闘へ
            boss.TransitionToState(boss.StateIdle);
        }
    }

    public override void Update() { }
    public override void FixedUpdate() { }

    public override void Exit()
    {
        if (transitionCoroutine != null)
        {
            boss.StopCoroutine(transitionCoroutine);
        }

        boss.HideBossStamp();
        boss.targetScale = boss.originalVisualLocalScale;
        boss.targetZRotation = 0f;
        boss.targetVisualOffset = Vector3.zero;
        if (boss.bossAnimator != null) boss.bossAnimator.speed = 1f;

        // コライダーとダメージ判定を確実に解放
        boss.SetAllCollidersEnabled(true);
        boss.SetAllDamageSourcesEnabled(true);
    }
}