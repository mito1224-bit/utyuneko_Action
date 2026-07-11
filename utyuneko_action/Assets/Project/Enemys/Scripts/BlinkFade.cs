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
/// ★シェーダー差し替え方式（2026/07/11）:
///   モデルが Opaque な Shader Graph（例: URP 同梱の ArnoldStandardSurface。FBX 取り込みで自動割当）だと、
///   Surface タイプと Blend/ZWrite がグラフに焼き込まれていて、実行時にマテリアルの float を差し替えても
///   透過へ切り替えられない。さらに色プロパティ名が `_BASE_COLOR`（大文字）で `_BaseColor`/`_Color` を
///   探すだけの旧方式ではアルファを書き込めず「何も起こらない」状態だった。
///   そこで Begin() では、透過に切り替えられないマテリアルだけ URP/Lit（透過設定）へシェーダーごと差し替え、
///   元のベースマップ・ベースカラーをコピーして見た目を保ったままアルファを脈動させる。
///   もともと透過が効くマテリアル（URP Lit / Standard / Sprites 等）はシェーダーを触らず従来どおり扱う。
///
/// 複製は renderer.materials（レンダラー所有＝GameObject 破棄時に自動解放）で作るのでリークしない。
/// シェーダーを差し替えても複製はレンダラー所有のままなので、End() を呼ばずに破棄されても解放される
/// （通常エネミーの死亡は End() を呼ばず Destroy されるため、この性質が必要）。
/// 視線ライン（LineRenderer）・軌跡（TrailRenderer）は対象外。
/// </summary>
public class BlinkFade
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ArnoldBaseColorId = Shader.PropertyToID("_BASE_COLOR");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int ArnoldBaseMapId = Shader.PropertyToID("_BASE_COLOR_MAP");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
    private static readonly int ArnoldEmissionColorId = Shader.PropertyToID("_EMISSION_COLOR");
    private static readonly int ArnoldEmissionMapId = Shader.PropertyToID("_EMISSION_COLOR_MAP");

    // 透過へ差し替えるときに使う URP/Lit シェーダー（1度だけ検索）。URP 前提。見つからなければ差し替えは行わない。
    private static Shader litShader;
    private static bool litShaderResolved;

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
            originalMats[i] = r.sharedMaterials;  // 復帰用に共有元を先に控える

            Material[] mats = r.materials; // 複製（レンダラー所有＝破棄時に自動解放）
            var cols = new Color[mats.Length];
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] == null) continue;
                cols[m] = PrepareForFade(mats[m]);
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

    // ─── マテリアルをフェード可能な状態へ整える ───

    /// <summary>
    /// 複製マテリアル 1 枚をアルファフェード可能な状態にする。戻り値はフェードの基準色（不透明時の色）。
    ///   - もともと透過を切り替えられる（URP Lit / Standard / Sprites 等）→ そのまま透過モードへ。
    ///   - 切り替えられない Opaque な Shader Graph（Arnold 等）→ URP/Lit（透過）へシェーダーごと差し替え、
    ///     元のベースマップ・ベースカラーをコピーして見た目を保つ。
    /// </summary>
    private static Color PrepareForFade(Material m)
    {
        Color baseCol = GetBaseColor(m); // 差し替え前に元色を控える（Arnold の _BASE_COLOR も拾う）

        if (!SupportsRuntimeAlpha(m))
        {
            Shader lit = ResolveLitShader();
            if (lit != null)
            {
                // シェーダー差し替え前に元の見た目要素を控える（差し替え後は Arnold プロパティが消えるため）
                Texture baseMap = GetBaseMap(m, out Vector2 mapScale, out Vector2 mapOffset);
                Color emisCol = GetEmissionColor(m);
                Texture emisMap = GetEmissionMap(m);

                m.shader = lit;                       // レンダラー所有のまま透過対応シェーダーへ差し替え

                if (baseMap != null)
                {
                    m.SetTexture(BaseMapId, baseMap);
                    m.SetTextureScale(BaseMapId, mapScale);
                    m.SetTextureOffset(BaseMapId, mapOffset);
                }

                // エミッション（自発光）を引き継ぐ。色が黒でなく、あるいは発光マップがある場合のみ有効化する。
                bool hasEmission = emisMap != null || emisCol.maxColorComponent > 0.0001f;
                if (hasEmission)
                {
                    if (emisMap != null) m.SetTexture(EmissionMapId, emisMap);
                    m.SetColor(EmissionColorId, emisCol);
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                // ベースカラーは下の SetBaseColor（初期は不透明）で反映する
            }
        }

        MakeTransparent(m);
        SetBaseColor(m, new Color(baseCol.r, baseCol.g, baseCol.b, 1f)); // 開始は不透明
        return baseCol;
    }

    // このマテリアルは実行時にアルファ透過を切り替えられるか（＝シェーダー差し替え不要か）
    private static bool SupportsRuntimeAlpha(Material m)
    {
        if (m.HasProperty("_Surface")) return true; // URP Lit / Simple Lit
        if (m.HasProperty("_Mode")) return true;    // Built-in Standard
        string n = m.shader != null ? m.shader.name : string.Empty;
        // もともと半透明前提のシェーダー（スプライト等）はそのままアルファが効く
        return n.Contains("Sprite") || n.Contains("UI/") || n.Contains("Unlit/Transparent");
    }

    private static Shader ResolveLitShader()
    {
        if (!litShaderResolved)
        {
            litShader = Shader.Find("Universal Render Pipeline/Lit");
            litShaderResolved = true;
        }
        return litShader;
    }

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
        if (m.HasProperty(BaseColorId)) return m.GetColor(BaseColorId);         // URP Lit
        if (m.HasProperty(ArnoldBaseColorId)) return m.GetColor(ArnoldBaseColorId); // Arnold Shader Graph
        if (m.HasProperty(ColorId)) return m.GetColor(ColorId);                 // Built-in / Sprites
        return Color.white;
    }

    private static void SetBaseColor(Material m, Color c)
    {
        if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
        if (m.HasProperty(ArnoldBaseColorId)) m.SetColor(ArnoldBaseColorId, c);
        if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
    }

    private static Texture GetBaseMap(Material m, out Vector2 scale, out Vector2 offset)
    {
        int id = -1;
        if (m.HasProperty(ArnoldBaseMapId)) id = ArnoldBaseMapId; // Arnold Shader Graph
        else if (m.HasProperty(BaseMapId)) id = BaseMapId;        // URP
        else if (m.HasProperty(MainTexId)) id = MainTexId;        // Built-in / Sprites

        if (id != -1)
        {
            scale = m.GetTextureScale(id);
            offset = m.GetTextureOffset(id);
            return m.GetTexture(id);
        }

        scale = Vector2.one;
        offset = Vector2.zero;
        return null;
    }

    private static Color GetEmissionColor(Material m)
    {
        if (m.HasProperty(ArnoldEmissionColorId)) return m.GetColor(ArnoldEmissionColorId); // Arnold Shader Graph
        if (m.HasProperty(EmissionColorId)) return m.GetColor(EmissionColorId);             // URP / Standard
        return Color.black;
    }

    private static Texture GetEmissionMap(Material m)
    {
        if (m.HasProperty(ArnoldEmissionMapId)) return m.GetTexture(ArnoldEmissionMapId); // Arnold Shader Graph
        if (m.HasProperty(EmissionMapId)) return m.GetTexture(EmissionMapId);             // URP / Standard
        return null;
    }
}
