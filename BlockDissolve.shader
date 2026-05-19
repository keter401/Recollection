Shader "Custom/RandomBlockDissolve"
{
    Properties
    {
        BaseColor("Base Color", Color) = (1,1,1,1)
        DissolveProgress("Dissolve Progress", Range(0,1)) = 0  // ディゾルブの進行度（0=完全表示、1=完全消滅）
        BlockMinSize("Block Min Size", Float) = 0.1             // ブロックサイズの最小値（ワールド単位）
        BlockMaxSize("Block Max Size", Float) = 5.0             // ブロックサイズの最大値（ワールド単位）
        FlyHeight("Fly Height", Float) = 5.0                    // ディゾルブ時にブロックが飛び上がる高さ
        Alpha("Alpha", Range(0,1)) = 1                          // 全体の透明度
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" }
        LOD 100


        // ====================================================================================
        // Pass 1: BlockDissolve
        // ブロック単位でランダムに消滅するディゾルブ効果を実装する
        // 消滅時にブロックがY方向へ飛び上がる演出でSF的な転送表現を実現する
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
            };

            // ====================================================================================
            // 出力構造体：頂点シェーダーからフラグメントシェーダーへ渡すデータ
            // ====================================================================================
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;   // クリップ空間座標（スクリーン描画用）
                float3 worldPos    : TEXCOORD0;      // オフセット適用後のワールド座標（frag側のグリッド計算に使用）
                float  randNoise   : TEXCOORD1;      // このブロックの消滅タイミングを決めるランダム値[0,1]
                float  blockSize   : TEXCOORD2;      // このブロックに割り当てられたサイズ
            };

            // ====================================================================================
            // マテリアルプロパティ
            // ====================================================================================
            float4 BaseColor;
            float  DissolveProgress;
            float  BlockMinSize;
            float  BlockMaxSize;
            float  FlyHeight;
            float  Alpha;

            // ====================================================================================
            // 擬似乱数生成関数
            // ワールド座標をシードとしてsin+fracで[0,1]の乱数を返す
            // 同じ座標からは常に同じ値が返るため、ブロック単位で一貫した乱数になる
            // ====================================================================================
            float rand(float3 p)
            {
                return frac(sin(dot(p, float3(12.9898, 78.233, 45.164))) * 43758.5453);
            }

            // ====================================================================================
            // 頂点シェーダー
            // ブロックごとにランダムなサイズと消滅タイミングを決定し、
            // DissolveProgressに応じてY方向へ飛び上がるオフセットを加算する
            // ====================================================================================
            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);

                // 粗いグリッド（1単位）でランダム値を取得し、ブロックサイズを決定する
                // 頂点ごとにブロックサイズが変わらないよう、floor()で整数グリッドに揃える
                float3 coarseGrid = floor(worldPos);
                float  randVal    = rand(coarseGrid);
                float  blockSize  = lerp(BlockMinSize, BlockMaxSize, randVal);

                // 決定したblockSizeでワールド座標をグリッドに揃え、ブロックごとの乱数を取得
                // このnoiseがブロックの「消滅タイミング」を決める（小さいほど早く消える）
                float3 gridPos = floor(worldPos / blockSize);
                float  noise   = rand(gridPos);

                // DissolveProgressがnoiseを超えた瞬間からブロックが飛び上がり始める
                // 0.1で割ることで0.1の幅をかけてなめらかに遷移させる（急に飛ばないようにする）
                float progress = saturate((DissolveProgress - noise) / 0.1);

                // Y方向に飛び上がるオフセットを計算してワールド座標に加算
                float3 offset = float3(0, progress * FlyHeight, 0);
                worldPos += offset;

                // オフセット後のワールド座標をfragに渡す（frag側でも同じグリッド計算をするため）
                OUT.positionHCS = TransformWorldToHClip(worldPos);
                OUT.worldPos    = worldPos;
                OUT.randNoise   = noise;
                OUT.blockSize   = blockSize;
                return OUT;
            }

            // ====================================================================================
            // フラグメントシェーダー
            // vert側と同じグリッド・乱数の計算を行い、消滅判定をする
            // DissolveProgressがこのブロックのnoiseを超えたらピクセルを破棄（消滅）
            // ====================================================================================
            half4 frag(Varyings IN) : SV_Target
            {
                // vert側でオフセット後のworldPosを渡しているため、飛び上がった後の座標で計算される
                float3 grid  = floor(IN.worldPos / IN.blockSize);
                float  noise = rand(grid);

                // DissolveProgressがこのブロックのnoiseを超えたら破棄（消滅）
                if (DissolveProgress > noise)
                    discard;

                return float4(BaseColor.rgb, BaseColor.a * Alpha);
            }
            ENDHLSL
        }
    }
}
