using UnityEngine;

/// <summary>
/// 👑 崩壊した補佐：衝撃波（ショックウェーブ）本体の挙動を管理する専用クラス。
/// コライダー（当たり判定）の半径、エフェクトのプレハブ、エフェクトのサイズ（Scale）、
/// および生存時間を、プレハブのインスペクターから個別に設定して一元管理します。
/// </summary>
public class GlitchHosaShockwave : MonoBehaviour
{
    [Header("📐 衝撃波（当たり判定）のサイズ調整")]
    [Tooltip("衝撃波の半径（コライダーのサイズに直接反映されます）")]
    public float radius = 2.5f;

    [Header("✨ エフェクト（演出ビジュアル）の設定")]
    [Tooltip("衝撃波の発生時に同時に生成（Instantiate）するエフェクト（パーティクル等）のプレハブ")]
    public GameObject effectPrefab;
    [Tooltip("生成したエフェクトのローカルスケール（見た目の大きさ）の倍率")]
    public float effectScaleMultiplier = 1.0f;

    [Header("⏰ 生存時間")]
    [Tooltip("この衝撃波オブジェクト（当たり判定）が生成されてから、自動で消滅（Destroy）するまでの時間（秒）")]
    public float lifeTime = 0.5f;

    void Start()
    {
        // ① 当たり判定（円形または四角形）のコライダーサイズを radius に完璧に同期
        if (TryGetComponent<CircleCollider2D>(out var circleCol))
        {
            circleCol.radius = radius;
        }
        else if (TryGetComponent<BoxCollider2D>(out var boxCol))
        {
            boxCol.size = new Vector2(radius * 2f, radius * 2f);
        }

        // ② インスペクターにエフェクトプレハブが登録されている場合、その場で生成してサイズを適用！
        if (effectPrefab != null)
        {
            // 衝撃波（当たり判定）と同じ座標・角度にエフェクトを生成
            GameObject effectInstance = Instantiate(effectPrefab, transform.position, Quaternion.identity);

            // 👑【超重要：自動クリーンアップ親子構造】
            // 生成したエフェクトを、当たり判定オブジェクト（自分自身）の「子オブジェクト」にします。
            // これにより、この親オブジェクトが寿命で Destroy された時に、エフェクトも巻き添えで100%自動消滅します！
            effectInstance.transform.SetParent(transform);
            effectInstance.transform.localPosition = Vector3.zero;
            effectInstance.transform.localRotation = Quaternion.identity;

            // 👑【サイズ同期】インスペクターで設定した「effectScaleMultiplier」の大きさへダイレクトにリサイズ！
            effectInstance.transform.localScale = Vector3.one * effectScaleMultiplier;
        }

        // ③ 設定された時間（lifeTime）が経過したら、自身（および子オブジェクトのエフェクト）をメモリから自動消去
        Destroy(gameObject, lifeTime);
    }
}