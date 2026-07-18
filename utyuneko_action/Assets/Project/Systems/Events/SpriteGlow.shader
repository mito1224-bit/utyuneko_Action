Shader "Custom/SpriteGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _Color ("Glow Color (HDR)", Color) = (1,1,1,1)
        _GlowIntensity ("Glow Intensity", Range(1, 15)) = 2.0
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
        Blend One OneMinusSrcAlpha // スプライト標準のプレマルチプライドアルファブレンド

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
            fixed4 _Color;
            float _GlowIntensity;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 texColor = tex2D(_MainTex, IN.texcoord);
                
                // スプライトの色 ✕ マテリアルの色
                fixed4 finalColor = texColor * IN.color;

                // 👑【ここが光る魔法】アルファ値（透明度）はそのままに、RGB（光の色）だけに輝度倍率を掛け算する！
                finalColor.rgb *= _GlowIntensity;

                // プレマルチプライドアルファの処理
                finalColor.rgb *= finalColor.a;

                return finalColor;
            }
        ENDCG
        }
    }
}