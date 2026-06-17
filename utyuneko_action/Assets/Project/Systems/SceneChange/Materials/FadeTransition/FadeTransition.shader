Shader "Transition/FadeTransition"
{
    Properties
    {
        _Progress ("Progress",  Range(0, 1)) = 0
        _Exponent ("Exponent",  Range(0.1, 5.0)) = 1.0  // Å© í«â¡
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
            Name "FadeTransitionPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            SAMPLER(sampler_BlitTexture);

            float _Progress;
            float _Exponent;  // Å© í«â¡

            half4 Frag(Varyings input) : SV_Target
            {
                half4 screenColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, input.texcoord);

                float easedProgress = pow(_Progress, _Exponent);  // Å© ïœçX

                return lerp(screenColor, half4(0, 0, 0, 1), easedProgress);
            }
            ENDHLSL
        }
    }
}