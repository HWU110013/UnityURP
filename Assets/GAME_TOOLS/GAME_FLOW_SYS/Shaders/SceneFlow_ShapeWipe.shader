// 形狀擦除：內建多種 SDF 形狀 + 自訂貼圖，支援密度拼貼與波紋擴散
Shader "CatzTools/SceneFlow/ShapeWipe"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Smoothness ("邊緣柔化", Range(0, 0.5)) = 0.02
        [Enum(Cross,0,Hexagon,1,Custom,2)]
        _ShapeType ("形狀類型", Float) = 0
        _ShapeTex ("自訂形狀貼圖（白色=形狀）", 2D) = "white" {}
        _Density ("密度（長邊分割數）", Range(1, 50)) = 1
        _OffsetX ("水平偏移", Range(-0.5, 0.5)) = 0
        _OffsetY ("垂直偏移", Range(-0.5, 0.5)) = 0
        _RippleStrength ("波紋強度（0=關閉）", Range(0, 1)) = 0
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            fixed4 _Color;
            float _Progress;
            float _Smoothness;
            float _ShapeType;
            sampler2D _ShapeTex;
            float _Density;
            float _OffsetX;
            float _OffsetY;
            float _RippleStrength;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // ── 形狀距離（到邊界的距離，負=內部，正=外部，不 clamp） ──

            float shapeRaw(float2 p, int shapeType)
            {
                if (shapeType == 0) // Cross（十字）
                {
                    float2 ap = abs(p);
                    float d1 = max(ap.x - 0.12, ap.y - 0.4);
                    float d2 = max(ap.x - 0.4, ap.y - 0.12);
                    return min(d1, d2);
                }
                if (shapeType == 1) // Hexagon（六角形）
                {
                    float2 ap = abs(p);
                    return max(ap.x * 0.866025 + ap.y * 0.5, ap.y) - 0.35;
                }
                // fallback circle
                return length(p) - 0.3;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 螢幕比例修正
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 uv = i.uv;
                uv.x -= _OffsetX;
                uv.y -= _OffsetY;

                // 密度：以長邊為基準
                float density = max(1.0, _Density);
                float tileCountX, tileCountY;
                if (aspect >= 1.0)
                {
                    tileCountX = density;
                    tileCountY = ceil(density / aspect);
                }
                else
                {
                    tileCountY = density;
                    tileCountX = ceil(density * aspect);
                }

                // Tile UV
                float2 tileUV = float2(uv.x * tileCountX, uv.y * tileCountY);
                float2 tileID = floor(tileUV);
                float2 localUV = frac(tileUV);

                // 每個 tile 內座標轉為 -0.5 ~ 0.5，修正長寬比
                float2 p = localUV - 0.5;
                float tileAspect = (tileCountY * aspect) / tileCountX;
                p.x *= tileAspect;

                // 形狀遮罩（0=形狀中心先覆蓋，1=角落最後覆蓋）
                int shapeType = (int)_ShapeType;
                float mask;

                if (shapeType == 2) // Custom texture
                {
                    // 白色=形狀中心(先覆蓋=0)，黑色=最後覆蓋(=1)
                    mask = 1.0 - tex2D(_ShapeTex, localUV).r;
                }
                else
                {
                    // 用中心最小值到角落最大值做完整 remap → 確保從極小展開
                    float halfX = 0.5 * tileAspect;
                    float halfY = 0.5;

                    float raw = shapeRaw(p, shapeType);
                    float centerVal = shapeRaw(float2(0, 0), shapeType); // 中心（最小）

                    float c0 = shapeRaw(float2( halfX,  halfY), shapeType);
                    float c1 = shapeRaw(float2(-halfX,  halfY), shapeType);
                    float c2 = shapeRaw(float2( halfX, -halfY), shapeType);
                    float c3 = shapeRaw(float2(-halfX, -halfY), shapeType);
                    float cornerVal = max(max(c0, c1), max(c2, c3)); // 角落（最大）

                    float range = cornerVal - centerVal;
                    range = max(range, 0.001);
                    mask = saturate((raw - centerVal) / range);
                }

                // 波紋擴散：中心 tile 先動，邊緣 tile 後動
                float rippleDelay = 0.0;
                if (_RippleStrength > 0.01)
                {
                    float2 center = float2(tileCountX * 0.5, tileCountY * 0.5);
                    float maxTileDist = length(center);
                    float tileDist = length(tileID + 0.5 - center);
                    rippleDelay = (tileDist / max(maxTileDist, 0.001)) * _RippleStrength;
                }

                // 用 pow 拉開中心和邊緣的差距，讓形狀從極小展開
                mask = pow(mask, 0.6);

                // Progress 補償：確保 Progress=0 時完全透明，Progress=1 時完全覆蓋
                float totalExpand = _Smoothness * 2.0 + _RippleStrength;
                float progress = _Progress * (1.0 + totalExpand) - _Smoothness;
                float localProgress = progress - rippleDelay;

                // mask 小(0)=形狀中心先覆蓋，mask 大(1)=角落最後覆蓋
                float alpha = 1.0 - smoothstep(localProgress - _Smoothness, localProgress + _Smoothness, mask);

                return fixed4(_Color.rgb, saturate(alpha));
            }
            ENDCG
        }
    }
}
