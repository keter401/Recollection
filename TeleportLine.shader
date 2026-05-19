Shader "Blocks/TeleportLine"
{
    Properties
    {
        [Header(Line)]
        [MainTexture] _MainTex ("Main Texture", 2D) = "white" {}
        _MoveSpeed ("Move Speed", float) = 1.0          // ラインのスクロール速度
        _Width     ("Width", float) = 1.0               // ラインの周期幅（UV空間）
        _Distance  ("Distance", Range(0.0, 1.0)) = 0.3  // 周期内でラインが表示される割合
        _LineColor ("Line Color", Color) = (1.0, 0.0, 0.0, 1.0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100


        // ====================================================================================
        // Pass 1: Line
        // ライティングなしでスクロールするラインパターンを描画する
        // UV.xを時間でスクロールさせ、fmodで周期的なラインを生成する
        // StencilバッファにRef=1を書き込みPlayer・Echo・Goalシェーダーと連動する
        // ====================================================================================
        Pass
        {
            Name "Line"
            Tags { "LightMode" = "UniversalForward" }

            // StencilバッファにRef=1を書き込む
            // Player.shaderのDrawStencilパスがこの値を検出してシルエット描画領域を決定する
            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

            // ====================================================================================
            // 入力構造体：CPUからGPUへ渡す頂点データ
            // ====================================================================================
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            // ====================================================================================
            // 出力構造体：頂点シェーダーからフラグメントシェーダーへ渡すデータ
            // ====================================================================================
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            // ====================================================================================
            // マテリアルプロパティ
            // ====================================================================================
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            float4 _MainTex_ST;
            float  _MoveSpeed;
            float  _Width;
            float  _Distance;
            float4 _LineColor;

            // ====================================================================================
            // 頂点シェーダー
            // オブジェクト空間 → クリップ空間へ変換し、UVを渡す
            // ====================================================================================
            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // オブジェクト空間 → クリップ空間へ変換
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;

                // MainTexのTiling/Offsetを適用したUVを渡す
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            // ====================================================================================
            // フラグメントシェーダー
            // UV.xを時間でスクロールさせ、fmodで周期的なラインパターンを生成する
            // _Distanceより大きいピクセルを破棄してラインを描画する
            // ====================================================================================
            float4 frag(Varyings IN) : SV_Target
            {
                // 時間 × 速度でUV.xをスクロールさせるオフセットを計算
                float offset = _Time.y * _MoveSpeed;

                // UV.xにオフセットを加えて_Width周期でラップし、ライン位置を計算
                // 例: _Width=1.0、_Distance=0.3 のとき、0〜0.3の範囲だけ表示される
                float lineValue = fmod(IN.uv.x + offset, _Width);

                // lineValueが_Distanceより大きいピクセルを破棄してラインを描画
                clip(_Distance - lineValue);

                return _LineColor;
            }
            ENDHLSL
        }
    }
}
