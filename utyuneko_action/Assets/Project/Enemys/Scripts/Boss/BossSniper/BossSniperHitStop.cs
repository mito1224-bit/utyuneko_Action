using UnityEngine;
using System.Collections;

/// <summary>
/// ボススナイパーのヒットストップ演出（独立コンポーネント）。
///
/// 2系統を状況で使い分ける:
///   - ボスだけフリーズ（FreezeBossOnly）: プレイヤー・UIは通常速度のまま、ボスの内部更新だけを止める。
///     BossSniper.HitStopActive を立て、コントローラ側が Update/FixedUpdate でステート更新をスキップする。
///     頻度の高い通常ダメージ向け（全体を止めるとガクつくため）。
///   - 全体スロー（SlowAll）: Time.timeScale を一時的に落として戻す。手応えを強く出したいとき向け。
///     HPバー・フラッシュは unscaledDeltaTime で組んであるので、スロー中も実時間で動き続ける。
///
/// 呼び分け（BossSniperHealth から呼ばれる想定）:
///   - 通常ダメージ         → FreezeBossOnly(normalFreezeDuration)
///   - スタンの倍率一撃      → SlowAll(stunSlowScale, stunSlowDuration)
///   - 撃破                  → SlowAll(defeatSlowScale, defeatSlowDuration)  ※終了後に通常速度へ戻す
///
/// timeScale は unscaled 時間で計測して戻すので、確実に元へ復帰する。
/// 多重呼び出しは後勝ち（実行中のものを停止して新しい方で上書き）。
/// </summary>
public class BossSniperHitStop : MonoBehaviour
{
    [Header("通常ダメージ（ボスだけフリーズ）")]
    [Tooltip("通常ヒット時、ボスの内部更新を止める時間（秒）。短めがおすすめ")]
    public float normalFreezeDuration = 0.04f;

    [Header("スタン倍率一撃（全体スロー）")]
    [Tooltip("スタン一撃時の timeScale（小さいほど強く止まる）")]
    [Range(0f, 1f)] public float stunSlowScale = 0.2f;

    [Tooltip("スタン一撃時のスロー時間（実時間・秒）")]
    public float stunSlowDuration = 0.12f;

    [Header("撃破（全体スロー）")]
    [Tooltip("撃破時の timeScale")]
    [Range(0f, 1f)] public float defeatSlowScale = 0.2f;

    [Tooltip("撃破時のスロー時間（実時間・秒）")]
    public float defeatSlowDuration = 0.35f;

    [Header("シェイク連動（BossSniperShake があれば揺らす）")]
    [Tooltip("通常ダメージのシェイク強さ（殴られた方向の軸±に揺れる）")]
    public float normalShakeStrength = 0.12f;

    [Tooltip("通常ダメージのシェイク時間（秒）")]
    public float normalShakeDuration = 0.08f;

    [Tooltip("スタン一撃のシェイク強さ（全方向ジッター）")]
    public float stunShakeStrength = 0.25f;

    [Tooltip("スタン一撃のシェイク時間（秒）")]
    public float stunShakeDuration = 0.18f;

    [Tooltip("撃破のシェイク強さ（全方向ジッター）")]
    public float defeatShakeStrength = 0.4f;

    [Tooltip("撃破のシェイク時間（秒）")]
    public float defeatShakeDuration = 0.4f;

    private BossSniper boss;
    private BossSniperShake shake;
    private Coroutine freezeRoutine;
    private Coroutine slowRoutine;
    private float baseFixedDeltaTime;

    void Awake()
    {
        boss = GetComponent<BossSniper>();
        if (boss == null) boss = GetComponentInParent<BossSniper>();
        shake = GetComponent<BossSniperShake>();
        baseFixedDeltaTime = Time.fixedDeltaTime;
    }

    /// <summary>
    /// 通常ダメージ用：ボスだけフリーズ ＋ 殴られた方向の軸±シェイク。
    /// hitDir はプレイヤー→ボスの方向（BossSniperBeamUnit.LastHitDirection）。
    /// </summary>
    public void PlayNormal(Vector2 hitDir)
    {
        FreezeBossOnly(normalFreezeDuration);
        if (shake != null) shake.ShakeAxis(hitDir, normalShakeStrength, normalShakeDuration);
    }

    /// <summary>スタン倍率一撃用：全体スロー ＋ 全方向シェイク。</summary>
    public void PlayStun()
    {
        SlowAll(stunSlowScale, stunSlowDuration);
        if (shake != null) shake.ShakeRandom(stunShakeStrength, stunShakeDuration);
    }

    /// <summary>撃破用：全体スロー（長め） ＋ 全方向シェイク（強め）。</summary>
    public void PlayDefeat()
    {
        SlowAll(defeatSlowScale, defeatSlowDuration);
        if (shake != null) shake.ShakeRandom(defeatShakeStrength, defeatShakeDuration);
    }

    /// <summary>ボスの内部更新だけを一定時間止める（プレイヤー・UIは通常速度）。</summary>
    public void FreezeBossOnly(float duration)
    {
        if (duration <= 0f || boss == null) return;
        if (freezeRoutine != null) StopCoroutine(freezeRoutine);
        freezeRoutine = StartCoroutine(FreezeRoutine(duration));
    }

    private IEnumerator FreezeRoutine(float duration)
    {
        boss.HitStopActive = true;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime; // 全体スローと併発しても実時間で確実に明ける
            yield return null;
        }
        boss.HitStopActive = false;
        freezeRoutine = null;
    }

    /// <summary>Time.timeScale を落として一定時間後に通常へ戻す（全体スロー）。</summary>
    public void SlowAll(float scale, float duration)
    {
        if (duration <= 0f) return;
        if (slowRoutine != null) StopCoroutine(slowRoutine);
        slowRoutine = StartCoroutine(SlowRoutine(Mathf.Clamp01(scale), duration));
    }

    private IEnumerator SlowRoutine(float scale, float duration)
    {
        Time.timeScale = scale;
        // 物理の粒度もスケールに合わせると、スロー中の物理挙動が滑らかになる
        Time.fixedDeltaTime = baseFixedDeltaTime * scale;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixedDeltaTime;
        slowRoutine = null;
    }

    void OnDisable()
    {
        // 途中でオブジェクトが無効化されても timeScale を戻し忘れないよう保険
        if (slowRoutine != null)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = baseFixedDeltaTime;
            slowRoutine = null;
        }
        if (freezeRoutine != null && boss != null)
        {
            boss.HitStopActive = false;
            freezeRoutine = null;
        }
    }
}