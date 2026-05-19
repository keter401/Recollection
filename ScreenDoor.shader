Shader "Blocks/ScreenDoor_SilhouetteCanvas"
{
    Properties
    {
        [Header(Shading)]
        [MainTexture] _MainTex ("MainTex", 2D) = "white" {}
        [MainColor] _MainColor ("Main Color", Color) = (1, 1, 1, 1)
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _BlockSpecularColor ("Specular Color", Color) = (0, 0, 0, 1)
        _SpecularTex ("Specular Map", 2D) = "white" {}
        _Roughness ("Specular Roughness", Float) = 0.2          // 粗さ（0=鏡面、1=拡散）
        _IOR ("Specular IOR", Float) = 1.5                      // 屈折率（空気=1.0、ガラス≒1.5）
        _BlockEmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionTex ("Emission Map", 2D) = "white" {}

        [Header(ScreenDoor)]
        _BayerTex ("BayerTex", 2D) = "black" {}                // ベイヤー行列テクスチャ（ディザリング閾値）
        _BlockSize ("BlockSize", Int) = 20                      // ディザドットの大きさ（ピクセル単位）
        _Radius ("Radius", Range(0.001, 100)) = 5               // ScreenDoorが始まるカメラからの距離
    }

    SubShader
    {
        // GeometryキューでURPパイプライン指定
        // Player.shaderはGeometry+1で描画されるため、このシェーダーが先に描画される
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha


        // ====================================================================================
        // Pass 1: ShadowCaster
        // このオブジェクトが他オブジェクトに落とす影を描画する
        // URPのデフォルトではTransparentキューのオブジェクトは影を落とさないため独自実装
        // ====================================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ====================================================================================
            // 入力構造体：CPUからGPUへ渡す頂点データ
            // ====================================================================================
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // ====================================================================================
            // 出力構造体：頂点シェーダーからフラグメントシェーダーへ渡すデータ
            // ====================================================================================
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // ====================================================================================
            // シャドウバイアス適用関数
            // シャドウアクネ防止のためノーマル方向にわずかなオフセットをかける
            // ライトに向いている面は外側へ、裏面は内側へオフセットする
            // ====================================================================================
            float4 GetShadowCasterPosition(float3 posWS, float3 normalWS)
            {
                float bias = 0.005;
                float3 lightDir = _MainLightPosition.xyz;
                float3 offset = normalWS * bias * (dot(normalWS, lightDir) >= 0 ? 1 : -1);
                return TransformWorldToHClip(posWS + offset);
            }

            // ====================================================================================
            // 頂点シェーダー
            // ====================================================================================
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionHCS = GetShadowCasterPosition(posWS, normalWS);
                return OUT;
            }

            // ====================================================================================
            // フラグメントシェーダー
            // シャドウマップへの書き込みのみ行うためカラー出力は不要
            // ====================================================================================
            half4 frag(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }


        // ====================================================================================
        // Pass 2: ScreenDoor（ライティングなし・軽量）
        // ライト計算を省いた軽量Pass
        // MainTexをそのまま表示しつつScreenDoorディザで近距離のピクセルを間引く
        // Player.shaderのStencil連動のため、このPassでもディザ処理が必要
        // ====================================================================================
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ====================================================================================
            // 入力構造体：CPUからGPUへ渡す頂点データ
            // ====================================================================================
            struct Attributes
            {
                float4 positionOS : POSITION;   // オブジェクト空間の頂点座標
                float2 uv : TEXCOORD0;           // テクスチャUV座標
            };

            // ====================================================================================
            // 出力構造体：頂点シェーダーからフラグメントシェーダーへ渡すデータ
            // ====================================================================================
            struct Varyings
            {
                float2 uv : TEXCOORD0;           // テクスチャUV座標
                float4 positionHCS : SV_POSITION; // クリップ空間座標（スクリーン描画用）
                float3 positionWS : TEXCOORD1;   // ワールド空間座標（カメラ距離計算に使用）
                float4 positionSS : TEXCOORD2;   // スクリーン空間座標（BayerテクスチャUV計算に使用）
            };

            // ====================================================================================
            // マテリアルプロパティ
            // ====================================================================================
            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);
            TEXTURE2D(_BayerTex);   SAMPLER(sampler_BayerTex);
            float4 _MainTex_ST;
            float _BlockSize;
            float _Radius;

            // ====================================================================================
            // 頂点シェーダー
            // 各空間座標を取得し、BayerテクスチャUV計算用のスクリーン座標も渡す
            // ====================================================================================
            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // GetVertexPositionInputsで各空間座標を一括取得
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);

                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                // ComputeScreenPosでクリップ座標を[0,1]スクリーン空間へ変換
                // BayerパターンをスクリーンのUVに固定するために使用
                OUT.positionSS  = ComputeScreenPos(posInputs.positionCS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);

                return OUT;
            }

            // ====================================================================================
            // フラグメントシェーダー
            // カメラ距離に応じてBayerマトリクスで間引き、近距離ほど多くのピクセルを破棄する
            // ====================================================================================
            float4 frag(Varyings IN) : SV_Target
            {
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // カメラからのワールド距離を計算
                float dist = distance(IN.positionWS, _WorldSpaceCameraPos);

                // 距離を_Radiusで正規化し[0,1]にクランプ（近い=0、遠い=1）
                float clamp_distance = saturate(dist / _Radius);

                // スクリーン座標からBayerテクスチャのUVを計算
                // _BlockSizeが大きいほどディザドットが粗くなる
                float2 uv_BayerTex = (IN.positionSS.xy / IN.positionSS.w) * (_ScreenParams.xy / _BlockSize);

                // BayerMatrixから閾値をサンプリング（0〜1のディザパターン）
                float threshold = SAMPLE_TEXTURE2D(_BayerTex, sampler_BayerTex, uv_BayerTex).r;

                // 距離が閾値より小さい（近い）ピクセルを破棄してディザを表現
                clip(clamp_distance - threshold);

                return col;
            }
            ENDHLSL
        }


        // ====================================================================================
        // Pass 3: ForwardLit
        // フルPBRライティング + ScreenDoorディザリング
        // NormalMap / Emission / 影の受け取り / ライトマップに対応
        // StencilバッファにRef=1を書き込みPlayer.shaderのシルエット描画と連動する
        // ====================================================================================
        Pass
        {
            Name "ForwardLit"
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
            // 影・追加ライト・ソフトシャドウ・ライトマップ等のキーワード
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS_PIXEL
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _MIXED_LIGHTING_SUBTRACTIVE
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _LIGHT_LAYERS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

            // ====================================================================================
            // 入力構造体：CPUからGPUへ渡す頂点データ
            // ====================================================================================
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;     // 接線（NormalMap変換に必要）
                float2 uv : TEXCOORD0;
            };

            // ====================================================================================
            // 出力構造体：頂点シェーダーからフラグメントシェーダーへ渡すデータ
            // ====================================================================================
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;   // xyz=接線方向、w=バイタンジェント符号
                float3 viewDirWS : TEXCOORD4;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 5); // ライトマップUV or SH（TEXCOORD5）
                float4 fogCoord : TEXCOORD6;
            };

            // ====================================================================================
            // マテリアルプロパティ
            // ====================================================================================
            TEXTURE2D(_MainTex);     SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);   SAMPLER(sampler_NormalMap);
            TEXTURE2D(_SpecularTex); SAMPLER(sampler_SpecularTex);
            TEXTURE2D(_EmissionTex); SAMPLER(sampler_EmissionTex);
            TEXTURE2D(_BayerTex);    SAMPLER(sampler_BayerTex);

            float4 _MainColor;
            float3 _BlockSpecularColor;
            float3 _BlockEmissionColor;
            float _IOR;
            float _Roughness;
            float _BlockSize;
            float _Radius;

            // ====================================================================================
            // TBN行列構築関数
            // 接線・バイタンジェント・法線からタンジェント→ワールド変換行列を構築する
            // バイタンジェント = 法線 × 接線（符号で向きを補正）
            // ====================================================================================
            float3x3 MyCreateTangentToWorld(float3 normalWS, float3 tangentWS, float tangentSign)
            {
                float3 bitangent = cross(normalWS, tangentWS) * tangentSign;
                return float3x3(tangentWS, bitangent, normalWS);
            }

            // ====================================================================================
            // 頂点シェーダー
            // URPのヘルパーでオブジェクト空間から各空間へ座標変換する
            // ====================================================================================
            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // URPのヘルパーでオブジェクト空間 → 各空間へ変換
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.uv = IN.uv;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = normInputs.normalWS;
                OUT.tangentWS = float4(normInputs.tangentWS, IN.tangentOS.w);
                OUT.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);
                OUTPUT_LIGHTMAP_UV(IN.uv, unity_LightmapST, OUT.lightmapUV);
                OUTPUT_SH(normInputs.normalWS, OUT.vertexSH);
                OUT.fogCoord = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            // ====================================================================================
            // フラグメントシェーダー
            // PBRライティング計算後にScreenDoorディザリングでピクセルを間引く
            // ====================================================================================
            half4 frag(Varyings IN) : SV_Target
            {
                // ----- SurfaceData（PBRマテリアル情報）-----
                SurfaceData surfaceData;
                half4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                surfaceData.albedo     = baseColor.rgb * _MainColor.rgb;
                surfaceData.alpha      = baseColor.a * _MainColor.a;
                surfaceData.metallic   = 0.0;
                surfaceData.occlusion  = 1.0;
                surfaceData.emission   = 0;

                // IORからフレネル反射率F0を計算（シュリックの近似式）
                float F0 = pow((_IOR - 1.0) / (_IOR + 1.0), 2.0);
                surfaceData.specular   = F0.xxx;
                surfaceData.smoothness = 1.0 - _Roughness;

                // スペキュラーテクスチャを使う場合はこちらを有効化
                // half4 specularTex = SAMPLE_TEXTURE2D(_SpecularTex, sampler_SpecularTex, IN.uv);
                // surfaceData.specular = specularTex.rgb * _BlockSpecularColor.rgb;

                // NormalMapをタンジェント空間でサンプリング
                float3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uv));
                surfaceData.normalTS  = normalTS;
                surfaceData.occlusion = 1.0;

                // エミッションテクスチャ × エミッションカラー
                half4 emissionTex = SAMPLE_TEXTURE2D(_EmissionTex, sampler_EmissionTex, IN.uv);
                surfaceData.emission           = emissionTex.rgb * _BlockEmissionColor.rgb;
                surfaceData.clearCoatMask      = 0.0;
                surfaceData.clearCoatSmoothness = 0.0;

                // ----- InputData（ライティング計算用の入力情報）-----
                InputData inputData;
                inputData.positionWS = IN.positionWS;

                // タンジェント空間の法線をワールド空間へ変換
                float3x3 tangentToWorld = MyCreateTangentToWorld(IN.normalWS, IN.tangentWS.xyz, IN.tangentWS.w);
                inputData.normalWS         = TransformTangentToWorld(surfaceData.normalTS, tangentToWorld);
                inputData.viewDirectionWS  = normalize(IN.viewDirWS);
                inputData.shadowCoord      = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord         = IN.fogCoord;
                inputData.vertexLighting   = VertexLighting(IN.positionWS, IN.normalWS);
                inputData.bakedGI          = SAMPLE_GI(IN.lightmapUV, IN.vertexSH, inputData.normalWS);

                // URPのPBRライティングを計算
                half4 color = UniversalFragmentPBR(inputData, surfaceData);

                // ----- ScreenDoorディザリング -----
                // カメラからのワールド距離を計算
                float dist = distance(IN.positionWS, _WorldSpaceCameraPos);

                // 距離を_Radiusで正規化し[0,1]にクランプ（近い=0、遠い=1）
                float clamp_distance = saturate(dist / _Radius);

                // クリップ座標からBayerテクスチャのUVを計算
                // Pass2と異なりpositionHCSのxy/wはNDC[-1,1]のためスケールが異なる
                float2 uv_BayerTex = (IN.positionHCS.xy / IN.positionHCS.w) * (_ScreenParams.xy / _BlockSize);

                // BayerMatrixから閾値をサンプリング（0〜1のディザパターン）
                float threshold = SAMPLE_TEXTURE2D(_BayerTex, sampler_BayerTex, uv_BayerTex).r;

                // 距離が閾値より小さい（近い）ピクセルを破棄してディザを表現
                clip(clamp_distance - threshold);

                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
