Shader "FullScreen/DamageGlitch"
{
    Properties
    {
        _Intensity          ("Intensity",          Float) = 0    // 横ズレの強さ。大きいほど激しくズレる
        _BlockScale         ("Block Scale",        Float) = 8    // ブロックの分割数。大きいほど細かいブロックになる
        _Speed              ("Speed",              Float) = 10   // ブロックの切り替わり速度。大きいほど速くスナップする
        _ChromaticIntensity ("Chromatic Intensity",Float) = 0    // RGBにじみの最大強さ
        _ChromaticSpeed     ("Chromatic Speed",    Float) = 8    // RGBずれの切り替わり速度。大きいほど速くスナップする
        _DiagonalAmount     ("Diagonal Amount",    Float) = 0.3  // 斜めズレの割合。0で横のみ、1で45度の斜めになる
        _WarpStrength       ("Warp Strength",      Float) = 0.5  // ブロックサイズの不規則さ。大きいほどバラバラになる
        _ThresholdSpeed     ("Threshold Speed",    Float) = 0.2  // ズレる割合の変動速度
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        Cull Off
        ZTest Always
        Blend Off

        Pass
        {
            Name "DamageGlitchPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Intensity;
            float _BlockScale;
            float _Speed;
            float _ChromaticIntensity;
            float _ChromaticSpeed;
            float _DiagonalAmount;
            float _WarpStrength;
            float _ThresholdSpeed;

            // 1D ハッシュ：1つの入力から 0〜1 のランダムな値を返す
            float hash(float n)
            {
                return frac(sin(n) * 43758.5453123);
            }

            // 2D ハッシュ：2次元の座標から 0〜1 のランダムな値を返す
            float hash2(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // ---- スナップした時間 ----
                // floor で時間を整数化して、連続的な動きをスナップに変換
                float timeStep = floor(_Time.y * _Speed);

                // ---- ブロックサイズの不規則化 ----
                // 細かいグリッドで UV.y を歪め、ブロックの境界を不規則にずらす
                float fineRow = floor(uv.y * _BlockScale * 3.0);
                // fineRow ごとにランダムな歪み量を生成（-0.5〜0.5）
                float warp = (hash(fineRow) - 0.5) * _WarpStrength;
                // UV.y に歪みを加える
                float warpedY = uv.y + warp * (1.0 / _BlockScale);
                // 歪んだ UV.y でブロック行を決定 → 高さがバラバラになる
                float blockRow = floor(warpedY * _BlockScale);

                // ---- ブロックごとのランダム値 ----
                float2 blockCoord = float2(blockRow * 0.1, timeStep * 0.17);
                float noise1 = hash2(blockCoord);                     // ズレるかどうか判定用
                float noise2 = hash2(blockCoord + float2(5.3, 9.1)); // ズレ量用

                // ---- ズレる割合の不規則化 ----
                float thresholdTime = floor(_Time.y * _ThresholdSpeed);
                // 0.5に近い→たくさんズレる / 0.9に近い→ほとんどズレない
                float threshold = lerp(0.5, 0.9, hash(thresholdTime * 0.37));
                float doGlitch = step(threshold, noise1);

                // ---- ブロックオフセット計算 ----
                // noise2 を -0.5〜0.5 にシフトして左右どちらにもズレるようにする
                float offsetX = (noise2 - 0.5) * doGlitch * _Intensity;
                // 斜め方向のズレ
                float offsetY = offsetX * _DiagonalAmount;
                float2 glitchedUV = uv + float2(offsetX, offsetY);

                // ---- RGBずれのスナップ ----
                // 方向（chromaRand）も大きさ（chromaMagnitude）も両方スナップ
                // → _ChromaticIntensity の連続変化に引きずられずに鋭く切り替わる
                float chromaTimeStep  = floor(_Time.y * _ChromaticSpeed);
                float chromaRand      = (hash(chromaTimeStep * 0.573) - 0.5) * 2.0; // -1〜1 の方向
                float chromaMagnitude = hash(chromaTimeStep * 0.931);                // 0〜1 の大きさ
                float chromaOffset    = chromaRand * chromaMagnitude * _ChromaticIntensity;

                // ---- RGBチャンネル別サンプリング（色収差） ----
                float2 rUV = glitchedUV + float2( chromaOffset, 0);
                float2 gUV = glitchedUV;
                float2 bUV = glitchedUV + float2(-chromaOffset, 0);

                half4 colR = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, rUV);
                half4 colG = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, gUV);
                half4 colB = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, bUV);

                return half4(colR.r, colG.g, colB.b, 1.0);
            }
            ENDHLSL
        }
    }
}