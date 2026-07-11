using UnityEngine;

/// <summary>
/// 自滅スタン（弱点タイム）。壁に突進を外して激突した直後に入る。
///
///   - 入った瞬間に衝撃波を左右へ発生（技④）→ スタン中も棒立ちさせない
///   - 盾のガードを無効化（正面が開く）→ どこからでもバーストで殴れる
///   - 被弾ダメージ倍率アップは BossChargerHealth.stunDamageMultiplier が担当
///   - 点滅で「今がチャンス」を見せる
/// </summary>
public class BossChargerStunState : BossChargerBaseState
{
    private float timer;
    private float blinkPhase;
    private BlinkFade blinkFade;

    public BossChargerStunState(BossChargerController boss) : base(boss) { }

    public override void Enter()
    {
        timer = boss.stunDuration;
        blinkPhase = 0f;

        // 壁激突の衝撃波（技④）
        boss.SpawnShockwaves();

        // 盾を下げて全身を弱点にする
        boss.shield?.SetGuardEnabled(false);

        // 半透明フェードの明滅を開始（LineRenderer/TrailRenderer は BlinkFade 側で除外）
        if (boss.stunBlinkInterval > 0f)
        {
            blinkFade = new BlinkFade(boss.GetComponentsInChildren<Renderer>(true));
            blinkFade.Begin();
        }
    }

    public override void Update()
    {
        UpdateBlink();

        timer -= Time.deltaTime;
        if (timer <= 0f) boss.TransitionToState(boss.StateIdle);
    }

    public override void Exit()
    {
        // フェードを戻す前に進行中の白フラッシュを確定させる（EnemyCharger.EndStunFade と同じ流儀）。
        // 先に End() すると、フラッシュが掴んでいる複製マテリアルが破棄され、
        // フラッシュ復帰時に破棄済みマテリアルへ戻ってピンク化するため。
        boss.Health?.StopFlash();

        // フェードを元へ戻し、盾を構え直す
        blinkFade?.End();
        blinkFade = null;
        boss.shield?.SetGuardEnabled(true);
    }

    // モデルのアルファを不透明↔半透明で脈動させる（Animator の m_Enabled 上書きの影響を受けない）
    private void UpdateBlink()
    {
        if (blinkFade == null || !blinkFade.IsActive) return;

        float speed = Mathf.PI * 2f / Mathf.Max(0.0001f, boss.stunBlinkInterval);
        blinkPhase += Time.deltaTime * speed;

        float t = Mathf.Cos(blinkPhase) * 0.5f + 0.5f; // 0..1
        blinkFade.SetAlpha(Mathf.Lerp(boss.stunBlinkMinAlpha, 1f, t));
    }
}
