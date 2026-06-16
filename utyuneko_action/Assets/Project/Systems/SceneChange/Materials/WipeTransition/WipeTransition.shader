Shader "Transition/WipeTransition"
{
    Properties
    {
        _Progress    ("Progress",     Range(0, 1))     = 0
        _EdgeSoftness("Edge Softness", Range(0.001, 0.05)) = 0.01
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        ZWrite Off
        ZTest  Always
        Cull   Off
        Blend  Off

        Pass
        {
            Name "WipeTransitionPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            // _BlitTexture / sampler_BlitTexture / Varyings / Vert は Blit.hlsl で定義済み
            SAMPLER(sampler_BlitTexture);
            //half4 screenColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);


            float _Progress;
            float _EdgeSoftness;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // アスペクト比補正付きで中心からの距離を計算
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 diff  = (uv - 0.5) * float2(aspect, 1.0);
                float  dist  = length(diff);

                // Progress=0 → 円が最大（画面全体が見える）
                // Progress=1 → 円が消える（全面黒）
                float maxDist = length(float2(aspect * 0.5, 0.5)); // 画面端までの最大距離
                float radius  = (1.0 - _Progress) * maxDist;

                // 円内: 画面表示 / 円外: 黒（ソフトエッジ付き）
                float mask = smoothstep(radius + _EdgeSoftness, radius - _EdgeSoftness, dist);

                half4 screenColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);
                return lerp(half4(0, 0, 0, 1), screenColor, mask);
            }
            ENDHLSL
        }
    }
}