Shader "Custom/Effects/GlowBeamSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _CoreColor ("Core Color (Center)", Color) = (1, 1, 1, 1)
        [HDR] _BeamColor ("Beam Color (Outer)", Color) = (0, 0.8, 1, 1)
        
        _BeamSpeed ("Beam Speed", Float) = 8.0
        _NoiseScaleX ("Noise Scale X", Float) = 15.0
        _NoiseScaleY ("Noise Scale Y", Float) = 4.0
        _NoiseStrength ("Noise Distortion", Range(0, 1)) = 0.3
        
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
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
        Blend One One // 加算合成（より派手に発光させるため）

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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
            fixed4 _CoreColor;
            fixed4 _BeamColor;
            float _BeamSpeed;
            float _NoiseScaleX;
            float _NoiseScaleY;
            float _NoiseStrength;

            // 簡易的な疑似乱数ノイズ関数
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            // グラデーションノイズ
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(hash(i + float2(0.0,0.0)), hash(i + float2(1.0,0.0)), u.x),
                            lerp(hash(i + float2(0.0,1.0)), hash(i + float2(1.0,1.0)), u.x), u.y);
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                // 1. 時間経過で横方向にUVをスクロールさせる
                float timeOffset = _Time.y * _BeamSpeed;
                float2 noiseUV = uv * float2(_NoiseScaleX, _NoiseScaleY);
                noiseUV.x -= timeOffset;

                // 2. ノイズの生成
                float n = noise(noiseUV);

                // 3. ビームの形状（上下の端をフェードアウト、中央を太く）
                // uv.y が 0.5 のとき（中央）が最も濃くなるように計算
                float beamShape = 1.0 - abs(uv.y - 0.5) * 2.0;
                
                // ノイズでビームの輪郭をうねらせる
                beamShape += (n - 0.5) * _NoiseStrength;
                beamShape = clamp(beamShape, 0.0, 1.0);

                // 4. 「芯（コア）」と「外側の光（オーラ）」をブレンド
                float core = pow(beamShape, 8.0);   // 鋭い中央の白い芯
                float glow = pow(beamShape, 2.0);   // 広がる太いビーム光

                // 5. HDRカラーの掛け合わせと合成
                fixed4 finalColor = (_CoreColor * core) + (_BeamColor * glow);
                
                // スプライトの元のテクスチャ（アルファチャンネル等）の考慮
                fixed4 tex = tex2D(_MainTex, uv);
                
                return finalColor * IN.color * tex.a;
            }
            ENDCG
        }
    }
}