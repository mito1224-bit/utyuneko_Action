Shader "Custom/SANABIGlitchAdditive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)

        [Header(Glitch Settings)]
        _Speed ("Glitch Speed", Float) = 25.0
        _Frequency ("Glitch Frequency", Range(0, 1)) = 0.3
        _Intensity ("Glitch Intensity", Range(0, 1.0)) = 0.1
        _RGBDistortion ("RGB Split", Range(0, 1.0)) = 0.1

        [Header(SANABI Style Scanline)]
        _ScanlineDensity ("Scanline Density", Float) = 300.0 // 縞々の細かさ
        _ScanlineBoost ("Scanline Glow Boost", Range(1.0, 5.0)) = 2.5 // 縞々の発光強さ
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off Lighting Off ZWrite Off 
        
        // 【ここが重要！】アルファ値（透明度）を考慮した加算ブレンド
        // 背景と綺麗に混ざり合いながらも、UIの「形」がある場所だけが鋭く発光します
        Blend SrcAlpha One 

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; float4 screenPos : TEXCOORD1; };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Speed;
            float _Frequency;
            float _Intensity;
            float _RGBDistortion;
            float _ScanlineDensity;
            float _ScanlineBoost;

            float rand(float2 co) {
                return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
            }

            v2f vert(appdata_t v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                o.screenPos = ComputeScreenPos(o.vertex); // 画面上の座標を取得
                return o;
            }

            fixed4 frag(v2f IN) : SV_Target {
                float time = floor(_Time.y * _Speed);
                
                // 画面の実際のピクセル位置を計算
                float2 pixelPos = IN.texcoord * _ScreenParams.xy;
                float blockY = floor(pixelPos.y / 15.0); // 15ピクセル幅のブロック
                
                // グリッチの発生判定
                float glitchTrigger = step(1.0 - _Frequency, rand(float2(time, 312.45)));
                float noise = rand(float2(blockY, time));
                
                // ピクセルベースのズレ幅計算
                float maxOffset = 120.0;
                float uvOffset = (noise - 0.5) * (_Intensity * maxOffset / _ScreenParams.x) * glitchTrigger;
                float rgbSplit = (_RGBDistortion * 25.0 / _ScreenParams.x) * glitchTrigger;

                // RGB個別サンプリング（色収差）
                float2 uvR = IN.texcoord + float2(uvOffset + rgbSplit, 0.0);
                float2 uvG = IN.texcoord + float2(uvOffset, 0.0);
                float2 uvB = IN.texcoord + float2(uvOffset - rgbSplit, 0.0);

                half4 colorR = tex2D(_MainTex, uvR);
                half4 colorG = tex2D(_MainTex, uvG);
                half4 colorB = tex2D(_MainTex, uvB);

                // RGBを結合してUIの基本色を乗算
                fixed4 color = fixed4(colorR.r, colorG.g, colorB.b, colorG.a);
                color *= IN.color;

                // 【SANABI風演出】画面連動型のスキャンライン（走査線）
                // 画面の絶対的なY座標を使って綺麗なボーダー柄を作る
                float scanline = sin(IN.screenPos.y / IN.screenPos.w * _ScanlineDensity) * 0.5 + 0.5;
                
                // スキャンラインの縞々に応じて、発光強度（Boost）を変化させる
                color.rgb *= lerp(1.0, _ScanlineBoost, scanline);
                
                // 加算ブレンド用に、自身のアルファ（透明度）を色情報に乗算する
                // これにより、文字やアイコンの形（中身）が削れずにクッキリ残ります
                color.rgb *= color.a;

                return color;
            }
        ENDCG
        }
    }
}