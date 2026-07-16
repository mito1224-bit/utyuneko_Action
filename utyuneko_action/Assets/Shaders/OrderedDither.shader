Shader "Hidden/PostFX/OrderedDither"
{
    Properties
    {
        _ColorSteps      ("Color Levels (per channel)", Range(2, 32)) = 6
        _DitherStrength  ("Dither Amount", Range(0, 1)) = 1
        _PixelScale      ("Dither Pixel Size", Range(1, 8)) = 1
        [Toggle(_USE_4X4)] _Use4x4       ("Use 4x4 matrix (off = 8x8)", Float) = 0
        [Toggle(_MONO)]    _Monochrome   ("Dither on luminance only", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        Pass
        {
            Name "OrderedDither"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _USE_4X4
            #pragma shader_feature_local _MONO

            // Core.hlsl pulls in the fullscreen-triangle helpers + texture macros.
            // We declare _BlitTexture ourselves so we don't depend on Blit.hlsl,
            // whose path varies between URP versions.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_BlitTexture);
            float4 _BlitScaleBias;
            // sampler_LinearClamp is already declared upstream by Core.hlsl, so we just use it.

            CBUFFER_START(UnityPerMaterial)
                float _ColorSteps;
                float _DitherStrength;
                float _PixelScale;
            CBUFFER_END

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord   : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                float2 uv = GetFullScreenTriangleTexCoord(input.vertexID);
                output.texcoord = uv * _BlitScaleBias.xy + _BlitScaleBias.zw;
                return output;
            }

            // --- Bayer threshold matrices (values 0..N-1) ---
            static const float Bayer4[16] =
            {
                 0.0,  8.0,  2.0, 10.0,
                12.0,  4.0, 14.0,  6.0,
                 3.0, 11.0,  1.0,  9.0,
                15.0,  7.0, 13.0,  5.0
            };

            static const float Bayer8[64] =
            {
                 0.0, 32.0,  8.0, 40.0,  2.0, 34.0, 10.0, 42.0,
                48.0, 16.0, 56.0, 24.0, 50.0, 18.0, 58.0, 26.0,
                12.0, 44.0,  4.0, 36.0, 14.0, 46.0,  6.0, 38.0,
                60.0, 28.0, 52.0, 20.0, 62.0, 30.0, 54.0, 22.0,
                 3.0, 35.0, 11.0, 43.0,  1.0, 33.0,  9.0, 41.0,
                51.0, 19.0, 59.0, 27.0, 49.0, 17.0, 57.0, 25.0,
                15.0, 47.0,  7.0, 39.0, 13.0, 45.0,  5.0, 37.0,
                63.0, 31.0, 55.0, 23.0, 61.0, 29.0, 53.0, 21.0
            };

            // Returns an ordered-dither threshold in [0,1) for a given pixel.
            float GetThreshold(float2 pixel)
            {
                #ifdef _USE_4X4
                    int x = (int)fmod(pixel.x, 4.0);
                    int y = (int)fmod(pixel.y, 4.0);
                    return (Bayer4[y * 4 + x] + 0.5) / 16.0;
                #else
                    int x = (int)fmod(pixel.x, 8.0);
                    int y = (int)fmod(pixel.y, 8.0);
                    return (Bayer8[y * 8 + x] + 0.5) / 64.0;
                #endif
            }

            // Quantize to L levels, using the ordered-dither threshold to decide
            // whether each pixel's fractional part rounds up or down.
            float3 OrderedQuantize(float3 c, float threshold, float levels)
            {
                float L = max(levels, 2.0) - 1.0;
                float3 scaled = c * L;
                float3 lo     = floor(scaled);
                float3 fracp  = scaled - lo;
                float3 idx    = lo + step(threshold, fracp); // frac >= threshold -> +1
                return idx / L;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 src = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                // Effect works in LDR; clamp so HDR bloom highlights don't break the math.
                float3 col = saturate(src.rgb);

                // Pixel coordinate of the dither cell (PixelScale makes the dots chunkier).
                float2 pixel = floor(input.texcoord * _ScreenParams.xy / max(_PixelScale, 1.0));
                float threshold = GetThreshold(pixel);

                #ifdef _MONO
                    // Dither on luminance -> clean monochrome dot pattern, keeps hue.
                    float lum   = dot(col, float3(0.299, 0.587, 0.114));
                    float qlum  = OrderedQuantize(lum.xxx, threshold, _ColorSteps).x;
                    float3 dith = col * (qlum / max(lum, 1e-4));
                #else
                    // Per-channel -> slight color noise, more "digital signal" feel.
                    float3 dith = OrderedQuantize(col, threshold, _ColorSteps);
                #endif

                float3 outCol = lerp(col, dith, saturate(_DitherStrength));
                return half4(outCol, src.a);
            }
            ENDHLSL
        }
    }
}
