Shader "Custom/SpriteGlitchGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0

        [Header(Glitch Settings)]
        _Speed ("Glitch Speed", Float) = 20.0
        _Frequency ("Glitch Frequency", Range(0, 1)) = 0.4
        _Intensity ("Glitch Intensity", Range(0, 1.0)) = 0.2 
        _RGBDistortion ("RGB Split", Range(0, 1.0)) = 0.2 

        [Header(Glow Settings)]
        [HDR] _GlowColor ("Base Glow Color", Color) = (1,1,1,1)
        _GlitchGlowBoost ("Glitch Glow Boost", Float) = 2.0
    }

    SubShader
    {
        // ★ SpriteRenderer 用のタグ設定
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha // 通常のアルファブレンド

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            
            float _Speed;
            float _Frequency;
            float _Intensity;
            float _RGBDistortion;

            float4 _GlowColor;
            float _GlitchGlowBoost;

            float rand(float2 co) {
                return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                // SpriteRendererのColorコンポーネントを適用
                o.color = v.color * _Color;
                
                #ifdef PIXELSNAP_ON
                o.vertex = UnityPixelSnap (o.vertex);
                #endif
                
                return o;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
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

                // --- 発光制御 ---
                float4 hdrGlowColor = _GlowColor;
                float finalIntensity = 1.0 + (glitchTrigger * glitchNoise * _GlitchGlowBoost);
                
                // IN.color (SpriteRendererのColor) とHDRカラーを乗算
                color *= IN.color * hdrGlowColor * finalIntensity;

                return color;
            }
        ENDCG
        }
    }
}