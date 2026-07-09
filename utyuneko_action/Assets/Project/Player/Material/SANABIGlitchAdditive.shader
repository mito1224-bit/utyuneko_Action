Shader "Custom/UIGlitchGlowCorrect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Glitch Settings)]
        _Speed ("Glitch Speed", Float) = 20.0
        _Frequency ("Glitch Frequency", Range(0, 1)) = 0.4
        _Intensity ("Glitch Intensity", Range(0, 1.0)) = 0.2 
        _RGBDistortion ("RGB Split", Range(0, 1.0)) = 0.2 

        [Header(Glow Settings)]
        // 【重要】[HDR]タグをつける
        [HDR] _GlowColor ("Base Glow Color", Color) = (1,1,1,1)
        // グリッチ発生時に上乗せする発光強度
        _GlitchGlowBoost ("Glitch Glow Boost", Float) = 2.0

        [Header(UI System Required)]
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; float4 worldPosition : TEXCOORD1; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; float4 worldPosition : TEXCOORD1; };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;

            float _Speed;
            float _Frequency;
            float _Intensity;
            float _RGBDistortion;
            
            // 追加の発光設定
            float4 _GlowColor;
            float _GlitchGlowBoost;

            float rand(float2 co) {
                return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
            }

            v2f vert(appdata_t v) {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f IN) : SV_Target {
                float time = floor(_Time.y * _Speed);
                float2 pixelPos = IN.texcoord * _ScreenParams.xy;
                float blockY = floor(pixelPos.y / 20.0);
                float glitchNoise = rand(float2(blockY, time));
                
                // グリッチ発生の判定 (0 or 1)
                float glitchTrigger = step(1.0 - _Frequency, rand(float2(time, 543.21)));
                
                // グリッチによるUVズレ
                float maxPixelOffset = 150.0; 
                float uvOffset = (glitchNoise - 0.5) * (_Intensity * maxPixelOffset / _ScreenParams.x) * glitchTrigger;

                float maxRGBDistortion = 30.0;
                float rgbDistortionUV = (_RGBDistortion * maxRGBDistortion / _ScreenParams.x) * glitchTrigger;

                float2 uvR = IN.texcoord + float2(uvOffset + rgbDistortionUV, 0.0);
                float2 uvG = IN.texcoord + float2(uvOffset, 0.0);
                float2 uvB = IN.texcoord + float2(uvOffset - rgbDistortionUV, 0.0);

                half4 colorR = tex2D(_MainTex, uvR);
                half4 colorG = tex2D(_MainTex, uvG);
                half4 colorB = tex2D(_MainTex, uvB);

                // RGBスプリット
                fixed4 color = fixed4(colorR.r, colorG.g, colorB.b, colorG.a);

                // --- ここからが発光制御 ---

                // 通常時のHDRカラーを適用 (Intensityは白飛びしない1.5程度にしておく)
                float4 hdrGlowColor = _GlowColor;
                
                // 【重要】グリッチが発生したブロック (glitchTrigger * glitchNoise) だけ、
                // Intensityを一時的に爆上げ（ブースト）する
                float finalIntensity = 1.0 + (glitchTrigger * glitchNoise * _GlitchGlowBoost);
                
                // 眩しくなりすぎないよう、ブーストを乗算
                color *= IN.color * hdrGlowColor * finalIntensity;

                // --- 発光制御ここまで ---

                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                return color;
            }
        ENDCG
        }
    }
}