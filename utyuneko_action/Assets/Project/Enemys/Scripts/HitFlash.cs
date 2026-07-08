using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 敵・ボス共通の被弾「白フラッシュ」演出（自己完結モジュール）。
///
/// 使い方:
///   - 任意の敵／ボスの GameObject にアタッチするだけ（EnemyShield などと同じドロップ運用）。
///   - 被弾時に Flash() を呼ぶ。EnemyHealth / BossChargerHealth はアタッチされていれば自動で叩く
///     （GetComponent で探すので、付いていなければ何も起きない＝完全に任意）。
///
/// 仕組み（BossSniperFlash / Boss2 の白フラッシュと同じ流儀を汎用化したもの）:
///   - visualRoot 配下の全レンダラー（SpriteRenderer / SkinnedMeshRenderer / MeshRenderer）を
///     一時的に白マテリアルへ差し替え、flashDuration 後に元マテリアルへ戻す。
///   - 元マテリアルは差し替え時に記録して確実に復帰。生成した白マテリアルは OnDestroy で破棄。
///
/// 連続被弾しても多重にならないよう、進行中のフラッシュは再スタートで上書きする。
/// </summary>
public class HitFlash : MonoBehaviour
{
    [Header("対象")]
    [Tooltip("フラッシュさせる見た目のルート（未指定なら自分。配下の全レンダラーが対象）")]
    public Transform visualRoot;

    [Header("演出")]
    [Tooltip("白フラッシュの表示時間（秒）")]
    public float flashDuration = 0.08f;

    [Tooltip("差し替える白マテリアル。未指定なら実行時に白マテリアルを自動生成する")]
    public Material customFlashMaterial;

    private Material defaultFlashMaterial;
    private bool isFlashing;
    private Coroutine flashCoroutine;

    // 差し替えたレンダラーと、その元マテリアルの記録
    private struct Entry
    {
        public SpriteRenderer sr;
        public SkinnedMeshRenderer smr;
        public MeshRenderer mr;
        public Material orig;
    }
    private readonly List<Entry> entries = new List<Entry>();

    void Awake()
    {
        if (visualRoot == null) visualRoot = transform;
    }

    void Start()
    {
        // GUI/Text Shader はテクスチャのアルファ形状を真っ白に塗る（白飛びに最適）。無ければ Sprites/Default で代用
        Shader guiText = Shader.Find("GUI/Text Shader");
        defaultFlashMaterial = guiText != null
            ? new Material(guiText) { color = Color.white }
            : new Material(Shader.Find("Sprites/Default")) { color = Color.white };
    }

    /// <summary>白フラッシュを1回再生する。被弾側（EnemyHealth 等）から呼ぶ。</summary>
    public void Flash()
    {
        if (!isActiveAndEnabled) return;
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    // UnityEvent（onDamaged(float) など）へインスペクターから直接バインドできるオーバーロード（値は無視）
    public void Flash(float _) => Flash();
    public void Flash(int _) => Flash();

    private IEnumerator FlashRoutine()
    {
        Material matToUse = customFlashMaterial != null ? customFlashMaterial : defaultFlashMaterial;

        // 今回のフラッシュで対象レンダラーと元マテリアルを記録（3種すべて拾う）
        if (!isFlashing)
        {
            isFlashing = true;
            entries.Clear();

            foreach (var sr in visualRoot.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr != null) entries.Add(new Entry { sr = sr, orig = sr.sharedMaterial });

            foreach (var smr in visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr != null) entries.Add(new Entry { smr = smr, orig = smr.sharedMaterial });

            foreach (var mr in visualRoot.GetComponentsInChildren<MeshRenderer>(true))
                if (mr != null) entries.Add(new Entry { mr = mr, orig = mr.sharedMaterial });
        }

        // 白へ差し替え
        if (matToUse != null)
        {
            foreach (var e in entries)
            {
                if (e.sr != null) e.sr.sharedMaterial = matToUse;
                if (e.smr != null) e.smr.sharedMaterial = matToUse;
                if (e.mr != null) e.mr.sharedMaterial = matToUse;
            }
        }

        yield return new WaitForSeconds(flashDuration);

        // 元へ復帰
        foreach (var e in entries)
        {
            if (e.sr != null) { e.sr.sharedMaterial = e.orig; e.sr.color = Color.white; }
            if (e.smr != null) e.smr.sharedMaterial = e.orig;
            if (e.mr != null) e.mr.sharedMaterial = e.orig;
        }

        isFlashing = false;
        flashCoroutine = null;
    }

    void OnDestroy()
    {
        // 自動生成した白マテリアルのみ破棄（customFlashMaterial は外部管理なので触らない）
        if (defaultFlashMaterial != null) Destroy(defaultFlashMaterial);
    }
}
