using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 「点滅（表示ON/OFF）」の代わりに「半透明の明滅（アルファのフェード）」を行う再利用ヘルパー。
/// 敵の死亡吹き飛び・スタン・自爆カウントダウンなどの明滅演出で共用する。
///
/// なぜマテリアルのアルファか:
///   - `renderer.enabled` / `forceRenderingOff` の切り替えは「完全に消える」ハードな点滅になる。
///   - アルファなら半透明にフェードでき、柔らかい明滅になる。
///   - アルファは Animator の `m_Enabled` 上書きの影響を受けない（表示切替と別チャンネル）。
///
/// 不透明マテリアルはアルファを下げても見た目が変わらないため、Begin() で各レンダラーの
/// マテリアルを複製し、透明ブレンド対応へ切り替えてから明滅させる。End() で元へ戻す。
/// 複製は renderer.materials（レンダラー所有＝GameObject 破棄時に自動解放）で作るのでリークしない。
/// 視線ライン（LineRenderer）・軌跡（TrailRenderer）は対象外。
/// </summary>
public class BlinkFade
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private readonly List<Renderer> renderers = new List<Renderer>();
    private Material[][] instancedMats;   // 透明化した複製マテリアル（レンダラーごと）
    private Material[][] originalMats;     // 元の共有マテリアル（End で戻す）
    private Color[][] baseColors;          // 元の色（アルファだけ差し替えるため保持）
    private bool active;

    public BlinkFade(IEnumerable<Renderer> targets)
    {
        foreach (var r in targets)
        {
            if (r == null || r is LineRenderer || r is TrailRenderer) continue;
            renderers.Add(r);
        }
    }

    /// <summary>透明モードへ切り替えて明滅を開始する。</summary>
    public void Begin()
    {
        if (active || renderers.Count == 0) return;
        active = true;

        int n = renderers.Count;
        instancedMats = new Material[n][];
        originalMats = new Material[n][];
        baseColors = new Color[n][];

        for (int i = 0; i < n; i++)
        {
            Renderer r = renderers[i];
            originalMats[i] = r.sharedMaterials;

            Material[] mats = r.materials; // 複製（レンダラー所有）
            var cols = new Color[mats.Length];
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] == null) continue;
                MakeTransparent(mats[m]);
                cols[m] = GetBaseColor(mats[m]);
            }
            instancedMats[i] = mats;
            baseColors[i] = cols;
        }
    }

    /// <summary>見た目のアルファを一括設定（0=透明 / 1=不透明）。Begin 済みのときのみ有効。</summary>
    public void SetAlpha(float alpha)
    {
        if (!active) return;
        alpha = Mathf.Clamp01(alpha);

        for (int i = 0; i < instancedMats.Length; i++)
        {
            Material[] mats = instancedMats[i];
            Color[] cols = baseColors[i];
            if (mats == null) continue;

            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] == null) continue;
                Color c = cols[m];
                c.a = alpha;
                SetBaseColor(mats[m], c);
            }
        }
    }

    /// <summary>元のマテリアルへ戻す（スタン明けなど。死亡消滅時は呼ばなくてよい）。</summary>
    public void End()
    {
        if (!active) return;
        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] != null && originalMats[i] != null)
                renderers[i].sharedMaterials = originalMats[i];

            // 差し替え用に複製したマテリアルを破棄（リーク防止）
            if (instancedMats[i] != null)
            {
                foreach (var m in instancedMats[i])
                    if (m != null) Object.Destroy(m);
            }
        }
        active = false;
    }

    public bool IsActive => active;

    // ─── マテリアルの透明化・色の取得/設定（Standard / URP Lit / Sprites 等に対応） ───

    private static void MakeTransparent(Material m)
    {
        // URP Lit / Simple Lit（_Surface を持つ）
        if (m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface", 1f); // 0=Opaque, 1=Transparent
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f); // Alpha
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return;
        }

        // Built-in Standard（_Mode を持つ）
        if (m.HasProperty("_Mode"))
        {
            m.SetFloat("_Mode", 2f); // 2=Fade
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return;
        }

        // Sprites/Default など、もともと半透明対応のシェーダー。念のため描画順だけ透明へ
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private static Color GetBaseColor(Material m)
    {
        if (m.HasProperty(BaseColorId)) return m.GetColor(BaseColorId); // URP
        if (m.HasProperty(ColorId)) return m.GetColor(ColorId);         // Built-in / Sprites
        return Color.white;
    }

    private static void SetBaseColor(Material m, Color c)
    {
        if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
        if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
    }
}
