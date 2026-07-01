Shader "FullScreen/SquareStepTransition"
{
    // URP の「Full Screen Pass Renderer Feature」に割り当てて使うことを前提にしたシェーダー。
    // Canvas / UI Graphic は使わず、カメラの描画結果(_BlitTexture)に直接エフェクトを合成する。
    //
    // 画面の四辺から角ばった四角い枠が段階的(階段状)に中央へ迫っていき、
    // _Progress = 1 で画面全体が完全に単色(_ShapeColor)で塗りつぶされる。
    // アニメーション中は、そのとき一番新しく現れているリングだけ _InnerColor になる。

    Properties
    {
        _ShapeColor ("Frame/Fill Color (外側の枠)", Color) = (0.85, 0.1, 0.55, 1)
        _InnerColor ("Innermost Frame Color (現在の先端リング)", Color) = (1.0, 0.35, 0.7, 1)

        _Progress ("Progress (0=未着手 / 1=完全被覆)", Range(0,1)) = 0

        _StepCount ("Step Count (段階数)", Range(1,16)) = 4
        _EdgeSoftness ("Edge Softness (AA用、小さめ推奨)", Range(0.0, 0.05)) = 0.006

        [Toggle] _PreserveSquareAspect ("Keep True Square (画面比率を無視して正方形にする)", Float) = 0

        _PivotX ("Pivot X (0-1)", Range(0,1)) = 0.5
        _PivotY ("Pivot Y (0-1)", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "SquareStepTransitionFullScreen"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            // Full Screen Pass Renderer Feature は内部で Blit.hlsl の Vert/Attributes/Varyings
            // (および _BlitTexture / sampler_LinearClamp / _BlitScaleBias) を使ってフルスクリーン
            // 三角形を描画するため、こちらを include するのが正しい(Shader Graph専用の
            // "Shaders/Utils/Fullscreen.hlsl" は直接 include できないパスなので使わない)。
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShapeColor;
                float4 _InnerColor;
                float _Progress;
                float _StepCount;
                float _EdgeSoftness;
                float _PreserveSquareAspect;
                float _PivotX;
                float _PivotY;
            CBUFFER_END

            // チェビシェフ距離(正方形の等距離線 = 角ばった四角形になる)。
            float squareDistance(float2 p)
            {
                return max(abs(p.x), abs(p.y));
            }

            // 0..1 の t を StepCount 段階の「階段状」の値に変換する。
            float quantize(float t, float steps)
            {
                steps = max(steps, 1.0);
                return floor(saturate(t) * steps + 1e-5) / steps;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                float4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // --- 中心(ピボット)基準の -1..1 座標 ---
                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1e-5); // width/height
                float2 pivot = float2(_PivotX, _PivotY);
                float2 p = (uv - pivot) * 2.0;

                // デフォルトは画面のUV比率そのまま(四辺が同時に中央へ到達する長方形の枠)。
                if (_PreserveSquareAspect > 0.5)
                {
                    if (aspect >= 1.0) p.x *= aspect;
                    else p.y /= aspect;
                }

                float d = squareDistance(p); // 0(中心) 〜 1(画面端)

                // --- 進行度: 0=完全に開いている 〜 1=完全に画面全体を覆う、の単調な動き ---
                float t = saturate(_Progress);
                float steppedPhase = quantize(t, _StepCount);
                float threshold = 1.0 - steppedPhase;

                // d >= threshold の領域を塗りつぶす。t=1 で threshold=0 になり画面全体を確実に覆う。
                float mask = smoothstep(threshold - _EdgeSoftness, threshold + _EdgeSoftness, d);

                // --- 「一番内側」= そのときアニメーションで一番新しく出現しているリング ---
                float bandWidth = 1.0 / max(_StepCount, 1.0);
                float newestBandOuterEdge = threshold + bandWidth;
                float toOlder = smoothstep(newestBandOuterEdge - _EdgeSoftness, newestBandOuterEdge + _EdgeSoftness, d);
                float4 dynamicFill = lerp(_InnerColor, _ShapeColor, toOlder);

                // _Progress = 1(完全被覆)のときは内外の区別をやめて単色にする。
                float fullyCovered = step(0.999, steppedPhase);
                float4 fillColor = lerp(dynamicFill, _ShapeColor, fullyCovered);

                float4 col = lerp(sceneColor, fillColor, mask);
                col.a = 1.0;
                return col;
            }
            ENDHLSL
        }
    }
}
