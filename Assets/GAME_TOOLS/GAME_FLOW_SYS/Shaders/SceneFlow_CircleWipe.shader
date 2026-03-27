// 圓形擦除：密度控制大小，支援波紋擴散
Shader "CatzTools/SceneFlow/CircleWipe"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Smoothness ("邊緣柔化", Range(0, 1)) = 0.05
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

            fixed4 frag(v2f i) : SV_Target
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;

                // 以長邊為基準算 tile 數
                float tileX, tileY;
                if (aspect >= 1.0)
                {
                    tileX = _Density;
                    tileY = ceil(_Density / aspect);
                }
                else
                {
                    tileY = _Density;
                    tileX = ceil(_Density * aspect);
                }

                // 拼貼 UV
                float2 uv = i.uv + float2(_OffsetX, _OffsetY);
                float2 scaled = uv * float2(tileX, tileY);
                float2 tileId = floor(scaled);
                float2 tiled = frac(scaled);

                // tile 內圓形距離
                float2 center = tiled - 0.5;
                float tileAspect = tileX / tileY * aspect;
                center.x *= (aspect >= 1.0) ? 1.0 : tileAspect;
                center.y *= (aspect >= 1.0) ? tileAspect : 1.0;
                // 簡化：讓每個 tile 內的圓是正圓
                center = tiled - 0.5;

                float dist = length(center);
                float maxRadius = 0.5 + _Smoothness;

                // 波紋：強度 0 = 全部同時，1 = 最大延遲
                float localProgress = _Progress;
                if (_RippleStrength > 0.001)
                {
                    float2 screenCenter = float2(tileX, tileY) * 0.5;
                    float tileDist = length(tileId + 0.5 - screenCenter);
                    float maxTileDist = length(screenCenter);
                    float spread = _RippleStrength * 0.9; // 映射到 0~0.9 避免分母為零
                    float delay = (tileDist / max(maxTileDist, 0.001)) * spread;
                    localProgress = saturate((_Progress - delay) / (1.0 - spread));
                }

                float radius = (1.0 - localProgress) * maxRadius;
                float alpha = smoothstep(radius - _Smoothness, radius, dist);

                return fixed4(_Color.rgb, alpha);
            }
            ENDCG
        }
    }
}
