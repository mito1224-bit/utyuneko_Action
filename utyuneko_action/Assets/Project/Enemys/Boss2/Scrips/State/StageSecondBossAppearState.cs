using UnityEngine;
using System.Collections;

public class StageSecondBossAppearState : StageSecondBossBaseState
{
    private float appearTimer = 0f;
    private int sequenceStep = 0;
    private Vector3 originalScale;
    private CameraFollowWithZoom cameraController;

    // ⏳ 登場演出の合計時間（例：1.5秒間カメラがボスを大迫力凝視する）
    private float appearDuration = 1.5f;

    public StageSecondBossAppearState(StageSecondBossController boss) : base(boss) { }

    public override void Enter()
    {
        appearTimer = 0f;
        sequenceStep = 0;

        Debug.Log("<color=red>🎬 ボス：登場デモ演出を開始。完全無敵化 ＆ カメラを目的地へロックオン！</color>");

        if (boss.BossStatgeCamera)
        {
            boss.BossStatgeCamera.gameObject.SetActive(false);
        }

        // 演出中にボスが弾を投げたり、触れてプレイヤーがダメージを受けないように全判定を安全に完全OFF！
        boss.SetAllDamageSourcesEnabled(false);
        if (boss.bossAnimator != null) boss.bossAnimator.speed = 0f;

        Transform bossVisual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;
        originalScale = bossVisual.localScale;

        // 物理移動を一時的に完全カットして空中に神々しく固定
        Rigidbody2D rb = boss.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }

        // 🛠️【先回りカメラ連動】君のカメラ関数を呼び出し、目覚めたボス本体を画面中央にググッと大迫力ズームイン！
        cameraController = Object.FindFirstObjectByType<CameraFollowWithZoom>();
        if (cameraController != null)
        {
            cameraController.StartTrackTarget(boss.transform, 4.5f, 2.5f); // 登場なので少し早めのスピードで凝視！
        }

        // 登場時の目覚ましSE（大咆哮など）を鳴らす！
        SoundManager.Instance.PlaySE(SeType.EnemyCharge);

        ShakeTarget.Instance.Shake(2.5f, 2.5f);
    }

    public override void Update()
    {
        appearTimer += Time.deltaTime;
        Transform bossVisual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;

        // 🎬 登場演出中のきも格好いいビジュアル：その場で不気味にグワングワンと脈動させる
        float pulse = 1.0f + Mathf.Abs(Mathf.Sin(Time.time * 16f)) * 0.12f;
        bossVisual.localScale = originalScale * pulse;

        // ⏱️ 1.5秒のデモ時間が終了したら、通常のバトルへ移行！
        if (appearTimer >= appearDuration)
        {
            // スケールを元のサイズに美しく戻す
            bossVisual.localScale = originalScale;

            // 👑 通常のIdle戦闘ステートへガチャンと移行して、ボス戦正式スタート！
            boss.TransitionToState(boss.StateIdle);
        }
    }

    public override void Exit()
    {
        SoundManager.Instance.PlayBGM(BgmType.BossBattle, 1.0f);

        // 演出が終わったので、各種ステータスを確実に通常の「戦闘モード」へ復元する鉄壁のセーフティ
        Transform bossVisual = boss.ultVisualOffsetObject != null ? boss.ultVisualOffsetObject : boss.transform;
        bossVisual.localScale = originalScale;

        if (boss.bossAnimator != null)
        {
            boss.bossAnimator.speed = 1.0f;
        }

        // 🛠️【カメラ連動】登場デモが終わったので、カメラをプレイヤーの元へフワッと戻す！
        if (cameraController != null)
        {
            cameraController.ReturnToPlayerFromEvent(0.5f);
        }
        if (boss.BossStatgeCamera)
        {
            boss.BossStatgeCamera.gameObject.SetActive(true);
        }

        // ボスの攻撃判定とバリアをすべてONにして、ガチバトル開始！
        boss.SetAllDamageSourcesEnabled(true);
        boss.ResetBarrier();

        Debug.Log("<color=green>⚔️ ボス：登場演出完了！ バリア展開、戦闘グリッドを起動します！</color>");
    }
}