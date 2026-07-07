Shader "Custom/URPMistyGlowSprite_Thick"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _BaseColor ("Base Color (Tint)", Color) = (0, 0.8, 1, 1)
        
        [Header(Outer Edge Glow Settings)]
        _EdgeWidth ("Edge Width (フチの太さ)", Range(0.0, 50.0)) = 15.0 // 最大値を50に拡張
        _EdgeIntensity ("Edge Glow Intensity (フチの光量)", Range(0.0, 10.0)) = 4.0
        
        [Header(Inner Misty Settings)]
        _MistySpeed ("Misty Speed (もわもわの速さ)", Float) = 2.0
        _MistyScale ("Misty Scale (もわもわの細かさ)", Float) = 12.0
        _MistyIntensity ("Misty Alpha (内側の不透明度)", Range(0.0, 1.0)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "MistyGlowPass"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            float4 _BaseColor;
            float _EdgeWidth;
            float _EdgeIntensity;
            float _MistySpeed;
            float _MistyScale;
            float _MistyIntensity;

            float noise(float2 uv)
            {
                return frac(sin(dot(uv, float2(127.1, 311.7))) * 43758.5453123);
            }

            float valueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);

                float r0 = noise(i);
                float r1 = noise(i + float2(1.0, 0.0));
                float r2 = noise(i + float2(0.0, 1.0));
                float r3 = noise(i + float2(1.0, 1.0));

                return lerp(lerp(r0, r1, f.x), lerp(r2, r3, f.x), f.y);
            }

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            float4 frag (Varyings input) : SV_Target
            {
                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                // --- 1. 【改良】多重サンプリングによる極太フチ検出 ---
                float totalAlpha = 0.0;

                // 4段階の距離に分けて、それぞれ8方向（計32箇所）をサンプリング
                // これにより、フチが太くなっても隙間ができず、滑らかなグラデーションになります
                for (int step = 1; step <= 4; step++)
                {
                    // 段階的に外側へ広げる (全体の25%, 50%, 75%, 100% の距離)
                    float2 offset = _MainTex_TexelSize.xy * _EdgeWidth * (step * 0.25);
                    
                    // 上下左右の4方向
                    totalAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(offset.x, 0)).a;
                    totalAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(offset.x, 0)).a;
                    totalAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, offset.y)).a;
                    totalAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(0, offset.y)).a;
                    
                    // 斜め4方向 (0.7071 は斜めの距離を整えるための値)
                    float2 diagOffset = offset * 0.7071;
                    totalAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + diagOffset).a;
                    totalAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - diagOffset).a;
                    totalAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(diagOffset.x, -diagOffset.y)).a;
                    totalAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(-diagOffset.x, diagOffset.y)).a;
                }
                totalAlpha /= 32.0; // 32回分の平均を取る

                // 外側のフチだけを抽出
                float outerEdge = saturate(totalAlpha - texColor.a);

                // --- 2. 内側のもわもわ（霧）の計算 ---
                float2 move1 = float2(_Time.y * _MistySpeed * 0.3, _Time.y * _MistySpeed * 0.2);
                float2 move2 = float2(-_Time.y * _MistySpeed * 0.2, _Time.y * _MistySpeed * 0.4);
                
                float n1 = valueNoise(input.uv * _MistyScale + move1);
                float n2 = valueNoise(input.uv * (_MistyScale * 1.5) + move2);
                float mistyGlow = saturate(n1 * n2 * 1.5);

                // --- 3. カラーと透明度の結合 ---
                float4 finalColor = float4(0, 0, 0, 0);

                // フチ部分
                float3 edgeColor = _BaseColor.rgb * _EdgeIntensity * outerEdge;
                float edgeAlpha = outerEdge;

                // 内側部分
                float3 innerColor = _BaseColor.rgb * mistyGlow * texColor.rgb;
                float innerAlpha = texColor.a * _MistyIntensity * (0.3 + mistyGlow * 0.7);

                finalColor.rgb = edgeColor + innerColor;
                finalColor.a = max(edgeAlpha, innerAlpha);

                finalColor *= input.color;

                return finalColor;
            }
            ENDHLSL
        }
    }
}