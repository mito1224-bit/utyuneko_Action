using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// ボススナイパーの被弾時「白フラッシュ」演出（独立コンポーネント）。
///
/// 使い方:
///   - ボス本体（BossSniper と同じ GameObject 推奨）に付ける。
///   - BossSniperHealth の onDamaged(float) イベントに、この Flash() をインスペクターから配線する。
///     （onDamaged は本物にダメージが入ったときだけ発火するので、自動的に「本物だけ光る」。
///       偽物はダメージ処理を通らないのでフラッシュしない。）
///
/// 仕組み（別ボスの DamageFlashRoutine を、バリア/ウルト等の固有依存を外して単純化したもの）:
///   - 対象のレンダラー（SpriteRenderer / SkinnedMeshRenderer / MeshRenderer の3種）を
///     一時的に白マテリアルへ差し替え、flashDuration 後に元へ戻す。
///   - 元マテリアルは初回に記憶して確実に復元する。
///
/// 通常ダメージとスタン倍率一撃で演出差は付けない（一律）。
/// </summary>
public class BossSniperFlash : MonoBehaviour
{
    [Header("対象")]
    [Tooltip("フラッシュさせる見た目のルート。未指定なら BossSniperBeamUnit.visualTransform、無ければ自分の Transform 配下")]
    public Transform visualRoot;

    [Header("演出")]
    [Tooltip("白フラッシュの表示時間（秒）")]
    public float flashDuration = 0.08f;

    [Tooltip("差し替える白マテリアル。未指定なら実行時に白マテリアルを自動生成する")]
    public Material customFlashMaterial;

    private Material defaultFlashMaterial;
    private bool isFlashing;
    private Coroutine flashCoroutine;

    // 差し替えたレンダラーと、その元マテリアルの記憶
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
        // 対象ルートの解決：明示指定 → BeamUnit.visualTransform → 自分
        if (visualRoot == null)
        {
            BossSniperBeamUnit unit = GetComponent<BossSniperBeamUnit>();
            if (unit == null) unit = GetComponentInParent<BossSniperBeamUnit>();
            if (unit != null && unit.visualTransform != null) visualRoot = unit.visualTransform;
        }
        if (visualRoot == null) visualRoot = transform;
    }

    void Start()
    {
        Shader guiText = Shader.Find("GUI/Text Shader");
        defaultFlashMaterial = guiText != null
            ? new Material(guiText) { color = Color.white }
            : new Material(Shader.Find("Sprites/Default")) { color = Color.white };
    }

    /// <summary>白フラッシュを1回再生する。BossSniperHealth.onDamaged に配線して使う。</summary>
    public void Flash()
    {
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    /// <summary>onDamaged(float) に直接配線できるよう、引数付きのオーバーロードも用意（値は無視）。</summary>
    public void Flash(float _)
    {
        Flash();
    }

    private IEnumerator FlashRoutine()
    {
        Material matToUse = customFlashMaterial != null ? customFlashMaterial : defaultFlashMaterial;

        // 初回のフラッシュで対象レンダラーと元マテリアルを記憶（3種すべて拾う）
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

        // 元へ復元
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
        // 自動生成した白マテリアルを破棄（customFlashMaterial は外部管理なので触らない）
        if (defaultFlashMaterial != null) Destroy(defaultFlashMaterial);
    }
}