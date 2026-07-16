using UnityEngine;

/// <summary>
/// 死亡。接触ダメージを全て無効化し、盾を落とし、半透明フェードで明滅させながら
/// onDefeated（扉開放・イベント起動など）を発火して deathDestroyDelay 後に消滅する。
/// 明滅はスタン時と同じ BlinkFade（アルファ脈動＝完全点滅ではない）を使う。
/// </summary>
public class BossChargerDeadState : BossChargerBaseState
{
    private float timer;
    private float blinkPhase;
    private BlinkFade blinkFade;

    public BossChargerDeadState(BossChargerController boss) : base(boss) { }

    public override void Enter()
    {
        timer = boss.deathDestroyDelay;
        blinkPhase = 0f;

        // 進行中の白フラッシュを止めてマテリアルを元へ戻してから死亡フェードに入る
        // （HitFlash と BlinkFade がどちらもマテリアルを差し替えるので競合＝ピンク化を防ぐ）。
        boss.Health?.StopFlash();

        // 死体に触れてもダメージを受けないように（EnemyKnockback の死亡処理と同じ流儀）
        boss.SetAllDamageSourcesEnabled(false);
        boss.shield?.SetGuardEnabled(false);

        // 死亡時はアウトラインを消す（明滅の有無に関わらず）。
        // アウトラインは RenderingLayerMask フィルタなので、モデルを既定レイヤーへ戻して対象外にする。
        var deathRenderers = boss.GetComponentsInChildren<Renderer>(true);
        BlinkFade.HideOutline(deathRenderers);

        // 死亡明滅（半透明フェード）。LineRenderer/TrailRenderer は BlinkFade 側で除外される。
        if (boss.deathBlinkInterval > 0f)
        {
            blinkFade = new BlinkFade(deathRenderers);
            blinkFade.Begin(); // マテリアルを透明対応の複製へ差し替え
        }

        boss.onDefeated?.Invoke();
    }

    public override void Update()
    {
        UpdateBlink();

        timer -= Time.deltaTime;
        if (timer <= 0f) Object.Destroy(boss.gameObject);
    }

    // モデルのアルファを不透明↔半透明で脈動させる（Animator の m_Enabled 上書きの影響を受けない）
    private void UpdateBlink()
    {
        if (blinkFade == null || !blinkFade.IsActive) return;

        float speed = Mathf.PI * 2f / Mathf.Max(0.0001f, boss.deathBlinkInterval);
        blinkPhase += Time.deltaTime * speed;

        float t = Mathf.Cos(blinkPhase) * 0.5f + 0.5f; // 0..1
        blinkFade.SetAlpha(Mathf.Lerp(boss.deathBlinkMinAlpha, 1f, t));
    }
}
